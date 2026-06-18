using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Infrastructure.Integration.GoogleDrive;

public sealed class GoogleDriveIntegrationTransport : IIntegrationTransport
{
    private readonly GoogleDriveTransportOptions _options;

    public GoogleDriveIntegrationTransport(IOptions<GoogleDriveTransportOptions> options)
    {
        _options = options.Value;
    }

    public Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct)
    {
        throw new NotImplementedException("Google Drive Integration Transport está planejado para fase posterior e não está implementado.");
    }

    public Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct)
    {
        throw new NotImplementedException("Google Drive Integration Transport está planejado para fase posterior e não está implementado.");
    }

    public Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct)
    {
        throw new NotImplementedException("Google Drive Integration Transport está planejado para fase posterior e não está implementado.");
    }

    public Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct)
    {
        throw new NotImplementedException("Google Drive Integration Transport está planejado para fase posterior e não está implementado.");
    }
}
