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

    public class CreateOpenProjectUserStoryRequest
    {
        public int ProjectId { get; set; }
        public string? RequirementMarkdown { get; set; }
    }

    public class OpenProjectProjectOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Identifier { get; set; }
    }

    public class GlpiImprovementTicketsResponse
    {
        public List<GlpiImprovementTicketDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public string StatusFilter { get; set; } = "not_solved";
        public DateTime? LastSynchronizedAt { get; set; }
        public bool SynchronizationPending { get; set; }
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)Math.Max(1, PageSize)));
    }

    public class GlpiImprovementTicketDto
    {
        public long GlpiTicketId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? GlpiTicketUrl { get; set; }
        public DateTime? OpenedAt { get; set; }
        public int DaysOpen { get; set; }
        public string? ClientEntityName { get; set; }
        public string GlpiStatusName { get; set; } = "Não informado";
        public int? WorkPackageId { get; set; }
        public string? WorkPackageUrl { get; set; }
        public string? WorkPackageStatus { get; set; }
        public string? WorkPackageCreator { get; set; }
        public int? WorkPackageDaysOpen { get; set; }
    }
}
