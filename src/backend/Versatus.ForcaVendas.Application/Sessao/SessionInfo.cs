namespace Versatus.ForcaVendas.Application.Sessao;

public sealed record SessionInfo(
    string SessionId,
    string UserId,
    string TenantId,
    DateTimeOffset LoginAt,
    DateTimeOffset LastHeartbeatAt);
