namespace Axiom.Atlas.Domain.Entities.TimeClock
{
    public class GlobalTimeClockSetting
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public int ToleranceMinutes { get; set; } = 10;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
