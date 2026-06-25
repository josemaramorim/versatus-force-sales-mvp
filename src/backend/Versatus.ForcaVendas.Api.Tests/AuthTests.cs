using System;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http.Json;
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

public class AuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly InMemorySessionStore _sessionStore = new();
    private readonly InMemoryTenantSubscriptionRepository _tenantSubscriptionRepository = new();

    public AuthTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISessionStore>();
                services.AddSingleton<ISessionStore>(_sessionStore);

                services.RemoveAll<ITenantSubscriptionRepository>();
                services.AddSingleton<ITenantSubscriptionRepository>(_tenantSubscriptionRepository);

                services.RemoveAll<IUsuarioRepository>();
                services.AddSingleton<IUsuarioRepository, InMemoryUsuarioRepository>();

                services.RemoveAll<ISessionAuditEventRepository>();
                services.AddSingleton<ISessionAuditEventRepository, InMemorySessionAuditEventRepository>();

                var authOptions = new AuthOptions
                {
                    Tenants = new List<string>
                    {
                        "00000000-0000-0000-0000-000000000001",
                        "00000000-0000-0000-0000-000000000002"
                    },
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
    public async Task Login_Heartbeat_Logout_flow()
    {
        _tenantSubscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 4, true, "Demo Corp");
        var client = _factory.CreateClient();

        var loginReq = new LoginRequest("admin@demo1.versatus.com", "Mudar@!123");
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

    [Fact]
    public async Task Login_WithInvalidCredentials_Returns401()
    {
        _tenantSubscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 4, true, "Demo Corp");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@demo1.versatus.com", "senha_errada"));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_Returns401()
    {
        _tenantSubscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 4, true, "Demo Corp");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("usuario_inexistente@email.com", "qualquer"));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void InMemoryUsuarioRepository_EmailUnicidadeGlobal_NaoPermiteDuplicata()
    {
        var email = "duplicado@empresa.com";

        var usuarios = new[]
        {
            new Usuario
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Username = "user1",
                Email = email,
                PasswordHash = "hash",
                Role = "vendedor",
                Ativo = true,
                CriadoEm = DateTimeOffset.UtcNow
            }
        };

        var repo = InMemoryUsuarioRepository.FromUsers(usuarios);

        var resultado = repo.GetByEmailAsync(email).GetAwaiter().GetResult();
        resultado.Should().NotBeNull();
        resultado!.Email.Should().Be(email);

        var outro = repo.GetByEmailAsync("outro@empresa.com").GetAwaiter().GetResult();
        outro.Should().BeNull();
    }

    [Fact]
    public void InMemoryUsuarioRepository_GetByEmail_RetornaNullQuandoNaoExiste()
    {
        var repo = new InMemoryUsuarioRepository();

        var resultado = repo.GetByEmailAsync("naoexiste@x.com").GetAwaiter().GetResult();

        resultado.Should().BeNull();
    }

    [Fact]
    public void InMemoryUsuarioRepository_GetByEmail_RetornaUsuarioCorreto()
    {
        var repo = new InMemoryUsuarioRepository();

        var admin = repo.GetByEmailAsync("admin@demo1.versatus.com").GetAwaiter().GetResult();

        admin.Should().NotBeNull();
        admin!.Username.Should().Be("admin");
        admin.TenantId.Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000001"));
    }

    [Fact]
    public async Task Login_SemCamposTenant_TenantResolvidoInternamente()
    {
        _tenantSubscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 4, true, "Demo Corp");
        var client = _factory.CreateClient();

        var loginResp = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@demo1.versatus.com", "Mudar@!123"));
        loginResp.EnsureSuccessStatusCode();

        var body = await loginResp.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.TokenType.Should().Be("Bearer");

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(body.AccessToken);
        jwt.Claims.Should().Contain(c => c.Type == "tenant_id");
        var tenantClaim = jwt.Claims.First(c => c.Type == "tenant_id").Value;
        tenantClaim.Should().Be("00000000-0000-0000-0000-000000000001");
    }

    [Fact]
    public async Task Request_SemToken_EhBloqueadoComStatus401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/tenant/ping");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_QuandoSeatLimitAtingido_DeveRetornar403()
    {
        _tenantSubscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 1, true, "Demo Corp");
        var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@demo1.versatus.com", "Mudar@!123"));
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@demo1.versatus.com", "Mudar@!123"));
        second.StatusCode.Should().Be(System.Net.HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_AposEviccaoDaSessao_DevePermitirNovoAcesso()
    {
        _tenantSubscriptionRepository.ConfigureTenant("00000000-0000-0000-0000-000000000001", 1, true, "Demo Corp");
        var client = _factory.CreateClient();

        var firstLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@demo1.versatus.com", "Mudar@!123"));
        firstLogin.EnsureSuccessStatusCode();

        var firstBody = await firstLogin.Content.ReadFromJsonAsync<LoginResponse>();
        firstBody.Should().NotBeNull();
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(firstBody!.AccessToken);
        var sessionId = jwt.Claims.First(c => c.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti).Value;

        _sessionStore.ForceExpire(sessionId);

        var secondLogin = await client.PostAsJsonAsync("/auth/login", new LoginRequest("admin@demo1.versatus.com", "Mudar@!123"));
        secondLogin.EnsureSuccessStatusCode();
    }
}
