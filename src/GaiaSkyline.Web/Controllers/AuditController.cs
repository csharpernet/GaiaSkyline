using GaiaSkyline.Application.Auditing;
using Microsoft.AspNetCore.Mvc;

namespace GaiaSkyline.Web.Controllers;

/// <summary>The append-only audit trail, filterable by action, entity type and date range.</summary>
[Route("admin/audit")]
public sealed class AuditController(IAuditReadStore auditReadStore) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? action, string? entityType, DateOnly? from, DateOnly? to, int page = 1,
        CancellationToken cancellationToken = default)
    {
        ViewData["Title"] = "Audit log";
        var query = new AuditLogQuery(action, entityType, from, to, page, PageSize: 50);
        var result = await auditReadStore.QueryAsync(query, cancellationToken);
        return View(result);
    }
}
