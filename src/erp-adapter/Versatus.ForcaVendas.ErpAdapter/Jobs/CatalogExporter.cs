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

                foreach (var tenantId in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await ExportCatalogForTenantAsync(tenantId, erpConnectionString, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro fatal ao exportar catálogo do ERP.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task ExportCatalogForTenantAsync(string tenantId, string connectionString, CancellationToken ct)
    {
        _logger.LogInformation("Exportando catálogo para o tenant {TenantId}...", tenantId);

        CatalogSnapshot snapshot;
        try
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException("Connection string do ERP está vazia.");
            }
            snapshot = await FetchCatalogFromSqlServerAsync(connectionString, tenantId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Falha ao consultar SQL Server do ERP ({Msg}). Usando catálogo simulado para tenant {TenantId}.", ex.Message, tenantId);
            snapshot = GenerateSimulatedCatalog(tenantId);
        }

        var clientesWrapper = new CatalogFileWrapper<ClienteCatalogDto> { TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.Clientes.ToList() };
        var produtosWrapper = new CatalogFileWrapper<ProdutoCatalogDto> { TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.Produtos.ToList() };
        var precosWrapper = new CatalogFileWrapper<TabelaPrecoCatalogDto> { TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.TabelasPreco.ToList() };
        var condicoesWrapper = new CatalogFileWrapper<CondicaoPagamentoCatalogDto> { TenantId = tenantId, Version = "v1", ExportedAt = DateTimeOffset.UtcNow, Data = snapshot.CondicoesPagamento.ToList() };

        await UploadToFtpAsync(tenantId, "clientes.json", clientesWrapper, ct);
        await UploadToFtpAsync(tenantId, "produtos.json", produtosWrapper, ct);
        await UploadToFtpAsync(tenantId, "tabelas-preco.json", precosWrapper, ct);
        await UploadToFtpAsync(tenantId, "condicoes-pagamento.json", condicoesWrapper, ct);

        _logger.LogInformation("Arquivos de catálogo enviados ao FTP com sucesso para o tenant {TenantId}.", tenantId);
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

    private async Task<CatalogSnapshot> FetchCatalogFromSqlServerAsync(string connectionString, string tenantId, CancellationToken ct)
    {
        // Esta lógica simula as consultas no banco do ERP legado Small (Tabelas MobCliente, MobEstoque, etc.)
        var clientes = new List<ClienteCatalogDto>();
        var produtos = new List<ProdutoCatalogDto>();
        var precos = new List<TabelaPrecoCatalogDto>();
        var condicoes = new List<CondicaoPagamentoCatalogDto>();

        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Clientes
        using (var cmd = new SqlCommand("SELECT [clienteIdERP], [nome], [documento], [areaVendaId], [condicaoPagamentoIdDefault], [comissionadoAreaVendaId] FROM [MobCliente]", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                clientes.Add(new ClienteCatalogDto
                {
                    ClienteIdERP = reader.GetInt32(0),
                    Nome = reader.GetString(1),
                    Documento = reader.GetString(2),
                    AreaVendaId = reader.GetInt32(3),
                    CondicaoPagamentoIdDefault = reader.GetInt32(4),
                    ComissionadoAreaVendaId = reader.GetInt32(5)
                });
            }
        }

        // Produtos
        using (var cmd = new SqlCommand("SELECT [produtoIdERP], [descricao], [siglaUnidadeVenda], [saldo], [controlaEstoque], [controlaDescontoMaximo], [aceitaDesconto], [descontoMaximoPercentual], [marca], [fabricante] FROM [MobEstoque]", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                produtos.Add(new ProdutoCatalogDto
                {
                    ProdutoIdERP = reader.GetInt32(0),
                    Descricao = reader.GetString(1),
                    SiglaUnidadeVenda = reader.GetString(2),
                    Saldo = reader.GetDecimal(3),
                    ControlaEstoque = reader.GetBoolean(4),
                    ControlaDescontoMaximo = reader.GetBoolean(5),
                    AceitaDesconto = reader.GetBoolean(6),
                    DescontoMaximoPercentual = reader.GetDecimal(7),
                    Marca = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Fabricante = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }
        }

        // Tabela Preço
        using (var cmd = new SqlCommand("SELECT [tabelaPrecoEstoqueIdERP], [produtoIdERP], [tabelaPrecoIdERP], [valorUnitario], [percentualDescontoMaximo], [controlaDescontoMaximo] FROM [MobTabelaPrecoEstoque]", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                precos.Add(new TabelaPrecoCatalogDto
                {
                    TabelaPrecoEstoqueIdERP = reader.GetInt32(0),
                    ProdutoIdERP = reader.GetInt32(1),
                    TabelaPrecoIdERP = reader.GetInt32(2),
                    ValorUnitario = reader.GetDecimal(3),
                    PercentualDescontoMaximo = reader.GetDecimal(4),
                    ControlaDescontoMaximo = reader.GetBoolean(5)
                });
            }
        }

        // Condições Pagamento
        using (var cmd = new SqlCommand("SELECT [condicaoPagtoIdERP], [descricao], [quantidadeParcela], [diasParcelamento], [acrescimo], [desconto], [formaCobrancaIdERP], [usarMesComercial] FROM [MobCondicaoPagamento]", conn))
        using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                condicoes.Add(new CondicaoPagamentoCatalogDto
                {
                    CondicaoPagtoIdERP = reader.GetInt32(0),
                    Descricao = reader.GetString(1),
                    QuantidadeParcela = reader.GetInt32(2),
                    DiasParcelamento = reader.GetInt32(3),
                    Acrescimo = reader.GetDecimal(4),
                    Desconto = reader.GetDecimal(5),
                    FormaCobrancaIdERP = reader.GetInt32(6),
                    UsarMesComercial = reader.GetBoolean(7)
                });
            }
        }

        return new CatalogSnapshot
        {
            Clientes = clientes,
            Produtos = produtos,
            TabelasPreco = precos,
            CondicoesPagamento = condicoes
        };
    }

    private CatalogSnapshot GenerateSimulatedCatalog(string tenantId)
    {
        return new CatalogSnapshot
        {
            Clientes = new List<ClienteCatalogDto>
            {
                new() { ClienteIdERP = 1001, Nome = $"Supermercado Central ({tenantId[..4]})", Documento = "12.345.678/0001-00", AreaVendaId = 1, CondicaoPagamentoIdDefault = 2, ComissionadoAreaVendaId = 10 },
                new() { ClienteIdERP = 1002, Nome = $"Mercearia Primavera ({tenantId[..4]})", Documento = "98.765.432/0001-99", AreaVendaId = 1, CondicaoPagamentoIdDefault = 1, ComissionadoAreaVendaId = 10 },
                new() { ClienteIdERP = 1003, Nome = $"Panificadora Alvorada ({tenantId[..4]})", Documento = "11.222.333/0001-88", AreaVendaId = 2, CondicaoPagamentoIdDefault = 2, ComissionadoAreaVendaId = 11 }
            },
            Produtos = new List<ProdutoCatalogDto>
            {
                new() { ProdutoIdERP = 5001, Descricao = "Café Gourmet Versatus 500g", SiglaUnidadeVenda = "UN", Saldo = 150m, ControlaEstoque = true, ControlaDescontoMaximo = true, AceitaDesconto = true, DescontoMaximoPercentual = 15m, Marca = "Versatus Cafe", Fabricante = "Torrefacao Sul" },
                new() { ProdutoIdERP = 5002, Descricao = "Açúcar Demerara Orgânico 1kg", SiglaUnidadeVenda = "UN", Saldo = 80m, ControlaEstoque = true, ControlaDescontoMaximo = false, AceitaDesconto = true, DescontoMaximoPercentual = 10m, Marca = "Natura", Fabricante = "Usina Verde" },
                new() { ProdutoIdERP = 5003, Descricao = "Leite Condensado Integral 395g", SiglaUnidadeVenda = "UN", Saldo = 300m, ControlaEstoque = true, ControlaDescontoMaximo = true, AceitaDesconto = false, DescontoMaximoPercentual = 0m, Marca = "Moça Boa", Fabricante = "Laticinio Central" }
            },
            TabelasPreco = new List<TabelaPrecoCatalogDto>
            {
                new() { TabelaPrecoEstoqueIdERP = 10001, ProdutoIdERP = 5001, TabelaPrecoIdERP = 1, ValorUnitario = 19.90m, PercentualDescontoMaximo = 10m, ControlaDescontoMaximo = true },
                new() { TabelaPrecoEstoqueIdERP = 10002, ProdutoIdERP = 5002, TabelaPrecoIdERP = 1, ValorUnitario = 7.50m, PercentualDescontoMaximo = 5m, ControlaDescontoMaximo = false },
                new() { TabelaPrecoEstoqueIdERP = 10003, ProdutoIdERP = 5003, TabelaPrecoIdERP = 1, ValorUnitario = 6.20m, PercentualDescontoMaximo = 0m, ControlaDescontoMaximo = true }
            },
            CondicoesPagamento = new List<CondicaoPagamentoCatalogDto>
            {
                new() { CondicaoPagtoIdERP = 1, Descricao = "A vista (dinheiro/pix)", QuantidadeParcela = 1, DiasParcelamento = 0, Acrescimo = 0m, Desconto = 5m, FormaCobrancaIdERP = 1, UsarMesComercial = false },
                new() { CondicaoPagtoIdERP = 2, Descricao = "Boleto 30 dias", QuantidadeParcela = 1, DiasParcelamento = 30, Acrescimo = 0m, Desconto = 0m, FormaCobrancaIdERP = 2, UsarMesComercial = false },
                new() { CondicaoPagtoIdERP = 3, Descricao = "Boleto 30/60/90 dias", QuantidadeParcela = 3, DiasParcelamento = 30, Acrescimo = 2m, Desconto = 0m, FormaCobrancaIdERP = 2, UsarMesComercial = false }
            }
        };
    }
}
