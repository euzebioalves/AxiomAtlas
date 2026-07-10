namespace Axiom.Atlas.Domain.Entities.Notifications
{
    public class DesktopNotification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; }
        public int WorkPackageId { get; set; }
        public string WorkPackageSubject { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? PreviousStatusName { get; set; }
        public string? WorkPackageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? DeliveredAt { get; set; }
    }
}
