using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Tests.Stubs;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Domain.Auth;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;
using Xunit;

namespace Versatus.ForcaVendas.Api.Tests;

public class AuthContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
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

                var authOptions = new AuthOptions
                {
                    Jwt = new JwtOptions
                    {
                        Issuer = "versatus-force-sales",
                        Audience = "versatus-force-sales-clients",
                        SecretKey = "VersatusForceSalesDevSecretKey2026!",
                        AccessTokenMinutes = 60,
                        RefreshTokenDays = 7
                    }
                };

                services.AddSingleton(Options.Create(authOptions));
            });
        });
    }

    [Fact]
    public async Task LoginResponse_DeveConterCamposObrigatoriosDoContrato()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            email = "admin@demo1.versatus.com",
            password = "Mudar@!123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.TryGetProperty("accessToken", out _).Should().BeTrue();
        json.TryGetProperty("refreshToken", out _).Should().BeTrue();
        json.TryGetProperty("expiresInSeconds", out _).Should().BeTrue();
        json.TryGetProperty("tokenType", out var tokenType).Should().BeTrue();
        tokenType.GetString().Should().Be("Bearer");
    }

    [Fact]
    public async Task LoginSemEmail_DeveRetornar400ConformeContrato()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            password = "Mudar@!123"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HeartbeatSemBearer_DeveRetornar401ConformeContrato()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsync("/auth/heartbeat", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutSemBearer_DeveRetornar401ConformeContrato()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/logout", new { refreshToken = "dummy" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
