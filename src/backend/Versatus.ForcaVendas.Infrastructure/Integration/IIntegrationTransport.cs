using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Infrastructure.Integration;

public interface IIntegrationTransport
{
    // Pedidos: App -> ERP
    Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct);
    
    // Resultados: ERP -> App (polling)
    Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct);
    Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct);
    
    // Catálogo: ERP -> App (sync)
    Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct);
}
