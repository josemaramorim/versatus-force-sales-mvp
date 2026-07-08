using System.Reflection;
using System.Runtime.InteropServices;

namespace Versatus.ForcaVendas.Api.Version;

public static class VersionEndpoints
{
    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/version", () =>
        {
            var version = typeof(VersionEndpoints).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? "1.0.0-unknown";

            return Results.Ok(new
            {
                appName = "Versatus Force Sales API",
                version,
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                dotnetVersion = RuntimeInformation.FrameworkDescription
            });
        }).AllowAnonymous();

        return app;
    }
}
