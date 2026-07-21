using Axiom.Atlas.Web.Model.Releases;

namespace Axiom.Atlas.Web.Services.Releases;

public interface IGitHubReleaseNotesService
{
    Task<ReleaseNotesViewModel> GetReleaseNotesAsync(CancellationToken cancellationToken = default);
}
