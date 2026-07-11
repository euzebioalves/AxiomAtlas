namespace Axiom.Atlas.Web.Model.Integrations
{
    public class OpenProjectSettingsViewModel
    {
        // Qual ambiente está ativo no momento (ex: "Production" ou "Homologation")
        public string? ActiveEnvironment { get; set; }

        public EnvironmentSettingViewModel Production { get; set; } = new();
        public EnvironmentSettingViewModel Homologation { get; set; } = new();
    }

    public class EnvironmentSettingViewModel
    {
        public string? BaseUrl { get; set; }
        public string? PrimaryToken { get; set; }
    }

    public class OpenProjectConnectionTestViewModel
    {
        public string? Environment { get; set; }
        public string? BaseUrl { get; set; }
        public string? PrimaryToken { get; set; }
    }

    public class GlpiSettingsViewModel
    {
        public string? BaseUrl { get; set; }
        public string? AppToken { get; set; }
        public string? UserToken { get; set; }
        public string? ClassificationFieldKey { get; set; }
        public string? DevOpsUrlFieldKey { get; set; }
    }
}
