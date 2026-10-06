using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace GaiaSkyline.Web.Azure;

/// <summary>
/// Configures ASP.NET Core Data Protection for every environment (Stage 8 Part B, ADR 0022).
///
/// The key ring protects everything that must survive a restart, a slot swap and scale-out across
/// instances: Identity auth cookies, the antiforgery token, guest magic-link + partner-invite +
/// booking tokens, the content-preview and booking-access cookies, and the encrypted site settings
/// (iCal URLs, the pricing API key). By default ASP.NET Core keeps the ring in process memory, which
/// loses all of those on any of those events — unacceptable in production.
///
/// When the Azure bindings are present the ring is persisted to the <c>dataprotection</c> Blob container
/// and wrapped (encrypted at rest) with the Key Vault key, reached through the App Service managed
/// identity. All instances and both deployment slots share one application name, so a key created by one
/// instance is readable by every other — the invariant the cross-instance test proves.
///
/// In Production the bindings are mandatory: a missing one fails startup rather than silently falling back
/// to ephemeral keys. Development and CI keep the in-process ring (single instance; nothing to share).
/// </summary>
public static class DataProtectionSetup
{
    public const string ApplicationName = "GaiaSkyline";
    private const string KeysBlobPath = "dataprotection/keys.xml";

    public static void AddGaiaSkylineDataProtection(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var azure = configuration.GetSection(AzureOptions.SectionName).Get<AzureOptions>() ?? new AzureOptions();
        services.Configure<AzureOptions>(configuration.GetSection(AzureOptions.SectionName));

        // One application name across instances and slots → one shared key ring (never regenerated on swap).
        var builder = services.AddDataProtection().SetApplicationName(ApplicationName);

        if (azure.IsConfigured)
        {
            var credential = new DefaultAzureCredential();
            builder
                .PersistKeysToAzureBlobStorage(new Uri(azure.BlobServiceUri, KeysBlobPath), credential)
                .ProtectKeysWithAzureKeyVault(azure.DataProtectionKeyIdentifier, credential);
            return;
        }

        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Production requires persisted Data Protection keys: set Azure:StorageAccountName, "
                + "Azure:KeyVaultUri and Azure:DataProtectionKeyName. Without them the key ring is in-process "
                + "and every restart, slot swap or scale-out would invalidate auth cookies, magic-link and "
                + "partner-invite tokens, and the encrypted site settings.");
        }

        // Development / CI: the default in-process key ring is fine for a single instance.
    }
}
