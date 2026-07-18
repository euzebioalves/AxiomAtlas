using Audit.EntityFramework;
using Axiom.Atlas.Domain.Entities.AuditLogs;
using Axiom.Atlas.Domain.Entities.TimeEntries;
using Axiom.Atlas.Domain.Entities.TimeClock;
using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Domain.Entities.Integrations;
using Axiom.Atlas.Domain.Entities.Notifications;
using Axiom.Atlas.Domain.Entities.ServiceDesk;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Axiom.Atlas.Persistence
{
    public class AppDbContext : AuditIdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        //public DbSet<RequirementNote> RequirementNotes { get; set; }

        //AuditLogs
        public DbSet<AuditLog> AuditLogs { get; set; }

        //TimeEntries
        public DbSet<WorkPackageCache> WorkPackageCaches { get; set; }
        public DbSet<TimeEntry> TimeEntries { get; set; }

        //TimeClock
        public DbSet<UserWorkScheduleSetting> UserWorkScheduleSettings { get; set; }
        public DbSet<GlobalTimeClockSetting> GlobalTimeClockSettings { get; set; }
        public DbSet<TimeClockPunch> TimeClockPunches { get; set; }
        public DbSet<TimeClockUnjustifiedAbsence> TimeClockUnjustifiedAbsences { get; set; }
        public DbSet<TimeClockAbsence> TimeClockAbsences { get; set; }
        public DbSet<TimeClockAbsenceAttachment> TimeClockAbsenceAttachments { get; set; }

        //Integrations
        public DbSet<IntegrationSettings> Integrations { get; set; }
        public DbSet<GlpiTicketWorkspace> GlpiTicketWorkspaces { get; set; }
        public DbSet<GlpiImprovementTicket> GlpiImprovementTickets { get; set; }
        public DbSet<IntegrationSynchronizationJob> IntegrationSynchronizationJobs { get; set; }

        //Desktop notifications
        public DbSet<UserDesktopNotificationSetting> UserDesktopNotificationSettings { get; set; }
        public DbSet<OpenProjectWorkPackageStatusSnapshot> OpenProjectWorkPackageStatusSnapshots { get; set; }
        public DbSet<DesktopNotification> DesktopNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            //builder.Entity<RequirementNote>()
            //.HasOne(rn => rn.User)
            //.WithMany()
            //.HasForeignKey(rn => rn.UserId)
            //.IsRequired();

            // Converte o Status de int (0, 1, 2) para texto ("Pending", "Synced", "Error") no PostgreSQL
            builder.Entity<TimeEntry>()
                .Property(e => e.SyncStatus)
                .HasConversion<string>();

            builder.Entity<UserWorkScheduleSetting>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.UserId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.LunchIntervalMinutes).IsRequired();
                entity.HasIndex(x => x.UserId).IsUnique();
            });

            builder.Entity<GlobalTimeClockSetting>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ToleranceMinutes).IsRequired();
            });

            builder.Entity<TimeClockPunch>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.UserId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
                entity.Property(x => x.Nsr).HasMaxLength(9);
                entity.Property(x => x.Observation).HasColumnType("text");
                entity.HasIndex(x => new { x.UserId, x.PunchDate, x.Type }).IsUnique();
            });

            builder.Entity<TimeClockUnjustifiedAbsence>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.UserId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(50);
                entity.Property(x => x.Observation).HasColumnType("text");
                entity.HasIndex(x => new { x.UserId, x.AbsenceDate }).IsUnique();
            });

            builder.Entity<TimeClockAbsence>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.UserId).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(80);
                entity.Property(x => x.PeriodType).HasConversion<string>().HasMaxLength(50);
                entity.Property(x => x.Observation).HasColumnType("text");
                entity.HasMany(x => x.Attachments)
                    .WithOne(x => x.Absence)
                    .HasForeignKey(x => x.AbsenceId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<TimeClockAbsenceAttachment>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
                entity.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
                entity.Property(x => x.Content).IsRequired();
            });

            builder.Entity<IntegrationSettings>(entity =>
            {
                entity.ToTable("Integrations");

                entity.HasKey(x => x.Id);

                entity.Property(x => x.Provider)
                    .HasMaxLength(100);

                entity.Property(x => x.Environment)
                    .HasMaxLength(50);

                entity.Property(x => x.BaseUrl)
                    .HasMaxLength(500);

                entity.Property(x => x.PrimaryToken)
                    .HasMaxLength(1000);

                entity.Property(x => x.SecondaryToken)
                    .HasMaxLength(1000);

                entity.Property(x => x.AdditionalSettings)
                    .HasColumnType("text");

                entity.HasIndex(x => new { x.Provider, x.Environment })
                    .IsUnique()
                    .HasDatabaseName("IX_IntegrationSettings_Provider_Environment");
            });

            builder.Entity<GlpiTicketWorkspace>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
                entity.Property(x => x.EntityPath).HasMaxLength(1000);
                entity.Property(x => x.ClientEntityName).HasMaxLength(300);
                entity.Property(x => x.Classification).HasMaxLength(300);
                entity.Property(x => x.TicketPayloadJson).HasColumnType("text");
                entity.Property(x => x.FollowUpsJson).HasColumnType("text");
                entity.Property(x => x.AttachmentsJson).HasColumnType("text");
                entity.Property(x => x.RequirementMarkdown).HasColumnType("text");
                entity.Property(x => x.OpenProjectWorkPackageUrl).HasMaxLength(1000);
                entity.Property(x => x.GlpiDevOpsFieldId).HasMaxLength(100);
                entity.Property(x => x.GlpiDevOpsUrl).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(100).IsRequired();
                entity.HasIndex(x => x.GlpiTicketId).IsUnique();
            });

            builder.Entity<GlpiImprovementTicket>(entity =>
            {
                entity.HasKey(x => x.GlpiTicketId);
                entity.Property(x => x.GlpiTicketId).ValueGeneratedNever();
                entity.Property(x => x.Subject).HasMaxLength(500).IsRequired();
                entity.Property(x => x.GlpiTicketUrl).HasMaxLength(1000);
                entity.Property(x => x.StatusName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.EntityPath).HasMaxLength(1000);
                entity.Property(x => x.ClientEntityName).HasMaxLength(300);
                entity.Property(x => x.WorkPackageUrl).HasMaxLength(1000);
                entity.Property(x => x.WorkPackageStatus).HasMaxLength(200);
                entity.Property(x => x.WorkPackageCreator).HasMaxLength(300);
                entity.HasIndex(x => new { x.IsInImprovementQueue, x.StatusCode, x.OpenedAt });
            });

            builder.Entity<IntegrationSynchronizationJob>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(100);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(50);
                entity.Property(x => x.CorrelationKey).HasMaxLength(200).IsRequired();
                entity.Property(x => x.RequestedByUserId).HasMaxLength(100);
                entity.Property(x => x.LastError).HasColumnType("text");
                entity.HasIndex(x => new { x.Status, x.AvailableAt });
                entity.HasIndex(x => new { x.Type, x.CorrelationKey, x.CreatedAt });
                entity.HasIndex(x => x.WorkspaceId);
            });

            builder.Entity<UserDesktopNotificationSetting>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.UserId).IsUnique();
            });

            builder.Entity<OpenProjectWorkPackageStatusSnapshot>(entity =>
            {
                entity.HasKey(x => x.WorkPackageId);
                entity.Property(x => x.StatusName).HasMaxLength(200).IsRequired();
            });

            builder.Entity<DesktopNotification>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.Property(x => x.WorkPackageSubject).HasMaxLength(500).IsRequired();
                entity.Property(x => x.StatusName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.PreviousStatusName).HasMaxLength(200);
                entity.Property(x => x.ReasonComment).HasMaxLength(1000);
                entity.Property(x => x.WorkPackageUrl).HasMaxLength(1000);
                entity.HasIndex(x => new { x.UserId, x.DeliveredAt });
            });
        }
    }
}
