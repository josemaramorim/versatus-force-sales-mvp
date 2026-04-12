namespace Versatus.ForcaVendas.Application.Catalogo;

public sealed record ClientSummary(
    string ClientId,
    string Nome,
    string Documento,
    string? AreaVenda);
