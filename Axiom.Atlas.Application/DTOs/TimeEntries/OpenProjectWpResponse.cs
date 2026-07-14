namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class OpenProjectWpResponse
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        public string? CreatedAt { get; set; }
        public OpenProjectLinks? _links { get; set; }
    }

    public class OpenProjectLinks
    {
        public OpenProjectProjectLink? Project { get; set; }
        public OpenProjectProjectLink? Status { get; set; }
        public OpenProjectProjectLink? Author { get; set; }
        public OpenProjectProjectLink? Assignee { get; set; }
        public OpenProjectProjectLink? Responsible { get; set; }
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

    public class OpenProjectWorkPackageMonitoringItemDto
    {
        public int Id { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public List<int> ResponsibleUserIds { get; set; } = new();
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
    }

    public class OpenProjectWorkPackageSummaryDto
    {
        public int Id { get; set; }
        public string? StatusName { get; set; }
        public string? CreatorName { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
