namespace Axiom.Atlas.Domain.Entities.AuditLogs
{
    public class AuditLog
    {
        public int Id { get; set; }
        public DateTime DataHora { get; set; }
        public string? IpAddress { get; set; }
        public string? Usuario { get; set; }
        public string? TipoAcao { get; set; } // Create, Update, Delete, Login
        public string? Tabela { get; set; }
        public string? ChavePrimaria { get; set; }
        public string? ValoresAntigos { get; set; } // JSON
        public string? ValoresNovos { get; set; }   // JSON
    }
}
