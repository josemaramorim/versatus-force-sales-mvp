using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Versatus.ForcaVendas.Infrastructure.Integration;
using Versatus.ForcaVendas.Infrastructure.Integration.Ftp;
using Versatus.ForcaVendas.Infrastructure.Integration.GoogleDrive;
using Versatus.ForcaVendas.Infrastructure.Integration.RabbitMq;

namespace Microsoft.Extensions.DependencyInjection;

public static class IntegrationExtensions
{
    public static IServiceCollection AddIntegrationTransport(
        this IServiceCollection services, IConfiguration config)
    {
        var transport = (config.GetValue<string>("Integration:Transport") ?? "RabbitMq").Trim();

        if (transport.Equals("Ftp", StringComparison.OrdinalIgnoreCase))
            return services.AddSingleton<IIntegrationTransport, FtpIntegrationTransport>()
                           .Configure<FtpTransportOptions>(config.GetSection("Integration:Ftp"));

        if (transport.Equals("GoogleDrive", StringComparison.OrdinalIgnoreCase))
            return services.AddSingleton<IIntegrationTransport, GoogleDriveIntegrationTransport>()
                           .Configure<GoogleDriveTransportOptions>(config.GetSection("Integration:GoogleDrive"));

        // Fallback: RabbitMq (planejado para fase futura — lança NotImplementedException em runtime)
        return services.AddSingleton<IIntegrationTransport, RabbitMqIntegrationTransport>()
                       .Configure<RabbitMqTransportOptions>(config.GetSection("Integration:RabbitMq"));
    }
}
