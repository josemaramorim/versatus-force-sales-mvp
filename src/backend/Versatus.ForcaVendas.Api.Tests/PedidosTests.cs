using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Api.Tests.Stubs;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Domain.Auth;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class PedidosTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PedidosTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("DatabaseProvider", "InMemory");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISessionStore>();
                services.AddSingleton<ISessionStore, InMemorySessionStore>();

                services.RemoveAll<ITenantSubscriptionRepository>();
                services.AddSingleton<ITenantSubscriptionRepository, InMemoryTenantSubscriptionRepository>();

                services.RemoveAll<IUsuarioRepository>();
                services.AddSingleton<IUsuarioRepository, InMemoryUsuarioRepository>();

                services.RemoveAll<ISessionAuditEventRepository>();
                services.AddSingleton<ISessionAuditEventRepository, InMemorySessionAuditEventRepository>();

                var dbOptionsDescriptors = services.Where(d => d.ServiceType == typeof(DbContextOptions<PedidosDbContext>)).ToList();
                foreach (var desc in dbOptionsDescriptors) services.Remove(desc);

                var dbContextDescriptors = services.Where(d => d.ServiceType == typeof(PedidosDbContext)).ToList();
                foreach (var desc in dbContextDescriptors) services.Remove(desc);

                services.AddDbContext<PedidosDbContext>(options =>
                    options.UseInMemoryDatabase($"pedidos-tests-{Guid.NewGuid()}"));
            });
        });
    }

    [Fact]
    public async Task Post_pedidos_creates_rascunho_with_itens_and_parcelas()
    {
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var request = new CriarPedidoRequest(
            ClienteId: "cli-001",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 10, 100m, 50m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("2", DateTime.UtcNow.Date.AddDays(7), "boleto"),
            Observacao: "Pedido com observacao para teste");

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain("/pedidos/");

        var body = await response.Content.ReadFromJsonAsync<CreatePedidoResponse>();
        body.Should().NotBeNull();
        body!.Status.Should().Be("rascunho");
        body.ItensCount.Should().Be(1);
        body.ParcelasCount.Should().Be(2);
        body.TotalBruto.Should().Be(1000m);
        body.TotalDesconto.Should().Be(50m);
        body.TotalLiquido.Should().Be(950m);
    }

    [Fact]
    public async Task Get_pedidos_returns_created_pedido()
    {
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var request = new CriarPedidoRequest(
            ClienteId: "cli-001",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 2, 10m, 0m),
                new CriarPedidoItemRequest("prod-002", "SKU-002", "Produto 2", 1, 5m, 0m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("2", DateTime.UtcNow.Date.AddDays(7), "boleto"),
            Observacao: "Entregar no periodo da tarde");

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
        body.TotalDesconto.Should().Be(0m);
        body.TotalLiquido.Should().Be(25m);
        body.Observacao.Should().Be("Entregar no periodo da tarde");
    }

    [Fact]
    public async Task Get_pedidos_list_returns_totals_for_authenticated_tenant()
    {
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        var request = new CriarPedidoRequest(
            ClienteId: "cli-002",
            Itens:
            [
                new CriarPedidoItemRequest("prod-010", "SKU-010", "Produto 10", 4, 25m, 10m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("1", DateTime.UtcNow.Date.AddDays(7), "boleto"));

        var post = await client.PostAsJsonAsync("/pedidos", request);
        post.EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<PedidoSummaryResponse[]>("/pedidos");
        list.Should().NotBeNull();
        list.Should().ContainSingle(p =>
            p.ClienteId == "cli-002"
            && p.TotalBruto == 100m
            && p.TotalDesconto == 10m
            && p.TotalLiquido == 90m);
    }

    [Fact]
    public async Task Post_pedidos_invalid_item_discount_returns_bad_request()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var request = new CriarPedidoRequest(
            ClienteId: "cli-003",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 1, 10m, 11m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("1", DateTime.UtcNow.Date.AddDays(7), "boleto"));

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        loginResponse.EnsureSuccessStatusCode();
        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private sealed record GetPedidoResponse(string PedidoId, string TenantId, string ClienteId, DateTimeOffset CriadoEm, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido, PedidoItemDto[] Itens, PedidoParcelaDto[] Parcelas, string? Observacao);
    private sealed record PedidoSummaryResponse(string PedidoId, string TenantId, string ClienteId, DateTimeOffset CriadoEm, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido);
    private sealed record PedidoItemDto(string ProdutoId, string Sku, string Nome, decimal Quantidade, decimal PrecoUnitario, decimal Desconto, decimal Total);
    private sealed record PedidoParcelaDto(int Numero, DateTime DataVencimento, decimal Valor, string FormaPagamento);
    private sealed record CreatePedidoResponse(string PedidoId, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido);
}
