using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Versatus.ForcaVendas.Infrastructure.Integration.Ftp;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.ErpAdapter.Jobs;

public sealed class CatalogExporter : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly FtpTransportOptions _ftpOptions;
    private readonly ILogger<CatalogExporter> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, DateTime> _lastFullSyncDates = new();

    public CatalogExporter(
        IConfiguration config,
        IOptions<FtpTransportOptions> ftpOptions,
        ILogger<CatalogExporter> logger)
    {
        _config = config;
        _ftpOptions = ftpOptions.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue<int>("ErpAdapter:CatalogExportIntervalSeconds", 60);
        if (intervalSeconds <= 0) intervalSeconds = 60;

        _logger.LogInformation("Iniciando CatalogExporter com intervalo de {Interval} segundos.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tenants = _config.GetSection("Auth:Tenants").Get<string[]>() ?? Array.Empty<string>();
                var erpConnectionString = _config.GetConnectionString("ErpDatabase") ?? string.Empty;
                var now = DateTime.Now;

                foreach (var tenantId in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var filialId = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:FilialId", 1);
                    var fullSyncHour = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:FullSyncHour", 3);

                    _lastFullSyncDates.TryGetValue(tenantId, out var lastFullSyncDate);

                    bool forceFullSync = false;
                    if (now.Hour == fullSyncHour && now.Date != lastFullSyncDate)
                    {
                        forceFullSync = true;
                        _lastFullSyncDates[tenantId] = now.Date;
                        _logger.LogInformation("Horário de Full Sync ({Hour}h) atingido para o tenant {TenantId}. Disparando Full Sync diário.", fullSyncHour, tenantId);
                    }

                    await ExportCatalogForTenantAsync(tenantId, filialId, erpConnectionString, forceFullSync, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro fatal ao exportar catálogo do ERP.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task ExportCatalogForTenantAsync(string tenantId, int filialId, string connectionString, bool forceFullSync, CancellationToken ct)
    {
        var syncFilePath = Path.Combine(AppContext.BaseDirectory, $"last_sync_{tenantId}.txt");
        DateTime? ultimoSync = null;

        if (!forceFullSync && File.Exists(syncFilePath))
        {
            try
            {
                var text = await File.ReadAllTextAsync(syncFilePath, ct);
                if (DateTime.TryParse(text.Trim(), out var date))
                {
                    ultimoSync = date;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao ler arquivo de timestamp de sincronização local para tenant {TenantId}.", tenantId);
            }
        }

        bool isDelta = !forceFullSync && ultimoSync.HasValue;
        _logger.LogInformation("Exportando catálogo para o tenant {TenantId} (Filial {FilialId}). Modo: {Mode}. Último Sync: {UltimoSync}", 
            tenantId, filialId, isDelta ? "Delta (Incremental)" : "Full (Carga Total)", ultimoSync);

        CatalogSnapshot snapshot;
        try
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string do ERP está vazia.");
            }
            snapshot = await FetchCatalogFromSqlServerAsync(connectionString, filialId, ultimoSync, ct);
            snapshot.IsFullSync = !isDelta;

            // Popular parâmetros do tenant
            snapshot.TenantParameters = new TenantParametersDto
            {
                TabelaPrecoIdDefault = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:TabelaPrecoIdDefault", 1),
                PermiteAlterarTabelaPreco = _config.GetValue<bool>($"ErpAdapter:Tenants:{tenantId}:PermiteAlterarTabelaPreco", true)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao consultar SQL Server do ERP para tenant {TenantId}. Catálogo NÃO atualizado.", tenantId);
            return;
        }

        var clientesWrapper = new CatalogFileWrapper<ClienteCatalogDto> { IsFullSync = snapshot.IsFullSync, TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.Clientes.ToList() };
        var produtosWrapper = new CatalogFileWrapper<ProdutoCatalogDto> { IsFullSync = snapshot.IsFullSync, TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.Produtos.ToList() };
        var precosWrapper = new CatalogFileWrapper<TabelaPrecoCatalogDto> { IsFullSync = snapshot.IsFullSync, TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.TabelasPreco.ToList() };
        var precosMetadataWrapper = new CatalogFileWrapper<TabelaPrecoMetadataDto> { IsFullSync = snapshot.IsFullSync, TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.TabelasPrecoMetadata.ToList() };
        var condicoesWrapper = new CatalogFileWrapper<CondicaoPagamentoCatalogDto> { IsFullSync = snapshot.IsFullSync, TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.CondicoesPagamento.ToList() };

        await UploadToFtpAsync(tenantId, "clientes.json", clientesWrapper, ct);
        await UploadToFtpAsync(tenantId, "produtos.json", produtosWrapper, ct);
        await UploadToFtpAsync(tenantId, "tabelas-preco.json", precosWrapper, ct);
        await UploadToFtpAsync(tenantId, "tabelas-preco-metadata.json", precosMetadataWrapper, ct);
        await UploadToFtpAsync(tenantId, "condicoes-pagamento.json", condicoesWrapper, ct);
        await UploadToFtpAsync(tenantId, "tenant-parameters.json", snapshot.TenantParameters, ct);

        try
        {
            await File.WriteAllTextAsync(syncFilePath, DateTime.Now.ToString("o"), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar arquivo de timestamp de sincronização local para tenant {TenantId}.", tenantId);
        }

        _logger.LogInformation("Arquivos de catálogo enviados ao FTP com sucesso para o tenant {TenantId}. IsFullSync: {IsFullSync}", tenantId, snapshot.IsFullSync);
    }

    private async Task UploadToFtpAsync<T>(string tenantId, string fileName, T content, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(content, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var destPath = FtpFolderStructure.GetCatalogFilePath(_ftpOptions.BasePath, tenantId, fileName);
        var destDir = FtpFolderStructure.GetCatalogDirectory(_ftpOptions.BasePath, tenantId);

        if (_ftpOptions.UseSftp)
        {
            using var client = new SftpClient(_ftpOptions.Host, _ftpOptions.Port, _ftpOptions.Username, _ftpOptions.Password);
            await Task.Run(() => client.Connect(), ct);
            
            // Cria diretórios recursivamente no SFTP
            var parts = destDir.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var currentPath = destDir.StartsWith("/") ? "/" : "";
            foreach (var part in parts)
            {
                currentPath = currentPath == "/" ? $"/{part}" : (string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}");
                if (!client.Exists(currentPath))
                {
                    client.CreateDirectory(currentPath);
                }
            }

            using var ms = new MemoryStream(bytes);
            await Task.Run(() => client.UploadFile(ms, destPath, true), ct);
            client.Disconnect();
        }
        else
        {
            using var client = new AsyncFtpClient(_ftpOptions.Host, _ftpOptions.Username, _ftpOptions.Password, _ftpOptions.Port);
            await client.AutoConnect(ct);
            await client.CreateDirectory(destDir, ct);
            await client.UploadBytes(bytes, destPath, FtpRemoteExists.Overwrite, true, token: ct);
            await client.Disconnect(ct);
        }
    }

    private static int ReadInt32Safe(SqlDataReader reader, int index, int defaultValue = 0)
    {
        return reader.IsDBNull(index) ? defaultValue : Convert.ToInt32(reader.GetValue(index));
    }

    private static decimal ReadDecimalSafe(SqlDataReader reader, int index, decimal defaultValue = 0m)
    {
        return reader.IsDBNull(index) ? defaultValue : Convert.ToDecimal(reader.GetValue(index));
    }

    private static string ReadStringSafe(SqlDataReader reader, int index, string defaultValue = "")
    {
        return reader.IsDBNull(index) ? defaultValue : reader.GetString(index);
    }

    private async Task<CatalogSnapshot> FetchCatalogFromSqlServerAsync(string connectionString, int filialId, DateTime? ultimoSync, CancellationToken ct)
    {
        var clientes = new List<ClienteCatalogDto>();
        var produtos = new List<ProdutoCatalogDto>();
        var precos = new List<TabelaPrecoCatalogDto>();
        var precosMetadata = new List<TabelaPrecoMetadataDto>();
        var condicoes = new List<CondicaoPagamentoCatalogDto>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Clientes
        var clientesQuery = @"
            SELECT 
                c.IDGLOCLIENTE,
                c.NOME,
                COALESCE(NULLIF(c.CNPJ, ''), NULLIF(c.CPF, ''), '') AS DOCUMENTO,
                COALESCE(cf.IDGLOAREAVENDA, 1) AS IDGLOAREAVENDA,
                COALESCE(c.ITEMFINANCEIROPADRAO, 1) AS IDMOBCONDICAOPAGAMENTO,
                COALESCE(cf.IDGLOCOMISSIONADO, 1) AS IDGLOCOMISSIONADO
            FROM VWCLIENTE c
            LEFT JOIN GLOCLIENTEFILIAL cf ON c.IDGLOCLIENTE = cf.IDGLOCLIENTE AND c.IDGLOFILIAL = cf.IDGLOFILIAL
            WHERE c.ATIVO = 1 AND c.IDGLOFILIAL = @FilialId";

        if (ultimoSync.HasValue)
        {
            clientesQuery = @"
                SELECT 
                    c.IDGLOCLIENTE,
                    c.NOME,
                    COALESCE(NULLIF(c.CNPJ, ''), NULLIF(c.CPF, ''), '') AS DOCUMENTO,
                    COALESCE(cf.IDGLOAREAVENDA, 1) AS IDGLOAREAVENDA,
                    COALESCE(c.ITEMFINANCEIROPADRAO, 1) AS IDMOBCONDICAOPAGAMENTO,
                    COALESCE(cf.IDGLOCOMISSIONADO, 1) AS IDGLOCOMISSIONADO
                FROM VWCLIENTE c
                INNER JOIN GLOCLIENTE gc ON c.IDGLOCLIENTE = gc.IDGLOCLIENTE
                LEFT JOIN GLOCLIENTEFILIAL cf ON c.IDGLOCLIENTE = cf.IDGLOCLIENTE AND c.IDGLOFILIAL = cf.IDGLOFILIAL
                WHERE c.ATIVO = 1 AND c.IDGLOFILIAL = @FilialId
                  AND (gc.DATAALTERACAO > @UltimoSync OR gc.DATAINCLUSAO > @UltimoSync)";
        }

        using (var cmd = new SqlCommand(clientesQuery, conn))
        {
            cmd.Parameters.AddWithValue("@FilialId", filialId);
            if (ultimoSync.HasValue)
            {
                cmd.Parameters.AddWithValue("@UltimoSync", ultimoSync.Value);
            }
            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    clientes.Add(new ClienteCatalogDto
                    {
                        ClienteIdERP = ReadInt32Safe(reader, 0),
                        Nome = ReadStringSafe(reader, 1),
                        Documento = ReadStringSafe(reader, 2),
                        AreaVendaId = ReadInt32Safe(reader, 3, 1),
                        CondicaoPagamentoIdDefault = ReadInt32Safe(reader, 4, 1),
                        ComissionadoAreaVendaId = ReadInt32Safe(reader, 5, 1)
                    });
                }
            }
        }

        // Produtos
        var produtosQuery = @"
            SELECT 
                e.IDESTESTOQUE,
                e.DESCRICAO,
                COALESCE(e.SIGLAUNIDADEVENDA, 'UN') AS SIGLAUNIDADEVENDA,
                COALESCE(e.SALDOATUALESTOQUE, 0) AS SALDOATUALESTOQUE,
                COALESCE(e.DESCRICAOMARCA, '') AS DESCRICAOMARCA,
                COALESCE(e.DESCRICAOFABRICANTE, '') AS DESCRICAOFABRICANTE,
                COALESCE(eg.DESCRICAO, 'Geral') AS DESCRICAOGRUPO
            FROM VWRITEMESTOQUE e
            LEFT JOIN ESTPRODUTO ep ON e.IDESTPRODUTO = ep.IDESTPRODUTO
            LEFT JOIN ESTGRUPO eg ON ep.IDESTGRUPO = eg.IDESTGRUPO
            WHERE e.Ativo = 1 AND e.IDGLOFILIAL = @FilialId";

        if (ultimoSync.HasValue)
        {
            produtosQuery = @"
                SELECT 
                    e.IDESTESTOQUE,
                    e.DESCRICAO,
                    COALESCE(e.SIGLAUNIDADEVENDA, 'UN') AS SIGLAUNIDADEVENDA,
                    COALESCE(e.SALDOATUALESTOQUE, 0) AS SALDOATUALESTOQUE,
                    COALESCE(e.DESCRICAOMARCA, '') AS DESCRICAOMARCA,
                    COALESCE(e.DESCRICAOFABRICANTE, '') AS DESCRICAOFABRICANTE,
                    COALESCE(eg.DESCRICAO, 'Geral') AS DESCRICAOGRUPO
                FROM VWRITEMESTOQUE e
                INNER JOIN ESTPRODUTO ep ON e.IDESTPRODUTO = ep.IDESTPRODUTO
                LEFT JOIN ESTGRUPO eg ON ep.IDESTGRUPO = eg.IDESTGRUPO
                WHERE e.Ativo = 1 AND e.IDGLOFILIAL = @FilialId
                  AND (ep.DATAALTERACAO > @UltimoSync OR ep.DATAINCLUSAO > @UltimoSync)";
        }

        using (var cmd = new SqlCommand(produtosQuery, conn))
        {
            cmd.Parameters.AddWithValue("@FilialId", filialId);
            if (ultimoSync.HasValue)
            {
                cmd.Parameters.AddWithValue("@UltimoSync", ultimoSync.Value);
            }
            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    produtos.Add(new ProdutoCatalogDto
                    {
                        ProdutoIdERP = ReadInt32Safe(reader, 0),
                        Descricao = ReadStringSafe(reader, 1),
                        SiglaUnidadeVenda = ReadStringSafe(reader, 2, "UN"),
                        Saldo = ReadDecimalSafe(reader, 3),
                        ControlaEstoque = true,
                        ControlaDescontoMaximo = true,
                        AceitaDesconto = true,
                        DescontoMaximoPercentual = 15.00m,
                        Marca = reader.IsDBNull(4) ? null : reader.GetString(4),
                        Fabricante = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Categoria = ReadStringSafe(reader, 6, "Geral")
                    });
                }
            }
        }

        // Tabela Preço
        var precosQuery = @"
            SELECT 
                t.IDVENTABELAPRECOESTOQUE,
                t.IDESTESTOQUE,
                t.IDVENTABELAPRECO,
                t.PRECO,
                COALESCE(t.PERCENTUALDESCONTOMAXIMO, 0) AS PERCENTUALDESCONTOMAXIMO,
                COALESCE(t.DESCONTOMAXIMODIFERENTE, 0) AS DESCONTOMAXIMODIFERENTE,
                COALESCE(tp.DESCRICAO, '') AS DESCRICAO,
                COALESCE(tp.PROMOCAO, 0) AS PROMOCAO,
                tp.VIGENCIAINICIO,
                tp.VIGENCIAFIM
            FROM VENTABELAPRECOESTOQUE t
            LEFT JOIN VENTABELAPRECO tp ON t.IDVENTABELAPRECO = tp.IDVENTABELAPRECO
            WHERE t.ATIVO = 1 AND t.IDGLOFILIAL = @FilialId";

        if (ultimoSync.HasValue)
        {
            precosQuery = @"
                SELECT 
                    t.IDVENTABELAPRECOESTOQUE,
                    t.IDESTESTOQUE,
                    t.IDVENTABELAPRECO,
                    t.PRECO,
                    COALESCE(t.PERCENTUALDESCONTOMAXIMO, 0) AS PERCENTUALDESCONTOMAXIMO,
                    COALESCE(t.DESCONTOMAXIMODIFERENTE, 0) AS DESCONTOMAXIMODIFERENTE,
                    COALESCE(tp.DESCRICAO, '') AS DESCRICAO,
                    COALESCE(tp.PROMOCAO, 0) AS PROMOCAO,
                    tp.VIGENCIAINICIO,
                    tp.VIGENCIAFIM
                FROM VENTABELAPRECOESTOQUE t
                LEFT JOIN VENTABELAPRECO tp ON t.IDVENTABELAPRECO = tp.IDVENTABELAPRECO
                WHERE t.ATIVO = 1 AND t.IDGLOFILIAL = @FilialId
                  AND (t.DATAALTERACAO > @UltimoSync OR t.DATAINCLUSAO > @UltimoSync)";
        }

        using (var cmd = new SqlCommand(precosQuery, conn))
        {
            cmd.Parameters.AddWithValue("@FilialId", filialId);
            if (ultimoSync.HasValue)
            {
                cmd.Parameters.AddWithValue("@UltimoSync", ultimoSync.Value);
            }
            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    precos.Add(new TabelaPrecoCatalogDto
                    {
                        TabelaPrecoEstoqueIdERP = ReadInt32Safe(reader, 0),
                        ProdutoIdERP = ReadInt32Safe(reader, 1),
                        TabelaPrecoIdERP = ReadInt32Safe(reader, 2),
                        ValorUnitario = ReadDecimalSafe(reader, 3),
                        PercentualDescontoMaximo = ReadDecimalSafe(reader, 4),
                        ControlaDescontoMaximo = ReadInt32Safe(reader, 5) != 0,
                        Descricao = ReadStringSafe(reader, 6),
                        IsPromocional = ReadInt32Safe(reader, 7) != 0,
                        VigenciaInicio = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                        VigenciaFim = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
                    });
                }
            }
        }

        // Tabela Preço Metadata
        var metadataQuery = @"
            SELECT 
                IDVENTABELAPRECO,
                DESCRICAO,
                COALESCE(PROMOCAO, 0) AS PROMOCAO,
                VIGENCIAINICIO,
                VIGENCIAFIM
            FROM VENTABELAPRECO
            WHERE ATIVO = 1";

        using (var cmd = new SqlCommand(metadataQuery, conn))
        {
            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    precosMetadata.Add(new TabelaPrecoMetadataDto
                    {
                        TabelaPrecoIdERP = ReadInt32Safe(reader, 0),
                        Descricao = ReadStringSafe(reader, 1),
                        IsPromocional = ReadInt32Safe(reader, 2) != 0,
                        Ativa = true,
                        VigenciaInicio = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                        VigenciaFim = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
                    });
                }
            }
        }

        // Condições Pagamento
        var condicoesQuery = @"
            SELECT 
                IDGLOCONDICAOPAGAMENTO,
                DESCRICAO,
                QUANTIDADEPARCELA,
                DIASPARCELAMENTO,
                COALESCE(ACRESCIMO, 0) AS ACRESCIMO,
                COALESCE(DESCONTO, 0) AS DESCONTO,
                COALESCE(IDGLOFORMACOBRANCA, 1) AS IDGLOFORMACOBRANCA,
                COALESCE(USARMESCOMERCIAL, 0) AS USARMESCOMERCIAL
            FROM GLOCONDICAOPAGAMENTO
            WHERE ATIVO = 1";

        if (ultimoSync.HasValue)
        {
            condicoesQuery = @"
                SELECT 
                    IDGLOCONDICAOPAGAMENTO,
                    DESCRICAO,
                    QUANTIDADEPARCELA,
                    DIASPARCELAMENTO,
                    COALESCE(ACRESCIMO, 0) AS ACRESCIMO,
                    COALESCE(DESCONTO, 0) AS DESCONTO,
                    COALESCE(IDGLOFORMACOBRANCA, 1) AS IDGLOFORMACOBRANCA,
                    COALESCE(USARMESCOMERCIAL, 0) AS USARMESCOMERCIAL
                FROM GLOCONDICAOPAGAMENTO
                WHERE ATIVO = 1
                  AND (DATAALTERACAO > @UltimoSync OR DATAINCLUSAO > @UltimoSync)";
        }

        using (var cmd = new SqlCommand(condicoesQuery, conn))
        {
            if (ultimoSync.HasValue)
            {
                cmd.Parameters.AddWithValue("@UltimoSync", ultimoSync.Value);
            }
            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    condicoes.Add(new CondicaoPagamentoCatalogDto
                    {
                        CondicaoPagtoIdERP = ReadInt32Safe(reader, 0),
                        Descricao = ReadStringSafe(reader, 1),
                        QuantidadeParcela = ReadInt32Safe(reader, 2, 1),
                        DiasParcelamento = ReadInt32Safe(reader, 3),
                        Acrescimo = ReadDecimalSafe(reader, 4),
                        Desconto = ReadDecimalSafe(reader, 5),
                        FormaCobrancaIdERP = ReadInt32Safe(reader, 6, 1),
                        UsarMesComercial = ReadInt32Safe(reader, 7) != 0
                    });
                }
            }
        }

        return new CatalogSnapshot
        {
            Clientes = clientes,
            Produtos = produtos,
            TabelasPreco = precos,
            TabelasPrecoMetadata = precosMetadata,
            CondicoesPagamento = condicoes
        };
    }
}
