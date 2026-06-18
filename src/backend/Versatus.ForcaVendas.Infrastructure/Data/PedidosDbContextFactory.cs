using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Versatus.ForcaVendas.Infrastructure.Data;

public sealed class PedidosDbContextFactory : IDesignTimeDbContextFactory<PedidosDbContext>
{
    public PedidosDbContext CreateDbContext(string[] args)
    {
        // Tenta achar o diretório da API onde o appsettings.json está
        string apiPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Versatus.ForcaVendas.Api");
        if (!Directory.Exists(apiPath))
        {
            apiPath = Path.Combine(Directory.GetCurrentDirectory(), "src", "backend", "Versatus.ForcaVendas.Api");
        }

        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<PedidosDbContext>();
        
        // Puxa a string chamada "DefaultConnection" do JSON
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("String de conexão 'DefaultConnection' não encontrada no appsettings.json.");
        }

        optionsBuilder.UseNpgsql(connectionString);
        return new PedidosDbContext(optionsBuilder.Options);
    }
}
