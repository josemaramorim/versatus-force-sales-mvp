namespace Versatus.ForcaVendas.Infrastructure.Integration.Ftp;

public sealed class FtpTransportOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 21;
    public bool UseSftp { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string BasePath { get; set; } = string.Empty;
    public int CatalogPollIntervalSeconds { get; set; } = 300;
    public int ResultPollIntervalSeconds { get; set; } = 30;
}
