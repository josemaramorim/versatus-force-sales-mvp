using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Tests.Stubs;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Domain.Auth;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class CatalogTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly InMemoryTenantSubscriptionRepository _subscriptionRepository = new();

    public CatalogTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISessionStore>();
                services.AddSingleton<ISessionStore, InMemorySessionStore>();

                services.RemoveAll<ITenantSubscriptionRepository>();
                services.AddSingleton<ITenantSubscriptionRepository>(_subscriptionRepository);

                services.RemoveAll<IUsuarioRepository>();
                services.AddSingleton<IUsuarioRepository, InMemoryUsuarioRepository>();

                services.RemoveAll<ISessionAuditEventRepository>();
                services.AddSingleton<ISessionAuditEventRepository, InMemorySessionAuditEventRepository>();

                services.RemoveAll<IProductCatalogRepository>();
                services.AddSingleton<IProductCatalogRepository, TestProductCatalogRepository>();
            });
        });

        _subscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 4, true, "Demo Corp 1");
        _subscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000002", 4, true, "Demo Corp 2");
    }

    [Fact]
    public async Task Get_products_filters_by_query_for_authenticated_user()
    {
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var response = await client.GetAsync("/catalogo/produtos?q=cafe&limit=10");
        response.EnsureSuccessStatusCode();

        var products = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        products.Should().NotBeNull();
        products.Should().ContainSingle();
        products![0].Sku.Should().Be("SKU-CAFE-001");
        products[0].Name.Should().Contain("Cafe");
    }

    [Fact]
    public async Task Get_products_blocks_request_with_invalid_limit()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/catalogo/produtos?limit=100001");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_products_applies_tenant_scope_from_token()
    {
        var client = _factory.CreateClient();

        var adminToken = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken.AccessToken);
        var tenant1 = await client.GetFromJsonAsync<List<ProductResponse>>("/catalogo/produtos?limit=10");

        var gestorToken = await LoginAsync(client, "gestor@demo2.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", gestorToken.AccessToken);
        var tenant2 = await client.GetFromJsonAsync<List<ProductResponse>>("/catalogo/produtos?limit=10");

        tenant1.Should().NotBeNull();
        tenant2.Should().NotBeNull();
        tenant1!.Select(p => p.ProductId).Should().Contain("prod-001");
        tenant1.Select(p => p.ProductId).Should().NotContain("prod-101");
        tenant2!.Select(p => p.ProductId).Should().Contain("prod-101");
        tenant2.Select(p => p.ProductId).Should().NotContain("prod-001");
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        loginResponse.EnsureSuccessStatusCode();
        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
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
            new("prod-002", "SKU-ACUC-001", "Acucar Refinado 1kg", "UN", 6.50m, 85m),
            new("prod-101", "SKU-ERVA-001", "Erva Mate 1kg", "UN", 14.20m, 45m)
        ];

        public Task<IReadOnlyList<ProductSummary>> SearchProductsAsync(
            CatalogSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            var normalized = request.Query?.Trim() ?? string.Empty;
            var result = Products
                .Where(p => request.TenantId switch
                {
                    "00000000-0000-0000-0000-000000000001" => p.ProductId.StartsWith("prod-00", StringComparison.OrdinalIgnoreCase),
                    "00000000-0000-0000-0000-000000000002" => p.ProductId.StartsWith("prod-10", StringComparison.OrdinalIgnoreCase),
                    _ => false
                })
                .Where(p => string.IsNullOrWhiteSpace(normalized)
                    || p.Sku.Contains(normalized, StringComparison.OrdinalIgnoreCase)
                    || p.Name.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .Take(request.Limit)
                .ToList();

            return Task.FromResult((IReadOnlyList<ProductSummary>)result);
        }
    }
}