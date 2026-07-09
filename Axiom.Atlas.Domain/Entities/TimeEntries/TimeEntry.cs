using Axiom.Atlas.Domain.Enums;

namespace Axiom.Atlas.Domain.Entities.TimeEntries
{
    public class TimeEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string? UserId { get; set; }
        public int WorkPackageId { get; set; }
        public WorkPackageCache? WorkPackage { get; set; }
        public DateTime SpentOn { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public decimal Hours { get; set; }
        public string? Comment { get; set; }
        public int ActivityId { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? SyncErrorMessage { get; set; }
        public int? OpenProjectTimeEntryId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
