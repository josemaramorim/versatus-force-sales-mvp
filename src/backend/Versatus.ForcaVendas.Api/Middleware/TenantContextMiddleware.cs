using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Versatus.ForcaVendas.Api.Auth;

namespace Versatus.ForcaVendas.Api.Middleware;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    private static readonly string[] PublicPrefixes =
    [
        "/auth/login",
        "/auth/refresh",
        "/swagger"
    ];

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenantContext,
        IOptions<AuthOptions> authOptions)
    {
        if (IsPublicPath(context.Request.Path))
        {
            await next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Missing bearer token." });
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!TryValidateAndReadTenant(token, authOptions.Value.Jwt, out var tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid token." });
            return;
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { message = "Token does not contain tenant_id claim." });
            return;
        }

        tenantContext.TenantId = tenantId;
        await next(context);
    }

    private static bool IsPublicPath(PathString path)
    {
        if (path.Equals("/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return PublicPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryValidateAndReadTenant(string token, JwtOptions jwtOptions, out string? tenantId)
    {
        tenantId = null;
        try
        {
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            tenantId = principal.FindFirst("tenant_id")?.Value;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
