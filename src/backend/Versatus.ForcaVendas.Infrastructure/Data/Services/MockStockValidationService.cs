using Versatus.ForcaVendas.Domain.Pedidos;
using Versatus.ForcaVendas.Domain.Pedidos.Services;

namespace Versatus.ForcaVendas.Infrastructure.Data.Services;

public sealed class MockStockValidationService : IStockValidationService
{
    public Task<bool> ValidateStockAsync(IEnumerable<PedidoItem> itens, string tenantId, CancellationToken cancellationToken = default)
    {
        // Mock simples para o MVP. Assumimos sempre que há saldo disponível.
        // No futuro, podemos injetar HttpClient e buscar no ERP.
        return Task.FromResult(true);
    }
}
