using Microsoft.EntityFrameworkCore;
using Versatus.ForcaVendas.Domain.Pedidos;

namespace Versatus.ForcaVendas.Infrastructure.Data;

public sealed class PedidosDbContext : DbContext
{
    public PedidosDbContext(DbContextOptions<PedidosDbContext> options) : base(options)
    {
    }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoItem> PedidoItens => Set<PedidoItem>();
    public DbSet<PedidoParcela> PedidoParcelas => Set<PedidoParcela>();
    public DbSet<PedidoStatus> PedidoStatuses => Set<PedidoStatus>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Pedido>(entity =>
        {
            entity.ToTable("pedidos");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ClienteId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.CriadoEm).IsRequired();
            entity.HasOne(x => x.Status)
                .WithMany(x => x.Pedidos)
                .HasForeignKey(x => x.StatusId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PedidoItem>(entity =>
        {
            entity.ToTable("pedido_itens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProdutoId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Sku).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Nome).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Quantidade).HasPrecision(18, 3);
            entity.Property(x => x.PrecoUnitario).HasPrecision(18, 2);
            entity.Property(x => x.Desconto).HasPrecision(18, 2);
            entity.Property(x => x.Total).HasPrecision(18, 2);

            entity.HasOne(x => x.Pedido)
                .WithMany(x => x.Itens)
                .HasForeignKey(x => x.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PedidoParcela>(entity =>
        {
            entity.ToTable("pedido_parcelas");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Numero).IsRequired();
            entity.Property(x => x.DataVencimento).HasColumnType("date");
            entity.Property(x => x.Valor).HasPrecision(18, 2);
            entity.Property(x => x.FormaPagamento).HasMaxLength(32).IsRequired();

            entity.HasOne(x => x.Pedido)
                .WithMany(x => x.Parcelas)
                .HasForeignKey(x => x.PedidoId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PedidoStatus>(entity =>
        {
            entity.ToTable("pedido_status");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Codigo).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Descricao).HasMaxLength(120).IsRequired();

            entity.HasData(
                new PedidoStatus { Id = PedidoStatus.RascunhoId, Codigo = "rascunho", Descricao = "Rascunho" },
                new PedidoStatus { Id = PedidoStatus.EnviadoId, Codigo = "enviado", Descricao = "Enviado" },
                new PedidoStatus { Id = PedidoStatus.ProcessadoId, Codigo = "processado", Descricao = "Processado" },
                new PedidoStatus { Id = PedidoStatus.ErroId, Codigo = "erro", Descricao = "Erro" }
            );
        });
    }
}
