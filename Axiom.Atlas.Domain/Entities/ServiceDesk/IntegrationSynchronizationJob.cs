namespace Axiom.Atlas.Domain.Entities.ServiceDesk
{
    public enum IntegrationSynchronizationJobType
    {
        RefreshGlpiImprovementTickets = 1,
        UpdateGlpiWorkPackageLink = 2
    }

    public enum IntegrationSynchronizationJobStatus
    {
        Pending = 1,
        Processing = 2,
        Succeeded = 3,
        Failed = 4
    }

    // A durable record of an integration operation. Retrying a job must never recreate its remote resource.
    public class IntegrationSynchronizationJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public IntegrationSynchronizationJobType Type { get; set; }
        public IntegrationSynchronizationJobStatus Status { get; set; } = IntegrationSynchronizationJobStatus.Pending;
        public string CorrelationKey { get; set; } = string.Empty;
        public Guid? WorkspaceId { get; set; }
        public long? GlpiTicketId { get; set; }
        public int? OpenProjectWorkPackageId { get; set; }
        public string? RequestedByUserId { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; } = 5;
        public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? LastError { get; set; }
    }
}
