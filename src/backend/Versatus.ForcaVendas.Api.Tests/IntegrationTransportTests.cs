using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Data.Services;
using Versatus.ForcaVendas.Infrastructure.Integration;
using Versatus.ForcaVendas.Infrastructure.Integration.Ftp;
using Versatus.ForcaVendas.Infrastructure.Integration.GoogleDrive;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;
using Versatus.ForcaVendas.Infrastructure.Integration.RabbitMq;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class IntegrationTransportTests
{
    [Theory]
    [InlineData("Ftp", typeof(FtpIntegrationTransport))]
    [InlineData("GoogleDrive", typeof(GoogleDriveIntegrationTransport))]
    [InlineData("RabbitMq", typeof(RabbitMqIntegrationTransport))]
    [InlineData(null, typeof(RabbitMqIntegrationTransport))] // Fallback padrão
    public void DI_Resolve_Correct_Transport_Type(string? transportType, Type expectedType)
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>();
        if (transportType != null)
        {
            inMemorySettings["Integration:Transport"] = transportType;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        
        // Act
        services.AddIntegrationTransport(configuration);
        var provider = services.BuildServiceProvider();
        var resolved = provider.GetService<IIntegrationTransport>();

        // Assert
        resolved.Should().NotBeNull();
        resolved.Should().BeOfType(expectedType);
    }

    [Fact]
    public void Serialization_Matches_CamelCase_Json_Contract()
    {
        // Arrange
        var payload = new OrderExportPayload
        {
            EventId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            TenantId = "tenant-123",
            PedidoId = Guid.NewGuid(),
            Payload = new OrderExportData
            {
                ClienteIdERP = 1234,
                CondicaoPagamentoIdERP = 3,
                DataEmissao = "2026-06-17",
                Observacao = "Teste",
                Orcamento = false,
                Origem = "web",
                ValorTotal = 100m,
                ValorFinal = 100m
            }
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var json = JsonSerializer.Serialize(payload, options);

        // Assert
        json.Should().Contain("\"eventType\"");
        json.Should().Contain("\"eventVersion\"");
        json.Should().Contain("\"eventId\"");
        json.Should().Contain("\"tenantId\"");
        json.Should().Contain("\"pedidoId\"");
        json.Should().Contain("\"clienteIdERP\"");
        json.Should().Contain("\"condicaoPagamentoIdERP\"");
    }

    [Fact]
    public async Task CriarPedidoCommandHandler_Calls_PublishOrderAsync_And_Transits_To_Enviado()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PedidosDbContext>()
            .UseInMemoryDatabase($"pedido-dispatch-success-{Guid.NewGuid()}")
            .Options;

        await using var db = new PedidosDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var transportMock = new TestIntegrationTransport();
        var handler = new CriarPedidoCommandHandler(db, new MockPaymentConditionService(), transportMock);

        var command = new CriarPedidoCommand(
            TenantId: "00000000-0000-0000-0000-000000000001",
            ClienteId: "cli-1234",
            Itens:
            [
                new CriarPedidoItemRequest("prod-101", "sku-501", "Produto Teste", 2, 10m, 1m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("3", DateTime.UtcNow.Date.AddDays(7), "forma-1"));

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Status.Should().Be("enviado");
        transportMock.PublishOrderCalled.Should().BeTrue();
        transportMock.CapturedOrder.Should().NotBeNull();
        transportMock.CapturedOrder!.TenantId.Should().Be(command.TenantId);
        transportMock.CapturedOrder.PedidoId.Should().Be(result.PedidoId);
        transportMock.CapturedOrder.Payload.ClienteIdERP.Should().Be(1234);
        transportMock.CapturedOrder.Payload.CondicaoPagamentoIdERP.Should().Be(3);
        transportMock.CapturedOrder.Payload.Itens.Should().ContainSingle(i => i.ProdutoIdERP == 101 && i.TabelaPrecoEstoqueIdERP == 501);
        transportMock.CapturedOrder.Payload.Parcelas.Should().Contain(p => p.FormaCobrancaIdERP == 1);
    }

    [Fact]
    public async Task CriarPedidoCommandHandler_FailSafe_When_Publish_Throws()
    {
        // Arrange
        var dbOptions = new DbContextOptionsBuilder<PedidosDbContext>()
            .UseInMemoryDatabase($"pedido-dispatch-fail-{Guid.NewGuid()}")
            .Options;

        await using var db = new PedidosDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var transportMock = new TestIntegrationTransport { ThrowOnPublish = true };
        var handler = new CriarPedidoCommandHandler(db, new MockPaymentConditionService(), transportMock);

        var command = new CriarPedidoCommand(
            TenantId: "00000000-0000-0000-0000-000000000001",
            ClienteId: "cli-1234",
            Itens:
            [
                new CriarPedidoItemRequest("prod-101", "sku-501", "Produto Teste", 2, 10m, 1m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("3", DateTime.UtcNow.Date.AddDays(7), "forma-1"));

        // Act & Assert
        // O handler NÃO deve propagar a exception do transporte (fail-safe)
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);
        await act.Should().NotThrowAsync();

        var result = await handler.Handle(command, CancellationToken.None);
        result.Status.Should().Be("rascunho"); // Deve permanecer em rascunho
    }

    private class TestIntegrationTransport : IIntegrationTransport
    {
        public bool PublishOrderCalled { get; private set; }
        public OrderExportPayload? CapturedOrder { get; private set; }
        public bool ThrowOnPublish { get; set; }

        public Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct)
        {
            PublishOrderCalled = true;
            CapturedOrder = order;
            if (ThrowOnPublish)
            {
                throw new Exception("Falha de teste intencional.");
            }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct)
        {
            return Task.FromResult<IReadOnlyList<OrderResultPayload>>(Array.Empty<OrderResultPayload>());
        }

        public Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct)
        {
            return Task.FromResult<CatalogSnapshot?>(null);
        }
    }
}
