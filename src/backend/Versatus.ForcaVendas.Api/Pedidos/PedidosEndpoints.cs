using MediatR;
using Microsoft.EntityFrameworkCore;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Infrastructure.Data;

namespace Versatus.ForcaVendas.Api.Pedidos;

public static class PedidosEndpoints
{
    public static WebApplication MapPedidosEndpoints(this WebApplication app)
    {
        app.MapPost("/pedidos", async (
            ITenantContext tenantContext,
            CriarPedidoRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var errors = request.Validate();
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await mediator.Send(new CriarPedidoCommand(
                tenantContext.TenantId,
                request.ClienteId,
                request.Itens,
                request.CondicaoPagamento,
                request.Observacao), cancellationToken);

            return Results.Created($"/pedidos/{result.PedidoId}", new
            {
                pedidoId = result.PedidoId,
                status = result.Status,
                itensCount = result.ItensCount,
                parcelasCount = result.ParcelasCount,
                totalBruto = result.TotalBruto,
                totalDesconto = result.TotalDesconto,
                totalLiquido = result.TotalLiquido
            });
        })
        .WithName("CreatePedido")
        .WithOpenApi();

        app.MapGet("/pedidos/{id}", async (
            ITenantContext tenantContext,
            Guid id,
            PedidosDbContext db,
            IPedidoCache pedidoCache,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var pedido = await db.Pedidos
                .Where(p => p.Id == id)
                .Include(p => p.Itens)
                .Include(p => p.Parcelas)
                .Include(p => p.Status)
                .FirstOrDefaultAsync(cancellationToken);

            if (pedido is null)
            {
                if (!pedidoCache.TryGet(id, out var cached) || cached is null)
                {
                    return Results.NotFound();
                }

                pedido = cached;
            }

            var itens = pedido.Itens.Select(i => new PedidoItemDto(
                i.ProdutoId,
                i.Sku,
                i.Nome,
                i.Quantidade,
                i.PrecoUnitario,
                i.Desconto,
                i.Total)).ToList();

            var parcelas = pedido.Parcelas.OrderBy(p => p.Numero).Select(p => new PedidoParcelaDto(
                p.Numero,
                p.DataVencimento,
                p.Valor,
                p.FormaPagamento)).ToList();

            var response = new PedidoResponse(
                pedido.Id,
                pedido.TenantId,
                pedido.ClienteId,
                pedido.CriadoEm,
                pedido.Status?.Codigo ?? "",
                itens.Count,
                parcelas.Count,
                pedido.TotalBruto,
                pedido.TotalDesconto,
                pedido.TotalLiquido,
                itens,
                parcelas,
                pedido.Observacao);

            return Results.Ok(response);
        })
        .WithName("GetPedido")
        .WithOpenApi();

        app.MapGet("/pedidos", async (
            ITenantContext tenantContext,
            PedidosDbContext db,
            IPedidoCache pedidoCache,
            string? clienteId,
            string? status,
            int? page,
            int? pageSize,
            CancellationToken cancellationToken) =>
        {
            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var query = db.Pedidos
                .AsNoTracking()
                .Include(p => p.Status)
                .Include(p => p.Itens)
                .Include(p => p.Parcelas)
                .Where(p => p.TenantId == tenantContext.TenantId);

            if (!string.IsNullOrWhiteSpace(clienteId))
            {
                query = query.Where(p => p.ClienteId == clienteId);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(p => p.Status != null && p.Status.Codigo == status);
            }

            int pageNumber = page.GetValueOrDefault(1);
            int pageSizeNumber = pageSize.GetValueOrDefault(20);
            if (pageNumber < 1) pageNumber = 1;
            if (pageSizeNumber < 1 || pageSizeNumber > 100) pageSizeNumber = 20;

            var pedidos = await query
                .OrderByDescending(p => p.CriadoEm)
                .Skip((pageNumber - 1) * pageSizeNumber)
                .Take(pageSizeNumber)
                .ToListAsync(cancellationToken);

            if (pedidos.Count == 0)
            {
                var cachedQuery = pedidoCache.GetByTenant(tenantContext.TenantId)
                    .AsEnumerable();

                if (!string.IsNullOrWhiteSpace(clienteId))
                {
                    cachedQuery = cachedQuery.Where(p => p.ClienteId == clienteId);
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    cachedQuery = cachedQuery.Where(p => p.Status != null && p.Status.Codigo == status);
                }

                pedidos = cachedQuery
                    .OrderByDescending(p => p.CriadoEm)
                    .Skip((pageNumber - 1) * pageSizeNumber)
                    .Take(pageSizeNumber)
                    .ToList();
            }

            var result = pedidos.Select(p => new
            {
                pedidoId = p.Id,
                tenantId = p.TenantId,
                clienteId = p.ClienteId,
                criadoEm = p.CriadoEm,
                status = p.Status?.Codigo ?? string.Empty,
                itensCount = p.Itens.Count,
                parcelasCount = p.Parcelas.Count,
                totalBruto = p.TotalBruto,
                totalDesconto = p.TotalDesconto,
                totalLiquido = p.TotalLiquido
            });

            return Results.Ok(result);
        })
        .WithName("ListPedidosQuery")
        .WithOpenApi();

        return app;
    }
}