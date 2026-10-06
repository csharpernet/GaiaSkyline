using GaiaSkyline.Application.Storage;

namespace GaiaSkyline.Infrastructure.Media;

/// <summary>
/// Local-disk <see cref="IMediaFileStore"/> (dev + CI + local-only mode). The rendition pipeline writes
/// directly into the working directory — which is <c>wwwroot/media</c>, the directory static files serve —
/// so publishing is a no-op and renaming just moves the files in place. Byte-identical to the pre-Stage-8
/// behaviour; Azure Blob replaces it only in a deployed environment (ADR 0024). Operates purely on the
/// per-call working directory, so it needs no web-host dependency.
/// </summary>
internal sealed class LocalDiskMediaFileStore : IMediaFileStore
{
    private static readonly int[] Widths = [400, 800, 1600];
    private static readonly string[] Extensions = ["jpg", "jpeg", "webp", "avif", "png", "mp4", "webm"];

    public bool ServesMedia => false;

    // The files are already written to their served location; nothing to publish.
    public Task PublishStemAsync(string workingDirectory, string stem, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task MoveStemAsync(string workingDirectory, string oldStem, string newStem, CancellationToken cancellationToken)
    {
        // Move the raster set {stem}-{w}.{ext}; skip any missing so a partially generated or hand-seeded
        // asset still renames cleanly (matches the prior in-service MoveRenditions behaviour).
        foreach (var width in Widths)
        {
            foreach (var ext in Extensions)
            {
                var from = Path.Combine(workingDirectory, $"{oldStem}-{width}.{ext}");
                if (!File.Exists(from))
                {
                    continue;
                }

                var to = Path.Combine(workingDirectory, $"{newStem}-{width}.{ext}");
                if (File.Exists(to))
                {
                    File.Delete(to);
                }

                File.Move(from, to);
            }
        }

        return Task.CompletedTask;
    }

    // Static files serve media locally; the app never streams it, so this is unused.
    public Task<MediaFileContent?> OpenAsync(string fileName, CancellationToken cancellationToken) =>
        Task.FromResult<MediaFileContent?>(null);
}
