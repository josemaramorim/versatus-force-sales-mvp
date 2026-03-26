using FluentValidation;

namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed class CriarPedidoRequestValidator : AbstractValidator<CriarPedidoRequest>
{
    public CriarPedidoRequestValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty().WithMessage("clienteId is required");
        RuleFor(x => x.Itens).NotNull().Must(x => x != null && x.Count > 0).WithMessage("at least one item is required");
        RuleForEach(x => x.Itens).SetValidator(new CriarPedidoItemRequestValidator());
        RuleFor(x => x.CondicaoPagamento).NotNull().WithMessage("condicaoPagamento is required");
    }
}

public sealed class CriarPedidoItemRequestValidator : AbstractValidator<CriarPedidoItemRequest>
{
    public CriarPedidoItemRequestValidator()
    {
        RuleFor(x => x.ProdutoId).NotEmpty().WithMessage("produtoId is required");
        RuleFor(x => x.Sku).NotEmpty().WithMessage("sku is required");
        RuleFor(x => x.Nome).NotEmpty().WithMessage("nome is required");
        RuleFor(x => x.Quantidade).GreaterThan(0).WithMessage("quantidade must be greater than zero");
        RuleFor(x => x.PrecoUnitario).GreaterThan(0).WithMessage("precoUnitario must be greater than zero");
        RuleFor(x => x.Desconto).GreaterThanOrEqualTo(0).WithMessage("desconto must be >= 0");
        RuleFor(x => x).Custom((item, ctx) =>
        {
            var bruto = item.Quantidade * item.PrecoUnitario;
            if (item.Desconto > bruto)
            {
                ctx.AddFailure("desconto", "desconto cannot exceed bruto total for item");
            }
        });
    }
}

public sealed class CriarPedidoCondicaoPagamentoRequestValidator : AbstractValidator<CriarPedidoCondicaoPagamentoRequest>
{
    public CriarPedidoCondicaoPagamentoRequestValidator()
    {
        RuleFor(x => x.QuantidadeParcelas).GreaterThan(0).WithMessage("quantidadeParcelas must be greater than zero");
        RuleFor(x => x.PrimeiroVencimento).NotEmpty().WithMessage("primeiroVencimento is required");
        RuleFor(x => x.FormaPagamento).NotEmpty().WithMessage("formaPagamento is required");
    }
}
