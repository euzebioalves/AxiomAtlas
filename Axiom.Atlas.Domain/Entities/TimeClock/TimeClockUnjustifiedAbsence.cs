using Axiom.Atlas.Domain.Enums;

namespace Axiom.Atlas.Domain.Entities.TimeClock
{
    public class TimeClockUnjustifiedAbsence
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public DateTime AbsenceDate { get; set; }
        public TimeClockUnjustifiedAbsenceType Type { get; set; }
        public TimeSpan? StartTime { get; set; }
        public TimeSpan? EndTime { get; set; }
        public string? Observation { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
