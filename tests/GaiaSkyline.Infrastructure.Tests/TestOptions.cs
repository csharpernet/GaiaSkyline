using Microsoft.Extensions.Options;

namespace GaiaSkyline.Infrastructure.Tests;

/// <summary>Wraps a fixed value as IOptionsSnapshot for services that read live (§12) settings.</summary>
public static class TestOptions
{
    public static IOptionsSnapshot<T> Snapshot<T>(T value)
        where T : class, new() => new FixedSnapshot<T>(value);

    private sealed class FixedSnapshot<T>(T value) : IOptionsSnapshot<T>
        where T : class, new()
    {
        public T Value => value;

        public T Get(string? name) => value;
    }
}
