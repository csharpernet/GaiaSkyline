using FluentAssertions;
using GaiaSkyline.Application.Security;
using GaiaSkyline.Infrastructure.Identity;

namespace GaiaSkyline.Infrastructure.Tests;

public sealed class PwnedPasswordValidatorTests
{
    [Fact]
    public async Task Breached_password_is_rejected()
    {
        var validator = new PwnedPasswordValidator(new StubPwnedPasswords(pwned: true));

        var result = await validator.ValidateAsync(null!, new ApplicationUser(), "hunter2000000");

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Code == "PwnedPassword");
    }

    [Fact]
    public async Task Clean_password_is_accepted()
    {
        var validator = new PwnedPasswordValidator(new StubPwnedPasswords(pwned: false));

        var result = await validator.ValidateAsync(null!, new ApplicationUser(), "a-long-unique-passphrase");

        result.Succeeded.Should().BeTrue();
    }

    private sealed class StubPwnedPasswords(bool pwned) : IPwnedPasswordsClient
    {
        public Task<bool> IsPwnedAsync(string password, CancellationToken cancellationToken) => Task.FromResult(pwned);
    }
}
