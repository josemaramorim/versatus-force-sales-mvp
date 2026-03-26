namespace Versatus.ForcaVendas.Application.Catalogo;

public sealed record ProductSummary(
    string ProductId,
    string Sku,
    string Name,
    string Unit,
    decimal Price,
    decimal AvailableStock);