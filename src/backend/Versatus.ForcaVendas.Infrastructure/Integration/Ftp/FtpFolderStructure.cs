using System.IO;

namespace Versatus.ForcaVendas.Infrastructure.Integration.Ftp;

public static class FtpFolderStructure
{
    private static string CombineFtp(string basePart, params string[] additionalParts)
    {
        var path = basePart.Replace("\\", "/").TrimEnd('/');
        foreach (var part in additionalParts)
        {
            var cleanPart = part.Replace("\\", "/").Trim('/');
            if (!string.IsNullOrEmpty(cleanPart))
            {
                path = $"{path}/{cleanPart}";
            }
        }
        return path.StartsWith("/") ? path : $"/{path}";
    }

    public static string GetCatalogDirectory(string basePath, string tenantId)
        => CombineFtp(basePath, tenantId, "catalogo");

    public static string GetCatalogFilePath(string basePath, string tenantId, string fileName)
        => CombineFtp(basePath, tenantId, "catalogo", fileName);

    public static string GetOrdersDirectory(string basePath, string tenantId, string subFolder)
        => CombineFtp(basePath, tenantId, "pedidos", subFolder);

    public static string GetOrdersFilePath(string basePath, string tenantId, string subFolder, string fileName)
        => CombineFtp(basePath, tenantId, "pedidos", subFolder, fileName);

    public static string GetResultsDirectory(string basePath, string tenantId, string subFolder)
        => CombineFtp(basePath, tenantId, "resultados", subFolder);

    public static string GetResultsFilePath(string basePath, string tenantId, string subFolder, string fileName)
        => CombineFtp(basePath, tenantId, "resultados", subFolder, fileName);
}
