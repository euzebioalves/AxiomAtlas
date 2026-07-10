namespace Axiom.Atlas.Domain.Entities.Notifications
{
    public class OpenProjectWorkPackageStatusSnapshot
    {
        public int WorkPackageId { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    }
}
