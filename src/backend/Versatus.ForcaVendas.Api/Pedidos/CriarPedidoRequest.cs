namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed record CriarPedidoRequest(
    string ClienteId,
    IReadOnlyList<CriarPedidoItemRequest> Itens,
    CriarPedidoCondicaoPagamentoRequest CondicaoPagamento,
    string? Observacao = null,
    bool? IsNovoCliente = null,
    CriarPreClienteRequest? PreCliente = null)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (IsNovoCliente != true && string.IsNullOrWhiteSpace(ClienteId))
        {
            errors["clienteId"] = ["clienteId is required."];
        }

        if (IsNovoCliente == true)
        {
            if (PreCliente is null)
            {
                errors["preCliente"] = ["preCliente is required when isNovoCliente is true."];
            }
            else
            {
                if (string.IsNullOrWhiteSpace(PreCliente.Nome))
                {
                    errors["preCliente.nome"] = ["preCliente.nome is required."];
                }
                if (string.IsNullOrWhiteSpace(PreCliente.Documento))
                {
                    errors["preCliente.documento"] = ["preCliente.documento is required."];
                }
            }
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

                if (item.Desconto < 0)
                {
                    errors[$"itens[{i}].desconto"] = ["desconto must be greater than or equal to zero."];
                }
                else
                {
                    var totalBrutoItem = item.Quantidade * item.PrecoUnitario;
                    if (item.Desconto > totalBrutoItem)
                    {
                        errors[$"itens[{i}].desconto"] = ["desconto cannot exceed bruto total for item."];
                    }
                }
            }
        }

        if (CondicaoPagamento is null)
        {
            errors["condicaoPagamento"] = ["condicaoPagamento is required."];
        }
        else
        {
            if (string.IsNullOrWhiteSpace(CondicaoPagamento.ResolveCondicaoPagamentoId()))
            {
                errors["condicaoPagamento.condicaoPagamentoId"] = ["condicaoPagamentoId is required."];
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

        if (!string.IsNullOrWhiteSpace(Observacao) && Observacao.Length > 1000)
        {
            errors["observacao"] = ["observacao must contain at most 1000 characters."];
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
    decimal Desconto,
    int? TabelaPrecoEstoqueIdERP = null);

public sealed record CriarPedidoCondicaoPagamentoRequest(
    string? CondicaoPagamentoId,
    DateTime PrimeiroVencimento,
    string FormaPagamento,
    string? QuantidadeParcelas = null)
{
    public string ResolveCondicaoPagamentoId()
    {
        if (!string.IsNullOrWhiteSpace(CondicaoPagamentoId))
        {
            return CondicaoPagamentoId;
        }

        return QuantidadeParcelas?.Trim() ?? string.Empty;
    }
}

public sealed record CriarPreClienteRequest(
    string Nome,
    string Documento,
    string? Telefone = null,
    string? Email = null,
    string? Logradouro = null,
    string? Numero = null,
    string? Complemento = null,
    string? Bairro = null,
    string? Cidade = null,
    string? Uf = null,
    string? Cep = null
);
