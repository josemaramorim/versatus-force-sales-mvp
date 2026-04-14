using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Data.Services;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class PedidoDomainRulesTests
{
    [Fact]
    public void CriarPedidoItemValidator_quandoDescontoMaiorQueBruto_deveFalhar()
    {
        var validator = new CriarPedidoItemRequestValidator();
        var item = new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 1, 10m, 11m);

        var result = validator.Validate(item);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "desconto");
    }

    [Fact]
    public async Task CriarPedidoCommandHandler_deveCalcularTotaisCorretamente()
    {
        var dbOptions = new DbContextOptionsBuilder<PedidosDbContext>()
            .UseInMemoryDatabase($"pedido-domain-rules-{Guid.NewGuid()}")
            .Options;

        await using var db = new PedidosDbContext(dbOptions);
        await db.Database.EnsureCreatedAsync();

        var handler = new CriarPedidoCommandHandler(db, new MockPaymentConditionService());

        var command = new CriarPedidoCommand(
            TenantId: "00000000-0000-0000-0000-000000000001",
            ClienteId: "cli-001",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 2, 10m, 1m),
                new CriarPedidoItemRequest("prod-002", "SKU-002", "Produto 2", 1, 5m, 0m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("2", DateTime.UtcNow.Date.AddDays(7), "boleto"));

        var result = await handler.Handle(command, CancellationToken.None);

        result.TotalBruto.Should().Be(25m);
        result.TotalDesconto.Should().Be(1m);
        result.TotalLiquido.Should().Be(24m);
        result.ParcelasCount.Should().Be(2);
        result.Status.Should().Be("rascunho");
    }
}
