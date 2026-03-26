namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class PedidoStatus
{
    public const int RascunhoId = 1;
    public const int EnviadoId = 2;
    public const int ProcessadoId = 3;
    public const int ErroId = 4;

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
