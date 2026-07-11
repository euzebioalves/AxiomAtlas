namespace Axiom.Atlas.Application.DTOs.ServiceDesk
{
    public class ImportGlpiTicketRequest { public string Query { get; set; } = string.Empty; }
    public class GlpiTicketWorkspaceDto
    {
        public Guid Id { get; set; }
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
    }
    public class SaveRequirementDraftRequest { public string? RequirementMarkdown { get; set; } }
}
