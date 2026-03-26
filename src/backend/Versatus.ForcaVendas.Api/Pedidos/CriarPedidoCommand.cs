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

public sealed record CriarPedidoResult(Guid PedidoId, string Status, int ItensCount, int ParcelasCount);

public sealed class CriarPedidoCommandHandler : IRequestHandler<CriarPedidoCommand, CriarPedidoResult>
{
    private readonly PedidosDbContext _dbContext;

    public CriarPedidoCommandHandler(PedidosDbContext dbContext)
    {
        _dbContext = dbContext;
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

        var totalPedido = itens.Sum(i => i.Total);
        var parcelas = CriarParcelas(
            pedidoId,
            totalPedido,
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

        var status = await _dbContext.PedidoStatuses
            .Where(s => s.Id == PedidoStatus.RascunhoId)
            .Select(s => s.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        return new CriarPedidoResult(pedido.Id, status ?? "rascunho", itens.Count, parcelas.Count);
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
