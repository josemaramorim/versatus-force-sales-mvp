using System.Collections.Concurrent;
using Versatus.ForcaVendas.Domain.Pedidos;

namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed class InMemoryPedidoCache : IPedidoCache
{
    private readonly ConcurrentDictionary<Guid, Pedido> _map = new();

    public void Set(Pedido pedido)
    {
        _map[pedido.Id] = pedido;
    }

    public bool TryGet(Guid id, out Pedido? pedido)
    {
        return _map.TryGetValue(id, out pedido);
    }

    public IReadOnlyCollection<Pedido> GetByTenant(string tenantId)
    {
        return _map.Values
            .Where(p => string.Equals(p.TenantId, tenantId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
