using Versatus.ForcaVendas.Domain.Pedidos;

namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed record PedidoItemDto(
    string ProdutoId,
    string Sku,
    string Nome,
    decimal Quantidade,
    decimal PrecoUnitario,
    decimal Desconto,
    decimal Total);

public sealed record PedidoParcelaDto(
    int Numero,
    DateTime DataVencimento,
    decimal Valor,
    string FormaPagamento);

public sealed record PedidoResponse(
    Guid PedidoId,
    string TenantId,
    string ClienteId,
    DateTimeOffset CriadoEm,
    string Status,
    int ItensCount,
    int ParcelasCount,
    decimal TotalBruto,
    decimal TotalDesconto,
    decimal TotalLiquido,
    IReadOnlyList<PedidoItemDto> Itens,
    IReadOnlyList<PedidoParcelaDto> Parcelas);

public sealed record PedidoSummaryDto(
    Guid PedidoId,
    string ClienteId,
    DateTimeOffset CriadoEm,
    string Status,
    decimal TotalBruto,
    decimal TotalDesconto,
    decimal TotalLiquido);
