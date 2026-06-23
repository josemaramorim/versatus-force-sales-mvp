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

public sealed class OrderImporter : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly FtpTransportOptions _ftpOptions;
    private readonly ILogger<OrderImporter> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OrderImporter(
        IConfiguration config,
        IOptions<FtpTransportOptions> ftpOptions,
        ILogger<OrderImporter> logger)
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
        var intervalSeconds = _config.GetValue<int>("ErpAdapter:OrderImportIntervalSeconds", 10);
        if (intervalSeconds <= 0) intervalSeconds = 10;

        _logger.LogInformation("Iniciando OrderImporter com intervalo de {Interval} segundos.", intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var tenants = _config.GetSection("Auth:Tenants").Get<string[]>() ?? Array.Empty<string>();

                foreach (var tenantId in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await ProcessPendingOrdersForTenantAsync(tenantId, stoppingToken);
                }

                // Polling de faturamento real (apenas se não estiver em modo simulação)
                var useSimulatedCatalog = _config.GetValue<bool>("ErpAdapter:UseSimulatedCatalog", false);
                if (!useSimulatedCatalog)
                {
                    await ProcessFaturamentoRetornosAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro fatal ao processar importador de pedidos do ERP.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessPendingOrdersForTenantAsync(string tenantId, CancellationToken ct)
    {
        if (_ftpOptions.UseSftp)
        {
            await ProcessPendingOrdersSftpAsync(tenantId, ct);
        }
        else
        {
            await ProcessPendingOrdersFtpAsync(tenantId, ct);
        }
    }

    private async Task ProcessPendingOrdersFtpAsync(string tenantId, CancellationToken ct)
    {
        using var client = new AsyncFtpClient(_ftpOptions.Host, _ftpOptions.Username, _ftpOptions.Password, _ftpOptions.Port);
        await client.AutoConnect(ct);

        var pendentesDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "pendentes");
        var processandoDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "processando");
        var concluidosDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "concluidos");
        var resultadosDir = FtpFolderStructure.GetResultsDirectory(_ftpOptions.BasePath, tenantId, "pendentes");

        if (!await client.DirectoryExists(pendentesDir, ct))
        {
            await client.Disconnect(ct);
            return;
        }

        var files = await client.GetListing(pendentesDir, FtpListOption.Modify, ct);
        var jsonFiles = files.Where(f => f.Type == FtpObjectType.File && f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();

        if (jsonFiles.Count > 0)
        {
            _logger.LogInformation("[FTP] Encontrados {Count} pedidos pendentes para importar no tenant {TenantId}.", jsonFiles.Count, tenantId);
        }

        foreach (var file in jsonFiles)
        {
            if (ct.IsCancellationRequested) break;

            var sourcePath = file.FullName;
            var processingPath = FtpFolderStructure.GetOrdersFilePath(_ftpOptions.BasePath, tenantId, "processando", file.Name);
            var completedPath = FtpFolderStructure.GetOrdersFilePath(_ftpOptions.BasePath, tenantId, "concluidos", file.Name);

            try
            {
                // 1. Mover arquivo para processando (Atômico)
                await client.CreateDirectory(processandoDir, ct);
                await client.MoveFile(sourcePath, processingPath, FtpRemoteExists.Overwrite, ct);

                // 2. Baixar e ler pedido
                var bytes = await client.DownloadBytes(processingPath, ct);
                var json = Encoding.UTF8.GetString(bytes);
                var order = JsonSerializer.Deserialize<OrderExportPayload>(json, _jsonOptions);

                if (order != null)
                {
                    var useSimulatedCatalog = _config.GetValue<bool>("ErpAdapter:UseSimulatedCatalog", false);
                    if (useSimulatedCatalog)
                    {
                        // 3. Faturar/Processar no ERP (Simulação de negócio)
                        var resultPayload = ProcessOrderBusinessLogic(order);

                        // 4. Salvar resultado em resultados/pendentes/
                        await client.CreateDirectory(resultadosDir, ct);
                        var resultJson = JsonSerializer.Serialize(resultPayload, _jsonOptions);
                        var resultBytes = Encoding.UTF8.GetBytes(resultJson);
                        var resultFilePath = FtpFolderStructure.GetResultsFilePath(_ftpOptions.BasePath, tenantId, "pendentes", $"resultado-{order.PedidoId}.json");
                        await client.UploadBytes(resultBytes, resultFilePath, FtpRemoteExists.Overwrite, true, token: ct);

                        _logger.LogInformation("[FTP SIMULADO] Pedido {PedidoId} importado e resultado simulado publicado.", order.PedidoId);
                    }
                    else
                    {
                        // 3. Gravar no banco real do SQL Server
                        var filialId = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:FilialId", 1);
                        await ProcessOrderInDatabaseAsync(order, filialId);
                    }

                    // 5. Mover pedido para concluidos/
                    await client.CreateDirectory(concluidosDir, ct);
                    await client.MoveFile(processingPath, completedPath, FtpRemoteExists.Overwrite, ct);

                    _logger.LogInformation("[FTP] Pedido {PedidoId} processado com sucesso.", order.PedidoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FTP] Erro ao processar importação do arquivo {FileName} para o tenant {TenantId}.", file.Name, tenantId);
            }
        }

        await client.Disconnect(ct);
    }

    private async Task ProcessPendingOrdersSftpAsync(string tenantId, CancellationToken ct)
    {
        using var client = new SftpClient(_ftpOptions.Host, _ftpOptions.Port, _ftpOptions.Username, _ftpOptions.Password);
        await Task.Run(() => client.Connect(), ct);

        var pendentesDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "pendentes");
        var processandoDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "processando");
        var concluidosDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "concluidos");
        var resultadosDir = FtpFolderStructure.GetResultsDirectory(_ftpOptions.BasePath, tenantId, "pendentes");

        var exists = await Task.Run(() => client.Exists(pendentesDir), ct);
        if (!exists)
        {
            client.Disconnect();
            return;
        }

        var files = await Task.Run(() => client.ListDirectory(pendentesDir), ct);
        var jsonFiles = files.Where(f => f.IsRegularFile && f.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();

        if (jsonFiles.Count > 0)
        {
            _logger.LogInformation("[SFTP] Encontrados {Count} pedidos pendentes para importar no tenant {TenantId}.", jsonFiles.Count, tenantId);
        }

        foreach (var file in jsonFiles)
        {
            if (ct.IsCancellationRequested) break;

            var sourcePath = file.FullName;
            var processingPath = FtpFolderStructure.GetOrdersFilePath(_ftpOptions.BasePath, tenantId, "processando", file.Name);
            var completedPath = FtpFolderStructure.GetOrdersFilePath(_ftpOptions.BasePath, tenantId, "concluidos", file.Name);

            try
            {
                // 1. Mover arquivo para processando (Atômico)
                await EnsureSftpDirExistsAsync(client, processandoDir);
                await Task.Run(() =>
                {
                    if (client.Exists(processingPath)) client.DeleteFile(processingPath);
                    client.RenameFile(sourcePath, processingPath);
                }, ct);

                // 2. Baixar e ler pedido
                using var ms = new MemoryStream();
                await Task.Run(() => client.DownloadFile(processingPath, ms), ct);
                var json = Encoding.UTF8.GetString(ms.ToArray());
                var order = JsonSerializer.Deserialize<OrderExportPayload>(json, _jsonOptions);

                if (order != null)
                {
                    var useSimulatedCatalog = _config.GetValue<bool>("ErpAdapter:UseSimulatedCatalog", false);
                    if (useSimulatedCatalog)
                    {
                        // 3. Faturar/Processar no ERP (Simulação de negócio)
                        var resultPayload = ProcessOrderBusinessLogic(order);

                        // 4. Salvar resultado em resultados/pendentes/
                        await EnsureSftpDirExistsAsync(client, resultadosDir);
                        var resultJson = JsonSerializer.Serialize(resultPayload, _jsonOptions);
                        var resultBytes = Encoding.UTF8.GetBytes(resultJson);
                        var resultFilePath = FtpFolderStructure.GetResultsFilePath(_ftpOptions.BasePath, tenantId, "pendentes", $"resultado-{order.PedidoId}.json");
                        
                        using (var resultMs = new MemoryStream(resultBytes))
                        {
                            await Task.Run(() => client.UploadFile(resultMs, resultFilePath, true), ct);
                        }
                        
                        _logger.LogInformation("[SFTP SIMULADO] Pedido {PedidoId} importado e resultado simulado publicado.", order.PedidoId);
                    }
                    else
                    {
                        // 3. Gravar no banco real do SQL Server
                        var filialId = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:FilialId", 1);
                        await ProcessOrderInDatabaseAsync(order, filialId);
                    }

                    // 5. Mover pedido para concluidos/
                    await EnsureSftpDirExistsAsync(client, concluidosDir);
                    await Task.Run(() =>
                    {
                        if (client.Exists(completedPath)) client.DeleteFile(completedPath);
                        client.RenameFile(processingPath, completedPath);
                    }, ct);

                    _logger.LogInformation("[SFTP] Pedido {PedidoId} processado com sucesso.", order.PedidoId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SFTP] Erro ao processar importação do arquivo {FileName} para o tenant {TenantId}.", file.Name, tenantId);
            }
        }

        client.Disconnect();
    }

    private async Task ProcessOrderInDatabaseAsync(OrderExportPayload order, int filialId)
    {
        var erpConnectionString = _config.GetConnectionString("ErpDatabase") ?? string.Empty;
        using var conn = new SqlConnection(erpConnectionString);
        await conn.OpenAsync();

        // 1. Verificar se já existe a venda importada por Código de Integração (Deduplicação / Idempotência)
        using (var checkCmd = new SqlCommand("SELECT COUNT(*) FROM MOBVENDA WHERE CODIGOINTEGRACAO = @CodigoIntegracao AND IDGLOFILIAL = @FilialId", conn))
        {
            checkCmd.Parameters.AddWithValue("@CodigoIntegracao", order.PedidoId.ToString());
            checkCmd.Parameters.AddWithValue("@FilialId", filialId);
            var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
            if (count > 0)
            {
                _logger.LogInformation("Pedido {PedidoId} já foi importado anteriormente na tabela MOBVENDA.", order.PedidoId);
                return;
            }
        }

        using var transaction = conn.BeginTransaction();
        try
        {
            // 2. Obter próximo IDMOBVENDA (geração de sequencial manual do ERP)
            int idMobVenda;
            using (var idCmd = new SqlCommand("SELECT COALESCE(MAX(IDMOBVENDA), 0) + 1 FROM MOBVENDA", conn, transaction))
            {
                idMobVenda = Convert.ToInt32(await idCmd.ExecuteScalarAsync());
            }

            // 3. Obter nome do cliente do banco do ERP para preencher NOMEPRECLIENTE
            string clienteNome = "FORCA VENDAS";
            using (var nameCmd = new SqlCommand("SELECT NOME FROM GLOCLIENTE WHERE IDGLOCLIENTE = @ClienteId", conn, transaction))
            {
                nameCmd.Parameters.AddWithValue("@ClienteId", order.Payload.ClienteIdERP);
                var obj = await nameCmd.ExecuteScalarAsync();
                if (obj != null && obj != DBNull.Value)
                {
                    clienteNome = obj.ToString() ?? "FORCA VENDAS";
                }
            }

            // 4. Inserir na MOBVENDA (Cabeçalho do Pedido)
            var dataEmissao = DateTime.TryParse(order.Payload.DataEmissao, out var dt) ? dt : DateTime.Now;
            using (var cmd = new SqlCommand(@"
                INSERT INTO MOBVENDA (
                    IDMOBVENDA, IDGLOFILIAL, IDMOBCLIENTE, NOMEPRECLIENTE, IDMOBCONDICAOPAGAMENTO, 
                    DATAEMISSAO, VALORTOTAL, DESCONTO, ACRESCIMO, NOMEUSUARIO, CHAVEDISPOSITIVO, 
                    ORCAMENTO, OBSERVACAO, EXPORTADA, PROCESSADA, IDGLOCOMISSIONADO, IDVENDOCUMENTO, 
                    IDMOBVENDAIMPORTACAO, OBSERVACAOGERACAOVENDA, NOVOCLIENTE, VALORFRETE, 
                    IDTIPOPLATAFORMA, CODIGOINTEGRACAO
                ) VALUES (
                    @IDMOBVENDA, @IDGLOFILIAL, @IDMOBCLIENTE, @NOMEPRECLIENTE, @IDMOBCONDICAOPAGAMENTO, 
                    @DATAEMISSAO, @VALORTOTAL, @DESCONTO, @ACRESCIMO, @NOMEUSUARIO, @CHAVEDISPOSITIVO, 
                    @ORCAMENTO, @OBSERVACAO, @EXPORTADA, @PROCESSADA, @IDGLOCOMISSIONADO, NULL, 
                    NULL, NULL, 0, @VALORFRETE, 
                    1, @CODIGOINTEGRACAO
                )", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@IDMOBVENDA", idMobVenda);
                cmd.Parameters.AddWithValue("@IDGLOFILIAL", filialId);
                cmd.Parameters.AddWithValue("@IDMOBCLIENTE", order.Payload.ClienteIdERP);
                cmd.Parameters.AddWithValue("@NOMEPRECLIENTE", clienteNome.Length > 100 ? clienteNome.Substring(0, 100) : clienteNome);
                cmd.Parameters.AddWithValue("@IDMOBCONDICAOPAGAMENTO", order.Payload.CondicaoPagamentoIdERP);
                cmd.Parameters.AddWithValue("@DATAEMISSAO", dataEmissao);
                cmd.Parameters.AddWithValue("@VALORTOTAL", order.Payload.ValorFinal);
                cmd.Parameters.AddWithValue("@DESCONTO", order.Payload.ValorTotalDesconto);
                cmd.Parameters.AddWithValue("@ACRESCIMO", order.Payload.ValorTotalAcrescimo);
                cmd.Parameters.AddWithValue("@NOMEUSUARIO", "ForcaVendas");
                cmd.Parameters.AddWithValue("@CHAVEDISPOSITIVO", "Web");
                cmd.Parameters.AddWithValue("@ORCAMENTO", order.Payload.Orcamento ? 1 : 0);
                cmd.Parameters.AddWithValue("@OBSERVACAO", (object?)order.Payload.Observacao ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@EXPORTADA", 0);
                cmd.Parameters.AddWithValue("@PROCESSADA", 0);
                cmd.Parameters.AddWithValue("@IDGLOCOMISSIONADO", 1); // Padrão comissionado
                cmd.Parameters.AddWithValue("@VALORFRETE", order.Payload.ValorFrete);
                cmd.Parameters.AddWithValue("@CODIGOINTEGRACAO", order.PedidoId.ToString());

                await cmd.ExecuteNonQueryAsync();
            }

            // 4. Inserir itens na MOBVENDAITEM
            foreach (var item in order.Payload.Itens)
            {
                int idMobVendaItem;
                using (var idItemCmd = new SqlCommand("SELECT COALESCE(MAX(IDMOBVENDAITEM), 0) + 1 FROM MOBVENDAITEM", conn, transaction))
                {
                    idMobVendaItem = Convert.ToInt32(await idItemCmd.ExecuteScalarAsync());
                }

                using (var cmd = new SqlCommand(@"
                    INSERT INTO MOBVENDAITEM (
                        IDMOBVENDAITEM, IDMOBVENDA, IDGLOFILIAL, IDMOBESTOQUE, IDMOBTABELAPRECOESTOQUE, 
                        QUANTIDADE, VALORUNITARIO, DESCONTO, ACRESCIMO, VALORTOTAL, SIGLAUNIDADE, 
                        OBSERVACAOGERACAOVENDA
                    ) VALUES (
                        @IDMOBVENDAITEM, @IDMOBVENDA, @IDGLOFILIAL, @IDMOBESTOQUE, @IDMOBTABELAPRECOESTOQUE, 
                        @QUANTIDADE, @VALORUNITARIO, @DESCONTO, @ACRESCIMO, @VALORTOTAL, @SIGLAUNIDADE, 
                        NULL
                    )", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@IDMOBVENDAITEM", idMobVendaItem);
                    cmd.Parameters.AddWithValue("@IDMOBVENDA", idMobVenda);
                    cmd.Parameters.AddWithValue("@IDGLOFILIAL", filialId);
                    cmd.Parameters.AddWithValue("@IDMOBESTOQUE", item.ProdutoIdERP);
                    cmd.Parameters.AddWithValue("@IDMOBTABELAPRECOESTOQUE", item.TabelaPrecoEstoqueIdERP);
                    cmd.Parameters.AddWithValue("@QUANTIDADE", item.Quantidade);
                    cmd.Parameters.AddWithValue("@VALORUNITARIO", item.PrecoUnitario);
                    cmd.Parameters.AddWithValue("@DESCONTO", item.ValorDesconto);
                    cmd.Parameters.AddWithValue("@ACRESCIMO", item.ValorAcrescimo);
                    cmd.Parameters.AddWithValue("@VALORTOTAL", item.ValorFinal);
                    cmd.Parameters.AddWithValue("@SIGLAUNIDADE", item.SiglaUnidade);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            // 5. Inserir parcelas na MOBVENDAPARCELA
            foreach (var parcela in order.Payload.Parcelas)
            {
                int idMobVendaParcela;
                using (var idParcelaCmd = new SqlCommand("SELECT COALESCE(MAX(IDMOBVENDAPARCELA), 0) + 1 FROM MOBVENDAPARCELA", conn, transaction))
                {
                    idMobVendaParcela = Convert.ToInt32(await idParcelaCmd.ExecuteScalarAsync());
                }

                var vencimento = DateTime.TryParse(parcela.Vencimento, out var dtVenc) ? dtVenc : DateTime.Now.AddDays(30);
                using (var cmd = new SqlCommand(@"
                    INSERT INTO MOBVENDAPARCELA (
                        IDMOBVENDAPARCELA, IDMOBVENDA, IDGLOFILIAL, NUMEROPARCELA, 
                        IDMOBFORMACOBRANCA, VALOR, DATAVENCIMENTO
                    ) VALUES (
                        @IDMOBVENDAPARCELA, @IDMOBVENDA, @IDGLOFILIAL, @NUMEROPARCELA, 
                        @IDMOBFORMACOBRANCA, @VALOR, @DATAVENCIMENTO
                    )", conn, transaction))
                {
                    cmd.Parameters.AddWithValue("@IDMOBVENDAPARCELA", idMobVendaParcela);
                    cmd.Parameters.AddWithValue("@IDMOBVENDA", idMobVenda);
                    cmd.Parameters.AddWithValue("@IDGLOFILIAL", filialId);
                    cmd.Parameters.AddWithValue("@NUMEROPARCELA", parcela.Numero);
                    cmd.Parameters.AddWithValue("@IDMOBFORMACOBRANCA", parcela.FormaCobrancaIdERP);
                    cmd.Parameters.AddWithValue("@VALOR", parcela.Valor);
                    cmd.Parameters.AddWithValue("@DATAVENCIMENTO", vencimento);

                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            _logger.LogInformation("Pedido {PedidoId} gravado com sucesso no SQL Server (MOBVENDA ID: {IdMobVenda})", order.PedidoId, idMobVenda);
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task ProcessFaturamentoRetornosAsync(CancellationToken ct)
    {
        var erpConnectionString = _config.GetConnectionString("ErpDatabase") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(erpConnectionString)) return;

        var pendingResults = new List<PendingResultItem>();
        using (var conn = new SqlConnection(erpConnectionString))
        {
            await conn.OpenAsync(ct);
            using (var cmd = new SqlCommand(@"
                SELECT IDMOBVENDA, IDVENDOCUMENTO, CODIGOINTEGRACAO, IDGLOFILIAL 
                FROM MOBVENDA 
                WHERE PROCESSADA = 1 AND EXPORTADA = 0 AND IDVENDOCUMENTO IS NOT NULL", conn))
            using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    pendingResults.Add(new PendingResultItem
                    {
                        IdMobVenda = reader.GetInt32(0),
                        DocumentoVendaId = reader.GetInt32(1).ToString(),
                        PedidoIdStr = reader.GetString(2),
                        FilialId = reader.GetInt32(3)
                    });
                }
            }
        }

        if (pendingResults.Count == 0) return;

        _logger.LogInformation("Encontrados {Count} faturamentos no SQL Server aguardando exportação de resultado.", pendingResults.Count);

        // Obter mapeamento reverso de FilialId para TenantId
        var tenantsMapping = new Dictionary<int, string>();
        var tenants = _config.GetSection("Auth:Tenants").Get<string[]>() ?? Array.Empty<string>();
        foreach (var tenantId in tenants)
        {
            var filialId = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:FilialId", 1);
            if (!tenantsMapping.ContainsKey(filialId))
            {
                tenantsMapping.Add(filialId, tenantId);
            }
        }

        using var connUpdate = new SqlConnection(erpConnectionString);
        await connUpdate.OpenAsync(ct);

        foreach (var item in pendingResults)
        {
            if (ct.IsCancellationRequested) break;

            if (!Guid.TryParse(item.PedidoIdStr, out var pedidoId))
            {
                _logger.LogWarning("Código de integração {Codigo} inválido para faturamento da venda {IdMobVenda}.", item.PedidoIdStr, item.IdMobVenda);
                continue;
            }

            if (!tenantsMapping.TryGetValue(item.FilialId, out var tenantId))
            {
                _logger.LogWarning("Não foi possível encontrar um tenant para a FilialId {FilialId} do faturamento {IdMobVenda}. Usando primeiro tenant disponível.", item.FilialId, item.IdMobVenda);
                tenantId = tenants.Length > 0 ? tenants[0] : "00000000-0000-0000-0000-000000000001";
            }

            var resultPayload = new OrderResultPayload
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                TenantId = tenantId,
                PedidoId = pedidoId,
                Payload = new OrderResultData
                {
                    Resultado = "processado",
                    DocumentoVendaId = $"NF-2026-{item.DocumentoVendaId}",
                    MotivoRejeicao = null,
                    SourceEventId = Guid.NewGuid()
                }
            };

            try
            {
                // Exportar resultado para o FTP
                await UploadResultToFtpAsync(tenantId, resultPayload, ct);

                // Marcar como exportada no banco
                using (var updateCmd = new SqlCommand("UPDATE MOBVENDA SET EXPORTADA = 1 WHERE IDMOBVENDA = @IdMobVenda", connUpdate))
                {
                    updateCmd.Parameters.AddWithValue("@IdMobVenda", item.IdMobVenda);
                    await updateCmd.ExecuteNonQueryAsync(ct);
                }

                _logger.LogInformation("Resultado de faturamento do pedido {PedidoId} exportado e marcado no banco.", pedidoId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao exportar resultado de faturamento para o pedido {PedidoId}.", pedidoId);
            }
        }
    }

    private async Task UploadResultToFtpAsync(string tenantId, OrderResultPayload result, CancellationToken ct)
    {
        var resultJson = JsonSerializer.Serialize(result, _jsonOptions);
        var resultBytes = Encoding.UTF8.GetBytes(resultJson);
        var resultFilePath = FtpFolderStructure.GetResultsFilePath(_ftpOptions.BasePath, tenantId, "pendentes", $"resultado-{result.PedidoId}.json");
        var resultDir = FtpFolderStructure.GetResultsDirectory(_ftpOptions.BasePath, tenantId, "pendentes");

        if (_ftpOptions.UseSftp)
        {
            using var client = new SftpClient(_ftpOptions.Host, _ftpOptions.Port, _ftpOptions.Username, _ftpOptions.Password);
            await Task.Run(() => client.Connect(), ct);
            await EnsureSftpDirExistsAsync(client, resultDir);
            
            using var resultMs = new MemoryStream(resultBytes);
            await Task.Run(() => client.UploadFile(resultMs, resultFilePath, true), ct);
            client.Disconnect();
        }
        else
        {
            using var client = new AsyncFtpClient(_ftpOptions.Host, _ftpOptions.Username, _ftpOptions.Password, _ftpOptions.Port);
            await client.AutoConnect(ct);
            await client.CreateDirectory(resultDir, ct);
            await client.UploadBytes(resultBytes, resultFilePath, FtpRemoteExists.Overwrite, true, token: ct);
            await client.Disconnect(ct);
        }
    }

    private async Task EnsureSftpDirExistsAsync(SftpClient client, string path)
    {
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var currentPath = path.StartsWith("/") ? "/" : "";
        foreach (var part in parts)
        {
            currentPath = currentPath == "/" ? $"/{part}" : (string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}");
            if (!client.Exists(currentPath))
            {
                client.CreateDirectory(currentPath);
            }
        }
    }

    private OrderResultPayload ProcessOrderBusinessLogic(OrderExportPayload order)
    {
        var hasErrorTrigger = !string.IsNullOrEmpty(order.Payload.Observacao) &&
            (order.Payload.Observacao.Contains("erro", StringComparison.OrdinalIgnoreCase) ||
             order.Payload.Observacao.Contains("rejeitar", StringComparison.OrdinalIgnoreCase));

        if (hasErrorTrigger)
        {
            return new OrderResultPayload
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                TenantId = order.TenantId,
                PedidoId = order.PedidoId,
                Payload = new OrderResultData
                {
                    Resultado = "erro",
                    DocumentoVendaId = null,
                    MotivoRejeicao = "Pedido rejeitado de forma simulada devido à flag de erro na observação.",
                    SourceEventId = order.EventId
                }
            };
        }
        else
        {
            var randomDocNum = new Random().Next(10000, 99999);
            return new OrderResultPayload
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                TenantId = order.TenantId,
                PedidoId = order.PedidoId,
                Payload = new OrderResultData
                {
                    Resultado = "processado",
                    DocumentoVendaId = $"NF-2026-{randomDocNum}",
                    MotivoRejeicao = null,
                    SourceEventId = order.EventId
                }
            };
        }
    }

    private class PendingResultItem
    {
        public int IdMobVenda { get; set; }
        public string DocumentoVendaId { get; set; } = string.Empty;
        public string PedidoIdStr { get; set; } = string.Empty;
        public int FilialId { get; set; }
    }
}
