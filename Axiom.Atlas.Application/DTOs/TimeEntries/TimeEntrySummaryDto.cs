namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class TimeEntrySummaryDto
    {
        public int TotalEntries { get; set; }
        public int PendingEntries { get; set; }
        public int SyncedEntries { get; set; }
        public int ErrorEntries { get; set; }
        public decimal TotalHours { get; set; }
        public decimal PendingHours { get; set; }
        public decimal SyncedHours { get; set; }
        public decimal ErrorHours { get; set; }
        public DateTime? LastEntryDate { get; set; }
    }
}
