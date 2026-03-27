using MediatR;
using Microsoft.EntityFrameworkCore;
using Versatus.ForcaVendas.Domain.Pedidos;
using Versatus.ForcaVendas.Infrastructure.Data;

namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed record CriarPedidoCommand(
    string TenantId,
    string ClienteId,
    IReadOnlyList<CriarPedidoItemRequest> Itens,
    CriarPedidoCondicaoPagamentoRequest CondicaoPagamento) : IRequest<CriarPedidoResult>;

public sealed record CriarPedidoResult(Guid PedidoId, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido);

public sealed class CriarPedidoCommandHandler : IRequestHandler<CriarPedidoCommand, CriarPedidoResult>
{
    private readonly PedidosDbContext _dbContext;
    private readonly IPedidoCache? _cache;

    public CriarPedidoCommandHandler(PedidosDbContext dbContext, IPedidoCache? cache = null)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<CriarPedidoResult> Handle(CriarPedidoCommand request, CancellationToken cancellationToken)
    {
        var pedidoId = Guid.NewGuid();
        var criadoEm = DateTimeOffset.UtcNow;

        var itens = request.Itens.Select(item =>
        {
            var totalItem = Math.Round((item.Quantidade * item.PrecoUnitario) - item.Desconto, 2, MidpointRounding.AwayFromZero);
            return new PedidoItem
            {
                Id = Guid.NewGuid(),
                PedidoId = pedidoId,
                ProdutoId = item.ProdutoId,
                Sku = item.Sku,
                Nome = item.Nome,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                Desconto = item.Desconto,
                Total = totalItem
            };
        }).ToList();

        var totalBruto = itens.Sum(i => Math.Round(i.Quantidade * i.PrecoUnitario, 2, MidpointRounding.AwayFromZero));
        var totalDesconto = itens.Sum(i => Math.Round(i.Desconto, 2, MidpointRounding.AwayFromZero));
        var totalLiquido = Math.Round(totalBruto - totalDesconto, 2, MidpointRounding.AwayFromZero);
        var parcelas = CriarParcelas(
            pedidoId,
            totalLiquido,
            request.CondicaoPagamento.QuantidadeParcelas,
            request.CondicaoPagamento.PrimeiroVencimento,
            request.CondicaoPagamento.FormaPagamento);

        var pedido = new Pedido
        {
            Id = pedidoId,
            TenantId = request.TenantId,
            ClienteId = request.ClienteId,
            CriadoEm = criadoEm,
            StatusId = PedidoStatus.RascunhoId,
            Itens = itens,
            Parcelas = parcelas
        };

        _dbContext.Pedidos.Add(pedido);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // populate cache for test-host fallback
        try
        {
            _cache?.Set(pedido);
        }
        catch
        {
            // ignore cache failures
        }

        var status = await _dbContext.PedidoStatuses
            .Where(s => s.Id == PedidoStatus.RascunhoId)
            .Select(s => s.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        return new CriarPedidoResult(pedido.Id, status ?? "rascunho", itens.Count, parcelas.Count, totalBruto, totalDesconto, totalLiquido);
    }

    private static List<PedidoParcela> CriarParcelas(
        Guid pedidoId,
        decimal valorTotal,
        int quantidadeParcelas,
        DateTime primeiroVencimento,
        string formaPagamento)
    {
        var parcelas = new List<PedidoParcela>(quantidadeParcelas);
        var valorBase = Math.Round(valorTotal / quantidadeParcelas, 2, MidpointRounding.AwayFromZero);

        for (var i = 1; i <= quantidadeParcelas; i++)
        {
            var valor = i == quantidadeParcelas
                ? Math.Round(valorTotal - (valorBase * (quantidadeParcelas - 1)), 2, MidpointRounding.AwayFromZero)
                : valorBase;

            parcelas.Add(new PedidoParcela
            {
                Id = Guid.NewGuid(),
                PedidoId = pedidoId,
                Numero = i,
                DataVencimento = primeiroVencimento.Date.AddMonths(i - 1),
                Valor = valor,
                FormaPagamento = formaPagamento
            });
        }

        return parcelas;
    }
}
