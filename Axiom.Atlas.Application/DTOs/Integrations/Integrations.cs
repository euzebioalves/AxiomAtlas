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
}
