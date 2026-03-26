namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class Pedido
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? AtualizadoEm { get; set; }
    public int StatusId { get; set; }

    public PedidoStatus? Status { get; set; }
    public ICollection<PedidoItem> Itens { get; set; } = new List<PedidoItem>();
    public ICollection<PedidoParcela> Parcelas { get; set; } = new List<PedidoParcela>();
}
