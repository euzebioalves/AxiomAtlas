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
        public IntegrationSynchronizationJobDto? GlpiLinkSynchronization { get; set; }
    }

    public class IntegrationSynchronizationJobDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime? NextAttemptAt { get; set; }
        public string? LastError { get; set; }
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
        public int SynchronizationIntervalSeconds { get; set; } = 300;
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

    /// <summary>
    /// Read model for the operational board. It intentionally uses the local GLPI projection
    /// so the board remains responsive while the integrations reconcile in the background.
    /// </summary>
    public class UnifiedBacklogResponse
    {
        public List<UnifiedBacklogItemDto> Items { get; set; } = new();
        public UnifiedBacklogSummaryDto Summary { get; set; } = new();
        public DateTime? LastSynchronizedAt { get; set; }
        public bool SynchronizationPending { get; set; }
        public int SynchronizationIntervalSeconds { get; set; } = 300;
    }

    public class UnifiedBacklogSummaryDto
    {
        public int Total { get; set; }
        public int Triage { get; set; }
        public int Analysis { get; set; }
        public int Delivery { get; set; }
        public int Attention { get; set; }
        public int Completed { get; set; }
        public int Critical { get; set; }
        public int AtRisk { get; set; }
        public int MyAnalyses { get; set; }
        public int PendingGlpiLinks { get; set; }
        public int OldestOpenDays { get; set; }
    }

    public class UnifiedBacklogItemDto
    {
        public long GlpiTicketId { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string? GlpiTicketUrl { get; set; }
        public DateTime? OpenedAt { get; set; }
        public int DaysOpen { get; set; }
        public string? ClientEntityName { get; set; }
        public string GlpiStatusName { get; set; } = "Não informado";
        public Guid? WorkspaceId { get; set; }
        public bool IsOwnedByCurrentUser { get; set; }
        public int? WorkPackageId { get; set; }
        public string? WorkPackageUrl { get; set; }
        public string? WorkPackageStatus { get; set; }
        public string? WorkPackageCreator { get; set; }
        public int? WorkPackageDaysOpen { get; set; }
        public string Stage { get; set; } = "triage";
        public string StageLabel { get; set; } = "Triagem GLPI";
        public string Priority { get; set; } = "Normal";
        public string PriorityReason { get; set; } = string.Empty;
        public bool IsAtRisk { get; set; }
        public bool IsGlpiLinkPending { get; set; }
    }
}
