using FluentAssertions;
using GaiaSkyline.Web.Azure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Web.Tests;

/// <summary>
/// Data Protection key-ring sharing (Stage 8 Part B, ADR 0022). In production the ring lives in Blob and
/// is wrapped by Key Vault; the invariant that matters is that a token encrypted by one instance decrypts
/// on another — otherwise a restart, slot swap or scale-out would invalidate auth cookies, magic-link and
/// partner-invite tokens, and the encrypted site settings. These tests prove that invariant against a
/// shared persisted store (the same sharing semantics the Blob store provides, without needing Azure):
/// two independent providers that share the application name and the key location cross-decrypt; ones that
/// do not, cannot.
/// </summary>
public sealed class DataProtectionSharingTests : IDisposable
{
    private readonly string _keyRingDir =
        Path.Combine(Path.GetTempPath(), "gaia-dp-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void A_token_protected_by_one_instance_is_unprotected_by_another()
    {
        const string purpose = "GaiaSkyline.PartnerInvite.v1";
        var original = $"invite-{Guid.NewGuid()}";

        // Instance A protects; a completely separate provider (Instance B) — same application name, same
        // key store — unprotects it, exactly as a second App Service instance or the swapped slot would.
        var protectedPayload = Instance().CreateProtector(purpose).Protect(original);
        var roundTripped = Instance().CreateProtector(purpose).Unprotect(protectedPayload);

        roundTripped.Should().Be(original);
    }

    [Fact]
    public void A_provider_without_the_shared_ring_cannot_decrypt()
    {
        var protectedPayload = Instance().CreateProtector("p").Protect("secret");

        // A different application name + an isolated key store (an unconfigured instance) must fail to read
        // it — this is what makes the shared, persisted ring necessary rather than incidental.
        var isolatedDir = Path.Combine(Path.GetTempPath(), "gaia-dp-iso-" + Guid.NewGuid().ToString("N"));
        try
        {
            var isolated = new ServiceCollection()
                .AddDataProtection()
                .SetApplicationName("SomethingElse")
                .PersistKeysToFileSystem(new DirectoryInfo(isolatedDir))
                .Services.BuildServiceProvider()
                .GetRequiredService<IDataProtectionProvider>();

            var act = () => isolated.CreateProtector("p").Unprotect(protectedPayload);
            act.Should().Throw<Exception>("keys are not shared with an isolated, differently-named instance");
        }
        finally
        {
            if (Directory.Exists(isolatedDir))
            {
                Directory.Delete(isolatedDir, recursive: true);
            }
        }
    }

    // A fresh provider over the shared ring — stands in for a separate instance / slot.
    private IDataProtectionProvider Instance() =>
        new ServiceCollection()
            .AddDataProtection()
            .SetApplicationName(DataProtectionSetup.ApplicationName)
            .PersistKeysToFileSystem(new DirectoryInfo(_keyRingDir))
            .Services.BuildServiceProvider()
            .GetRequiredService<IDataProtectionProvider>();

    public void Dispose()
    {
        if (Directory.Exists(_keyRingDir))
        {
            Directory.Delete(_keyRingDir, recursive: true);
        }
    }
}
