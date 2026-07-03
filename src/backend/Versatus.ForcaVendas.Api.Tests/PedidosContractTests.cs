using System;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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

public class PedidosContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PedidosContractTests(WebApplicationFactory<Program> factory)
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
                    options.UseInMemoryDatabase($"pedidos-contract-tests-{Guid.NewGuid()}"));
            });
        });
    }

    [Fact]
    public async Task PostPedidos_semBearer_deveRetornar401_contrato()
    {
        var client = _factory.CreateClient();

        var request = new CriarPedidoRequest(
            ClienteId: "cli-001",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 1, 10m, 0m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("1", DateTime.UtcNow.Date.AddDays(7), "boleto"));

        var response = await client.PostAsJsonAsync("/pedidos", request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostPedidos_valido_deveConterCamposObrigatorios_contrato()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "admin@demo1.versatus.com", "Mudar@!123");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var request = new CriarPedidoRequest(
            ClienteId: "cli-001",
            Itens:
            [
                new CriarPedidoItemRequest("prod-001", "SKU-001", "Produto 1", 2, 10m, 0m)
            ],
            CondicaoPagamento: new CriarPedidoCondicaoPagamentoRequest("1", DateTime.UtcNow.Date.AddDays(7), "boleto"));

        var response = await client.PostAsJsonAsync("/pedidos", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("pedidoId", out _).Should().BeTrue();
        json.TryGetProperty("status", out _).Should().BeTrue();
        json.TryGetProperty("itensCount", out _).Should().BeTrue();
        json.TryGetProperty("parcelasCount", out _).Should().BeTrue();
        json.TryGetProperty("totalBruto", out _).Should().BeTrue();
        json.TryGetProperty("totalDesconto", out _).Should().BeTrue();
        json.TryGetProperty("totalLiquido", out _).Should().BeTrue();
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));
        loginResponse.EnsureSuccessStatusCode();
        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }
}
