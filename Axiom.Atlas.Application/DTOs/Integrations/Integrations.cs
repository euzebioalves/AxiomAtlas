namespace Axiom.Atlas.Application.DTOs.Integrations
{
    public class SaveOpenProjectSettingsRequest
    {
        public string? ActiveEnvironment { get; set; }
        public EnvironmentSettingDto Production { get; set; } = new();
        public EnvironmentSettingDto Homologation { get; set; } = new();
    }

    public class EnvironmentSettingDto
    {
        public string? BaseUrl { get; set; }
        public string? PrimaryToken { get; set; }
    }

    public class TestOpenProjectConnectionRequest
    {
        public string? Environment { get; set; }
        public string? BaseUrl { get; set; }
        public string? PrimaryToken { get; set; }
    }

    public class OpenProjectConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Environment { get; set; }
        public string? BaseUrl { get; set; }
        public string? UserName { get; set; }
        public int ActivitiesCount { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class SaveGlpiSettingsRequest
    {
        public string? ActiveEnvironment { get; set; }
        public GlpiEnvironmentSettingDto Production { get; set; } = new();
        public GlpiEnvironmentSettingDto Homologation { get; set; } = new();
    }

    public class GlpiEnvironmentSettingDto
    {
        public string? BaseUrl { get; set; }
        public string? AppToken { get; set; }
        public string? UserToken { get; set; }
        public string? ClassificationFieldKey { get; set; }
        public string? DevOpsUrlFieldKey { get; set; }
    }

    public class TestGlpiConnectionRequest : GlpiEnvironmentSettingDto
    {
        public string? Environment { get; set; }
    }

    public class GlpiConnectionTestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? GlpiVersion { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class IntegrationSynchronizationOverviewDto
    {
        public int PendingCount { get; set; }
        public int ProcessingCount { get; set; }
        public int FailedCount { get; set; }
        public int SucceededCount { get; set; }
        public DateTime? LastCompletedAt { get; set; }
        public List<IntegrationSynchronizationJobDetailsDto> Jobs { get; set; } = new();
    }

    public class IntegrationSynchronizationJobDetailsDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CorrelationKey { get; set; } = string.Empty;
        public long? GlpiTicketId { get; set; }
        public int? OpenProjectWorkPackageId { get; set; }
        public int AttemptCount { get; set; }
        public int MaxAttempts { get; set; }
        public DateTime AvailableAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? LastError { get; set; }
    }
}
