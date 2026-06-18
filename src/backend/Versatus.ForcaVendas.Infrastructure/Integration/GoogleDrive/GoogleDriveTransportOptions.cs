namespace Versatus.ForcaVendas.Infrastructure.Integration.GoogleDrive;

public sealed class GoogleDriveTransportOptions
{
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public string RootFolderId { get; set; } = string.Empty;
    public int CatalogPollIntervalSeconds { get; set; } = 300;
    public int ResultPollIntervalSeconds { get; set; } = 60;
}
