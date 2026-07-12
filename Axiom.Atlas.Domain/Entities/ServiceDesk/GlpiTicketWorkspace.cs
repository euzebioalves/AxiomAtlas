namespace Axiom.Atlas.Domain.Entities.ServiceDesk
{
    public class GlpiTicketWorkspace
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public long GlpiTicketId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? EntityPath { get; set; }
        public string? ClientEntityName { get; set; }
        public string? Classification { get; set; }
        public string TicketPayloadJson { get; set; } = "{}";
        public string FollowUpsJson { get; set; } = "[]";
        public string AttachmentsJson { get; set; } = "[]";
        public string? RequirementMarkdown { get; set; }
        public int? OpenProjectWorkPackageId { get; set; }
        public string? OpenProjectWorkPackageUrl { get; set; }
        public string? GlpiDevOpsFieldId { get; set; }
        public string? GlpiDevOpsUrl { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
