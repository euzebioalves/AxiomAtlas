using Axiom.Atlas.Domain.Interfaces.Mail;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Axiom.Atlas.Infrastructure.Services.Mail
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var senderEmail = _config["EmailSettings:SenderEmail"]
                ?? throw new InvalidOperationException("EmailSettings:SenderEmail não configurado.");
            var smtpServer = _config["EmailSettings:SmtpServer"]
                ?? throw new InvalidOperationException("EmailSettings:SmtpServer não configurado.");
            var username = _config["EmailSettings:Username"]
                ?? throw new InvalidOperationException("EmailSettings:Username não configurado.");
            var password = _config["EmailSettings:Password"]
                ?? throw new InvalidOperationException("EmailSettings:Password não configurado.");

            if (!int.TryParse(_config["EmailSettings:SmtpPort"], out var smtpPort))
            {
                throw new InvalidOperationException("EmailSettings:SmtpPort não configurado ou inválido.");
            }

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_config["EmailSettings:SenderName"], senderEmail));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            try
            {
                // Conecta ao servidor (Gmail, Mailtrap, etc)
                await smtp.ConnectAsync(
                    smtpServer,
                    smtpPort,
                    MailKit.Security.SecureSocketOptions.StartTls);

                // Autentica
                await smtp.AuthenticateAsync(username, password);

                // Envia
                await smtp.SendAsync(email);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}
