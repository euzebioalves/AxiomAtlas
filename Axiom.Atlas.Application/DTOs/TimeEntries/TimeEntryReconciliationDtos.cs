namespace Axiom.Atlas.Application.DTOs.TimeEntries
{
    public class TimeEntryReconciliationReviewDto
    {
        public int PendingEntriesChecked { get; set; }
        public List<TimeEntryReconciliationCandidateDto> Candidates { get; set; } = new();
        public int AmbiguousEntries { get; set; }
    }

    public class TimeEntryReconciliationCandidateDto
    {
        public Guid LocalEntryId { get; set; }
        public int WorkPackageId { get; set; }
        public DateOnly SpentOn { get; set; }
        public decimal Hours { get; set; }
        public int ActivityId { get; set; }
        public string? Comment { get; set; }
        public int OpenProjectTimeEntryId { get; set; }
        public string? OpenProjectComment { get; set; }
    }

    public class ConfirmTimeEntryReconciliationRequest
    {
        public List<TimeEntryReconciliationConfirmationDto> Confirmations { get; set; } = new();
    }

    public class TimeEntryReconciliationConfirmationDto
    {
        public Guid LocalEntryId { get; set; }
        public int OpenProjectTimeEntryId { get; set; }
    }

    public class OpenProjectTimeEntryMatchDto
    {
        public int Id { get; set; }
        public int WorkPackageId { get; set; }
        public DateOnly SpentOn { get; set; }
        public decimal Hours { get; set; }
        public int ActivityId { get; set; }
        public string? Comment { get; set; }
    }
}
