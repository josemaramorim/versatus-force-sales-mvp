namespace Versatus.ForcaVendas.Infrastructure.Data;

/// <summary>
/// Trilha de idempotência para eventos de integração de pedidos com ERP.
/// Chave de deduplicação: (TenantId, PedidoId, SourceEventId).
/// </summary>
public sealed class EventoIntegracaoPedidoEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid PedidoId { get; set; }
    /// <summary>ID do evento de origem (producer-side) usado como chave de idempotência.</summary>
    public string SourceEventId { get; set; } = string.Empty;
    /// <summary>Tipo do evento: "despacho" | "retorno_sucesso" | "retorno_erro".</summary>
    public string Tipo { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? ProcessadoEm { get; set; }
    public bool? Sucesso { get; set; }
}
