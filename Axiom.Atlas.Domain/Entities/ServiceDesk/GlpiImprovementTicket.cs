namespace Axiom.Atlas.Domain.Entities.ServiceDesk
{
    // Read model used by the Service Desk queue. It is refreshed asynchronously from GLPI.
    public class GlpiImprovementTicket
    {
        public long GlpiTicketId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? GlpiTicketUrl { get; set; }
        public DateTime? OpenedAt { get; set; }
        public int? StatusCode { get; set; }
        public string StatusName { get; set; } = "Não informado";
        public string? EntityPath { get; set; }
        public string? ClientEntityName { get; set; }
        public int? WorkPackageId { get; set; }
        public string? WorkPackageUrl { get; set; }
        public string? WorkPackageStatus { get; set; }
        public string? WorkPackageCreator { get; set; }
        public DateTime? WorkPackageCreatedAt { get; set; }
        public bool IsInImprovementQueue { get; set; }
        public DateTime LastSynchronizedAt { get; set; } = DateTime.UtcNow;
    }
}
