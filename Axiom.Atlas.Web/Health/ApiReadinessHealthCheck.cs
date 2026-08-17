using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Axiom.Atlas.Web.Health;

public sealed class ApiReadinessHealthCheck(
    IHttpClientFactory httpClientFactory,
    ILogger<ApiReadinessHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = httpClientFactory.CreateClient("Api");
            using var response = await client.GetAsync("health/ready", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("A API está pronta.")
                : HealthCheckResult.Unhealthy("A API não está pronta.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "A verificação de prontidão da API falhou.");
            return HealthCheckResult.Unhealthy("A API não está acessível.");
        }
    }
}
