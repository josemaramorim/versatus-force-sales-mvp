using System;
using System.Collections.Generic;

namespace Versatus.ForcaVendas.Infrastructure.Integration.Models;

public sealed class OrderExportPayload
{
    public string EventType { get; set; } = "pedido.enviado";
    public string EventVersion { get; set; } = "v1";
    public Guid EventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid PedidoId { get; set; }
    public OrderExportData Payload { get; set; } = new();
}

public sealed class OrderExportData
{
    public int ClienteIdERP { get; set; }
    public bool IsNovoCliente { get; set; }
    public PreClienteExportDto? PreCliente { get; set; }
    public int CondicaoPagamentoIdERP { get; set; }
    public string DataEmissao { get; set; } = string.Empty; // yyyy-MM-dd
    public string? Observacao { get; set; }
    public bool Orcamento { get; set; }
    public string Origem { get; set; } = "web";
    public decimal ValorTotal { get; set; }
    public decimal ValorTotalDesconto { get; set; }
    public decimal ValorTotalAcrescimo { get; set; }
    public decimal ValorFinal { get; set; }
    public decimal ValorFrete { get; set; }
    public List<OrderItemExportDto> Itens { get; set; } = new();
    public List<OrderParcelaExportDto> Parcelas { get; set; } = new();
}

public sealed class OrderItemExportDto
{
    public int ProdutoIdERP { get; set; }
    public int TabelaPrecoEstoqueIdERP { get; set; }
    public string SiglaUnidade { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal PercentualDesconto { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal PercentualAcrescimo { get; set; }
    public decimal ValorAcrescimo { get; set; }
    public decimal ValorFinal { get; set; }
}

public sealed class OrderParcelaExportDto
{
    public int Numero { get; set; }
    public int FormaCobrancaIdERP { get; set; }
    public decimal Valor { get; set; }
    public string Vencimento { get; set; } = string.Empty; // yyyy-MM-dd
}

public sealed class PreClienteExportDto
{
    public string Nome { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }
    public string? Cep { get; set; }
}
