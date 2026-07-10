namespace Axiom.Atlas.Domain.Enums
{
    public enum SyncStatus
    {
        Pending = 0,    // Aguardando envio para o OP
        Synced = 1,     // Enviado com sucesso
        Error = 2       // Falhou (ex: API do OP fora do ar ou dados inválidos)
    }
}
