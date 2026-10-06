using Azure.Identity;
using Azure.Storage.Blobs;
using GaiaSkyline.Application.Media;
using GaiaSkyline.Application.Storage;
using GaiaSkyline.Web.Storage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaiaSkyline.Web.Azure;

/// <summary>
/// Binds Blob-backed media storage when the Azure bindings are present (deployed production); otherwise the
/// app keeps the local-disk defaults (dev / CI / local-only mode). Stage 8 Part B, ADR 0024. Reached through
/// the App Service managed identity — no storage keys in configuration.
/// </summary>
public static class MediaStorageSetup
{
    public const string MediaContainer = "media";

    public static void AddGaiaSkylineMediaStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var azure = configuration.GetSection(AzureOptions.SectionName).Get<AzureOptions>() ?? new AzureOptions();
        if (!azure.IsConfigured)
        {
            return; // local disk stays (registered by Infrastructure + the Program defaults)
        }

        var blobService = new BlobServiceClient(azure.BlobServiceUri, new DefaultAzureCredential());
        services.AddSingleton(blobService);

        services.RemoveAll<IMediaFileStore>();
        services.AddScoped<IMediaFileStore>(_ =>
            new BlobMediaFileStore(blobService.GetBlobContainerClient(MediaContainer)));

        services.RemoveAll<IMediaStorage>();
        services.AddScoped<IMediaStorage>(_ =>
            new BlobMediaStorage(blobService.GetBlobContainerClient(MediaContainer)));

        services.RemoveAll<IMediaDirectoryProvider>();
        services.AddSingleton<IMediaDirectoryProvider, TempMediaDirectoryProvider>();
    }

    /// <summary>
    /// The one-off media migration (DEPLOYMENT PHASE): uploads an existing local media directory to the Blob
    /// container, preserving filenames. Run via <c>dotnet run -- --migrate-media &lt;localDir&gt;</c>.
    /// </summary>
    public static async Task<bool> TryRunMediaMigrationAsync(string[] args, IConfiguration configuration)
    {
        var index = Array.IndexOf(args, "--migrate-media");
        if (index < 0)
        {
            return false;
        }

        var azure = configuration.GetSection(AzureOptions.SectionName).Get<AzureOptions>() ?? new AzureOptions();
        if (!azure.IsConfigured)
        {
            Console.WriteLine("--migrate-media requires the Azure bindings (Azure:StorageAccountName, Azure:KeyVaultUri).");
            return true;
        }

        var localDir = index + 1 < args.Length ? args[index + 1] : Path.Combine("wwwroot", "media");
        var blobService = new BlobServiceClient(azure.BlobServiceUri, new DefaultAzureCredential());
        var store = new BlobMediaFileStore(blobService.GetBlobContainerClient(MediaContainer));
        Console.WriteLine($"Migrating media from '{localDir}' to the '{MediaContainer}' container...");
        await store.PublishDirectoryAsync(localDir, CancellationToken.None);
        Console.WriteLine("Media migration complete.");
        return true;
    }
}
