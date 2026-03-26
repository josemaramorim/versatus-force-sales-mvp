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

    [Fact]
    public async Task Get_pedidos_returns_created_pedido()
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

        var post = await client.PostAsJsonAsync("/pedidos", request);
        post.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await post.Content.ReadFromJsonAsync<CreatePedidoResponse>();
        created.Should().NotBeNull();

        var id = Guid.Parse(created!.PedidoId);

        var get = await client.GetAsync($"/pedidos/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await get.Content.ReadFromJsonAsync<GetPedidoResponse>();
        body.Should().NotBeNull();
        body!.PedidoId.Should().Be(created.PedidoId);
        body.ItensCount.Should().Be(2);
        body.ParcelasCount.Should().Be(2);
        body.TotalBruto.Should().Be(25m);
        body.TotalLiquido.Should().Be(25m);
    }

    private sealed record GetPedidoResponse(string PedidoId, string TenantId, string ClienteId, DateTimeOffset CriadoEm, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido, PedidoItemDto[] Itens, PedidoParcelaDto[] Parcelas);
    private sealed record PedidoItemDto(string ProdutoId, string Sku, string Nome, decimal Quantidade, decimal PrecoUnitario, decimal Desconto, decimal Total);
    private sealed record PedidoParcelaDto(int Numero, DateTime DataVencimento, decimal Valor, string FormaPagamento);

    private sealed record CreatePedidoResponse(string PedidoId, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido);
}
