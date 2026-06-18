using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentFTP;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Infrastructure.Integration.Ftp;

public sealed class FtpIntegrationTransport : IIntegrationTransport
{
    private readonly FtpTransportOptions _options;
    private readonly ILogger<FtpIntegrationTransport> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public FtpIntegrationTransport(
        IOptions<FtpTransportOptions> options,
        ILogger<FtpIntegrationTransport> logger)
    {
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    public async Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(order, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        if (_options.UseSftp)
        {
            await PublishOrderSftpAsync(tenantId, order.PedidoId, bytes, ct);
        }
        else
        {
            await PublishOrderFtpAsync(tenantId, order.PedidoId, bytes, ct);
        }
    }

    public async Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct)
    {
        if (_options.UseSftp)
        {
            return await FetchPendingResultsSftpAsync(tenantId, ct);
        }
        else
        {
            return await FetchPendingResultsFtpAsync(tenantId, ct);
        }
    }

    public async Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct)
    {
        if (_options.UseSftp)
        {
            await AcknowledgeResultSftpAsync(tenantId, resultFileId, ct);
        }
        else
        {
            await AcknowledgeResultFtpAsync(tenantId, resultFileId, ct);
        }
    }

    public async Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct)
    {
        if (_options.UseSftp)
        {
            return await FetchCatalogSftpAsync(tenantId, ct);
        }
        else
        {
            return await FetchCatalogFtpAsync(tenantId, ct);
        }
    }

    #region FTP Implementations

    private async Task<AsyncFtpClient> GetFtpClientAsync(CancellationToken ct)
    {
        var client = new AsyncFtpClient(_options.Host, _options.Username, _options.Password, _options.Port);
        await client.AutoConnect(ct);
        return client;
    }

    private async Task PublishOrderFtpAsync(string tenantId, Guid pedidoId, byte[] content, CancellationToken ct)
    {
        using var client = await GetFtpClientAsync(ct);
        var dir = FtpFolderStructure.GetOrdersDirectory(_options.BasePath, tenantId, "pendentes");
        var filePath = FtpFolderStructure.GetOrdersFilePath(_options.BasePath, tenantId, "pendentes", $"pedido-{pedidoId}.json");

        _logger.LogInformation("Enviando pedido {PedidoId} via FTP para {Path}...", pedidoId, filePath);
        
        await client.CreateDirectory(dir, ct);
        await client.UploadBytes(content, filePath, FtpRemoteExists.Overwrite, true, token: ct);
    }

    private async Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsFtpAsync(string tenantId, CancellationToken ct)
    {
        using var client = await GetFtpClientAsync(ct);
        var dir = FtpFolderStructure.GetResultsDirectory(_options.BasePath, tenantId, "pendentes");

        if (!await client.DirectoryExists(dir, ct))
        {
            return Array.Empty<OrderResultPayload>();
        }

        var results = new List<OrderResultPayload>();
        var items = await client.GetListing(dir, FtpListOption.Modify | FtpListOption.Size, ct);

        foreach (var item in items)
        {
            if (item.Type == FtpObjectType.File && item.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var bytes = await client.DownloadBytes(item.FullName, ct);
                    var json = Encoding.UTF8.GetString(bytes);
                    var payload = JsonSerializer.Deserialize<OrderResultPayload>(json, _jsonOptions);
                    if (payload != null)
                    {
                        payload.ResultFileId = item.Name;
                        results.Add(payload);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao baixar/desserializar resultado FTP {FileName} no tenant {TenantId}.", item.Name, tenantId);
                }
            }
        }

        return results;
    }

    private async Task AcknowledgeResultFtpAsync(string tenantId, string resultFileId, CancellationToken ct)
    {
        using var client = await GetFtpClientAsync(ct);
        var sourcePath = FtpFolderStructure.GetResultsFilePath(_options.BasePath, tenantId, "pendentes", resultFileId);
        var destDir = FtpFolderStructure.GetResultsDirectory(_options.BasePath, tenantId, "processados");
        var destPath = FtpFolderStructure.GetResultsFilePath(_options.BasePath, tenantId, "processados", resultFileId);

        _logger.LogInformation("Confirmando recebimento de resultado FTP {FileName}...", resultFileId);

        await client.CreateDirectory(destDir, ct);
        await client.MoveFile(sourcePath, destPath, FtpRemoteExists.Overwrite, ct);
    }

    private async Task<CatalogSnapshot?> FetchCatalogFtpAsync(string tenantId, CancellationToken ct)
    {
        using var client = await GetFtpClientAsync(ct);
        var dir = FtpFolderStructure.GetCatalogDirectory(_options.BasePath, tenantId);

        if (!await client.DirectoryExists(dir, ct))
        {
            _logger.LogWarning("Diretório de catálogo FTP {Dir} não existe.", dir);
            return null;
        }

        var clientesPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "clientes.json");
        var produtosPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "produtos.json");
        var precosPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "tabelas-preco.json");
        var condicoesPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "condicoes-pagamento.json");

        if (!await client.FileExists(clientesPath, ct) ||
            !await client.FileExists(produtosPath, ct) ||
            !await client.FileExists(precosPath, ct) ||
            !await client.FileExists(condicoesPath, ct))
        {
            _logger.LogWarning("Catálogo incompleto no FTP para o tenant {TenantId}. Um ou mais arquivos .json estão ausentes.", tenantId);
            return null;
        }

        try
        {
            var clientesBytes = await client.DownloadBytes(clientesPath, ct);
            var produtosBytes = await client.DownloadBytes(produtosPath, ct);
            var precosBytes = await client.DownloadBytes(precosPath, ct);
            var condicoesBytes = await client.DownloadBytes(condicoesPath, ct);

            var clientes = JsonSerializer.Deserialize<CatalogFileWrapper<ClienteCatalogDto>>(Encoding.UTF8.GetString(clientesBytes), _jsonOptions);
            var produtos = JsonSerializer.Deserialize<CatalogFileWrapper<ProdutoCatalogDto>>(Encoding.UTF8.GetString(produtosBytes), _jsonOptions);
            var precos = JsonSerializer.Deserialize<CatalogFileWrapper<TabelaPrecoCatalogDto>>(Encoding.UTF8.GetString(precosBytes), _jsonOptions);
            var condicoes = JsonSerializer.Deserialize<CatalogFileWrapper<CondicaoPagamentoCatalogDto>>(Encoding.UTF8.GetString(condicoesBytes), _jsonOptions);

            if (clientes == null || produtos == null || precos == null || condicoes == null)
            {
                return null;
            }

            return new CatalogSnapshot
            {
                Clientes = clientes.Data,
                Produtos = produtos.Data,
                TabelasPreco = precos.Data,
                CondicoesPagamento = condicoes.Data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao baixar catálogo via FTP para o tenant {TenantId}.", tenantId);
            return null;
        }
    }

    #endregion

    #region SFTP Implementations

    private async Task<SftpClient> GetSftpClientAsync(CancellationToken ct)
    {
        var client = new SftpClient(_options.Host, _options.Port, _options.Username, _options.Password);
        await Task.Run(() => client.Connect(), ct);
        return client;
    }

    private async Task EnsureSftpDirectoryExistsAsync(SftpClient client, string path, CancellationToken ct)
    {
        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var currentPath = "";
        
        // Se o path original for absoluto, preserva o início
        if (path.StartsWith("/"))
        {
            currentPath = "/";
        }

        foreach (var part in parts)
        {
            currentPath = currentPath == "/" ? $"/{part}" : (string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}");
            
            var exists = await Task.Run(() => client.Exists(currentPath), ct);
            if (!exists)
            {
                await Task.Run(() => client.CreateDirectory(currentPath), ct);
            }
        }
    }

    private async Task PublishOrderSftpAsync(string tenantId, Guid pedidoId, byte[] content, CancellationToken ct)
    {
        using var client = await GetSftpClientAsync(ct);
        var dir = FtpFolderStructure.GetOrdersDirectory(_options.BasePath, tenantId, "pendentes");
        var filePath = FtpFolderStructure.GetOrdersFilePath(_options.BasePath, tenantId, "pendentes", $"pedido-{pedidoId}.json");

        _logger.LogInformation("Enviando pedido {PedidoId} via SFTP para {Path}...", pedidoId, filePath);

        await EnsureSftpDirectoryExistsAsync(client, dir, ct);
        using var ms = new MemoryStream(content);
        await Task.Run(() => client.UploadFile(ms, filePath, true), ct);
    }

    private async Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsSftpAsync(string tenantId, CancellationToken ct)
    {
        using var client = await GetSftpClientAsync(ct);
        var dir = FtpFolderStructure.GetResultsDirectory(_options.BasePath, tenantId, "pendentes");

        var exists = await Task.Run(() => client.Exists(dir), ct);
        if (!exists)
        {
            return Array.Empty<OrderResultPayload>();
        }

        var results = new List<OrderResultPayload>();
        var files = await Task.Run(() => client.ListDirectory(dir), ct);

        foreach (var file in files)
        {
            if (file.IsRegularFile && file.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using var ms = new MemoryStream();
                    await Task.Run(() => client.DownloadFile(file.FullName, ms), ct);
                    
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    var payload = JsonSerializer.Deserialize<OrderResultPayload>(json, _jsonOptions);
                    if (payload != null)
                    {
                        payload.ResultFileId = file.Name;
                        results.Add(payload);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao baixar/desserializar resultado SFTP {FileName} no tenant {TenantId}.", file.Name, tenantId);
                }
            }
        }

        return results;
    }

    private async Task AcknowledgeResultSftpAsync(string tenantId, string resultFileId, CancellationToken ct)
    {
        using var client = await GetSftpClientAsync(ct);
        var sourcePath = FtpFolderStructure.GetResultsFilePath(_options.BasePath, tenantId, "pendentes", resultFileId);
        var destDir = FtpFolderStructure.GetResultsDirectory(_options.BasePath, tenantId, "processados");
        var destPath = FtpFolderStructure.GetResultsFilePath(_options.BasePath, tenantId, "processados", resultFileId);

        _logger.LogInformation("Confirmando recebimento de resultado SFTP {FileName}...", resultFileId);

        await EnsureSftpDirectoryExistsAsync(client, destDir, ct);
        await Task.Run(() =>
        {
            if (client.Exists(destPath))
            {
                client.DeleteFile(destPath);
            }
            client.RenameFile(sourcePath, destPath);
        }, ct);
    }

    private async Task<CatalogSnapshot?> FetchCatalogSftpAsync(string tenantId, CancellationToken ct)
    {
        using var client = await GetSftpClientAsync(ct);
        var dir = FtpFolderStructure.GetCatalogDirectory(_options.BasePath, tenantId);

        var dirExists = await Task.Run(() => client.Exists(dir), ct);
        if (!dirExists)
        {
            _logger.LogWarning("Diretório de catálogo SFTP {Dir} não existe.", dir);
            return null;
        }

        var clientesPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "clientes.json");
        var produtosPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "produtos.json");
        var precosPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "tabelas-preco.json");
        var condicoesPath = FtpFolderStructure.GetCatalogFilePath(_options.BasePath, tenantId, "condicoes-pagamento.json");

        var checkFilesTask = Task.Run(() => 
            client.Exists(clientesPath) && 
            client.Exists(produtosPath) && 
            client.Exists(precosPath) && 
            client.Exists(condicoesPath), ct);

        var filesExist = await checkFilesTask;
        if (!filesExist)
        {
            _logger.LogWarning("Catálogo incompleto no SFTP para o tenant {TenantId}. Um ou mais arquivos .json estão ausentes.", tenantId);
            return null;
        }

        try
        {
            var downloadTasks = new[]
            {
                DownloadSftpBytesAsync(client, clientesPath, ct),
                DownloadSftpBytesAsync(client, produtosPath, ct),
                DownloadSftpBytesAsync(client, precosPath, ct),
                DownloadSftpBytesAsync(client, condicoesPath, ct)
            };

            var bytes = await Task.WhenAll(downloadTasks);

            var clientes = JsonSerializer.Deserialize<CatalogFileWrapper<ClienteCatalogDto>>(Encoding.UTF8.GetString(bytes[0]), _jsonOptions);
            var produtos = JsonSerializer.Deserialize<CatalogFileWrapper<ProdutoCatalogDto>>(Encoding.UTF8.GetString(bytes[1]), _jsonOptions);
            var precos = JsonSerializer.Deserialize<CatalogFileWrapper<TabelaPrecoCatalogDto>>(Encoding.UTF8.GetString(bytes[2]), _jsonOptions);
            var condicoes = JsonSerializer.Deserialize<CatalogFileWrapper<CondicaoPagamentoCatalogDto>>(Encoding.UTF8.GetString(bytes[3]), _jsonOptions);

            if (clientes == null || produtos == null || precos == null || condicoes == null)
            {
                return null;
            }

            return new CatalogSnapshot
            {
                Clientes = clientes.Data,
                Produtos = produtos.Data,
                TabelasPreco = precos.Data,
                CondicoesPagamento = condicoes.Data
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao baixar catálogo via SFTP para o tenant {TenantId}.", tenantId);
            return null;
        }
    }

    private async Task<byte[]> DownloadSftpBytesAsync(SftpClient client, string filePath, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await Task.Run(() => client.DownloadFile(filePath, ms), ct);
        return ms.ToArray();
    }

    #endregion
}
