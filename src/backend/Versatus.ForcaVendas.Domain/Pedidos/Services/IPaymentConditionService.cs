namespace Versatus.ForcaVendas.Domain.Pedidos.Services;

public interface IPaymentConditionService
{
    /// <summary>
    /// Calcula as parcelas de um pedido baseado no ID de condição de pagamento real ou mockado.
    /// </summary>
    Task<List<PedidoParcela>> CalcularParcelamentoAsync(Guid pedidoId, decimal valorTotalLiquido, string condicaoPagamentoId, DateTime primeiroVencimento, CancellationToken cancellationToken = default);
}
