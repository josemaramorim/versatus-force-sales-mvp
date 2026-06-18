using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
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
                    // 3. Faturar/Processar no ERP (Simulação de negócio)
                    var resultPayload = ProcessOrderBusinessLogic(order);

                    // 4. Salvar resultado em resultados/pendentes/
                    await client.CreateDirectory(resultadosDir, ct);
                    var resultJson = JsonSerializer.Serialize(resultPayload, _jsonOptions);
                    var resultBytes = Encoding.UTF8.GetBytes(resultJson);
                    var resultFilePath = FtpFolderStructure.GetResultsFilePath(_ftpOptions.BasePath, tenantId, "pendentes", $"resultado-{order.PedidoId}.json");
                    await client.UploadBytes(resultBytes, resultFilePath, FtpRemoteExists.Overwrite, true, token: ct);

                    // 5. Mover pedido para concluidos/
                    await client.CreateDirectory(concluidosDir, ct);
                    await client.MoveFile(processingPath, completedPath, FtpRemoteExists.Overwrite, ct);

                    _logger.LogInformation("[FTP] Pedido {PedidoId} importado e resultado publicado. Sucesso: {Sucesso}", order.PedidoId, resultPayload.Payload.Resultado == "processado");
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

                    // 5. Mover pedido para concluidos/
                    await EnsureSftpDirExistsAsync(client, concluidosDir);
                    await Task.Run(() =>
                    {
                        if (client.Exists(completedPath)) client.DeleteFile(completedPath);
                        client.RenameFile(processingPath, completedPath);
                    }, ct);

                    _logger.LogInformation("[SFTP] Pedido {PedidoId} importado e resultado publicado. Sucesso: {Sucesso}", order.PedidoId, resultPayload.Payload.Resultado == "processado");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SFTP] Erro ao processar importação do arquivo {FileName} para o tenant {TenantId}.", file.Name, tenantId);
            }
        }

        client.Disconnect();
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
}
