namespace Axiom.Atlas.Domain.Entities.TimeEntries
{
    public class WorkPackageCache
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        public int ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? ProjectIdentifier { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    }
}
