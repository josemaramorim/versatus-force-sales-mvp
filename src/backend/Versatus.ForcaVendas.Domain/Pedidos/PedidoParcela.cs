namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class PedidoParcela
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public int Numero { get; set; }
    public DateTime DataVencimento { get; set; }
    public decimal Valor { get; set; }
    public string FormaPagamento { get; set; } = string.Empty;
    public DateTimeOffset? PagoEm { get; set; }

    public Pedido? Pedido { get; set; }
}
