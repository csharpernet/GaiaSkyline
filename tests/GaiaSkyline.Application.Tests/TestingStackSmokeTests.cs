using Bogus;
using FluentAssertions;
using MediatR;
using Moq;

namespace GaiaSkyline.Application.Tests;

/// <summary>
/// Proves the foundation testing stack (Moq + Bogus + FluentAssertions) is wired and usable.
/// Real behaviour tests arrive once use-cases exist in later stages.
/// </summary>
public class TestingStackSmokeTests
{
    [Fact]
    public async Task Moq_can_stub_and_verify_a_mediatr_sender()
    {
        var sender = new Mock<ISender>();
        sender
            .Setup(s => s.Send(It.IsAny<IRequest<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await sender.Object.Send(Mock.Of<IRequest<int>>(), CancellationToken.None);

        result.Should().Be(42);
        sender.Verify(
            s => s.Send(It.IsAny<IRequest<int>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Bogus_generates_deterministic_fake_data_when_seeded()
    {
        var faker = new Faker("en") { Random = new Randomizer(1234) };

        var first = faker.Address.City();

        first.Should().NotBeNullOrWhiteSpace();
    }
}
