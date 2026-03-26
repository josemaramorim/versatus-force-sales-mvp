using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Tests.Stubs;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class CatalogTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CatalogTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var sessionDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISessionStore));
                if (sessionDescriptor is not null) services.Remove(sessionDescriptor);
                services.AddSingleton<ISessionStore, InMemorySessionStore>();

                var subscriptionDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ITenantSubscriptionRepository));
                if (subscriptionDescriptor is not null) services.Remove(subscriptionDescriptor);
                services.AddSingleton<ITenantSubscriptionRepository, InMemoryTenantSubscriptionRepository>();

                var productDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IProductCatalogRepository));
                if (productDescriptor is not null) services.Remove(productDescriptor);
                services.AddSingleton<IProductCatalogRepository, TestProductCatalogRepository>();
            });
        });
    }

    [Fact]
    public async Task Get_products_filters_by_query_for_authenticated_user()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("00000000-0000-0000-0000-000000000001", "admin", "123456"));
        loginResponse.EnsureSuccessStatusCode();

        var token = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var response = await client.GetAsync("/catalogo/produtos?q=cafe&limit=10");
        response.EnsureSuccessStatusCode();

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        products.Should().NotBeNull();
        products.Should().ContainSingle();
        products![0].Sku.Should().Be("SKU-CAFE-001");
        products[0].Name.Should().Contain("Cafe");
    }

    public sealed record ProductResponse(
        string ProductId,
        string Sku,
        string Name,
        string Unit,
        decimal Price,
        decimal AvailableStock);

    private sealed class TestProductCatalogRepository : IProductCatalogRepository
    {
        private static readonly ProductSummary[] Products =
        [
            new("prod-001", "SKU-CAFE-001", "Cafe Torrado 500g", "UN", 18.90m, 120m),
            new("prod-002", "SKU-ACUC-001", "Acucar Refinado 1kg", "UN", 6.50m, 85m)
        ];

        public Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(
            string tenantId,
            string? query,
            int limit,
            CancellationToken cancellationToken = default)
        {
            var normalized = query?.Trim() ?? string.Empty;
            var result = Products
                .Where(p => string.IsNullOrWhiteSpace(normalized)
                    || p.Sku.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            return Task.FromResult((IReadOnlyList<ProductSummary>)result);
        }
    }
}