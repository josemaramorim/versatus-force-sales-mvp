namespace Versatus.ForcaVendas.Application.Sessao;

/// <summary>
/// Configuration for the Redis session store.
/// Populated in Program.cs from Auth:Jwt:SessionTimeoutMinutes.
/// </summary>
public sealed class SessionStoreOptions
{
    /// <summary>Inactivity window in minutes. A heartbeat must arrive within this period or the seat is released.</summary>
    public int TimeoutMinutes { get; set; } = 20;
}
