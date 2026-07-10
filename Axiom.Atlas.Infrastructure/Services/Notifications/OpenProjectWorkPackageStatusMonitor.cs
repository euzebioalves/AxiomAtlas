using Axiom.Atlas.Domain.Entities.Notifications;
using Axiom.Atlas.Domain.Entities.TimeEntries;
using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;
using Axiom.Atlas.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Axiom.Atlas.Infrastructure.Services.Notifications
{
    public class OpenProjectWorkPackageStatusMonitor
    {
        private readonly AppDbContext _context;
        private readonly OpenProjectService _openProjectService;
        private readonly ILogger<OpenProjectWorkPackageStatusMonitor> _logger;

        public OpenProjectWorkPackageStatusMonitor(
            AppDbContext context,
            OpenProjectService openProjectService,
            ILogger<OpenProjectWorkPackageStatusMonitor> logger)
        {
            _context = context;
            _openProjectService = openProjectService;
            _logger = logger;
        }

        public async Task MonitorAsync(CancellationToken cancellationToken)
        {
            var workPackages = await _openProjectService.GetWorkPackagesForStatusMonitoringAsync(cancellationToken);
            if (workPackages.Count == 0)
            {
                return;
            }

            var workPackageIds = workPackages.Select(x => x.Id).ToArray();
            var snapshots = await _context.OpenProjectWorkPackageStatusSnapshots
                .Where(x => workPackageIds.Contains(x.WorkPackageId))
                .ToDictionaryAsync(x => x.WorkPackageId, cancellationToken);

            var transitions = new List<(Axiom.Atlas.Application.DTOs.TimeEntries.OpenProjectWorkPackageMonitoringItemDto WorkPackage, string PreviousStatus)>();
            var now = DateTime.UtcNow;

            foreach (var workPackage in workPackages)
            {
                if (!snapshots.TryGetValue(workPackage.Id, out var snapshot))
                {
                    _context.OpenProjectWorkPackageStatusSnapshots.Add(new OpenProjectWorkPackageStatusSnapshot
                    {
                        WorkPackageId = workPackage.Id,
                        StatusName = workPackage.StatusName,
                        LastSeenAt = now
                    });
                    continue;
                }

                var previousStatus = snapshot.StatusName;
                var statusChanged = !string.Equals(previousStatus, workPackage.StatusName, StringComparison.OrdinalIgnoreCase);
                snapshot.StatusName = workPackage.StatusName;
                snapshot.LastSeenAt = now;

                if (statusChanged && IsAttentionStatus(workPackage.StatusName) && workPackage.ResponsibleUserIds.Count > 0)
                {
                    transitions.Add((workPackage, previousStatus));
                }
            }

            if (transitions.Count == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var assigneeEmails = await _openProjectService.GetOpenProjectUserEmailsAsync(
                transitions.SelectMany(x => x.WorkPackage.ResponsibleUserIds),
                cancellationToken);
            if (assigneeEmails.Count == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            var normalizedEmails = assigneeEmails.Values
                .Select(NormalizeEmail)
                .Distinct()
                .ToArray();
            var users = await _context.Users
                .Where(x => x.IsActive && x.Email != null && normalizedEmails.Contains(x.Email.ToLower()))
                .ToListAsync(cancellationToken);
            var usersByEmail = users.ToDictionary(x => NormalizeEmail(x.Email), StringComparer.OrdinalIgnoreCase);
            var userIds = users.Select(x => x.Id).ToArray();
            var enabledSettings = await _context.UserDesktopNotificationSettings
                .Where(x => userIds.Contains(x.UserId) && x.IsEnabled)
                .Select(x => x.UserId)
                .ToHashSetAsync(cancellationToken);

            var baseUrl = await _openProjectService.GetActiveOpenProjectBaseUrlAsync();
            foreach (var transition in transitions)
            {
                var workPackageCache = await _openProjectService.GetWorkPackageAsync(transition.WorkPackage.Id);
                var workPackageUrl = OpenProjectService.BuildWorkPackageWebUrl(baseUrl, workPackageCache)
                    ?? $"{baseUrl}/work_packages/{transition.WorkPackage.Id}/activity";
                var reasonComment = await _openProjectService.GetLatestWorkPackageCommentAsync(
                    transition.WorkPackage.Id,
                    cancellationToken);

                foreach (var responsibleUserId in transition.WorkPackage.ResponsibleUserIds)
                {
                    if (!assigneeEmails.TryGetValue(responsibleUserId, out var responsibleEmail) ||
                        !usersByEmail.TryGetValue(NormalizeEmail(responsibleEmail), out var user) ||
                        !enabledSettings.Contains(user.Id))
                    {
                        continue;
                    }

                    _context.DesktopNotifications.Add(new DesktopNotification
                    {
                        UserId = user.Id,
                        WorkPackageId = transition.WorkPackage.Id,
                        WorkPackageSubject = transition.WorkPackage.Subject,
                        StatusName = transition.WorkPackage.StatusName,
                        PreviousStatusName = transition.PreviousStatus,
                        ReasonComment = reasonComment,
                        WorkPackageUrl = workPackageUrl,
                        CreatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Monitoramento OpenProject processou {TransitionCount} transições de status críticas.", transitions.Count);
        }

        private static bool IsAttentionStatus(string? statusName)
        {
            var normalizedStatus = (statusName ?? string.Empty)
                .Trim()
                .Replace("-", " ")
                .Replace("_", " ")
                .ToLowerInvariant();

            return normalizedStatus is "test failed" or "rejected";
        }

        private static string NormalizeEmail(string? email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }
    }
}
