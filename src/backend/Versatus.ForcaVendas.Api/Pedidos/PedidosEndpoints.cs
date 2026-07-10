using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Versatus.ForcaVendas.Api.Middleware;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Application.Catalogo;
using Versatus.ForcaVendas.Infrastructure.Integration;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;
using Versatus.ForcaVendas.Domain.Pedidos;

namespace Versatus.ForcaVendas.Api.Pedidos;

public static class PedidosEndpoints
{
    public static WebApplication MapPedidosEndpoints(this WebApplication app)
    {
        app.MapPost("/pedidos", async (
            ITenantContext tenantContext,
            CriarPedidoRequest request,
            IMediator mediator,
            IClientCatalogRepository clientCatalogRepository,
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

            if (request.IsNovoCliente == true && request.PreCliente != null)
            {
                var searchRequest = new CatalogSearchRequest(tenantContext.TenantId, string.Empty, 100000);
                var existingClients = await clientCatalogRepository.SearchClientsAsync(searchRequest, cancellationToken);

                string Clean(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

                var cleanNome = Clean(request.PreCliente.Nome);
                var cleanDoc = Clean(request.PreCliente.Documento);

                var duplicate = existingClients.Any(c => Clean(c.Nome) == cleanNome || Clean(c.Documento) == cleanDoc);
                if (duplicate)
                {
                    errors["preCliente"] = new[] { "Cliente já cadastrado no catálogo com este Nome ou CPF/CNPJ!" };
                    return Results.ValidationProblem(errors);
                }
            }

            var result = await mediator.Send(new CriarPedidoCommand(
                tenantContext.TenantId,
                request.ClienteId,
                request.Itens,
                request.CondicaoPagamento,
                request.Observacao,
                request.IsNovoCliente,
                request.PreCliente), cancellationToken);

            return Results.Created($"/pedidos/{result.PedidoId}", new CriarPedidoResponse(
                result.PedidoId,
                result.Status,
                result.ItensCount,
                result.ParcelasCount,
                result.TotalBruto,
                result.TotalDesconto,
                result.TotalLiquido));
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

            var result = pedidos.Select(p => new PedidoSummaryResponse(
                p.Id,
                p.TenantId,
                p.ClienteId,
                p.CriadoEm,
                p.Status?.Codigo ?? string.Empty,
                p.Itens.Count,
                p.Parcelas.Count,
                p.TotalBruto,
                p.TotalDesconto,
                p.TotalLiquido,
                NomeCliente: GetNomePreClienteComDocumento(p),
                IsNovoCliente: p.IsNovoCliente));

            return Results.Ok(result);
        })
        .WithName("ListPedidosQuery")
        .WithOpenApi();

        app.MapPost("/pedidos/{id}/reenviar", async (
            ITenantContext tenantContext,
            Guid id,
            ReenviarPedidoRequest? request,
            PedidosDbContext db,
            IIntegrationTransport integrationTransport,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken) =>
        {
            var logger = loggerFactory.CreateLogger("PedidosEndpoints");

            if (!tenantContext.HasTenant || string.IsNullOrWhiteSpace(tenantContext.TenantId))
            {
                return Results.Unauthorized();
            }

            var pedido = await db.Pedidos
                .Where(p => p.TenantId == tenantContext.TenantId && p.Id == id)
                .Include(p => p.Itens)
                .Include(p => p.Parcelas)
                .FirstOrDefaultAsync(cancellationToken);

            if (pedido is null)
            {
                return Results.NotFound();
            }

            if (pedido.StatusId != PedidoStatus.ErroId)
            {
                return Results.BadRequest("Apenas pedidos com erro de integracao no ERP podem ser reenviados.");
            }

            // 1. Limpar detalhes de erro anteriores da observação se existirem
            if (!string.IsNullOrEmpty(pedido.Observacao) && pedido.Observacao.Contains("Rejeitado pelo ERP:"))
            {
                var idx = pedido.Observacao.IndexOf("Rejeitado pelo ERP:");
                if (idx > 0)
                {
                    pedido.Observacao = pedido.Observacao.Substring(0, idx).TrimEnd(' ', '|');
                }
                else
                {
                    pedido.Observacao = null;
                }
            }

            // 2. Mudar status para EnviadoId
            pedido.StatusId = PedidoStatus.EnviadoId;
            pedido.AtualizadoEm = DateTimeOffset.UtcNow;

            // 3. Resolver ID da Condição de Pagamento ERP
            int condicaoPagtoIdERP = 0;
            if (!string.IsNullOrEmpty(request?.CondicaoPagamentoId))
            {
                condicaoPagtoIdERP = ParseErpId(request.CondicaoPagamentoId, "cond-");
            }

            if (condicaoPagtoIdERP == 0)
            {
                if (pedido.Parcelas.Count == 1)
                {
                    var primParcela = pedido.Parcelas.First();
                    var diffDays = (primParcela.DataVencimento - pedido.CriadoEm.Date).TotalDays;
                    condicaoPagtoIdERP = diffDays > 5 ? 3 : 1;
                }
                else if (pedido.Parcelas.Count == 2)
                {
                    condicaoPagtoIdERP = 4;
                }
                else if (pedido.Parcelas.Count > 2)
                {
                    condicaoPagtoIdERP = pedido.Parcelas.Count + 2;
                }
                else
                {
                    condicaoPagtoIdERP = 1;
                }
            }

            // 4. Montar o payload de exportação do pedido
            var payload = new OrderExportPayload
            {
                EventId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow,
                TenantId = pedido.TenantId,
                PedidoId = pedido.Id,
                Payload = new OrderExportData
                {
                    ClienteIdERP = ParseErpId(pedido.ClienteId, "cli-"),
                    IsNovoCliente = pedido.IsNovoCliente,
                    PreCliente = pedido.IsNovoCliente && !string.IsNullOrEmpty(pedido.PreClienteJson)
                        ? JsonSerializer.Deserialize<PreClienteExportDto>(pedido.PreClienteJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
                        : null,
                    CondicaoPagamentoIdERP = condicaoPagtoIdERP,
                    DataEmissao = pedido.CriadoEm.ToString("yyyy-MM-dd"),
                    Observacao = pedido.Observacao,
                    Orcamento = false,
                    Origem = "web",
                    ValorTotal = pedido.TotalBruto,
                    ValorTotalDesconto = pedido.TotalDesconto,
                    ValorTotalAcrescimo = 0.00m,
                    ValorFinal = pedido.TotalLiquido,
                    ValorFrete = 0.00m,
                    Itens = pedido.Itens.Select(item =>
                    {
                        var totalBrutoItem = item.Quantidade * item.PrecoUnitario;
                        var pctDesconto = totalBrutoItem > 0 ? Math.Round((item.Desconto / totalBrutoItem) * 100, 2, MidpointRounding.AwayFromZero) : 0m;
                        return new OrderItemExportDto
                        {
                            ProdutoIdERP = ParseErpId(item.ProdutoId, "prod-"),
                            TabelaPrecoEstoqueIdERP = item.TabelaPrecoEstoqueIdERP != 0 ? item.TabelaPrecoEstoqueIdERP : ParseErpId(item.Sku, "sku-"),
                            SiglaUnidade = "UN",
                            Quantidade = item.Quantidade,
                            PrecoUnitario = item.PrecoUnitario,
                            PercentualDesconto = pctDesconto,
                            ValorDesconto = item.Desconto,
                            PercentualAcrescimo = 0m,
                            ValorAcrescimo = 0m,
                            ValorFinal = item.Total
                        };
                    }).ToList(),
                    Parcelas = pedido.Parcelas.Select(parcela =>
                    {
                        var formaId = ParseErpId(parcela.FormaPagamento, "forma-");
                        if (formaId == 0) formaId = 1;
                        return new OrderParcelaExportDto
                        {
                            Numero = parcela.Numero,
                            FormaCobrancaIdERP = formaId,
                            Valor = parcela.Valor,
                            Vencimento = parcela.DataVencimento.ToString("yyyy-MM-dd")
                        };
                    }).ToList()
                }
            };

            try
            {
                await integrationTransport.PublishOrderAsync(pedido.TenantId, payload, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                
                logger.LogInformation("Pedido {PedidoId} reenviado com sucesso.", pedido.Id);
                return Results.Ok(new { Mensagem = "Pedido reenviado com sucesso para processamento no ERP." });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao publicar reenvio do pedido {PedidoId}.", pedido.Id);
                return Results.StatusCode(500);
            }
        })
        .WithName("ReenviarPedido")
        .WithOpenApi();

        return app;
    }

    private static string? GetNomePreClienteComDocumento(Versatus.ForcaVendas.Domain.Pedidos.Pedido p)
    {
        if (!p.IsNovoCliente || string.IsNullOrEmpty(p.NomePreCliente))
            return null;

        try
        {
            if (!string.IsNullOrEmpty(p.PreClienteJson))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(p.PreClienteJson);
                var root = doc.RootElement;
                string? docStr = null;
                if (root.TryGetProperty("documento", out var prop)) docStr = prop.GetString();
                else if (root.TryGetProperty("Documento", out prop)) docStr = prop.GetString();

                if (!string.IsNullOrEmpty(docStr))
                {
                    return $"[Novo] {p.NomePreCliente} ({docStr})";
                }
            }
        }
        catch
        {
            // ignored
        }

        return $"[Novo] {p.NomePreCliente}";
    }

    private static int ParseErpId(string id, string prefixToRemove)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        var clean = id.Replace(prefixToRemove, string.Empty);
        return int.TryParse(clean, out var val) ? val : 0;
    }
}

public record ReenviarPedidoRequest(string? CondicaoPagamentoId);