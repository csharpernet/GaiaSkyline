namespace GaiaSkyline.Web.Azure;

/// <summary>
/// Azure resource bindings supplied by the deployed App Service (Stage 8 Part B). When these are present
/// the app persists Data Protection keys to Blob (wrapped by the Key Vault key) and stores media in Blob;
/// when absent — local dev and CI — the app falls back to in-process keys and local-disk media. In
/// Production all three are mandatory and the app fails fast at startup if any is missing.
/// </summary>
public sealed class AzureOptions
{
    public const string SectionName = "Azure";

    /// <summary>The storage account name (e.g. <c>stgaiaskylineprod</c>); empty in dev/CI.</summary>
    public string StorageAccountName { get; set; } = string.Empty;

    /// <summary>The Key Vault base URI (e.g. <c>https://kv-gaiaskyline-prod.vault.azure.net/</c>); empty in dev/CI.</summary>
    public string KeyVaultUri { get; set; } = string.Empty;

    /// <summary>The Key Vault key that wraps the Data Protection ring at rest.</summary>
    public string DataProtectionKeyName { get; set; } = "dataprotection";

    /// <summary>True when the Blob/Key Vault bindings are configured (i.e. a deployed environment).</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(StorageAccountName)
        && !string.IsNullOrWhiteSpace(KeyVaultUri)
        && !string.IsNullOrWhiteSpace(DataProtectionKeyName);

    public Uri BlobServiceUri => new($"https://{StorageAccountName}.blob.core.windows.net");

    /// <summary>The versionless key identifier the Data Protection ring is wrapped with.</summary>
    public Uri DataProtectionKeyIdentifier => new(new Uri(KeyVaultUri), $"keys/{DataProtectionKeyName}");
}
