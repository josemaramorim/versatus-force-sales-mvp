using System;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Api.Tests.Stubs;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Infrastructure.Data;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class PedidosTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PedidosTests(WebApplicationFactory<Program> factory)
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

                var dbOptionsDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PedidosDbContext>));
                if (dbOptionsDescriptor is not null) services.Remove(dbOptionsDescriptor);

                var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(PedidosDbContext));
                if (dbContextDescriptor is not null) services.Remove(dbContextDescriptor);

                services.AddDbContext<PedidosDbContext>(options =>
                    options.UseInMemoryDatabase($"pedidos-tests-{Guid.NewGuid()}"));
            });
        });
    }

    [Fact]
    public async Task Post_pedidos_creates_rascunho_with_itens_and_parcelas()
    {
        var client = _factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync(
            "/auth/login",
            new LoginRequest("00000000-0000-0000-0000-000000000001", "admin", "123456"));
        loginResponse.EnsureSuccessStatusCode();

        var token = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var request = new CriarPedidoRequest(
            ClienteId: "cli-001",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 2, 10m, 0m),
                new CriarPedidoItemRequest("prod-002", "SKU-002", "Produto 2", 1, 5m, 0m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest(2, DateTime.UtcNow.Date.AddDays(7), "boleto"));

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/pedidos/");

        var body = await response.Content.ReadFromJsonAsync<CreatePedidoResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("rascunho");
        body.ItensCount.Should().Be(2);
        body.ParcelasCount.Should().Be(2);
        body.TotalBruto.Should().Be(25m);
        body.TotalDesconto.Should().Be(0m);
        body.TotalLiquido.Should().Be(25m);
    }

    private sealed record CreatePedidoResponse(string PedidoId, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido);
}
