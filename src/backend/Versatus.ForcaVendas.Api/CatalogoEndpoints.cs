using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Application.Catalogo;

namespace Versatus.ForcaVendas.Api;

public static class CatalogoEndpoints
{
    public static WebApplication MapCatalogoEndpoints(this WebApplication app)
    {
        app.MapGet("/catalogo/produtos", async (
            ITenantContext tenantContext,
            IProductCatalogRepository repository,
            string? q,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var effectiveLimit = limit.GetValueOrDefault(20);
            if (effectiveLimit <= 0 || effectiveLimit > 100)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["limit must be between 1 and 100."]
                });
            }

            var products = await repository.SearchProductsAsync(
                tenantContext.TenantId,
                q,
                effectiveLimit,
                cancellationToken);

            return Results.Ok(products);
        })
        .WithName("SearchProducts")
        .WithOpenApi();

        app.MapGet("/catalogo/clientes", async (
            ITenantContext tenantContext,
            IClientCatalogRepository repository,
            string? q,
            int? limit,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var effectiveLimit = limit.GetValueOrDefault(50);
            if (effectiveLimit <= 0 || effectiveLimit > 200)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["limit must be between 1 and 200."]
                });
            }

            var clients = await repository.SearchClientsAsync(
                tenantContext.TenantId,
                q,
                effectiveLimit,
                cancellationToken);

            return Results.Ok(clients);
        })
        .WithName("SearchClients")
        .WithOpenApi();

        return app;
    }
}