using System;
using System.Collections.Generic;

namespace Versatus.ForcaVendas.Application.Catalogo;

public sealed record ProductSummary(
    string ProductId,
    string Sku,
    string Name,
    string Unit,
    decimal Price,
    decimal AvailableStock,
    IReadOnlyList<PriceTableEntry>? PricesByTable = null);

public sealed record PriceTableEntry(
    int TabelaPrecoIdERP,
    int TabelaPrecoEstoqueIdERP,
    string Descricao,
    decimal ValorUnitario,
    bool IsPromocional,
    DateTime? VigenciaInicio,
    DateTime? VigenciaFim);