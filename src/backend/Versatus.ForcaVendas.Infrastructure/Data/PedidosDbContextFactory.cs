using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Versatus.ForcaVendas.Infrastructure.Data;

public sealed class PedidosDbContextFactory : IDesignTimeDbContextFactory<PedidosDbContext>
{
    public PedidosDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PedidosDbContext>();
        var connectionString = Environment.GetEnvironmentVariable("VERSATUS_DEFAULT_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=forca_vendas;Username=fvs;Password=TROQUE_EM_PRODUCAO";

        optionsBuilder.UseNpgsql(connectionString);
        return new PedidosDbContext(optionsBuilder.Options);
    }
}
