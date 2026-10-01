using FluentValidation;
using GaiaSkyline.Application.Content;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Application;

/// <summary>
/// Composition-root entry point for the Application layer. Registers MediatR (use-case
/// dispatch) and FluentValidation validators. No use-cases exist yet — this wires the
/// plumbing so later stages just drop handlers/validators into this assembly.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var applicationAssembly = typeof(IApplicationMarker).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        // Content read pipeline. IContentReadStore (EF) is provided by AddInfrastructure.
        services.AddMemoryCache();
        services.AddSingleton<IContentRevision, ContentRevision>();
        services.AddScoped<IContentService, ContentService>();

        return services;
    }
}
