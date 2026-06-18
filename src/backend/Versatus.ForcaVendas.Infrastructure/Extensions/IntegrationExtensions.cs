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
        var transport = config.GetValue<string>("Integration:Transport") ?? "RabbitMq";

        return transport switch
        {
            "Ftp" => services.AddSingleton<IIntegrationTransport, FtpIntegrationTransport>()
                             .Configure<FtpTransportOptions>(config.GetSection("Integration:Ftp")),
            "GoogleDrive" => services.AddSingleton<IIntegrationTransport, GoogleDriveIntegrationTransport>()
                                     .Configure<GoogleDriveTransportOptions>(config.GetSection("Integration:GoogleDrive")),
            _ => services.AddSingleton<IIntegrationTransport, RabbitMqIntegrationTransport>()
                         .Configure<RabbitMqTransportOptions>(config.GetSection("Integration:RabbitMq")),
        };
    }
}
