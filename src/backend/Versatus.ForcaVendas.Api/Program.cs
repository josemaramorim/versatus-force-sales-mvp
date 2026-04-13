using Microsoft.Extensions.Diagnostics.HealthChecks;
using Prometheus;
using Versatus.ForcaVendas.Api;
using Versatus.ForcaVendas.Api.Auth;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Api.Pedidos;

var builder = WebApplication.CreateBuilder(args);

Program.AddPresentationServices(builder);
Program.AddDependencyComposition(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendDev");
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
app.MapAuthSessionEndpoints();
app.MapCatalogoEndpoints();
app.MapPedidosEndpoints();

app.MapControllers();
app.Run();
