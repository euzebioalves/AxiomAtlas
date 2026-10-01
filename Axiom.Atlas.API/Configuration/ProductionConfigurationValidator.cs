using System.Net;

namespace Axiom.Atlas.API.Configuration;

/// <summary>
/// Validates the configuration that is required to safely run the API behind the
/// production reverse proxy.  Validation deliberately happens before the web
/// host is built so a partially configured container never starts accepting
/// traffic.
/// </summary>
public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        Require(configuration, "ConnectionStrings:DefaultConnection");
        Require(configuration, "JwtSettings:SecretKey", secret: true);
        Require(configuration, "JwtSettings:Issuer");
        Require(configuration, "JwtSettings:Audience");
        RequirePositiveInteger(configuration, "JwtSettings:ExpirationMinutes");
        Require(configuration, "DataProtection:KeysPath");
        RequireHttpsUrl(configuration, "PublicUrls:WebBaseUrl");
        Require(configuration, "EmailSettings:SmtpServer");
        RequirePositiveInteger(configuration, "EmailSettings:SmtpPort");
        Require(configuration, "EmailSettings:SenderName");
        Require(configuration, "EmailSettings:SenderEmail");
        Require(configuration, "EmailSettings:Username");
        Require(configuration, "EmailSettings:Password", secret: true);

        var jwtSecret = configuration["JwtSettings:SecretKey"]!;
        if (jwtSecret.Length < 32)
        {
            throw new InvalidOperationException("JwtSettings:SecretKey deve possuir ao menos 32 caracteres na produção.");
        }
    }

    private static void Require(IConfiguration configuration, string key, bool secret = false)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"A configuração obrigatória '{key}' não foi informada.");
        }

        if (!secret && value.Contains("localhost", StringComparison.OrdinalIgnoreCase) && key.StartsWith("PublicUrls", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"A configuração '{key}' não pode usar localhost na produção.");
        }
    }

    private static void RequirePositiveInteger(IConfiguration configuration, string key)
    {
        Require(configuration, key);
        if (!int.TryParse(configuration[key], out var value) || value <= 0)
        {
            throw new InvalidOperationException($"A configuração '{key}' deve ser um inteiro positivo.");
        }
    }

    private static void RequireHttpsUrl(IConfiguration configuration, string key)
    {
        Require(configuration, key);
        if (!Uri.TryCreate(configuration[key], UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException($"A configuração '{key}' deve ser uma URL HTTPS absoluta e válida.");
        }
    }
}
