namespace Axiom.Atlas.Domain.Interfaces.Mail
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }
}
