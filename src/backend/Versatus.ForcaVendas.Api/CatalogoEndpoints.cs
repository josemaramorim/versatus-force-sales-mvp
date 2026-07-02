using StackExchange.Redis;
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
            if (effectiveLimit <= 0 || effectiveLimit > 100000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["limit must be between 1 and 100000."]
                });
            }

            var products = await repository.SearchProductsAsync(
                new CatalogSearchRequest(tenantContext.TenantId, q, effectiveLimit),
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
            if (effectiveLimit <= 0 || effectiveLimit > 100000)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["limit"] = ["limit must be between 1 and 100000."]
                });
            }

            var clients = await repository.SearchClientsAsync(
                new CatalogSearchRequest(tenantContext.TenantId, q, effectiveLimit),
                cancellationToken);

            return Results.Ok(clients);
        })
        .WithName("SearchClients")
        .WithOpenApi();

        app.MapGet("/catalogo/tabelas-preco", async (
            ITenantContext tenantContext,
            IConnectionMultiplexer redis,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var db = redis.GetDatabase();
            var key = $"catalogo:{tenantContext.TenantId}:precos";
            var json = await db.StringGetAsync(key);

            if (!json.HasValue)
            {
                return Results.Content("[]", "application/json");
            }

            return Results.Content(json!, "application/json");
        })
        .WithName("GetTabelasPreco")
        .WithOpenApi();

        app.MapGet("/catalogo/condicoes-pagamento", async (
            ITenantContext tenantContext,
            IConnectionMultiplexer redis,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var db = redis.GetDatabase();
            var key = $"catalogo:{tenantContext.TenantId}:condicoes-pagamento";
            var json = await db.StringGetAsync(key);

            if (!json.HasValue)
            {
                return Results.Content("[]", "application/json");
            }

            return Results.Content(json!, "application/json");
        })
        .WithName("GetCondicoesPagamento")
        .WithOpenApi();

        app.MapGet("/catalogo/tabelas-preco-metadata", async (
            ITenantContext tenantContext,
            IConnectionMultiplexer redis,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var db = redis.GetDatabase();
            var key = $"catalogo:{tenantContext.TenantId}:tabelas-preco-metadata";
            var json = await db.StringGetAsync(key);

            if (!json.HasValue)
            {
                return Results.Content("[]", "application/json");
            }

            return Results.Content(json!, "application/json");
        })
        .WithName("GetTabelasPrecoMetadata")
        .WithOpenApi();

        app.MapGet("/catalogo/tenant-parameters", async (
            ITenantContext tenantContext,
            IConnectionMultiplexer redis,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var db = redis.GetDatabase();
            var key = $"catalogo:{tenantContext.TenantId}:tenant-parameters";
            var json = await db.StringGetAsync(key);

            if (!json.HasValue)
            {
                return Results.Content("{\"tabelaPrecoIdDefault\":1,\"permiteAlterarTabelaPreco\":true}", "application/json");
            }

            return Results.Content(json!, "application/json");
        })
        .WithName("GetTenantParameters")
        .WithOpenApi();

        return app;
    }
}