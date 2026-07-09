namespace Axiom.Atlas.Domain.Entities.TimeClock
{
    public class TimeClockAbsenceAttachment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AbsenceId { get; set; }
        public TimeClockAbsence? Absence { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
