using GaiaSkyline.Application.Documents;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>
/// A no-op <see cref="IGuestDocumentService"/> for the email tests, which assert on the message text, not the
/// attached PDF. Returning null means no PDF attachment is added — the PDF rendering itself is covered by
/// <see cref="GuestDocumentTests"/>.
/// </summary>
internal sealed class FakeGuestDocumentService : IGuestDocumentService
{
    public Task<byte[]?> GenerateAsync(string referenceCode, GuestDocumentType type, CancellationToken cancellationToken) =>
        Task.FromResult<byte[]?>(null);

    public Task<byte[]?> GenerateAsync(string referenceCode, GuestDocumentType type, string? language, CancellationToken cancellationToken) =>
        Task.FromResult<byte[]?>(null);
}
