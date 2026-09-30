using FluentAssertions;
using GaiaSkyline.Application;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Application.Tests;

public class ApplicationServiceRegistrationTests
{
    [Fact]
    public void AddApplication_registers_mediatr_pipeline_services()
    {
        var provider = new ServiceCollection()
            .AddApplication()
            .BuildServiceProvider();

        provider.GetService<IMediator>().Should().NotBeNull();
        provider.GetService<ISender>().Should().NotBeNull();
        provider.GetService<IPublisher>().Should().NotBeNull();
    }

    [Fact]
    public void AddApplication_is_idempotent_and_does_not_throw_without_handlers()
    {
        var act = () => new ServiceCollection().AddApplication().BuildServiceProvider();

        act.Should().NotThrow();
    }
}
