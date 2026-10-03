using GaiaSkyline.Domain.Identity;
using Hangfire.Dashboard;

namespace GaiaSkyline.Web.Security;

/// <summary>Restricts the Hangfire dashboard to a signed-in Owner (reuses the Stage 6 auth).</summary>
public sealed class OwnerDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var user = context.GetHttpContext().User;
        return user.Identity?.IsAuthenticated == true && user.IsInRole(UserRoles.Owner);
    }
}
