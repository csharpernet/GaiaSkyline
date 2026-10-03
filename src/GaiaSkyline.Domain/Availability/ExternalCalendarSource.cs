using GaiaSkyline.Domain.Common;
using GaiaSkyline.Domain.Identifiers;

namespace GaiaSkyline.Domain.Availability;

/// <summary>
/// A configured external calendar (e.g. "Hostify") the import job polls over iCal. The URL is a
/// credential: it is stored data-protection-encrypted (<see cref="IcsUrlProtected"/>), never logged,
/// and masked in any API output. Sync state (hash/last success/error/failure streak) lives here so the
/// job can short-circuit unchanged feeds and the health check can report status.
/// </summary>
public sealed class ExternalCalendarSource : Entity<ExternalCalendarSourceId>
{
    // Required by EF Core's materialization.
    private ExternalCalendarSource()
    {
    }

    public ExternalCalendarSource(
        ExternalCalendarSourceId id, string name, string icsUrlProtected, bool isEnabled, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(icsUrlProtected);

        Id = id;
        Name = name.Trim();
        IcsUrlProtected = icsUrlProtected;
        IsEnabled = isEnabled;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = null!;

    /// <summary>Data-protection ciphertext of the iCal URL. Never log or expose this.</summary>
    public string IcsUrlProtected { get; private set; } = null!;

    public bool IsEnabled { get; private set; }

    /// <summary>SHA-256 of the last fetched feed body; lets the job skip unchanged feeds.</summary>
    public string? LastHash { get; private set; }

    public DateTime? LastSuccessUtc { get; private set; }

    public string? LastError { get; private set; }

    public int ConsecutiveFailures { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public void RecordSuccess(string hash, DateTime atUtc)
    {
        LastHash = hash;
        LastSuccessUtc = atUtc;
        LastError = null;
        ConsecutiveFailures = 0;
    }

    public void RecordUnchanged(DateTime atUtc)
    {
        LastSuccessUtc = atUtc;
        LastError = null;
        ConsecutiveFailures = 0;
    }

    public void RecordFailure(string error, DateTime atUtc)
    {
        LastError = string.IsNullOrWhiteSpace(error) ? "Unknown error" : error.Trim();
        ConsecutiveFailures++;
    }

    public void UpdateUrl(string icsUrlProtected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icsUrlProtected);
        IcsUrlProtected = icsUrlProtected;
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
}
