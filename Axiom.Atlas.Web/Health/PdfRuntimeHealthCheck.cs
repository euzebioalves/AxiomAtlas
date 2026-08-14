using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Axiom.Atlas.Web.Health;

/// <summary>Exercises the minimum QuestPDF path so a missing font/runtime dependency fails readiness before traffic is accepted.</summary>
public sealed class PdfRuntimeHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var bytes = Document.Create(document => document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(10);
                page.Content().Text("Axiom Atlas PDF readiness");
            })).GeneratePdf();

            return Task.FromResult(bytes.Length > 0
                ? HealthCheckResult.Healthy("A geração de PDF está disponível.")
                : HealthCheckResult.Unhealthy("A geração de PDF não produziu conteúdo."));
        }
        catch (Exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("A geração de PDF não está disponível."));
        }
    }
}
