namespace GaiaSkyline.Domain.Common;

/// <summary>
/// Base class for aggregate roots / entities identified by a strongly-typed id.
/// Records domain events raised during a unit of work; the persistence layer is
/// responsible for dispatching and clearing them.
/// </summary>
/// <typeparam name="TId">The strongly-typed identifier (a value type).</typeparam>
public abstract class Entity<TId>
    where TId : struct
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>The entity identity.</summary>
    public TId Id { get; protected set; }

    /// <summary>Domain events raised on this entity since it was loaded/created.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Raise a domain event to be dispatched after persistence.</summary>
    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Clear the recorded domain events (called once they have been dispatched).</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
