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
    public DbSet<TenantSubscriptionEntity> TenantSubscriptions => Set<TenantSubscriptionEntity>();
    public DbSet<UsuarioEntity> Usuarios => Set<UsuarioEntity>();
    public DbSet<SessionAuditEventEntity> AuditEvents => Set<SessionAuditEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionAuditEventEntity>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TenantId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.Property(x => x.Timestamp).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
        });

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

        modelBuilder.Entity<TenantSubscriptionEntity>(entity =>
        {
            entity.ToTable("assinaturas");
            entity.HasKey(x => x.TenantId);
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.CompanyName).HasColumnName("nome_empresa").HasMaxLength(200).IsRequired();
            entity.Property(x => x.MaxConcurrentUsers).HasColumnName("max_usuarios_simultaneos").IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("ativo").IsRequired();

            entity.HasData(
                new TenantSubscriptionEntity
                {
                    TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    CompanyName = "Demo Tenant 1",
                    MaxConcurrentUsers = 4,
                    IsActive = true
                },
                new TenantSubscriptionEntity
                {
                    TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    CompanyName = "Demo Tenant 2",
                    MaxConcurrentUsers = 4,
                    IsActive = true
                }
            );
        });

        modelBuilder.Entity<UsuarioEntity>(entity =>
        {
            entity.ToTable("usuarios");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
            entity.Property(x => x.Ativo).HasColumnName("ativo").IsRequired();
            entity.Property(x => x.CriadoEm).HasColumnName("criado_em").IsRequired();

            entity.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();

            // Seed initial data
            var adminId = Guid.Parse("7c90e66f-0af5-4ded-90e2-0df0a0b2d001");
            var gestorId = Guid.Parse("7c90e66f-0af5-4ded-90e2-0df0a0b2d002");
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

            entity.HasData(
                new UsuarioEntity
                {
                    Id = adminId,
                    TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Username = "admin",
                    PasswordHash = defaultPasswordHash,
                    Role = "admin",
                    Ativo = true,
                    CriadoEm = DateTimeOffset.UtcNow
                },
                new UsuarioEntity
                {
                    Id = gestorId,
                    TenantId = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Username = "gestor",
                    PasswordHash = defaultPasswordHash,
                    Role = "gestor",
                    Ativo = true,
                    CriadoEm = DateTimeOffset.UtcNow
                }
            );
        });
    }
}
