namespace Axiom.Atlas.Web.Model.AuditLog
{
    public class AuditLogFilterViewModel
    {
        public DateTime? DataInicio { get; set; }
        public DateTime? DataFim { get; set; }
        public string? Tabela { get; set; }
        public string? TipoAcao { get; set; }
        public string? Usuario { get; set; }
    }
}
