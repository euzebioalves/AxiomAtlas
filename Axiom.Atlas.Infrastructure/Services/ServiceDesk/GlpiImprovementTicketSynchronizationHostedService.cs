using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            _queue.RequestSynchronization();
            var nextScheduledSynchronization = DateTimeOffset.UtcNow;

            while (!stoppingToken.IsCancellationRequested)
            {
                var requested = _queue.TryDequeue();
                if (requested || DateTimeOffset.UtcNow >= nextScheduledSynchronization)
                {
                    _queue.SetProcessing(true);
                    try
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var glpiService = scope.ServiceProvider.GetRequiredService<GlpiService>();
                        await glpiService.SynchronizeImprovementTicketsAsync(stoppingToken);
                        _logger.LogInformation("Fila local de solicitações de melhoria sincronizada com o GLPI.");
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (InvalidOperationException exception)
                    {
                        _logger.LogDebug(exception, "Sincronização GLPI aguardando uma integração ativa e válida.");
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Falha ao sincronizar a fila local de solicitações de melhoria.");
                    }
                    finally
                    {
                        _queue.SetProcessing(false);
                    }

                    nextScheduledSynchronization = DateTimeOffset.UtcNow.Add(_interval);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }
    }
}
