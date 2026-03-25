using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Tests.Stubs;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Application.Licenca;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class AuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // replace ISessionStore with in-memory stub
                var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ISessionStore));
                if (descriptor is not null) services.Remove(descriptor);
                services.AddSingleton<ISessionStore, InMemorySessionStore>();
                    var subDesc = services.SingleOrDefault(d => d.ServiceType == typeof(ITenantSubscriptionRepository));
                    if (subDesc is not null) services.Remove(subDesc);
                    services.AddSingleton<ITenantSubscriptionRepository, InMemoryTenantSubscriptionRepository>();
            });
        });
    }

    [Fact]
    public async Task Login_Heartbeat_Logout_flow()
    {
        var client = _factory.CreateClient();

        var loginReq = new LoginRequest("00000000-0000-0000-0000-000000000001", "admin", "123456");
        var loginResp = await client.PostAsJsonAsync("/auth/login", loginReq);
        loginResp.EnsureSuccessStatusCode();

        var loginBody = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        loginBody.Should().NotBeNull();
        loginBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
        loginBody.RefreshToken.Should().NotBeNullOrWhiteSpace();

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

        var hb = await client.PatchAsync("/auth/heartbeat", null);
        hb.EnsureSuccessStatusCode();

        var logoutResp = await client.PostAsJsonAsync("/auth/logout", new LogoutRequest { RefreshToken = loginBody.RefreshToken });
        logoutResp.EnsureSuccessStatusCode();
    }
}
