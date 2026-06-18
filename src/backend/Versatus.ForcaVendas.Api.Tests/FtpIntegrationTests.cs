using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentFTP;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Versatus.ForcaVendas.Infrastructure.Integration.Ftp;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class FtpIntegrationTests
{
    private const string Host = "localhost";
    private const int Port = 21;
    private const string Username = "test";
    private const string Password = "test";
    private const string BasePath = "/integration-tests";
    private readonly string _tenantId = Guid.NewGuid().ToString();

    private readonly IOptions<FtpTransportOptions> _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public FtpIntegrationTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new FtpTransportOptions
        {
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            BasePath = BasePath,
            UseSftp = false
        });

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    private async Task<bool> IsFtpServerAvailableAsync()
    {
        try
        {
            using var client = new AsyncFtpClient(Host, Username, Password, Port);
            await client.AutoConnect();
            // Tenta listar o diretório raiz para certificar que o login foi efetuado com sucesso e o servidor responde comandos
            await client.GetListing("/");
            await client.Disconnect();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task PublishOrder_And_FetchCatalog_And_ProcessResults_IntegrationFlow()
    {
        if (!await IsFtpServerAvailableAsync())
        {
            // Pula o teste graciosamente se o Docker ftp local não estiver ativo
            return;
        }

        var transport = new FtpIntegrationTransport(_options, NullLogger<FtpIntegrationTransport>.Instance);
        using var ftpDirect = new AsyncFtpClient(Host, Username, Password, Port);
        await ftpDirect.AutoConnect();

        try
        {
            // -------------------------------------------------------------
            // TESTE 1: PUBLICAR PEDIDO (PublishOrderAsync)
            // -------------------------------------------------------------
            var orderId = Guid.NewGuid();
            var orderExport = new OrderExportPayload
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                TenantId = _tenantId,
                PedidoId = orderId,
                Payload = new OrderExportData
                {
                    ClienteIdERP = 999,
                    CondicaoPagamentoIdERP = 2,
                    DataEmissao = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    ValorTotal = 150.00m,
                    ValorFinal = 150.00m,
                    Itens = new List<OrderItemExportDto>
                    {
                        new() { ProdutoIdERP = 100, Quantidade = 1, PrecoUnitario = 150.00m, ValorFinal = 150.00m }
                    }
                }
            };

            await transport.PublishOrderAsync(_tenantId, orderExport, CancellationToken.None);

            // Verificar se o arquivo foi carregado no diretório esperado do FTP
            var expectedOrderPath = FtpFolderStructure.GetOrdersFilePath(BasePath, _tenantId, "pendentes", $"pedido-{orderId}.json");
            var orderFileExists = await ftpDirect.FileExists(expectedOrderPath);
            orderFileExists.Should().BeTrue();

            // Baixar diretamente e verificar conteúdo
            var orderBytes = await ftpDirect.DownloadBytes(expectedOrderPath, CancellationToken.None);
            var orderJson = Encoding.UTF8.GetString(orderBytes);
            var downloadedOrder = JsonSerializer.Deserialize<OrderExportPayload>(orderJson, _jsonOptions);
            downloadedOrder.Should().NotBeNull();
            downloadedOrder!.PedidoId.Should().Be(orderId);
            downloadedOrder.Payload.ClienteIdERP.Should().Be(999);

            // -------------------------------------------------------------
            // TESTE 2: BUSCAR CATÁLOGO (FetchCatalogAsync)
            // -------------------------------------------------------------
            // Primeiro criamos e enviamos dados de catálogo falsos ao FTP
            var catalogDir = FtpFolderStructure.GetCatalogDirectory(BasePath, _tenantId);
            await ftpDirect.CreateDirectory(catalogDir);

            var clientesData = new CatalogFileWrapper<ClienteCatalogDto>
            {
                TenantId = _tenantId,
                Version = "v1",
                Data = [new() { ClienteIdERP = 777, Nome = "Cliente Teste Integracao", Documento = "12345" }]
            };
            var produtosData = new CatalogFileWrapper<ProdutoCatalogDto>
            {
                TenantId = _tenantId,
                Version = "v1",
                Data = [new() { ProdutoIdERP = 888, Descricao = "Prod Teste Int", Saldo = 10 }]
            };
            var tabelasData = new CatalogFileWrapper<TabelaPrecoCatalogDto>
            {
                TenantId = _tenantId,
                Version = "v1",
                Data = [new() { ProdutoIdERP = 888, ValorUnitario = 50.00m }]
            };
            var condicoesData = new CatalogFileWrapper<CondicaoPagamentoCatalogDto>
            {
                TenantId = _tenantId,
                Version = "v1",
                Data = [new() { CondicaoPagtoIdERP = 1, Descricao = "A vista" }]
            };

            await ftpDirect.UploadBytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clientesData, _jsonOptions)), FtpFolderStructure.GetCatalogFilePath(BasePath, _tenantId, "clientes.json"), FtpRemoteExists.Overwrite);
            await ftpDirect.UploadBytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(produtosData, _jsonOptions)), FtpFolderStructure.GetCatalogFilePath(BasePath, _tenantId, "produtos.json"), FtpRemoteExists.Overwrite);
            await ftpDirect.UploadBytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(tabelasData, _jsonOptions)), FtpFolderStructure.GetCatalogFilePath(BasePath, _tenantId, "tabelas-preco.json"), FtpRemoteExists.Overwrite);
            await ftpDirect.UploadBytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(condicoesData, _jsonOptions)), FtpFolderStructure.GetCatalogFilePath(BasePath, _tenantId, "condicoes-pagamento.json"), FtpRemoteExists.Overwrite);

            // Chamar a sincronização do catálogo
            var snapshot = await transport.FetchCatalogAsync(_tenantId, CancellationToken.None);
            snapshot.Should().NotBeNull();
            snapshot!.Clientes.Should().ContainSingle(c => c.ClienteIdERP == 777);
            snapshot.Produtos.Should().ContainSingle(p => p.ProdutoIdERP == 888);

            // -------------------------------------------------------------
            // TESTE 3: PROCESSAR RESULTADOS (FetchPendingResultsAsync & AcknowledgeResultAsync)
            // -------------------------------------------------------------
            var resultDir = FtpFolderStructure.GetResultsDirectory(BasePath, _tenantId, "pendentes");
            await ftpDirect.CreateDirectory(resultDir);

            var resultId = Guid.NewGuid();
            var resultPayload = new OrderResultPayload
            {
                EventId = resultId,
                CreatedAt = DateTimeOffset.UtcNow,
                TenantId = _tenantId,
                PedidoId = orderId,
                Payload = new OrderResultData
                {
                    Resultado = "processado",
                    DocumentoVendaId = "NF-999-99",
                    SourceEventId = orderExport.EventId
                }
            };

            var resultFilePath = FtpFolderStructure.GetResultsFilePath(BasePath, _tenantId, "pendentes", $"resultado-{orderId}.json");
            await ftpDirect.UploadBytes(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resultPayload, _jsonOptions)), resultFilePath, FtpRemoteExists.Overwrite);

            // Ler resultados
            var pendingResults = await transport.FetchPendingResultsAsync(_tenantId, CancellationToken.None);
            pendingResults.Should().ContainSingle(r => r.PedidoId == orderId);

            var targetResult = pendingResults[0];
            targetResult.ResultFileId.Should().Be($"resultado-{orderId}.json");

            // Confirmar processamento (Acknowledge)
            await transport.AcknowledgeResultAsync(_tenantId, targetResult.ResultFileId!, CancellationToken.None);

            // Verificar se foi movido para processados
            var pendingExists = await ftpDirect.FileExists(resultFilePath);
            pendingExists.Should().BeFalse();

            var processedFilePath = FtpFolderStructure.GetResultsFilePath(BasePath, _tenantId, "processados", $"resultado-{orderId}.json");
            var processedExists = await ftpDirect.FileExists(processedFilePath);
            processedExists.Should().BeTrue();
        }
        finally
        {
            try
            {
                if (ftpDirect.IsConnected)
                {
                    // Limpar diretório de teste no FTP para não deixar lixo
                    var tenantRootDir = $"/{BasePath.Trim('/')}/{_tenantId}";
                    if (await ftpDirect.DirectoryExists(tenantRootDir))
                    {
                        await ftpDirect.DeleteDirectory(tenantRootDir);
                    }
                }
            }
            catch
            {
                // Ignorar erro no cleanup para não ocultar a exceção principal do teste
            }

            try
            {
                if (ftpDirect.IsConnected)
                {
                    await ftpDirect.Disconnect();
                }
            }
            catch
            {
                // Ignorar
            }
        }
    }
}
