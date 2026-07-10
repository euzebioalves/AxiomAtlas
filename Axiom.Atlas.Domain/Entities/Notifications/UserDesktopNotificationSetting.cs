namespace Axiom.Atlas.Domain.Entities.Notifications
{
    public class UserDesktopNotificationSetting
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
