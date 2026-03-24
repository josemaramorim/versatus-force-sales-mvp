using Microsoft.Extensions.Options;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IRefreshTokenStore, InMemoryRefreshTokenStore>();
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<TenantContextMiddleware>();

app.MapPost("/auth/login", (
    LoginRequest request,
    IOptions<AuthOptions> options,
    IJwtTokenService tokenService,
    IRefreshTokenStore refreshTokenStore) =>
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

    var tokenPair = tokenService.Generate(user);
    refreshTokenStore.Save(tokenPair.RefreshToken, user.UserId, user.TenantId, tokenPair.RefreshTokenExpiresAtUtc);

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

app.Run();
