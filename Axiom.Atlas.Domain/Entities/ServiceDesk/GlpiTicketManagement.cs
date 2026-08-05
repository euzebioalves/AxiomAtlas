namespace Axiom.Atlas.Domain.Entities.ServiceDesk
{
    /// <summary>
    /// Operational metadata maintained by Axiom Atlas. It complements, but never replaces,
    /// the source information synchronized from GLPI and OpenProject.
    /// </summary>
    public class GlpiTicketManagement
    {
        public long GlpiTicketId { get; set; }
        public Guid? AssignedUserId { get; set; }
        public string? Priority { get; set; }
        public string? Stage { get; set; }
        public string? Classification { get; set; }
        public string UpdatedByUserId { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
