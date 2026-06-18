using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Infrastructure.Integration.RabbitMq;

public sealed class RabbitMqIntegrationTransport : IIntegrationTransport
{
    private readonly RabbitMqTransportOptions _options;

    public RabbitMqIntegrationTransport(IOptions<RabbitMqTransportOptions> options)
    {
        _options = options.Value;
    }

    public Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct)
    {
        throw new NotImplementedException("RabbitMQ Integration Transport está planejado para fase posterior e não está implementado.");
    }

    public Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct)
    {
        throw new NotImplementedException("RabbitMQ Integration Transport está planejado para fase posterior e não está implementado.");
    }

    public Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct)
    {
        throw new NotImplementedException("RabbitMQ Integration Transport está planejado para fase posterior e não está implementado.");
    }

    public Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct)
    {
        throw new NotImplementedException("RabbitMQ Integration Transport está planejado para fase posterior e não está implementado.");
    }
}
