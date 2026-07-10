namespace Axiom.Atlas.Domain.Entities.TimeClock
{
    public class UserWorkScheduleSetting
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } = string.Empty;
        public TimeSpan EntryTime { get; set; }
        public TimeSpan ExitTime { get; set; }
        public int LunchIntervalMinutes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
