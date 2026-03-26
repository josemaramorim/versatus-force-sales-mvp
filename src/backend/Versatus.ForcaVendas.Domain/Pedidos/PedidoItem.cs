namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class PedidoItem
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string ProdutoId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Desconto { get; set; }
    public decimal Total { get; set; }

    public Pedido? Pedido { get; set; }
}
