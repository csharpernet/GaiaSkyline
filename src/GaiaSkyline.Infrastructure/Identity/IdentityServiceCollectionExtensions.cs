using GaiaSkyline.Application.Auditing;
using GaiaSkyline.Application.Notifications;
using GaiaSkyline.Application.Security;
using GaiaSkyline.Infrastructure.Auditing;
using GaiaSkyline.Infrastructure.Notifications;
using GaiaSkyline.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GaiaSkyline.Infrastructure.Identity;

/// <summary>
/// Registers the Identity-adjacent services that live in Infrastructure: the HIBP breach-check client,
/// the breach password validator, the audit log and the Owner seeder. The Web host owns the Identity
/// core registration (<c>AddIdentity().AddEntityFrameworkStores&lt;AppDbContext&gt;()</c>), the cookie
/// options and the authorization policies, since those depend on the ASP.NET Core framework.
///
/// Call this <b>after</b> <c>AddIdentity</c> so the breach validator is appended to — not substituted
/// for — Identity's built-in length validator.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddGaiaIdentityStores(this IServiceCollection services)
    {
        services.AddHttpClient<IPwnedPasswordsClient, PwnedPasswordsClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.pwnedpasswords.com/");
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("Add-Padding", "true");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("GaiaSkyline/1.0");
        });

        // Appended to Identity's built-in validators; the HIBP check runs on register and change.
        services.AddScoped<IPasswordValidator<ApplicationUser>, PwnedPasswordValidator>();

        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IAuthEmailService, AuthEmailService>();
        services.AddScoped<IdentitySeeder>();

        return services;
    }
}
