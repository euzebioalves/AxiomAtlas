namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class TimeEntryListItemDto
    {
        public Guid Id { get; set; }
        public int WorkPackageId { get; set; }
        public string? WorkPackageSubject { get; set; }
        public string? WorkPackageProjectName { get; set; }
        public string? WorkPackageUrl { get; set; }
        public DateTime SpentOn { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public decimal Hours { get; set; }
        public string? Comment { get; set; }
        public int ActivityId { get; set; }
        public string SyncStatus { get; set; } = string.Empty;
        public string? SyncErrorMessage { get; set; }
        public int? OpenProjectTimeEntryId { get; set; }
        public string? OpenProjectTimeEntryUrl { get; set; }
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }
        public string? LockReason { get; set; }
    }
}
