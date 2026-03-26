namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed record CriarPedidoRequest(
    string ClienteId,
    IReadOnlyList<CriarPedidoItemRequest> Itens,
    CriarPedidoCondicaoPagamentoRequest CondicaoPagamento)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(ClienteId))
        {
            errors["clienteId"] = ["clienteId is required."];
        }

        if (Itens is null || Itens.Count == 0)
        {
            errors["itens"] = ["at least one item is required."];
        }
        else
        {
            for (var i = 0; i < Itens.Count; i++)
            {
                var item = Itens[i];
                if (string.IsNullOrWhiteSpace(item.ProdutoId))
                {
                    errors[$"itens[{i}].produtoId"] = ["produtoId is required."];
                }

                if (string.IsNullOrWhiteSpace(item.Sku))
                {
                    errors[$"itens[{i}].sku"] = ["sku is required."];
                }

                if (string.IsNullOrWhiteSpace(item.Nome))
                {
                    errors[$"itens[{i}].nome"] = ["nome is required."];
                }

                if (item.Quantidade <= 0)
                {
                    errors[$"itens[{i}].quantidade"] = ["quantidade must be greater than zero."];
                }

                if (item.PrecoUnitario <= 0)
                {
                    errors[$"itens[{i}].precoUnitario"] = ["precoUnitario must be greater than zero."];
                }
            }
        }

        if (CondicaoPagamento is null)
        {
            errors["condicaoPagamento"] = ["condicaoPagamento is required."];
        }
        else
        {
            if (CondicaoPagamento.QuantidadeParcelas <= 0)
            {
                errors["condicaoPagamento.quantidadeParcelas"] = ["quantidadeParcelas must be greater than zero."];
            }

            if (CondicaoPagamento.PrimeiroVencimento == default)
            {
                errors["condicaoPagamento.primeiroVencimento"] = ["primeiroVencimento is required."];
            }

            if (string.IsNullOrWhiteSpace(CondicaoPagamento.FormaPagamento))
            {
                errors["condicaoPagamento.formaPagamento"] = ["formaPagamento is required."];
            }
        }

        return errors;
    }
}

public sealed record CriarPedidoItemRequest(
    string ProdutoId,
    string Sku,
    string Nome,
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal Desconto);

public sealed record CriarPedidoCondicaoPagamentoRequest(
    int QuantidadeParcelas,
    DateTime PrimeiroVencimento,
    string FormaPagamento);
