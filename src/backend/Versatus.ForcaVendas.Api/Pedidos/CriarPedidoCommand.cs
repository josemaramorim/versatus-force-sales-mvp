using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Versatus.ForcaVendas.Domain.Pedidos;
using Versatus.ForcaVendas.Domain.Pedidos.Services;
using Versatus.ForcaVendas.Infrastructure.Data;
using Versatus.ForcaVendas.Infrastructure.Integration;
using Versatus.ForcaVendas.Infrastructure.Integration.Models;

namespace Versatus.ForcaVendas.Api.Pedidos;

public sealed record CriarPedidoCommand(
    string TenantId,
    string ClienteId,
    IReadOnlyList<CriarPedidoItemRequest> Itens,
    CriarPedidoCondicaoPagamentoRequest CondicaoPagamento,
    string? Observacao = null,
    bool? IsNovoCliente = null,
    CriarPreClienteRequest? PreCliente = null) : IRequest<CriarPedidoResult>;

public sealed record CriarPedidoResult(Guid PedidoId, string Status, int ItensCount, int ParcelasCount, decimal TotalBruto, decimal TotalDesconto, decimal TotalLiquido);

public sealed class CriarPedidoCommandHandler : IRequestHandler<CriarPedidoCommand, CriarPedidoResult>
{
    private readonly PedidosDbContext _dbContext;
    private readonly IPaymentConditionService _paymentService;
    private readonly IIntegrationTransport? _integrationTransport;
    private readonly ILogger<CriarPedidoCommandHandler>? _logger;
    private readonly IPedidoCache? _cache;

    public CriarPedidoCommandHandler(
        PedidosDbContext dbContext,
        IPaymentConditionService paymentService,
        IIntegrationTransport? integrationTransport = null,
        ILogger<CriarPedidoCommandHandler>? logger = null,
        IPedidoCache? cache = null)
    {
        _dbContext = dbContext;
        _paymentService = paymentService;
        _integrationTransport = integrationTransport;
        _logger = logger;
        _cache = cache;
    }

    public async Task<CriarPedidoResult> Handle(CriarPedidoCommand request, CancellationToken cancellationToken)
    {
        var pedidoId = Guid.NewGuid();
        var criadoEm = DateTimeOffset.UtcNow;

        var itens = request.Itens.Select(item =>
        {
            var totalItem = Math.Round((item.Quantidade * item.PrecoUnitario) - item.Desconto, 2, MidpointRounding.AwayFromZero);
            return new PedidoItem
            {
                Id = Guid.NewGuid(),
                PedidoId = pedidoId,
                ProdutoId = item.ProdutoId,
                Sku = item.Sku,
                Nome = item.Nome,
                Quantidade = item.Quantidade,
                PrecoUnitario = item.PrecoUnitario,
                Desconto = item.Desconto,
                Total = totalItem,
                TabelaPrecoEstoqueIdERP = item.TabelaPrecoEstoqueIdERP ?? 0
            };
        }).ToList();

        var totalBruto = itens.Sum(i => Math.Round(i.Quantidade * i.PrecoUnitario, 2, MidpointRounding.AwayFromZero));
        var totalDesconto = itens.Sum(i => Math.Round(i.Desconto, 2, MidpointRounding.AwayFromZero));
        var totalLiquido = Math.Round(totalBruto - totalDesconto, 2, MidpointRounding.AwayFromZero);
        
        // CALCULO DE PARCELAMENTO VIA SERVICE (Extension point)
        var parcelas = await _paymentService.CalcularParcelamentoAsync(
            pedidoId,
            totalLiquido,
            request.CondicaoPagamento.ResolveCondicaoPagamentoId(),
            request.CondicaoPagamento.PrimeiroVencimento,
            cancellationToken);

        var pedido = new Pedido
        {
            Id = pedidoId,
            TenantId = request.TenantId,
            ClienteId = request.ClienteId,
            CriadoEm = criadoEm,
            StatusId = PedidoStatus.RascunhoId,
            TotalBruto = totalBruto,
            TotalDesconto = totalDesconto,
            TotalLiquido = totalLiquido,
            Observacao = request.Observacao,
            IsNovoCliente = request.IsNovoCliente ?? false,
            NomePreCliente = request.PreCliente?.Nome,
            PreClienteJson = request.PreCliente != null ? System.Text.Json.JsonSerializer.Serialize(request.PreCliente) : null,
            Itens = itens,
            Parcelas = parcelas
        };

        _dbContext.Pedidos.Add(pedido);
        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            _cache?.Set(pedido);
        }
        catch
        {
            // ignore cache failures
        }

