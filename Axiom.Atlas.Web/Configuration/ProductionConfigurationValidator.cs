namespace Axiom.Atlas.Web.Configuration;

/// <summary>Fails fast when a production Web container was started without its required configuration.</summary>
public static class ProductionConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        Require(configuration, "ApiSettings:BaseUrl");
        Require(configuration, "DataProtection:KeysPath");
        Require(configuration, "AllowedHosts");
        RequirePositiveInteger(configuration, "Authentication:CookieExpirationMinutes");

        if (!Uri.TryCreate(configuration["ApiSettings:BaseUrl"], UriKind.Absolute, out var apiUri) || apiUri.Scheme != Uri.UriSchemeHttp)
        {
            throw new InvalidOperationException("ApiSettings:BaseUrl deve usar HTTP interno no ambiente de produção.");
        }
    }

    private static void Require(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value) || value.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"A configuração obrigatória '{key}' não foi informada.");
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
}
