using Microsoft.Extensions.Options;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Application.Licenca;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;

namespace Versatus.ForcaVendas.Api.Auth;

public static class AuthEndpoints
{
    public static WebApplication MapAuthSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/login", async (
            LoginRequest request,
            IOptions<AuthOptions> options,
            IJwtTokenService tokenService,
            IRefreshTokenStore refreshTokenStore,
            Versatus.ForcaVendas.Domain.Auth.IUsuarioRepository usuarioRepository,
            ITenantSubscriptionRepository subscriptionRepository,
            ISessionStore sessionStore,
            ISessionAuditEventRepository auditRepo,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var errors = request.Validate();
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var authOptions = options.Value;

            var usuarioBanco = await usuarioRepository.GetByEmailAsync(request.Email, cancellationToken);
            if (usuarioBanco is null)
            {
                return Results.Unauthorized();
            }

            if (!BCrypt.Net.BCrypt.Verify(request.Password, usuarioBanco.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var user = new DemoUser
            {
                UserId = usuarioBanco.Id.ToString(),
                TenantId = usuarioBanco.TenantId.ToString(),
                Username = usuarioBanco.Username,
                Email = usuarioBanco.Email,
                Role = usuarioBanco.Role,
                Password = ""
            };

            // Se a lista estática de tenants estiver configurada, valida contra ela.
            // Caso contrário (vazia/produção), a API é dinâmica e valida apenas pelo banco de dados (assinaturas).
            if (authOptions.Tenants.Count > 0)
            {
                var tenantExists = authOptions.Tenants
                    .Any(t => string.Equals(t, user.TenantId, StringComparison.OrdinalIgnoreCase));

                if (!tenantExists)
                {
                    return Results.Unauthorized();
                }
            }

            var subscription = await subscriptionRepository.GetByTenantIdAsync(user.TenantId, cancellationToken);
            if (subscription is null || !subscription.IsActive)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            var activeSessions = await sessionStore.CountActiveAsync(user.TenantId, cancellationToken);
            if (!subscription.HasAvailableSeats(activeSessions))
            {
                return Results.Problem(
                    detail: "Limite de usuarios simultaneos atingido para este tenant.",
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "Concurrent user limit reached");
            }

            var tokenPair = tokenService.Generate(user);
            refreshTokenStore.Save(tokenPair.RefreshToken, user.UserId, user.TenantId, tokenPair.RefreshTokenExpiresAtUtc);

            await sessionStore.AddAsync(tokenPair.SessionId, user.UserId, user.TenantId, cancellationToken);

            var auditEvent = new Versatus.ForcaVendas.Domain.Auditoria.SessionAuditEvent(
                Id: Guid.NewGuid().ToString(),
                UserId: user.UserId,
                TenantId: user.TenantId,
                EventType: Versatus.ForcaVendas.Domain.Auditoria.SessionAuditEventType.Login,
                Timestamp: DateTimeOffset.UtcNow,
                IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContext.Request.Headers["User-Agent"].ToString()
            );
            await auditRepo.AddAsync(auditEvent, cancellationToken);

            return Results.Ok(new LoginResponse(
                tokenPair.AccessToken,
                tokenPair.RefreshToken,
                tokenPair.AccessTokenExpiresInSeconds,
                "Bearer"));
        })
        .WithName("Login")
        .WithOpenApi();

        app.MapPost("/auth/refresh", async (
            RefreshTokenRequest request,
            IOptions<AuthOptions> options,
            IJwtTokenService tokenService,
            Versatus.ForcaVendas.Domain.Auth.IUsuarioRepository usuarioRepository,
            IRefreshTokenStore refreshTokenStore,
            CancellationToken cancellationToken) =>
        {
            var errors = request.Validate();
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            if (!refreshTokenStore.TryGetActive(request.RefreshToken, out var tokenInfo))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(tokenInfo.UserId, out var userId))
            {
                return Results.Unauthorized();
            }

            var usuarioBanco = await usuarioRepository.GetByIdAsync(userId, cancellationToken);

            if (usuarioBanco is null || !string.Equals(usuarioBanco.TenantId.ToString(), tokenInfo.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Unauthorized();
            }

            var user = new DemoUser
            {
                UserId = usuarioBanco.Id.ToString(),
                TenantId = usuarioBanco.TenantId.ToString(),
                Username = usuarioBanco.Username,
                Email = usuarioBanco.Email,
                Role = usuarioBanco.Role,
                Password = ""
            };

            var tokenPair = tokenService.Generate(user);
            refreshTokenStore.Revoke(request.RefreshToken);
            refreshTokenStore.Save(tokenPair.RefreshToken, user.UserId, user.TenantId, tokenPair.RefreshTokenExpiresAtUtc);

            return Results.Ok(new LoginResponse(
                tokenPair.AccessToken,
                tokenPair.RefreshToken,
                tokenPair.AccessTokenExpiresInSeconds,
                "Bearer"));
        })
        .WithName("RefreshToken")
        .WithOpenApi();

