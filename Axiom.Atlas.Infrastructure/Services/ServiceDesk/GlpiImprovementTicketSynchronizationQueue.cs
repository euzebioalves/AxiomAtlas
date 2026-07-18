using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Axiom.Atlas.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Axiom.Atlas.Infrastructure.Services.ServiceDesk
{
    // The queue state lives in PostgreSQL so restarts and transient GLPI failures do not lose work.
    public sealed class GlpiImprovementTicketSynchronizationQueue
    {
        private const int DefaultMaxAttempts = 5;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GlpiImprovementTicketSynchronizationQueue> _logger;

        public GlpiImprovementTicketSynchronizationQueue(
            IServiceScopeFactory scopeFactory,
            ILogger<GlpiImprovementTicketSynchronizationQueue> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task<IntegrationSynchronizationJob> RequestSynchronizationAsync(string? requestedByUserId = null) =>
            EnqueueAsync(
                IntegrationSynchronizationJobType.RefreshGlpiImprovementTickets,
                "glpi-improvements",
                null,
                null,
                null,
                requestedByUserId);

        public Task<IntegrationSynchronizationJob> RequestGlpiLinkUpdateAsync(
            Guid workspaceId,
            long glpiTicketId,
            int? workPackageId,
            string? requestedByUserId = null) =>
            EnqueueAsync(
                IntegrationSynchronizationJobType.UpdateGlpiWorkPackageLink,
                $"glpi-link:{workspaceId}",
                workspaceId,
                glpiTicketId,
                workPackageId,
                requestedByUserId);

        public async Task<bool> IsSynchronizationPendingAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await context.IntegrationSynchronizationJobs.AsNoTracking().AnyAsync(x =>
                x.Type == IntegrationSynchronizationJobType.RefreshGlpiImprovementTickets &&
                (x.Status == IntegrationSynchronizationJobStatus.Pending || x.Status == IntegrationSynchronizationJobStatus.Processing));
        }

        public async Task<IntegrationSynchronizationJob?> GetLatestGlpiLinkJobAsync(Guid workspaceId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await context.IntegrationSynchronizationJobs.AsNoTracking()
                .Where(x => x.Type == IntegrationSynchronizationJobType.UpdateGlpiWorkPackageLink && x.WorkspaceId == workspaceId)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<IntegrationSynchronizationJob?> RetryAsync(Guid jobId, string? requestedByUserId = null)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await context.IntegrationSynchronizationJobs.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == jobId);
            if (job == null)
            {
                return null;
            }

            // Keep the failed job immutable as operational history and enqueue a clean retry.
            return await EnqueueAsync(
                job.Type,
                job.CorrelationKey,
                job.WorkspaceId,
                job.GlpiTicketId,
                job.OpenProjectWorkPackageId,
                requestedByUserId);
        }

        public async Task RecoverInterruptedJobsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var interruptedJobs = await context.IntegrationSynchronizationJobs
                .Where(x => x.Status == IntegrationSynchronizationJobStatus.Processing)
                .ToListAsync(cancellationToken);

            if (interruptedJobs.Count == 0)
            {
                return;
            }

            var now = DateTime.UtcNow;
            foreach (var job in interruptedJobs)
            {
                job.Status = IntegrationSynchronizationJobStatus.Pending;
                job.AvailableAt = now;
                job.StartedAt = null;
                job.LastError = "A execução foi interrompida por uma reinicialização e será retomada automaticamente.";
            }

            await context.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "{RecoveredJobCount} operações de integração interrompidas foram devolvidas à fila.",
                interruptedJobs.Count);
        }

        public async Task<IntegrationSynchronizationJob?> ClaimNextAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;
            var job = await context.IntegrationSynchronizationJobs
                .Where(x => x.Status == IntegrationSynchronizationJobStatus.Pending && x.AvailableAt <= now)
                .OrderBy(x => x.AvailableAt)
                .ThenBy(x => x.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (job == null)
            {
                return null;
            }

            job.Status = IntegrationSynchronizationJobStatus.Processing;
            job.AttemptCount++;
            job.StartedAt = now;
            job.LastError = null;
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Operação de integração {JobId} assumida para processamento. Tipo: {JobType}. Tentativa: {Attempt}/{MaxAttempts}.",
                job.Id,
                job.Type,
                job.AttemptCount,
                job.MaxAttempts);
            return job;
        }

        public async Task MarkSucceededAsync(Guid jobId, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await context.IntegrationSynchronizationJobs.FindAsync([jobId], cancellationToken);
            if (job == null) return;
            job.Status = IntegrationSynchronizationJobStatus.Succeeded;
            job.CompletedAt = DateTime.UtcNow;
            job.LastError = null;
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Operação de integração {JobId} concluída com sucesso após {AttemptCount} tentativa(s).",
                job.Id,
                job.AttemptCount);
        }

        public async Task MarkFailedAsync(Guid jobId, Exception exception, CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await context.IntegrationSynchronizationJobs.FindAsync([jobId], cancellationToken);
            if (job == null) return;

            job.LastError = exception.Message;
            if (job.AttemptCount >= job.MaxAttempts)
            {
                job.Status = IntegrationSynchronizationJobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                _logger.LogError(
                    exception,
                    "Operação de integração {JobId} falhou definitivamente após {AttemptCount} tentativa(s).",
                    job.Id,
                    job.AttemptCount);
            }
            else
            {
                // 10s, 20s, 40s, 80s: quick recovery without hot-looping a remote API.
                var delaySeconds = Math.Min(300, 10 * Math.Pow(2, Math.Max(0, job.AttemptCount - 1)));
                job.Status = IntegrationSynchronizationJobStatus.Pending;
                job.AvailableAt = DateTime.UtcNow.AddSeconds(delaySeconds);
                _logger.LogWarning(
                    exception,
                    "Operação de integração {JobId} falhou na tentativa {AttemptCount} e será repetida em {DelaySeconds} segundo(s).",
                    job.Id,
                    job.AttemptCount,
                    delaySeconds);
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        private async Task<IntegrationSynchronizationJob> EnqueueAsync(
            IntegrationSynchronizationJobType type,
            string correlationKey,
            Guid? workspaceId,
            long? glpiTicketId,
            int? workPackageId,
            string? requestedByUserId)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var activeJob = await context.IntegrationSynchronizationJobs
                .Where(x => x.Type == type && x.CorrelationKey == correlationKey &&
                    (x.Status == IntegrationSynchronizationJobStatus.Pending || x.Status == IntegrationSynchronizationJobStatus.Processing))
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync();
            if (activeJob != null)
            {
                _logger.LogInformation(
                    "A operação {JobType} já está na fila como {JobId}; uma nova solicitação foi consolidada.",
                    type,
                    activeJob.Id);
                return activeJob;
            }

            var job = new IntegrationSynchronizationJob
            {
                Type = type,
                CorrelationKey = correlationKey,
                WorkspaceId = workspaceId,
                GlpiTicketId = glpiTicketId,
                OpenProjectWorkPackageId = workPackageId,
                RequestedByUserId = requestedByUserId,
                MaxAttempts = DefaultMaxAttempts
            };
            context.IntegrationSynchronizationJobs.Add(job);
            await context.SaveChangesAsync();
            _logger.LogInformation(
                "Operação de integração {JobId} adicionada à fila. Tipo: {JobType}; Chave: {CorrelationKey}.",
                job.Id,
                job.Type,
                job.CorrelationKey);
            return job;
        }
    }
}
