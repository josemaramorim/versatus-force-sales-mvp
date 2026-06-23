using System;
using System.Collections.Generic;

namespace Versatus.ForcaVendas.Infrastructure.Integration.Models;

public sealed class CatalogSnapshot
{
    public bool IsFullSync { get; set; } = true;
    public IReadOnlyList<ClienteCatalogDto> Clientes { get; set; } = Array.Empty<ClienteCatalogDto>();
    public IReadOnlyList<ProdutoCatalogDto> Produtos { get; set; } = Array.Empty<ProdutoCatalogDto>();
    public IReadOnlyList<TabelaPrecoCatalogDto> TabelasPreco { get; set; } = Array.Empty<TabelaPrecoCatalogDto>();
    public IReadOnlyList<CondicaoPagamentoCatalogDto> CondicoesPagamento { get; set; } = Array.Empty<CondicaoPagamentoCatalogDto>();
}

public sealed class CatalogFileWrapper<T>
{
    public bool IsFullSync { get; set; } = true;
    public DateTimeOffset ExportedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<T> Data { get; set; } = new();
}

public sealed class ClienteCatalogDto
{
    public int ClienteIdERP { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public int AreaVendaId { get; set; }
    public int CondicaoPagamentoIdDefault { get; set; }
    public int ComissionadoAreaVendaId { get; set; }
}

public sealed class ProdutoCatalogDto
{
    public int ProdutoIdERP { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public string SiglaUnidadeVenda { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
    public bool ControlaEstoque { get; set; }
    public bool ControlaDescontoMaximo { get; set; }
    public bool AceitaDesconto { get; set; }
    public decimal DescontoMaximoPercentual { get; set; }
    public string? Marca { get; set; }
    public string? Fabricante { get; set; }
}

public sealed class TabelaPrecoCatalogDto
{
    public int TabelaPrecoEstoqueIdERP { get; set; }
    public int ProdutoIdERP { get; set; }
    public int TabelaPrecoIdERP { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal PercentualDescontoMaximo { get; set; }
    public bool ControlaDescontoMaximo { get; set; }
    public string Descricao { get; set; } = string.Empty;
}

public sealed class CondicaoPagamentoCatalogDto
{
    public int CondicaoPagtoIdERP { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public int QuantidadeParcela { get; set; }
    public int DiasParcelamento { get; set; }
    public decimal Acrescimo { get; set; }
    public decimal Desconto { get; set; }
    public int FormaCobrancaIdERP { get; set; }
    public bool UsarMesComercial { get; set; }
}
