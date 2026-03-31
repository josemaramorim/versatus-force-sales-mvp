using Versatus.ForcaVendas.Domain.Pedidos;
using Versatus.ForcaVendas.Domain.Pedidos.Services;

namespace Versatus.ForcaVendas.Infrastructure.Data.Services;

public sealed class MockPaymentConditionService : IPaymentConditionService
{
    public Task<List<PedidoParcela>> CalcularParcelamentoAsync(
        Guid pedidoId, 
        decimal valorTotalLiquido, 
        string condicaoPagamentoId, 
        DateTime primeiroVencimento, 
        CancellationToken cancellationToken = default)
    {
        // No futuro esta logica baterá no ERP via API para pegar se são 3, 5 ou 10 parcelas com regras de 'Acrescimo' baseadas na Entidade CondicaoPagtoMobile legada
        // Hoje, para MVP, nós deduzimos que a string de pagamento enviada contem o numero de parcelas (ex "3")
        int quantidadeParcelas = 1;
        if(int.TryParse(condicaoPagamentoId, out var qtd) && qtd > 0)
        {
            quantidadeParcelas = qtd;
        }

        var parcelas = new List<PedidoParcela>(quantidadeParcelas);
        var valorBase = Math.Round(valorTotalLiquido / quantidadeParcelas, 2, MidpointRounding.AwayFromZero);

        for (var i = 1; i <= quantidadeParcelas; i++)
        {
            var valor = i == quantidadeParcelas
                ? Math.Round(valorTotalLiquido - (valorBase * (quantidadeParcelas - 1)), 2, MidpointRounding.AwayFromZero)
                : valorBase;

            parcelas.Add(new PedidoParcela
            {
                Id = Guid.NewGuid(),
                PedidoId = pedidoId,
                Numero = i,
                DataVencimento = primeiroVencimento.Date.AddMonths(i - 1),
                Valor = valor,
                FormaPagamento = "Emulado (" + condicaoPagamentoId + ")" // Pass-through for default MVP
            });
        }

        return Task.FromResult(parcelas);
    }
}
