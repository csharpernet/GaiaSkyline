using System.Collections.Concurrent;

namespace GaiaSkyline.Web.Testing;

/// <summary>One outbound email captured by the E2E seam.</summary>
public sealed record CapturedEmail(string To, string Subject, string Html, DateTimeOffset ReceivedAt);

/// <summary>
/// In-memory ring buffer of captured outbound emails (E2E only). Always registered so the test
/// controller resolves, but only fed when the capturing sender replaces the real one. Read newest-first.
/// </summary>
public sealed class E2EEmailSink
{
    private const int Capacity = 50;
    private readonly ConcurrentQueue<CapturedEmail> _emails = new();

    public void Add(CapturedEmail email)
    {
        _emails.Enqueue(email);
        while (_emails.Count > Capacity && _emails.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<CapturedEmail> Snapshot() => _emails.Reverse().ToList();
}