        app.MapGet("/tenant/ping", (ITenantContext tenantContext) =>
        {
            return Results.Ok(new
            {
                message = "Tenant context resolved.",
                tenantId = tenantContext.TenantId
            });
        })
        .WithName("TenantPing")
        .WithOpenApi();

        app.MapGet("/licenca/{tenantId}/limite", async (
            string tenantId,
            ITenantSubscriptionRepository repository,
            CancellationToken cancellationToken) =>
        {
            var subscription = await repository.GetByTenantIdAsync(tenantId, cancellationToken);
            if (subscription is null)
            {
                return Results.NotFound(new { message = "Tenant nao encontrado." });
            }

            return Results.Ok(new
            {
                tenantId = subscription.TenantId,
                companyName = subscription.CompanyName,
                maxConcurrentUsers = subscription.MaxConcurrentUsers,
                isActive = subscription.IsActive
            });
        })
        .WithName("GetTenantConcurrentUserLimit")
        .WithOpenApi();

        app.MapMethods("/auth/heartbeat", ["PATCH"], async (
            ITenantContext tenantContext,
            ISessionStore sessionStore,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.SessionId))
            {
                return Results.Unauthorized();
            }

            await sessionStore.HeartbeatAsync(tenantContext.SessionId, tenantContext.TenantId!, cancellationToken);
            return Results.Ok(new { message = "Session renewed.", sessionId = tenantContext.SessionId });
        })
        .WithName("SessionHeartbeat")
        .WithOpenApi();

        app.MapGet("/admin/sessions", async (
            ITenantContext tenantContext,
            ISessionStore sessionStore,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant)
            {
                return Results.Unauthorized();
            }

            var sessions = await sessionStore.GetActiveSessionsAsync(tenantContext.TenantId!, cancellationToken);
            return Results.Ok(sessions.Select(s => new
            {
                sessionId = s.SessionId,
                userId = s.UserId,
                tenantId = s.TenantId,
                loginAt = s.LoginAt,
                lastHeartbeatAt = s.LastHeartbeatAt
            }));
        })
        .WithName("GetActiveSessions")
        .WithOpenApi();

        app.MapPost("/admin/sessions/evict", async (
            ITenantContext tenantContext,
            ISessionStore sessionStore,
            IRefreshTokenStore refreshTokenStore,
            ISessionAuditEventRepository auditRepo,
            EvictRequest request,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant)
            {
                return Results.Unauthorized();
            }

            if (string.IsNullOrWhiteSpace(request?.SessionId))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sessionId"] = ["sessionId is required"]
                });
            }

            await sessionStore.RemoveAsync(request.SessionId, tenantContext.TenantId!, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.UserId))
            {
                refreshTokenStore.RevokeAllForUser(request.UserId);
            }
            else if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                refreshTokenStore.Revoke(request.RefreshToken);
            }

            var evictAudit = new Versatus.ForcaVendas.Domain.Auditoria.SessionAuditEvent(
                Id: Guid.NewGuid().ToString(),
                UserId: request.UserId ?? tenantContext.UserId ?? string.Empty,
                TenantId: tenantContext.TenantId!,
                EventType: Versatus.ForcaVendas.Domain.Auditoria.SessionAuditEventType.Eviction,
                Timestamp: DateTimeOffset.UtcNow
            );
            await auditRepo.AddAsync(evictAudit, cancellationToken);

            return Results.Ok(new { message = "Session evicted", sessionId = request.SessionId });
        })
        .WithName("EvictSession")
        .WithOpenApi();

        app.MapPost("/auth/logout", async (
            ITenantContext tenantContext,
            IRefreshTokenStore refreshTokenStore,
            ISessionStore sessionStore,
            ISessionAuditEventRepository auditRepo,
            HttpContext httpContext,
            LogoutRequest? request,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.SessionId))
            {
                return Results.Unauthorized();
            }

            if (request is not null && !string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                refreshTokenStore.Revoke(request.RefreshToken);
            }

            await sessionStore.RemoveAsync(tenantContext.SessionId, tenantContext.TenantId!, cancellationToken);
            var auditEvent = new Versatus.ForcaVendas.Domain.Auditoria.SessionAuditEvent(
                Id: Guid.NewGuid().ToString(),
                UserId: tenantContext.UserId ?? string.Empty,
                TenantId: tenantContext.TenantId!,
                EventType: Versatus.ForcaVendas.Domain.Auditoria.SessionAuditEventType.Logout,
                Timestamp: DateTimeOffset.UtcNow,
                IpAddress: httpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent: httpContext.Request.Headers["User-Agent"].ToString()
            );
            await auditRepo.AddAsync(auditEvent, cancellationToken);

            return Results.Ok(new { message = "Logged out", sessionId = tenantContext.SessionId });
        })
        .WithName("Logout")
        .WithOpenApi();

        return app;
    }
}