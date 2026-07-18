using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Axiom.Atlas.Infrastructure.Services.ServiceDesk
{
    public sealed class GlpiImprovementTicketSynchronizationHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly GlpiImprovementTicketSynchronizationQueue _queue;
        private readonly ILogger<GlpiImprovementTicketSynchronizationHostedService> _logger;
        private readonly TimeSpan _interval;

        public GlpiImprovementTicketSynchronizationHostedService(
            IServiceScopeFactory scopeFactory,
            GlpiImprovementTicketSynchronizationQueue queue,
            IConfiguration configuration,
            ILogger<GlpiImprovementTicketSynchronizationHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
            var intervalSeconds = configuration.GetValue<int?>("GlpiSynchronization:IntervalSeconds") ?? 300;
            _interval = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 60, 3600));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _queue.RecoverInterruptedJobsAsync(stoppingToken);
            await _queue.RequestSynchronizationAsync();
            var nextScheduledSynchronization = DateTimeOffset.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (DateTimeOffset.UtcNow >= nextScheduledSynchronization)
                {
                    await _queue.RequestSynchronizationAsync();
                    nextScheduledSynchronization = DateTimeOffset.UtcNow.Add(_interval);
                }

                var job = await _queue.ClaimNextAsync(stoppingToken);
                if (job == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    using var logScope = _logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["IntegrationJobId"] = job.Id,
                        ["IntegrationJobType"] = job.Type.ToString(),
                        ["CorrelationKey"] = job.CorrelationKey,
                        ["GlpiTicketId"] = job.GlpiTicketId,
                        ["OpenProjectWorkPackageId"] = job.OpenProjectWorkPackageId
                    });
                    using var scope = _scopeFactory.CreateScope();
                    var glpiService = scope.ServiceProvider.GetRequiredService<GlpiService>();
                    if (job.Type == IntegrationSynchronizationJobType.RefreshGlpiImprovementTickets)
                    {
                        await glpiService.SynchronizeImprovementTicketsAsync(stoppingToken);
                    }
                    else if (job.Type == IntegrationSynchronizationJobType.UpdateGlpiWorkPackageLink && job.WorkspaceId.HasValue)
                    {
                        await glpiService.LinkWorkspaceToGlpiAsync(job.WorkspaceId.Value, stoppingToken);
                    }

                    await _queue.MarkSucceededAsync(job.Id, stoppingToken);
                    _logger.LogInformation(
                        "Operação persistida de integração concluída em {ElapsedMilliseconds} ms.",
                        stopwatch.ElapsedMilliseconds);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Falha na operação persistida de integração.");
                    await _queue.MarkFailedAsync(job.Id, exception, stoppingToken);
                }
            }
        }
    }
}
