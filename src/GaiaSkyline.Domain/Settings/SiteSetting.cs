namespace GaiaSkyline.Domain.Settings;

/// <summary>
/// One owner-editable runtime setting (Stage 7 §12), keyed by a well-known name. Secret values are
/// stored Data-Protection-encrypted and only ever surfaced masked. Rows override the configuration
/// defaults; a missing row means "use appsettings/User Secrets".
/// </summary>
public sealed class SiteSetting
{
    // Required by EF Core's materialization.
    private SiteSetting()
    {
    }

    public SiteSetting(string key, string value, bool isSecret, DateTime updatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key.Trim();
        Value = value;
        IsSecret = isSecret;
        UpdatedAtUtc = updatedAtUtc;
    }

    public string Key { get; private set; } = null!;

    /// <summary>The raw value, or the protected payload when <see cref="IsSecret"/>.</summary>
    public string Value { get; private set; } = null!;

    public bool IsSecret { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Set(string value, bool isSecret, DateTime updatedAtUtc)
    {
        Value = value;
        IsSecret = isSecret;
        UpdatedAtUtc = updatedAtUtc;
    }
}
