using Microsoft.Extensions.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using MediatR;
using Prometheus;
using Versatus.ForcaVendas.Api.Health;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Application.Licenca;
using StackExchange.Redis;
using Versatus.ForcaVendas.Application.Sessao;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Api.Pedidos;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Data.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
builder.Services.AddScoped<ITenantSubscriptionRepository, NpgsqlTenantSubscriptionRepository>();
builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));
builder.Services.AddSingleton<ISessionStore, RedisSessionStore>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddSingleton<ISessionAuditEventRepository, InMemorySessionAuditEventRepository>();
builder.Services.AddSingleton<IProductCatalogRepository, InMemoryProductCatalogRepository>();
builder.Services.AddDbContext<PedidosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMediatR(typeof(CriarPedidoCommand));
builder.Services.AddValidatorsFromAssemblyContaining<CriarPedidoRequestValidator>();
builder.Services.AddHealthChecks()
    .AddCheck<RedisHealthCheck>("redis");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<TenantContextMiddleware>();

// Prometheus metrics for HTTP + metrics endpoint
app.UseHttpMetrics();

// Liveness: basic ping of the process (no external deps)
app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }))
    .WithName("Liveness");

// Readiness: execute registered health checks
app.MapGet("/health/ready", async (HealthCheckService hc) =>
{
    var report = await hc.CheckHealthAsync();
    var result = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds
        })
    };

    return Results.Json(result, statusCode: report.Status == HealthStatus.Healthy ? 200 : 503);
})
    .WithName("Readiness");

// Expose Prometheus metrics at /metrics
app.MapMetrics();
app.MapPost("/auth/login", async (
    LoginRequest request,
    IOptions<AuthOptions> options,
    IJwtTokenService tokenService,
    IRefreshTokenStore refreshTokenStore,
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
    if (authOptions.Tenants.Count == 0 || authOptions.Users.Count == 0)
    {
        return Results.Problem(
            detail: "Configuracao de autenticacao invalida no servidor.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var tenantExists = authOptions.Tenants
        .Any(t => string.Equals(t, request.TenantId, StringComparison.OrdinalIgnoreCase));

    if (!tenantExists)
    {
        return Results.Unauthorized();
    }

    var user = authOptions.Users.FirstOrDefault(u =>
        string.Equals(u.TenantId, request.TenantId, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(u.Username, request.Username, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(u.Password, request.Password, StringComparison.Ordinal));

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var subscription = await subscriptionRepository.GetByTenantIdAsync(user.TenantId, cancellationToken);
    if (subscription is null || !subscription.IsActive)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var activeSessions = await sessionStore.CountActiveAsync(user.TenantId, cancellationToken);
    if (activeSessions >= subscription.MaxConcurrentUsers)
    {
        return Results.Problem(
            detail: "Limite de usuarios simultaneos atingido para este tenant.",
            statusCode: StatusCodes.Status403Forbidden,
            title: "Concurrent user limit reached");
    }

    var tokenPair = tokenService.Generate(user);
    refreshTokenStore.Save(tokenPair.RefreshToken, user.UserId, user.TenantId, tokenPair.RefreshTokenExpiresAtUtc);

    await sessionStore.AddAsync(tokenPair.SessionId, user.UserId, user.TenantId, cancellationToken);

    // Audit login event
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
 
app.MapPost("/auth/refresh", (
    RefreshTokenRequest request,
    IOptions<AuthOptions> options,
    IJwtTokenService tokenService,
    IRefreshTokenStore refreshTokenStore) =>
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

    var user = options.Value.Users.FirstOrDefault(u =>
        string.Equals(u.UserId, tokenInfo.UserId, StringComparison.Ordinal) &&
        string.Equals(u.TenantId, tokenInfo.TenantId, StringComparison.OrdinalIgnoreCase));

    if (user is null)
    {
        return Results.Unauthorized();
    }

    var tokenPair = tokenService.Generate(user);

    // Rotate refresh token to reduce replay window.
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

app.MapGet("/catalogo/produtos", async (
    ITenantContext tenantContext,
    IProductCatalogRepository repository,
    string? q,
    int? limit,
    CancellationToken cancellationToken) =>
{
    if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
    {
        return Results.Unauthorized();
    }

    var effectiveLimit = limit.GetValueOrDefault(20);
    if (effectiveLimit <= 0 || effectiveLimit > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["limit"] = ["limit must be between 1 and 100."]
        });
    }

    var products = await repository.SearchProductsAsync(
        tenantContext.TenantId,
        q,
        effectiveLimit,
        cancellationToken);

    return Results.Ok(products);
})
.WithName("SearchProducts")
.WithOpenApi();

app.MapPost("/pedidos", async (
    ITenantContext tenantContext,
    CriarPedidoRequest request,
    IMediator mediator,
    CancellationToken cancellationToken) =>
{
    if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
    {
        return Results.Unauthorized();
    }

    var errors = request.Validate();
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var result = await mediator.Send(new CriarPedidoCommand(
        tenantContext.TenantId,
        request.ClienteId,
        request.Itens,
        request.CondicaoPagamento), cancellationToken);

    return Results.Created($"/pedidos/{result.PedidoId}", new
    {
        pedidoId = result.PedidoId,
        status = result.Status,
        itensCount = result.ItensCount,
        parcelasCount = result.ParcelasCount
    });
})
.WithName("CreatePedido")
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
            ["sessionId"] = new[] { "sessionId is required" }
        });
    }

    // Remove session from store
    await sessionStore.RemoveAsync(request.SessionId, tenantContext.TenantId!, cancellationToken);

    // Optionally revoke provided refresh token for the session
    if (!string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        refreshTokenStore.Revoke(request.RefreshToken);
    }

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
    // Audit logout event
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

app.Run();
