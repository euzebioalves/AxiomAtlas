namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class OpenProjectWpResponse
    {
        public string? Subject { get; set; }
        public OpenProjectLinks? _links { get; set; }
    }

    public class OpenProjectLinks
    {
        public OpenProjectProjectLink? Project { get; set; }
    }

    public class OpenProjectProjectLink
    {
        public string? Href { get; set; }
        public string? Title { get; set; }
    }

    public class OpenProjectWorkPackageSearchResult
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }

    public class OpenProjectProjectResponse
    {
        public int Id { get; set; }
        public string? Identifier { get; set; }
        public string? Name { get; set; }
    }
}
