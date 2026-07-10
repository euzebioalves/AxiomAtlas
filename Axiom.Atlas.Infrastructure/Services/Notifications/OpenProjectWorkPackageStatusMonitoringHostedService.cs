using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Axiom.Atlas.Infrastructure.Services.Notifications
{
    public class OpenProjectWorkPackageStatusMonitoringHostedService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OpenProjectWorkPackageStatusMonitoringHostedService> _logger;
        private readonly TimeSpan _interval;

        public OpenProjectWorkPackageStatusMonitoringHostedService(
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration,
            ILogger<OpenProjectWorkPackageStatusMonitoringHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            var intervalSeconds = configuration.GetValue<int?>("OpenProjectMonitoring:IntervalSeconds") ?? 300;
            _interval = TimeSpan.FromSeconds(Math.Clamp(intervalSeconds, 30, 3600));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var monitor = scope.ServiceProvider.GetRequiredService<OpenProjectWorkPackageStatusMonitor>();
                    await monitor.MonitorAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (InvalidOperationException exception)
                {
                    _logger.LogDebug(exception, "Monitoramento OpenProject aguardando uma integração ativa e válida.");
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Falha no monitoramento das Work Packages do OpenProject.");
                }

                await Task.Delay(_interval, stoppingToken);
            }
        }
    }
}
