using System;

namespace Versatus.ForcaVendas.Infrastructure.Integration.Models;

public sealed class OrderResultPayload
{
    public string EventType { get; set; } = "pedido.resultado";
    public string EventVersion { get; set; } = "v1";
    public Guid EventId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid PedidoId { get; set; }
    public OrderResultData Payload { get; set; } = new();

    // Propriedade auxiliar para rastreamento (ex: nome do arquivo no FTP/Drive)
    public string? ResultFileId { get; set; }
}

public sealed class OrderResultData
{
    public string Resultado { get; set; } = string.Empty; // "processado" | "erro"
    public string? DocumentoVendaId { get; set; }
    public string? MotivoRejeicao { get; set; }
    public int? ClienteIdERP { get; set; }
    public Guid SourceEventId { get; set; }
}
