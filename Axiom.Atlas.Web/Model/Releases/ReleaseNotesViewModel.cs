namespace Axiom.Atlas.Web.Model.Releases;

public sealed class ReleaseNotesViewModel
{
    public IReadOnlyList<ReleaseNoteViewModel> Releases { get; init; } = Array.Empty<ReleaseNoteViewModel>();
    public string? Notice { get; init; }
}

public sealed class ReleaseNoteViewModel
{
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }
    public IReadOnlyList<ReleaseChangeViewModel> Changes { get; init; } = Array.Empty<ReleaseChangeViewModel>();
}

public sealed class ReleaseChangeViewModel
{
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string BadgeClass { get; init; } = "badge-light-primary";
}
