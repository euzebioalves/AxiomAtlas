using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Axiom.Atlas.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialProductionBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    JobTitle = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    ProfilePicture = table.Column<byte[]>(type: "bytea", nullable: true),
                    ProfilePictureContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    Usuario = table.Column<string>(type: "text", nullable: true),
                    TipoAcao = table.Column<string>(type: "text", nullable: true),
                    Tabela = table.Column<string>(type: "text", nullable: true),
                    ChavePrimaria = table.Column<string>(type: "text", nullable: true),
                    ValoresAntigos = table.Column<string>(type: "text", nullable: true),
                    ValoresNovos = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DesktopNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkPackageId = table.Column<int>(type: "integer", nullable: false),
                    WorkPackageSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreviousStatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReasonComment = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkPackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DesktopNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlobalTimeClockSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ToleranceMinutes = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalTimeClockSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GlpiImprovementTickets",
                columns: table => new
                {
                    GlpiTicketId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GlpiTicketUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    StatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EntityPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientEntityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WorkPackageId = table.Column<int>(type: "integer", nullable: true),
                    WorkPackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    WorkPackageStatus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WorkPackageCreator = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    WorkPackageCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsInImprovementQueue = table.Column<bool>(type: "boolean", nullable: false),
                    LastSynchronizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiImprovementTickets", x => x.GlpiTicketId);
                });

            migrationBuilder.CreateTable(
                name: "GlpiTicketManagement",
                columns: table => new
                {
                    GlpiTicketId = table.Column<long>(type: "bigint", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Priority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Stage = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Classification = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiTicketManagement", x => x.GlpiTicketId);
                });

            migrationBuilder.CreateTable(
                name: "GlpiTicketWorkspaces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GlpiTicketId = table.Column<long>(type: "bigint", nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    EntityPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClientEntityName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Classification = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    TicketPayloadJson = table.Column<string>(type: "text", nullable: false),
                    FollowUpsJson = table.Column<string>(type: "text", nullable: false),
                    AttachmentsJson = table.Column<string>(type: "text", nullable: false),
                    RequirementMarkdown = table.Column<string>(type: "text", nullable: true),
                    OpenProjectWorkPackageId = table.Column<int>(type: "integer", nullable: true),
                    OpenProjectWorkPackageUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    GlpiDevOpsFieldId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    GlpiDevOpsUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiTicketWorkspaces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SecondaryToken = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AdditionalSettings = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationSynchronizationJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CorrelationKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: true),
                    GlpiTicketId = table.Column<long>(type: "bigint", nullable: true),
                    OpenProjectWorkPackageId = table.Column<int>(type: "integer", nullable: true),
                    RequestedByUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationSynchronizationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenProjectWorkPackageStatusSnapshots",
                columns: table => new
                {
                    WorkPackageId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StatusName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenProjectWorkPackageStatusSnapshots", x => x.WorkPackageId);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockAbsences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PeriodType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImportFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImportFileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockAbsences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockPunches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PunchDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PunchTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Nsr = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImportFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImportFileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockPunches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockUnjustifiedAbsences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AbsenceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Observation = table.Column<string>(type: "text", nullable: true),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalRecordId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ExternalUserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ImportFileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ImportFileHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SourceCreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockUnjustifiedAbsences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserDesktopNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserDesktopNotificationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserWorkScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntryTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ExitTime = table.Column<TimeSpan>(type: "interval", nullable: false),
                    LunchIntervalMinutes = table.Column<int>(type: "integer", nullable: false),
                    ShowWorkPackagesInCalendar = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWorkScheduleSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkPackageCaches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    ProjectName = table.Column<string>(type: "text", nullable: true),
                    ProjectIdentifier = table.Column<string>(type: "text", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkPackageCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlpiTicketWorkspaceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlpiTicketWorkspaceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlpiTicketWorkspaceImages_GlpiTicketWorkspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "GlpiTicketWorkspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeClockAbsenceAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AbsenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeClockAbsenceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeClockAbsenceAttachments_TimeClockAbsences_AbsenceId",
                        column: x => x.AbsenceId,
                        principalTable: "TimeClockAbsences",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TimeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    WorkPackageId = table.Column<int>(type: "integer", nullable: false),
                    SpentOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    EndTime = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Hours = table.Column<decimal>(type: "numeric", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    ActivityId = table.Column<int>(type: "integer", nullable: false),
                    SyncStatus = table.Column<string>(type: "text", nullable: false),
                    SyncErrorMessage = table.Column<string>(type: "text", nullable: true),
                    OpenProjectTimeEntryId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TimeEntries_WorkPackageCaches_WorkPackageId",
                        column: x => x.WorkPackageId,
                        principalTable: "WorkPackageCaches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DesktopNotifications_UserId_DeliveredAt",
                table: "DesktopNotifications",
                columns: new[] { "UserId", "DeliveredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GlpiImprovementTickets_IsInImprovementQueue_StatusCode_Open~",
                table: "GlpiImprovementTickets",
                columns: new[] { "IsInImprovementQueue", "StatusCode", "OpenedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_GlpiTicketManagement_AssignedUserId",
                table: "GlpiTicketManagement",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GlpiTicketWorkspaceImages_WorkspaceId",
                table: "GlpiTicketWorkspaceImages",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_GlpiTicketWorkspaces_GlpiTicketId",
                table: "GlpiTicketWorkspaces",
                column: "GlpiTicketId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSettings_Provider_Environment",
                table: "Integrations",
                columns: new[] { "Provider", "Environment" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSynchronizationJobs_Status_AvailableAt",
                table: "IntegrationSynchronizationJobs",
                columns: new[] { "Status", "AvailableAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSynchronizationJobs_Type_CorrelationKey_CreatedAt",
                table: "IntegrationSynchronizationJobs",
                columns: new[] { "Type", "CorrelationKey", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationSynchronizationJobs_WorkspaceId",
                table: "IntegrationSynchronizationJobs",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockAbsenceAttachments_AbsenceId",
                table: "TimeClockAbsenceAttachments",
                column: "AbsenceId");

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockAbsences_UserId_ExternalRecordId",
                table: "TimeClockAbsences",
                columns: new[] { "UserId", "ExternalRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockPunches_UserId_ExternalRecordId",
                table: "TimeClockPunches",
                columns: new[] { "UserId", "ExternalRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockPunches_UserId_PunchDate_Type",
                table: "TimeClockPunches",
                columns: new[] { "UserId", "PunchDate", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockUnjustifiedAbsences_UserId_AbsenceDate",
                table: "TimeClockUnjustifiedAbsences",
                columns: new[] { "UserId", "AbsenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeClockUnjustifiedAbsences_UserId_ExternalRecordId",
                table: "TimeClockUnjustifiedAbsences",
                columns: new[] { "UserId", "ExternalRecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TimeEntries_WorkPackageId",
                table: "TimeEntries",
                column: "WorkPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_UserDesktopNotificationSettings_UserId",
                table: "UserDesktopNotificationSettings",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserWorkScheduleSettings_UserId",
                table: "UserWorkScheduleSettings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "DesktopNotifications");

            migrationBuilder.DropTable(
                name: "GlobalTimeClockSettings");

            migrationBuilder.DropTable(
                name: "GlpiImprovementTickets");

            migrationBuilder.DropTable(
                name: "GlpiTicketManagement");

            migrationBuilder.DropTable(
                name: "GlpiTicketWorkspaceImages");

            migrationBuilder.DropTable(
                name: "Integrations");

            migrationBuilder.DropTable(
                name: "IntegrationSynchronizationJobs");

            migrationBuilder.DropTable(
                name: "OpenProjectWorkPackageStatusSnapshots");

            migrationBuilder.DropTable(
                name: "TimeClockAbsenceAttachments");

            migrationBuilder.DropTable(
                name: "TimeClockPunches");

            migrationBuilder.DropTable(
                name: "TimeClockUnjustifiedAbsences");

            migrationBuilder.DropTable(
                name: "TimeEntries");

            migrationBuilder.DropTable(
                name: "UserDesktopNotificationSettings");

            migrationBuilder.DropTable(
                name: "UserWorkScheduleSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "GlpiTicketWorkspaces");

            migrationBuilder.DropTable(
                name: "TimeClockAbsences");

            migrationBuilder.DropTable(
                name: "WorkPackageCaches");
        }
    }
}
