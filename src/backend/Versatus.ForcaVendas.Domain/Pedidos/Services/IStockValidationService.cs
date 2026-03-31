namespace Versatus.ForcaVendas.Domain.Pedidos.Services;

public interface IStockValidationService
{
    /// <summary>
    /// Valida se uma lista de itens possui estoque no sistema / ERP original.
    /// </summary>
    Task<bool> ValidateStockAsync(IEnumerable<PedidoItem> itens, string tenantId, CancellationToken cancellationToken = default);
}
