using GaiaSkyline.Web.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;

namespace GaiaSkyline.Web.Controllers;

/// <summary>
/// Test-only endpoint for the Playwright E2E suite (reads captured email). Returns 404 unless the E2E
/// seam is explicitly enabled (<c>E2E:Enabled</c>), which never happens in Production.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("test")]
[OutputCache(NoStore = true)]
public sealed class E2ETestController(IOptions<E2EOptions> options, E2EEmailSink sink) : ControllerBase
{
    [HttpGet("emails")]
    public IActionResult Emails() =>
        options.Value.Enabled ? Ok(sink.Snapshot()) : NotFound();
}
