namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class SyncTimeEntryResult
    {
        public bool Success { get; set; }
        public int? OpenProjectTimeEntryId { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
