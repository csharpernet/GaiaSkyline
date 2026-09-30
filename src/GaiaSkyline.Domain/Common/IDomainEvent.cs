namespace GaiaSkyline.Domain.Common;

/// <summary>
/// Marker for something that has happened in the domain and may be dispatched
/// (e.g. to integration handlers) after the owning aggregate is persisted.
/// </summary>
public interface IDomainEvent
{
    /// <summary>The instant, in UTC, at which the event occurred.</summary>
    DateTimeOffset OccurredOnUtc { get; }
}
