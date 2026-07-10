namespace Axiom.Atlas.Web.Model.AuditLog
{
    public class AuditLogViewModel
    {
        public int Id { get; set; }
        public DateTime DataHora { get; set; }
        public string Usuario { get; set; } = string.Empty;
        public string TipoAcao { get; set; } = string.Empty;
        public string Tabela { get; set; } = string.Empty;
        public string ChavePrimaria { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string ValoresAntigos { get; set; } = string.Empty;
        public string ValoresNovos { get; set; } = string.Empty;
    }
}
