using Axiom.Atlas.Web.Services.Releases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axiom.Atlas.Web.Controllers.Releases;

[Authorize]
public sealed class ReleasesController : Controller
{
    private readonly IGitHubReleaseNotesService _releaseNotesService;

    public ReleasesController(IGitHubReleaseNotesService releaseNotesService)
    {
        _releaseNotesService = releaseNotesService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _releaseNotesService.GetReleaseNotesAsync(cancellationToken);
        return View(model);
    }
}
