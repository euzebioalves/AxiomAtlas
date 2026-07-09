namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class CreateTimeEntryRequest
    {
        public int WorkPackageId { get; set; }
        public DateTime SpentOn { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public decimal Hours { get; set; }
        public string? Comment { get; set; }
        public int ActivityId { get; set; }
    }
}
