# 0002 — Strongly-typed identifier approach

- Status: Accepted
- Date: 2026-09-29
- Deciders: GaiaSkyline maintainers

## Context

Primitive `Guid` ids are easy to mix up (passing a `PropertyId` where a `BookingId` is expected
compiles fine but is a bug). We want compile-time safety for identities. The stack note allowed a
source generator or a simple `readonly record struct`.

## Decision

Use hand-written **`readonly record struct` wrappers over `Guid`**, one per identity
(`PropertyId`, `BookingId`, `PartnerId`, …), each with `New()`, `From(Guid)` and a `ToString()`
that returns the underlying guid.

```csharp
public readonly record struct PropertyId(Guid Value)
{
    public static PropertyId New() => new(Guid.NewGuid());
    public static PropertyId From(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

EF Core maps each id with a value converter (`id => id.Value`, `value => PropertyId.From(value)`)
so the database column is a plain `uniqueidentifier`.

We rejected a source generator (e.g. StronglyTypedId / Vogen) for now: it adds a build-time
dependency and generated-code opacity for what is currently three tiny types. The record-struct
pattern is transparent, allocation-free (value type), and gives value equality for free.

## Consequences

- **+** Zero dependencies; trivial to read and debug; value semantics and equality out of the box.
- **+** Uniform `New()`/`From()` vocabulary across the domain.
- **−** A little boilerplate per id and a per-id EF converter. If the number of ids grows large we
  will revisit adopting a source generator (this ADR would be superseded).
