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
            PropertyNameCaseInsensitive = true,
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
                
                try
                {
                    Guid pedidoId = Guid.Empty;
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                    if (nameWithoutExt.StartsWith("pedido-", StringComparison.OrdinalIgnoreCase))
                    {
                        var guidStr = nameWithoutExt.Substring(7);
                        Guid.TryParse(guidStr, out pedidoId);
                    }

                    // 1. Mover arquivo de processando/ para erros/
                    var errosDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "erros");
                    var errosPath = FtpFolderStructure.GetOrdersFilePath(_ftpOptions.BasePath, tenantId, "erros", file.Name);

                    await client.CreateDirectory(errosDir, ct);
                    if (await client.FileExists(processingPath, ct))
                    {
                        await client.MoveFile(processingPath, errosPath, FtpRemoteExists.Overwrite, ct);
                    }

                    // 2. Publicar o resultado de erro para notificar o vendedor
                    if (pedidoId != Guid.Empty)
                    {
                        var resultPayload = new OrderResultPayload
                        {
                            EventId = Guid.NewGuid(),
                            CreatedAt = DateTimeOffset.UtcNow,
                            TenantId = tenantId,
                            PedidoId = pedidoId,
                            Payload = new OrderResultData
                            {
                                Resultado = "erro",
                                DocumentoVendaId = null,
                                MotivoRejeicao = $"Erro ao processar no ERP: {ex.Message}",
                                SourceEventId = Guid.Empty
                            }
                        };

                        await client.CreateDirectory(resultadosDir, ct);
                        var resultJson = JsonSerializer.Serialize(resultPayload, _jsonOptions);
                        var resultBytes = Encoding.UTF8.GetBytes(resultJson);
                        var resultFilePath = FtpFolderStructure.GetResultsFilePath(_ftpOptions.BasePath, tenantId, "pendentes", $"resultado-{pedidoId}.json");
                        await client.UploadBytes(resultBytes, resultFilePath, FtpRemoteExists.Overwrite, true, token: ct);
                        
                        _logger.LogInformation("[FTP] Resultado de erro publicado para o Pedido {PedidoId}.", pedidoId);
                    }
                }
                catch (Exception moveEx)
                {
                    _logger.LogError(moveEx, "[FTP] Falha crítica ao mover arquivo com erro ou publicar resultado para o arquivo {FileName}.", file.Name);
                }
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

                try
                {
                    Guid pedidoId = Guid.Empty;
                    var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                    if (nameWithoutExt.StartsWith("pedido-", StringComparison.OrdinalIgnoreCase))
                    {
                        var guidStr = nameWithoutExt.Substring(7);
                        Guid.TryParse(guidStr, out pedidoId);
                    }

                    // 1. Mover arquivo de processando/ para erros/
                    var errosDir = FtpFolderStructure.GetOrdersDirectory(_ftpOptions.BasePath, tenantId, "erros");
                    var errosPath = FtpFolderStructure.GetOrdersFilePath(_ftpOptions.BasePath, tenantId, "erros", file.Name);

                    await EnsureSftpDirExistsAsync(client, errosDir);
                    await Task.Run(() =>
                    {
                        if (client.Exists(processingPath))
                        {
                            if (client.Exists(errosPath)) client.DeleteFile(errosPath);
                            client.RenameFile(processingPath, errosPath);
                        }
                    }, ct);

                    // 2. Publicar o resultado de erro para notificar o vendedor
                    if (pedidoId != Guid.Empty)
                    {
                        var resultPayload = new OrderResultPayload
                        {
                            EventId = Guid.NewGuid(),
                            CreatedAt = DateTimeOffset.UtcNow,
                            TenantId = tenantId,
                            PedidoId = pedidoId,
                            Payload = new OrderResultData
                            {
                                Resultado = "erro",
                                DocumentoVendaId = null,
                                MotivoRejeicao = $"Erro ao processar no ERP: {ex.Message}",
                                SourceEventId = Guid.Empty
                            }
                        };

                        await EnsureSftpDirExistsAsync(client, resultadosDir);
                        var resultJson = JsonSerializer.Serialize(resultPayload, _jsonOptions);
                        var resultBytes = Encoding.UTF8.GetBytes(resultJson);
                        var resultFilePath = FtpFolderStructure.GetResultsFilePath(_ftpOptions.BasePath, tenantId, "pendentes", $"resultado-{pedidoId}.json");
                        
                        using (var resultMs = new MemoryStream(resultBytes))
                        {
                            await Task.Run(() => client.UploadFile(resultMs, resultFilePath, true), ct);
                        }

                        _logger.LogInformation("[SFTP] Resultado de erro publicado para o Pedido {PedidoId}.", pedidoId);
                    }
                }
                catch (Exception moveEx)
                {
                    _logger.LogError(moveEx, "[SFTP] Falha crítica ao mover arquivo com erro ou publicar resultado para o arquivo {FileName}.", file.Name);
                }
            }
        }

        client.Disconnect();
    }

    private async Task ProcessOrderInDatabaseAsync(OrderExportPayload order, int filialId)
    {
        var erpConnectionString = _config.GetConnectionString("ErpDatabase") ?? string.Empty;
        using var conn = new SqlConnection(erpConnectionString);
        await conn.OpenAsync();

        // 1. Buscar limites das colunas no banco de dados para evitar String or Binary Truncation
        var mobVendaLimits = await GetColumnMaxLengthsAsync(conn, null, "MOBVENDA");
        var mobVendaItemLimits = await GetColumnMaxLengthsAsync(conn, null, "MOBVENDAITEM");

        // 2. Verificar se já existe a venda importada por Código de Integração (Deduplicação / Idempotência)
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

            // 3. Obter nome do cliente do banco do ERP para preencher NOMEPRECLIENTE ou do preCliente
            string clienteNome = "FORCA VENDAS";
            if (order.Payload.IsNovoCliente && order.Payload.PreCliente != null)
            {
                clienteNome = order.Payload.PreCliente.Nome;

                // Inserir na MOBPRECLIENTE
                var preClienteCols = await GetTableColumnsAsync(conn, transaction, "MOBPRECLIENTE");
                var preClienteLimits = await GetColumnMaxLengthsAsync(conn, transaction, "MOBPRECLIENTE");

                // Determinar se a PK (IDMOBPRECLIENTE) deve ser fornecida manualmente
                // ou se é IDENTITY (auto-gerada pelo banco)
                bool pkExiste = preClienteCols.Contains("IDMOBPRECLIENTE");
                bool pkIsIdentity = await IsIdentityColumnAsync(conn, transaction, "MOBPRECLIENTE", "IDMOBPRECLIENTE");

                var colList = new List<string> { "IDGLOFILIAL", "NOME" };
                var paramList = new List<string> { "@IDGLOFILIAL", "@NOME" };

                var preParams = new List<SqlParameter>
                {
                    new SqlParameter("@IDGLOFILIAL", filialId),
                    new SqlParameter("@NOME", SafeSubstring(order.Payload.PreCliente.Nome, "NOME", preClienteLimits, 150))
                };

                if (pkExiste && !pkIsIdentity)
                {
                    int idMobPreCliente;
                    using (var preIdCmd = new SqlCommand("SELECT COALESCE(MAX(IDMOBPRECLIENTE), 0) + 1 FROM MOBPRECLIENTE", conn, transaction))
                    {
                        idMobPreCliente = Convert.ToInt32(await preIdCmd.ExecuteScalarAsync());
                    }
                    colList.Insert(0, "IDMOBPRECLIENTE");
                    paramList.Insert(0, "@IDMOBPRECLIENTE");
                    preParams.Insert(0, new SqlParameter("@IDMOBPRECLIENTE", idMobPreCliente));
                }

                string? docCol = null;
                if (preClienteCols.Contains("DOCUMENTO")) docCol = "DOCUMENTO";
                else if (preClienteCols.Contains("CNPJCPF")) docCol = "CNPJCPF";
                else if (preClienteCols.Contains("CPFCNPJ")) docCol = "CPFCNPJ";

                if (docCol != null)
                {
                    colList.Add(docCol);
                    paramList.Add("@DOCUMENTO");
                    preParams.Add(new SqlParameter("@DOCUMENTO", SafeSubstring(order.Payload.PreCliente.Documento, docCol, preClienteLimits, 20)));
                }

                string? telCol = null;
                if (preClienteCols.Contains("TELEFONE")) telCol = "TELEFONE";
                else if (preClienteCols.Contains("FONE")) telCol = "FONE";

                if (telCol != null && order.Payload.PreCliente.Telefone != null)
                {
                    colList.Add(telCol);
                    paramList.Add("@TELEFONE");
                    preParams.Add(new SqlParameter("@TELEFONE", SafeSubstring(order.Payload.PreCliente.Telefone, telCol, preClienteLimits, 20)));
                }

                if (preClienteCols.Contains("EMAIL") && order.Payload.PreCliente.Email != null)
                {
                    colList.Add("EMAIL");
                    paramList.Add("@EMAIL");
                    preParams.Add(new SqlParameter("@EMAIL", SafeSubstring(order.Payload.PreCliente.Email, "EMAIL", preClienteLimits, 100)));
                }

                string? logCol = null;
                if (preClienteCols.Contains("LOGRADOURO")) logCol = "LOGRADOURO";
                else if (preClienteCols.Contains("ENDERECO")) logCol = "ENDERECO";

                if (logCol != null && order.Payload.PreCliente.Logradouro != null)
                {
                    colList.Add(logCol);
                    paramList.Add("@LOGRADOURO");
                    preParams.Add(new SqlParameter("@LOGRADOURO", SafeSubstring(order.Payload.PreCliente.Logradouro, logCol, preClienteLimits, 100)));
                }

                if (preClienteCols.Contains("NUMERO") && order.Payload.PreCliente.Numero != null)
                {
                    colList.Add("NUMERO");
                    paramList.Add("@NUMERO");
                    preParams.Add(new SqlParameter("@NUMERO", SafeSubstring(order.Payload.PreCliente.Numero, "NUMERO", preClienteLimits, 20)));
                }

                if (preClienteCols.Contains("COMPLEMENTO") && order.Payload.PreCliente.Complemento != null)
                {
                    colList.Add("COMPLEMENTO");
                    paramList.Add("@COMPLEMENTO");
                    preParams.Add(new SqlParameter("@COMPLEMENTO", SafeSubstring(order.Payload.PreCliente.Complemento, "COMPLEMENTO", preClienteLimits, 50)));
                }

                if (preClienteCols.Contains("BAIRRO") && order.Payload.PreCliente.Bairro != null)
                {
                    colList.Add("BAIRRO");
                    paramList.Add("@BAIRRO");
                    preParams.Add(new SqlParameter("@BAIRRO", SafeSubstring(order.Payload.PreCliente.Bairro, "BAIRRO", preClienteLimits, 50)));
                }

                string? cidCol = null;
                if (preClienteCols.Contains("CIDADE")) cidCol = "CIDADE";
                else if (preClienteCols.Contains("MUNICIPIO")) cidCol = "MUNICIPIO";

                if (cidCol != null && order.Payload.PreCliente.Cidade != null)
                {
                    colList.Add(cidCol);
                    paramList.Add("@CIDADE");
                    preParams.Add(new SqlParameter("@CIDADE", SafeSubstring(order.Payload.PreCliente.Cidade, cidCol, preClienteLimits, 50)));
                }

                string? ufCol = null;
                if (preClienteCols.Contains("UF")) ufCol = "UF";
                else if (preClienteCols.Contains("ESTADO")) ufCol = "ESTADO";

                if (ufCol != null && order.Payload.PreCliente.Uf != null)
                {
                    colList.Add(ufCol);
                    paramList.Add("@UF");
                    preParams.Add(new SqlParameter("@UF", SafeSubstring(order.Payload.PreCliente.Uf, ufCol, preClienteLimits, 2)));
                }

                if (preClienteCols.Contains("CEP") && order.Payload.PreCliente.Cep != null)
                {
                    colList.Add("CEP");
                    paramList.Add("@CEP");
                    preParams.Add(new SqlParameter("@CEP", SafeSubstring(order.Payload.PreCliente.Cep, "CEP", preClienteLimits, 15)));
                }

                var insertSql = $"INSERT INTO MOBPRECLIENTE ({string.Join(", ", colList)}) VALUES ({string.Join(", ", paramList)})";
                using (var preCmd = new SqlCommand(insertSql, conn, transaction))
                {
                    preCmd.Parameters.AddRange(preParams.ToArray());
                    await preCmd.ExecuteNonQueryAsync();
                }
            }
            else
            {
                using (var nameCmd = new SqlCommand("SELECT TOP 1 NOME FROM VWCLIENTE WHERE IDGLOCLIENTE = @ClienteId", conn, transaction))
                {
                    nameCmd.Parameters.AddWithValue("@ClienteId", order.Payload.ClienteIdERP);
                    var obj = await nameCmd.ExecuteScalarAsync();
                    if (obj != null && obj != DBNull.Value)
                    {
                        clienteNome = obj.ToString() ?? "FORCA VENDAS";
                    }
                }
            }

            // 4. Inserir na MOBVENDA (Cabeçalho do Pedido)
            var dataEmissao = DateTime.TryParse(order.Payload.DataEmissao, out var dt) ? dt : DateTime.Now;
            if (dataEmissao.Date > DateTime.Today)
            {
                dataEmissao = DateTime.Today; // Previne trava de venda futura devido a diferença de fuso horário
            }
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
                    NULL, NULL, @NOVOCLIENTE, @VALORFRETE, 
                    1, @CODIGOINTEGRACAO
                )", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@IDMOBVENDA", idMobVenda);
                cmd.Parameters.AddWithValue("@IDGLOFILIAL", filialId);
                cmd.Parameters.AddWithValue("@IDMOBCLIENTE", order.Payload.IsNovoCliente ? DBNull.Value : (object)order.Payload.ClienteIdERP);
                cmd.Parameters.AddWithValue("@NOMEPRECLIENTE", SafeSubstring(clienteNome, "NOMEPRECLIENTE", mobVendaLimits, 100));
                cmd.Parameters.AddWithValue("@IDMOBCONDICAOPAGAMENTO", order.Payload.CondicaoPagamentoIdERP);
                cmd.Parameters.AddWithValue("@DATAEMISSAO", dataEmissao);
                cmd.Parameters.AddWithValue("@VALORTOTAL", order.Payload.ValorFinal);
                cmd.Parameters.AddWithValue("@DESCONTO", order.Payload.ValorTotalDesconto);
                cmd.Parameters.AddWithValue("@ACRESCIMO", order.Payload.ValorTotalAcrescimo);
                cmd.Parameters.AddWithValue("@NOMEUSUARIO", SafeSubstring("ForcaVendas", "NOMEUSUARIO", mobVendaLimits, 10));
                cmd.Parameters.AddWithValue("@CHAVEDISPOSITIVO", SafeSubstring("Web", "CHAVEDISPOSITIVO", mobVendaLimits, 5));
                cmd.Parameters.AddWithValue("@ORCAMENTO", order.Payload.Orcamento ? 1 : 0);
                var obsVal = order.Payload.Observacao;
                cmd.Parameters.AddWithValue("@OBSERVACAO", obsVal != null ? (object)SafeSubstring(obsVal, "OBSERVACAO", mobVendaLimits, 250) : DBNull.Value);
                cmd.Parameters.AddWithValue("@EXPORTADA", 0);
                cmd.Parameters.AddWithValue("@PROCESSADA", 0);
                cmd.Parameters.AddWithValue("@IDGLOCOMISSIONADO", 1); // Padrão comissionado
                cmd.Parameters.AddWithValue("@VALORFRETE", order.Payload.ValorFrete);
                cmd.Parameters.AddWithValue("@NOVOCLIENTE", order.Payload.IsNovoCliente ? 1 : 0);
                cmd.Parameters.AddWithValue("@CODIGOINTEGRACAO", SafeSubstring(order.PedidoId.ToString(), "CODIGOINTEGRACAO", mobVendaLimits, 50));

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
                    cmd.Parameters.AddWithValue("@SIGLAUNIDADE", SafeSubstring(item.SiglaUnidade, "SIGLAUNIDADE", mobVendaItemLimits, 6, "UN"));

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
                SELECT IDMOBVENDA, IDVENDOCUMENTO, CODIGOINTEGRACAO, IDGLOFILIAL, COALESCE(NOVOCLIENTE, 0), IDMOBCLIENTE, NOMEPRECLIENTE
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
                        FilialId = reader.GetInt32(3),
                        NovoCliente = reader.GetInt32(4),
                        IdMobCliente = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                        NomePreCliente = reader.IsDBNull(6) ? null : reader.GetString(6)
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
                    ClienteIdERP = item.NovoCliente == 1 ? item.IdMobCliente : null,
                    SourceEventId = Guid.NewGuid()
                }
            };

            try
            {
                // Exportar resultado para o FTP
                await UploadResultToFtpAsync(tenantId, resultPayload, ct);

                // Executar atualizações locais em transação atômica
                using (var transaction = connUpdate.BeginTransaction())
                {
                    try
                    {
                        // Marcar como exportada no banco
                        using (var updateCmd = new SqlCommand("UPDATE MOBVENDA SET EXPORTADA = 1 WHERE IDMOBVENDA = @IdMobVenda", connUpdate, transaction))
                        {
                            updateCmd.Parameters.AddWithValue("@IdMobVenda", item.IdMobVenda);
                            await updateCmd.ExecuteNonQueryAsync(ct);
                        }

                        // Se for novo cliente, deletar do MOBPRECLIENTE local
                        if (item.NovoCliente == 1 && !string.IsNullOrWhiteSpace(item.NomePreCliente))
                        {
                            using (var deleteCmd = new SqlCommand("DELETE FROM MOBPRECLIENTE WHERE NOME = @NomePreCliente AND IDGLOFILIAL = @FilialId", connUpdate, transaction))
                            {
                                deleteCmd.Parameters.AddWithValue("@NomePreCliente", item.NomePreCliente);
                                deleteCmd.Parameters.AddWithValue("@FilialId", item.FilialId);
                                await deleteCmd.ExecuteNonQueryAsync(ct);
                            }
                            _logger.LogInformation("Pré-cliente '{NomePreCliente}' deletado da tabela MOBPRECLIENTE com sucesso após exportação do faturamento.", item.NomePreCliente);
                        }

                        await transaction.CommitAsync(ct);
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync(ct);
                        throw;
                    }
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

    private async Task<Dictionary<string, int>> GetColumnMaxLengthsAsync(SqlConnection conn, SqlTransaction? transaction, string tableName)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var cmd = new SqlCommand(@"
                SELECT COLUMN_NAME, CHARACTER_MAXIMUM_LENGTH 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @TableName AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL AND CHARACTER_MAXIMUM_LENGTH > 0", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result[reader.GetString(0)] = reader.GetInt32(1);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nao foi possivel consultar limites de colunas para tabela {TableName}: {Message}", tableName, ex.Message);
        }
        return result;
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(SqlConnection conn, SqlTransaction? transaction, string tableName)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using (var cmd = new SqlCommand(@"
                SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @TableName", conn, transaction))
            {
                cmd.Parameters.AddWithValue("@TableName", tableName);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        cols.Add(reader.GetString(0));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nao foi possivel consultar colunas para tabela {TableName}: {Message}", tableName, ex.Message);
        }
        return cols;
    }

    private string SafeSubstring(string? val, string columnName, Dictionary<string, int> limits, int fallbackLimit, string defaultVal = "")
    {
        var str = val ?? defaultVal;
        int maxLen = fallbackLimit;
        if (limits.TryGetValue(columnName, out var dbLimit) && dbLimit > 0)
        {
            maxLen = dbLimit;
        }

        if (str.Length > maxLen)
        {
            _logger.LogWarning("Dados excedem o tamanho da coluna {Column}. Valor original '{Original}' (tamanho {OriginalLength}) sera truncado para {MaxLength} caracteres.", 
                columnName, str, str.Length, maxLen);
            return str.Substring(0, maxLen);
        }
        return str;
    }

    private class PendingResultItem
    {
        public int IdMobVenda { get; set; }
        public string DocumentoVendaId { get; set; } = string.Empty;
        public string PedidoIdStr { get; set; } = string.Empty;
        public int FilialId { get; set; }
        public int NovoCliente { get; set; }
        public int? IdMobCliente { get; set; }
        public string? NomePreCliente { get; set; }
    }

    private async Task<bool> IsIdentityColumnAsync(SqlConnection conn, SqlTransaction? transaction, string tableName, string columnName)
    {
        try
        {
            using var cmd = new SqlCommand(@"
                SELECT COUNT(1)
                FROM sys.columns c
                JOIN sys.objects o ON c.object_id = o.object_id
                WHERE o.name = @TableName
                  AND c.name = @ColumnName
                  AND c.is_identity = 1", conn, transaction);

            cmd.Parameters.AddWithValue("@TableName", tableName);
            cmd.Parameters.AddWithValue("@ColumnName", columnName);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Nao foi possivel verificar identity para coluna {Column} da tabela {Table}: {Message}", columnName, tableName, ex.Message);
            // Se não conseguir verificar, assume que é identity para evitar erro de duplicidade
            return true;
        }
    }
}
