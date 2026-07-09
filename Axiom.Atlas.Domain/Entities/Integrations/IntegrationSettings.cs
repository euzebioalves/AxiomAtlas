namespace Axiom.Atlas.Domain.Entities.Integrations
{
    public class IntegrationSettings
    {
        public Guid Id { get; set; }

        // Identificação
        public string? Provider { get; set; } // Ex: "OpenProject", "GLPI"
        public string? Environment { get; set; } // Ex: "Homologation", "Production"
        public bool IsActive { get; set; }

        // Conexão
        public string? BaseUrl { get; set; }

        // Autenticação Principal (Criptografada no banco)
        // Para o OpenProject: Aqui vai o apikey. Para o GLPI: Aqui vai o user_token.
        public string? PrimaryToken { get; set; }

        // Autenticação Secundária (Criptografada no banco)
        // Para o OpenProject: Fica nulo. Para o GLPI: Aqui vai o app_token.
        public string? SecondaryToken { get; set; }

        // Flexibilidade Futura (Armazena um JSON estruturado)
        // Se amanhã integrarmos com um sistema que exige ClientId, ClientSecret e TenantId, 
        // jogamos nesse JSON sem precisar dar um novo "Add-Migration".
        public string? AdditionalSettings { get; set; }
    }
}
