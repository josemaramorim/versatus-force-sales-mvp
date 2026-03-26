using Versatus.ForcaVendas.Domain.Pedidos;

namespace Versatus.ForcaVendas.Api.Pedidos;

public interface IPedidoCache
{
    void Set(Pedido pedido);
    bool TryGet(Guid id, out Pedido? pedido);
}
