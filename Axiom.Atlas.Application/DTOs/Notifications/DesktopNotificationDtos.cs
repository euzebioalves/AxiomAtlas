namespace Axiom.Atlas.Application.DTOs.Notifications
{
    public class DesktopNotificationSettingsDto
    {
        public bool IsEnabled { get; set; }
    }

    public class SaveDesktopNotificationSettingsRequest
    {
        public bool IsEnabled { get; set; }
    }

    public class DesktopNotificationDto
    {
        public Guid Id { get; set; }
        public int WorkPackageId { get; set; }
        public string WorkPackageSubject { get; set; } = string.Empty;
        public string StatusName { get; set; } = string.Empty;
        public string? PreviousStatusName { get; set; }
        public string? ReasonComment { get; set; }
        public string? WorkPackageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