        if (_integrationTransport is not null)
        {
            try
            {
                var payload = new OrderExportPayload
                {
                    EventId = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                    TenantId = pedido.TenantId,
                    PedidoId = pedido.Id,
                    Payload = new OrderExportData
                    {
                        ClienteIdERP = ParseErpId(pedido.ClienteId, "cli-"),
                        IsNovoCliente = pedido.IsNovoCliente,
                        PreCliente = request.PreCliente != null ? new PreClienteExportDto
                        {
                            Nome = request.PreCliente.Nome,
                            Documento = request.PreCliente.Documento,
                            Telefone = request.PreCliente.Telefone,
                            Email = request.PreCliente.Email,
                            Logradouro = request.PreCliente.Logradouro,
                            Numero = request.PreCliente.Numero,
                            Complemento = request.PreCliente.Complemento,
                            Bairro = request.PreCliente.Bairro,
                            Cidade = request.PreCliente.Cidade,
                            Uf = request.PreCliente.Uf,
                            Cep = request.PreCliente.Cep
                        } : null,
                        CondicaoPagamentoIdERP = ParseErpId(request.CondicaoPagamento.ResolveCondicaoPagamentoId(), "cond-"),
                        DataEmissao = pedido.CriadoEm.ToString("yyyy-MM-dd"),
                        Observacao = pedido.Observacao,
                        Orcamento = false,
                        Origem = "web",
                        ValorTotal = pedido.TotalBruto,
                        ValorTotalDesconto = pedido.TotalDesconto,
                        ValorTotalAcrescimo = 0.00m,
                        ValorFinal = pedido.TotalLiquido,
                        ValorFrete = 0.00m,
                        Itens = pedido.Itens.Select(item =>
                        {
                            var totalBrutoItem = item.Quantidade * item.PrecoUnitario;
                            var pctDesconto = totalBrutoItem > 0 ? Math.Round((item.Desconto / totalBrutoItem) * 100, 2, MidpointRounding.AwayFromZero) : 0m;
                            return new OrderItemExportDto
                            {
                                ProdutoIdERP = ParseErpId(item.ProdutoId, "prod-"),
                                TabelaPrecoEstoqueIdERP = item.TabelaPrecoEstoqueIdERP != 0 ? item.TabelaPrecoEstoqueIdERP : ParseErpId(item.Sku, "sku-"),
                                SiglaUnidade = "UN",
                                Quantidade = item.Quantidade,
                                PrecoUnitario = item.PrecoUnitario,
                                PercentualDesconto = pctDesconto,
                                ValorDesconto = item.Desconto,
                                PercentualAcrescimo = 0m,
                                ValorAcrescimo = 0m,
                                ValorFinal = item.Total
                            };
                        }).ToList(),
                        Parcelas = pedido.Parcelas.Select(parcela =>
                        {
                            var formaId = ParseErpId(parcela.FormaPagamento, "forma-");
                            if (formaId == 0) formaId = 1; // Fallback para boleto/dinheiro
                            return new OrderParcelaExportDto
                            {
                                Numero = parcela.Numero,
                                FormaCobrancaIdERP = formaId,
                                Valor = parcela.Valor,
                                Vencimento = parcela.DataVencimento.ToString("yyyy-MM-dd")
                            };
                        }).ToList()
                    }
                };

                await _integrationTransport.PublishOrderAsync(pedido.TenantId, payload, cancellationToken);

                pedido.StatusId = PedidoStatus.EnviadoId;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Falha ao publicar pedido {PedidoId} para o transporte de integração. Mantendo em status rascunho.", pedido.Id);
            }
        }

        var status = await _dbContext.PedidoStatuses
            .Where(s => s.Id == pedido.StatusId)
            .Select(s => s.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        return new CriarPedidoResult(pedido.Id, status ?? "rascunho", itens.Count, parcelas.Count, totalBruto, totalDesconto, totalLiquido);
    }

    private static int ParseErpId(string id, string prefixToRemove)
    {
        var cleanId = id;
        if (!string.IsNullOrEmpty(prefixToRemove) && id.StartsWith(prefixToRemove, StringComparison.OrdinalIgnoreCase))
        {
            cleanId = id.Substring(prefixToRemove.Length);
        }
        return int.TryParse(cleanId, out var result) ? result : 0;
    }
}
