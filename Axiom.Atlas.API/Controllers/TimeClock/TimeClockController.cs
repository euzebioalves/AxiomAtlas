using Axiom.Atlas.Application.DTOs.TimeClock;
using Axiom.Atlas.Application.Services.TimeClock;
using Axiom.Atlas.Domain.Entities.TimeEntries;
using Axiom.Atlas.Domain.Entities.TimeClock;
using Axiom.Atlas.Domain.Entities.Users;
using Axiom.Atlas.Domain.Enums;
using Axiom.Atlas.Infrastructure.Services.TimeEntries;
using Axiom.Atlas.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Axiom.Atlas.API.Controllers.TimeClock
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AxiomAtlasPolicy")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class TimeClockController : ControllerBase
    {
        private static readonly TimeClockPunchType[] PunchSequence =
        [
            TimeClockPunchType.MorningEntry,
            TimeClockPunchType.MorningExit,
            TimeClockPunchType.AfternoonEntry,
            TimeClockPunchType.AfternoonExit
        ];

        private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

        private readonly AppDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly TimeClockCsvImportParser _csvImportParser;
        private readonly TimeClockAbsenceCsvImportParser _absenceCsvImportParser;
        private readonly OpenProjectService _openProjectService;

        public TimeClockController(
            AppDbContext context,
            UserManager<User> userManager,
            TimeClockCsvImportParser csvImportParser,
            TimeClockAbsenceCsvImportParser absenceCsvImportParser,
            OpenProjectService openProjectService)
        {
            _context = context;
            _userManager = userManager;
            _csvImportParser = csvImportParser;
            _absenceCsvImportParser = absenceCsvImportParser;
            _openProjectService = openProjectService;
        }

        [HttpGet("settings/user")]
        public async Task<IActionResult> GetUserSettings()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            var setting = await GetOrCreateUserScheduleAsync(userId);
            return Ok(MapSchedule(setting));
        }

        [HttpPut("settings/user")]
        public async Task<IActionResult> SaveUserSettings([FromBody] SaveUserWorkScheduleSettingRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            if (!TryParseTime(request.EntryTime, out var entryTime) ||
                !TryParseTime(request.ExitTime, out var exitTime))
            {
                return BadRequest(new { message = "Horário de entrada ou saída inválido." });
            }

            if (request.LunchIntervalMinutes < 0 || request.LunchIntervalMinutes > 240)
            {
                return BadRequest(new { message = "A duração do intervalo precisa estar entre 0 e 240 minutos." });
            }

            if (CalculateMinutesBetween(entryTime, exitTime) <= request.LunchIntervalMinutes)
            {
                return BadRequest(new { message = "A jornada diária precisa ser maior que o intervalo de almoço." });
            }

            var setting = await _context.UserWorkScheduleSettings
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (setting == null)
            {
                setting = new UserWorkScheduleSetting
                {
                    UserId = userId,
                    EntryTime = entryTime,
                    ExitTime = exitTime,
                    LunchIntervalMinutes = request.LunchIntervalMinutes
                };

                _context.UserWorkScheduleSettings.Add(setting);
            }
            else
            {
                setting.EntryTime = entryTime;
                setting.ExitTime = exitTime;
                setting.LunchIntervalMinutes = request.LunchIntervalMinutes;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(MapSchedule(setting));
        }

        [HttpPut("settings/calendar-preferences")]
        public async Task<IActionResult> SaveCalendarPreferences([FromBody] SaveTimeClockCalendarPreferenceRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            var setting = await GetOrCreateUserScheduleAsync(userId);
            setting.ShowWorkPackagesInCalendar = request.ShowWorkPackagesInCalendar;
            setting.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { setting.ShowWorkPackagesInCalendar });
        }

        [HttpGet("settings/global")]
        public async Task<IActionResult> GetGlobalSettings()
        {
            if (!await IsCurrentUserAdminAsync())
            {
                return Forbid();
            }

            var setting = await GetOrCreateGlobalSettingsAsync();
            return Ok(new GlobalTimeClockSettingDto { ToleranceMinutes = setting.ToleranceMinutes });
        }

        [HttpPut("settings/global")]
        public async Task<IActionResult> SaveGlobalSettings([FromBody] SaveGlobalTimeClockSettingRequest request)
        {
            if (!await IsCurrentUserAdminAsync())
            {
                return Forbid();
            }

            if (request.ToleranceMinutes < 0 || request.ToleranceMinutes > 120)
            {
                return BadRequest(new { message = "A tolerância precisa estar entre 0 e 120 minutos." });
            }

            var setting = await GetOrCreateGlobalSettingsAsync();
            setting.ToleranceMinutes = request.ToleranceMinutes;
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new GlobalTimeClockSettingDto { ToleranceMinutes = setting.ToleranceMinutes });
        }

        [HttpGet("calendar")]
        public async Task<IActionResult> GetCalendar([FromQuery] int year, [FromQuery] int month)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            if (!IsValidYearMonth(year, month))
            {
                return BadRequest(new { message = "Mês ou ano inválido." });
            }

            var calendar = await BuildCalendarAsync(userId, year, month);
            return Ok(calendar);
        }

        [HttpPost("punches")]
        public async Task<IActionResult> SavePunches([FromBody] SaveTimeClockPunchesRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            if (!TryParseDate(request.Date, out var punchDate))
            {
                return BadRequest(new { message = "Data inválida para o registro de ponto." });
            }

            var existingPunches = await _context.TimeClockPunches
                .Where(x => x.UserId == userId && x.PunchDate == punchDate)
                .ToListAsync();

            if (request.Punches.Count == 0)
            {
                if (existingPunches.Count == 0)
                {
                    return BadRequest(new { message = "Não há registros de ponto para remover nesta data." });
                }

                _context.TimeClockPunches.RemoveRange(existingPunches);
                await _context.SaveChangesAsync();

                return Ok(await BuildDayAsync(userId, punchDate));
            }

            var requestedTypes = new HashSet<TimeClockPunchType>();
            var parsedPunches = new List<(SaveTimeClockPunchRequest Request, TimeClockPunchType Type, TimeSpan Time)>();

            foreach (var punch in request.Punches)
            {
                if (!TryParsePunchType(punch.Type, out var type))
                {
                    return BadRequest(new { message = $"Tipo de registro inválido: {punch.Type}" });
                }

                if (!requestedTypes.Add(type))
                {
                    return BadRequest(new { message = "Não é permitido repetir o mesmo tipo de registro no mesmo dia." });
                }

                if (!TryParseTime(punch.Time, out var time))
                {
                    return BadRequest(new { message = $"Horário inválido para {GetPunchTypeLabel(type)}." });
                }

                if (!string.IsNullOrWhiteSpace(punch.Nsr) && !Regex.IsMatch(punch.Nsr.Trim(), "^\\d{9}$"))
                {
                    return BadRequest(new { message = "O NSR deve conter exatamente 9 dígitos numéricos." });
                }

                parsedPunches.Add((punch, type, time));
            }

            var removedPunches = existingPunches
                .Where(x => !requestedTypes.Contains(x.Type))
                .ToList();

            if (removedPunches.Count > 0)
            {
                _context.TimeClockPunches.RemoveRange(removedPunches);
            }

            foreach (var parsed in parsedPunches)
            {
                var entity = existingPunches.FirstOrDefault(x => x.Type == parsed.Type);

                if (entity == null)
                {
                    entity = new TimeClockPunch
                    {
                        UserId = userId,
                        PunchDate = punchDate,
                        Type = parsed.Type
                    };

                    _context.TimeClockPunches.Add(entity);
                }

                entity.PunchTime = parsed.Time;
                entity.Nsr = string.IsNullOrWhiteSpace(parsed.Request.Nsr) ? null : parsed.Request.Nsr.Trim();
                entity.Observation = string.IsNullOrWhiteSpace(parsed.Request.Observation) ? null : parsed.Request.Observation.Trim();
                entity.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            var day = await BuildDayAsync(userId, punchDate);
            return Ok(day);
        }

        [HttpDelete("punches/{id:guid}")]
        public async Task<IActionResult> DeletePunch(Guid id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            var punch = await _context.TimeClockPunches
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

            if (punch == null)
            {
                return NotFound();
            }

            _context.TimeClockPunches.Remove(punch);
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("punches/import")]
        [RequestSizeLimit(25_000_000)]
        public async Task<IActionResult> ImportPunches([FromForm] IFormFile? file, [FromForm] bool replaceExisting)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            if (!replaceExisting)
            {
                return BadRequest(new { message = "Confirme a substituição dos registros atuais para continuar a importação." });
            }

            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "Selecione um arquivo CSV com os registros de ponto." });
            }

            if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "O arquivo informado precisa estar no formato CSV." });
            }

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            var parsed = _csvImportParser.Parse(stream.ToArray(), file.FileName);
            if (parsed.Errors.Count > 0)
            {
                return BadRequest(new
                {
                    message = "O arquivo possui inconsistências e nenhum registro foi alterado.",
                    errors = parsed.Errors.Select(error => new { error.Line, error.Message })
                });
            }

            if (parsed.Rows.Count == 0 || parsed.DateStart is null || parsed.DateEnd is null || string.IsNullOrWhiteSpace(parsed.ExternalUserId))
            {
                return BadRequest(new { message = "O arquivo não contém registros de ponto válidos para importar." });
            }

            var importedAt = DateTime.UtcNow;
            var importBatchId = Guid.NewGuid();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            var existingPunches = await _context.TimeClockPunches
                .Where(x => x.UserId == userId)
                .ToListAsync();
            _context.TimeClockPunches.RemoveRange(existingPunches);
            await _context.SaveChangesAsync();

            var importedPunches = parsed.Rows.Select(row => new TimeClockPunch
            {
                UserId = userId,
                PunchDate = DateTime.SpecifyKind(row.PunchDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                PunchTime = row.PunchTime.ToTimeSpan(),
                Type = row.Type,
                Nsr = row.Nsr,
                Observation = row.Observation,
                ImportBatchId = importBatchId,
                ExternalRecordId = row.ExternalRecordId,
                ExternalUserId = row.ExternalUserId,
                ImportFileName = parsed.FileName,
                ImportFileHash = parsed.FileHash,
                SourceCreatedAt = row.SourceCreatedAt,
                SourceUpdatedAt = row.SourceUpdatedAt,
                ImportedAt = importedAt,
                CreatedAt = importedAt,
                UpdatedAt = importedAt
            }).ToList();

            _context.TimeClockPunches.AddRange(importedPunches);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new TimeClockImportResultDto
            {
                ImportedPunches = importedPunches.Count,
                ReplacedPunches = existingPunches.Count,
                DateStart = parsed.DateStart.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateEnd = parsed.DateEnd.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ExternalUserId = parsed.ExternalUserId
            });
        }

        [HttpPost("unjustified-absence")]
        public async Task<IActionResult> SaveUnjustifiedAbsence([FromBody] SaveTimeClockUnjustifiedAbsenceRequest request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            if (!TryParseDate(request.Date, out var absenceDate))
            {
                return BadRequest(new { message = "Data inválida para a falta." });
            }

            if (!TryParseUnjustifiedType(request.Type, out var type))
            {
                return BadRequest(new { message = "Tipo de falta inválido." });
            }

            var hasFullDayJustifiedAbsence = await _context.TimeClockAbsences
                .AnyAsync(x => x.UserId == userId &&
                               x.PeriodType == TimeClockAbsencePeriodType.FullDay &&
                               x.StartDate <= absenceDate &&
                               x.EndDate >= absenceDate);

            if (hasFullDayJustifiedAbsence)
            {
                return BadRequest(new { message = "Esta data já possui uma ausência justificada integral e não pode receber falta sem justificativa." });
            }

            TimeSpan? startTime = null;
            TimeSpan? endTime = null;

            if (type == TimeClockUnjustifiedAbsenceType.Partial)
            {
                if (!TryParseTime(request.StartTime, out var parsedStart) ||
                    !TryParseTime(request.EndTime, out var parsedEnd) ||
                    parsedEnd <= parsedStart)
                {
                    return BadRequest(new { message = "Para falta parcial, informe início e fim válidos." });
                }

                startTime = parsedStart;
                endTime = parsedEnd;
            }

            var entity = await _context.TimeClockUnjustifiedAbsences
                .FirstOrDefaultAsync(x => x.UserId == userId && x.AbsenceDate == absenceDate);

            if (entity == null)
            {
                entity = new TimeClockUnjustifiedAbsence
                {
                    UserId = userId,
                    AbsenceDate = absenceDate
                };

                _context.TimeClockUnjustifiedAbsences.Add(entity);
            }

            entity.Type = type;
            entity.StartTime = startTime;
            entity.EndTime = endTime;
            entity.Observation = string.IsNullOrWhiteSpace(request.Observation) ? null : request.Observation.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var day = await BuildDayAsync(userId, absenceDate);
            return Ok(day);
        }

        [HttpDelete("unjustified-absence/{id:guid}")]
        public async Task<IActionResult> DeleteUnjustifiedAbsence(Guid id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            var absence = await _context.TimeClockUnjustifiedAbsences
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

            if (absence == null)
            {
                return NotFound(new { message = "Falta sem justificativa não encontrada." });
            }

            _context.TimeClockUnjustifiedAbsences.Remove(absence);
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpGet("absences/management")]
        public async Task<IActionResult> GetAbsenceManagement()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { message = "Usuário não identificado no token." });

            var absences = await _context.TimeClockAbsences
                .Include(x => x.Attachments)
                .Where(x => x.UserId == userId)
                .ToListAsync();
            var unjustifiedAbsences = await _context.TimeClockUnjustifiedAbsences
                .Where(x => x.UserId == userId)
                .ToListAsync();

            var items = absences.Select(MapManagementAbsence)
                .Concat(unjustifiedAbsences.Select(MapManagementUnjustifiedAbsence))
                .OrderByDescending(x => x.StartDate)
                .ThenByDescending(x => x.StartTime)
                .ToList();
            return Ok(items);
        }

        [HttpGet("absences/{absenceId:guid}/attachments/{attachmentId:guid}")]
        public async Task<IActionResult> DownloadAbsenceAttachment(Guid absenceId, Guid attachmentId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { message = "Usuário não identificado no token." });

            var attachment = await _context.TimeClockAbsenceAttachments
                .Include(x => x.Absence)
                .FirstOrDefaultAsync(x => x.Id == attachmentId && x.AbsenceId == absenceId && x.Absence!.UserId == userId);
            if (attachment == null) return NotFound(new { message = "Anexo não encontrado." });
            return File(attachment.Content, attachment.ContentType, attachment.FileName);
        }

        [HttpPost("absences/import")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> ImportAbsences(
            [FromForm] IFormFile? csvFile,
            [FromForm] IFormFileCollection attachments,
            [FromForm] bool replaceExisting)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized(new { message = "Usuário não identificado no token." });
            if (!replaceExisting) return BadRequest(new { message = "Confirme a substituição das ausências atuais para continuar." });
            if (csvFile is null || csvFile.Length == 0 || !Path.GetExtension(csvFile.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Selecione o arquivo CSV de ausências." });

            await using var csvStream = new MemoryStream();
            await csvFile.CopyToAsync(csvStream);
            var parsed = _absenceCsvImportParser.Parse(csvStream.ToArray(), csvFile.FileName);
            if (parsed.Errors.Count > 0)
            {
                return BadRequest(new
                {
                    message = "O arquivo possui inconsistências e nenhuma ausência foi alterada.",
                    errors = parsed.Errors.Select(error => new { error.Line, error.Message })
                });
            }

            if (parsed.Rows.Count == 0 || string.IsNullOrWhiteSpace(parsed.ExternalUserId))
                return BadRequest(new { message = "O arquivo não contém ausências válidas para importar." });

            var attachmentFiles = attachments.ToList();
            foreach (var row in parsed.Rows.Where(x => !string.IsNullOrWhiteSpace(x.AttachmentName)))
            {
                var expectedName = $"{row.ExternalRecordId}_{row.AttachmentName}";
                if (!attachmentFiles.Any(file => MatchesImportedAttachment(file, expectedName, row.AttachmentName!)))
                    parsed.Errors.Add(new TimeClockCsvIssue(row.SourceLine, $"Anexo não encontrado na pasta selecionada: {row.AttachmentName}."));
            }
            if (parsed.Errors.Count > 0)
            {
                var receivedFileNames = attachmentFiles.Count == 0
                    ? "nenhum arquivo"
                    : string.Join(", ", attachmentFiles.Select(file => file.FileName));
                return BadRequest(new
                {
                    message = $"Os anexos não correspondem ao CSV e nenhuma ausência foi alterada. Arquivos recebidos: {receivedFileNames}.",
                    errors = parsed.Errors.Select(error => new { error.Line, error.Message })
                });
            }

            var importedAt = DateTime.UtcNow;
            var importBatchId = Guid.NewGuid();
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var existingAbsences = await _context.TimeClockAbsences.Where(x => x.UserId == userId).ToListAsync();
            var existingUnjustifiedAbsences = await _context.TimeClockUnjustifiedAbsences.Where(x => x.UserId == userId).ToListAsync();
            _context.TimeClockAbsences.RemoveRange(existingAbsences);
            _context.TimeClockUnjustifiedAbsences.RemoveRange(existingUnjustifiedAbsences);
            await _context.SaveChangesAsync();

            var importedAbsences = new List<TimeClockAbsence>();
            var importedUnjustifiedAbsences = new List<TimeClockUnjustifiedAbsence>();
            var importedAttachments = 0;
            foreach (var row in parsed.Rows)
            {
                if (row.IsUnjustified)
                {
                    importedUnjustifiedAbsences.Add(new TimeClockUnjustifiedAbsence
                    {
                        UserId = userId,
                        AbsenceDate = DateTime.SpecifyKind(row.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                        Type = row.PeriodType == TimeClockAbsencePeriodType.FullDay ? TimeClockUnjustifiedAbsenceType.FullDay : TimeClockUnjustifiedAbsenceType.Partial,
                        StartTime = row.StartTime?.ToTimeSpan(), EndTime = row.EndTime?.ToTimeSpan(), Observation = row.Observation,
                        ImportBatchId = importBatchId, ExternalRecordId = row.ExternalRecordId, ExternalUserId = row.ExternalUserId,
                        ImportFileName = parsed.FileName, ImportFileHash = parsed.FileHash, SourceCreatedAt = row.SourceCreatedAt,
                        SourceUpdatedAt = row.SourceUpdatedAt, ImportedAt = importedAt, CreatedAt = importedAt, UpdatedAt = importedAt
                    });
                    continue;
                }

                var absence = new TimeClockAbsence
                {
                    UserId = userId, Type = row.Type!.Value, PeriodType = row.PeriodType,
                    StartDate = DateTime.SpecifyKind(row.StartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                    EndDate = DateTime.SpecifyKind(row.EndDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
                    StartTime = row.StartTime?.ToTimeSpan(), EndTime = row.EndTime?.ToTimeSpan(), Observation = row.Observation,
                    ImportBatchId = importBatchId, ExternalRecordId = row.ExternalRecordId, ExternalUserId = row.ExternalUserId,
                    ImportFileName = parsed.FileName, ImportFileHash = parsed.FileHash, SourceCreatedAt = row.SourceCreatedAt,
                    SourceUpdatedAt = row.SourceUpdatedAt, ImportedAt = importedAt, CreatedAt = importedAt
                };
                if (!string.IsNullOrWhiteSpace(row.AttachmentName))
                {
                    var expectedName = $"{row.ExternalRecordId}_{row.AttachmentName}";
                    var file = attachmentFiles.Single(x => MatchesImportedAttachment(x, expectedName, row.AttachmentName!));
                    await using var attachmentStream = new MemoryStream();
                    await file.CopyToAsync(attachmentStream);
                    absence.Attachments.Add(new TimeClockAbsenceAttachment
                    {
                        FileName = row.AttachmentName, ContentType = row.AttachmentContentType ?? file.ContentType ?? "application/octet-stream",
                        Size = file.Length, Content = attachmentStream.ToArray()
                    });
                    importedAttachments++;
                }
                importedAbsences.Add(absence);
            }

            _context.TimeClockAbsences.AddRange(importedAbsences);
            _context.TimeClockUnjustifiedAbsences.AddRange(importedUnjustifiedAbsences);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new TimeClockAbsenceImportResultDto
            {
                ImportedAbsences = importedAbsences.Count, ImportedUnjustifiedAbsences = importedUnjustifiedAbsences.Count,
                ImportedAttachments = importedAttachments, ReplacedAbsences = existingAbsences.Count,
                ReplacedUnjustifiedAbsences = existingUnjustifiedAbsences.Count
            });
        }

        [HttpPost("absences")]
        [RequestSizeLimit(50_000_000)]
        public async Task<IActionResult> SaveAbsence(
            [FromForm] string type,
            [FromForm] string periodType,
            [FromForm] string startDate,
            [FromForm] string endDate,
            [FromForm] string? startTime,
            [FromForm] string? endTime,
            [FromForm] string? observation,
            [FromForm] string? userId)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(currentUserId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            var targetUserId = currentUserId;
            if (!string.IsNullOrWhiteSpace(userId) && await IsCurrentUserAdminAsync())
            {
                targetUserId = userId;
            }

            if (!TryParseAbsenceType(type, out var absenceType))
            {
                return BadRequest(new { message = "Tipo de ausência inválido." });
            }

            if (!TryParseAbsencePeriodType(periodType, out var parsedPeriodType))
            {
                return BadRequest(new { message = "Período de ausência inválido." });
            }

            if (!TryParseDate(startDate, out var parsedStartDate) ||
                !TryParseDate(endDate, out var parsedEndDate) ||
                parsedEndDate < parsedStartDate)
            {
                return BadRequest(new { message = "Período de datas inválido." });
            }

            TimeSpan? parsedStartTime = null;
            TimeSpan? parsedEndTime = null;

            if (parsedPeriodType == TimeClockAbsencePeriodType.Partial)
            {
                if (parsedStartDate != parsedEndDate)
                {
                    return BadRequest(new { message = "Ausência parcial deve ocorrer em uma única data." });
                }

                if (!TryParseTime(startTime, out var start) ||
                    !TryParseTime(endTime, out var end) ||
                    end <= start)
                {
                    return BadRequest(new { message = "Para ausência parcial, informe início e fim válidos." });
                }

                parsedStartTime = start;
                parsedEndTime = end;
            }

            var absence = new TimeClockAbsence
            {
                UserId = targetUserId,
                Type = absenceType,
                PeriodType = parsedPeriodType,
                StartDate = parsedStartDate,
                EndDate = parsedEndDate,
                StartTime = parsedStartTime,
                EndTime = parsedEndTime,
                Observation = string.IsNullOrWhiteSpace(observation) ? null : observation.Trim()
            };

            foreach (var file in Request.Form.Files)
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);

                absence.Attachments.Add(new TimeClockAbsenceAttachment
                {
                    FileName = Path.GetFileName(file.FileName),
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    Size = file.Length,
                    Content = memoryStream.ToArray()
                });
            }

            _context.TimeClockAbsences.Add(absence);

            if (parsedPeriodType == TimeClockAbsencePeriodType.FullDay)
            {
                var conflictingUnjustifiedAbsences = await _context.TimeClockUnjustifiedAbsences
                    .Where(x => x.UserId == targetUserId &&
                                x.AbsenceDate >= parsedStartDate &&
                                x.AbsenceDate <= parsedEndDate)
                    .ToListAsync();

                if (conflictingUnjustifiedAbsences.Count > 0)
                {
                    _context.TimeClockUnjustifiedAbsences.RemoveRange(conflictingUnjustifiedAbsences);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(MapAbsence(absence));
        }

        [HttpGet("audit")]
        public async Task<IActionResult> GetAudit([FromQuery] int year, [FromQuery] int month)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(new { message = "Usuário não identificado no token." });
            }

            if (!IsValidYearMonth(year, month))
            {
                return BadRequest(new { message = "Mês ou ano inválido." });
            }

            var calendar = await BuildCalendarAsync(userId, year, month);
            var entries = calendar.Days
                .Where(x => x.Punches.Count > 0 || x.Absences.Count > 0 || x.UnjustifiedAbsence != null || x.BalanceMinutes != 0)
                .Select(day => new TimeClockAuditEntryDto
                {
                    Date = day.Date,
                    Description = BuildAuditDescription(day),
                    SourceType = day.UnjustifiedAbsence != null ? "Falta" : day.Absences.Count > 0 ? "Ausência" : "Ponto",
                    Punches = day.Punches,
                    Absences = day.Absences,
                    UnjustifiedAbsence = day.UnjustifiedAbsence,
                    WorkedMinutes = day.WorkedMinutes,
                    ExpectedMinutes = day.ExpectedMinutes,
                    BalanceMinutes = day.BalanceMinutes,
                    WorkedLabel = day.WorkedLabel,
                    ExpectedLabel = day.ExpectedLabel,
                    BalanceLabel = day.BalanceLabel
                })
                .OrderBy(x => x.Date)
                .ToList();

            var balanceMinutes = calendar.Days.Sum(x => x.BalanceMinutes);

            return Ok(new TimeClockAuditDto
            {
                Year = year,
                Month = month,
                BalanceMinutes = balanceMinutes,
                BalanceLabel = FormatSignedMinutes(balanceMinutes),
                Entries = entries
            });
        }

        private async Task<TimeClockCalendarDto> BuildCalendarAsync(string userId, int year, int month)
        {
            var monthStart = DateTime.SpecifyKind(new DateTime(year, month, 1), DateTimeKind.Utc);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            var schedule = await GetOrCreateUserScheduleAsync(userId);
            var globalSettings = await GetOrCreateGlobalSettingsAsync();

            var punches = await _context.TimeClockPunches
                .Where(x => x.UserId == userId && x.PunchDate >= monthStart && x.PunchDate <= monthEnd)
                .ToListAsync();

            var unjustifiedAbsences = await _context.TimeClockUnjustifiedAbsences
                .Where(x => x.UserId == userId && x.AbsenceDate >= monthStart && x.AbsenceDate <= monthEnd)
                .ToListAsync();

            var absences = await _context.TimeClockAbsences
                .Include(x => x.Attachments)
                .Where(x => x.UserId == userId && x.StartDate <= monthEnd && x.EndDate >= monthStart)
                .ToListAsync();

            var timeEntries = await _context.Set<TimeEntry>()
                .Include(x => x.WorkPackage)
                .Where(x => x.UserId == userId && x.SpentOn >= monthStart && x.SpentOn < monthStart.AddMonths(1))
                .ToListAsync();
            var openProjectBaseUrl = await _openProjectService.GetActiveOpenProjectBaseUrlAsync();
            var workPackagesByDate = timeEntries
                .GroupBy(entry => DateOnly.FromDateTime(entry.SpentOn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .GroupBy(entry => entry.WorkPackageId)
                        .Select(entries =>
                        {
                            var workPackage = entries.First().WorkPackage;
                            return new TimeClockWorkPackageDto
                            {
                                Id = entries.Key,
                                Subject = workPackage?.Subject,
                                Url = OpenProjectService.BuildWorkPackageWebUrl(openProjectBaseUrl, entries.Key, workPackage?.ProjectIdentifier),
                                SyncStatus = entries.Any(entry => entry.SyncStatus == Axiom.Atlas.Domain.Enums.SyncStatus.Error)
                                    ? "error"
                                    : entries.Any(entry => entry.SyncStatus == Axiom.Atlas.Domain.Enums.SyncStatus.Pending)
                                        ? "pending"
                                        : "synced"
                            };
                        })
                        .OrderBy(workPackage => workPackage.Id)
                        .ToList());
            var workPackageHoursByDate = timeEntries
                .GroupBy(entry => DateOnly.FromDateTime(entry.SpentOn).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToDictionary(group => group.Key, group => group.Sum(entry => entry.Hours));

            var days = Enumerable.Range(1, DateTime.DaysInMonth(year, month))
                .Select(day =>
                {
                    var date = DateTime.SpecifyKind(new DateTime(year, month, day), DateTimeKind.Utc);
                    var calendarDay = BuildDay(
                        date,
                        schedule,
                        globalSettings.ToleranceMinutes,
                        punches,
                        absences,
                        unjustifiedAbsences);
                    calendarDay.WorkPackages = workPackagesByDate.GetValueOrDefault(FormatDate(date), []);
                    calendarDay.WorkPackageHours = workPackageHoursByDate.GetValueOrDefault(FormatDate(date));
                    return calendarDay;
                })
                .ToList();

            var accountedDays = days.Where(x => !x.IsFuture).ToList();
            var summary = new TimeClockSummaryDto
            {
                WorkedMinutes = accountedDays.Sum(x => x.WorkedMinutes),
                ExpectedMinutes = accountedDays.Sum(x => x.ExpectedMinutes),
                BalanceMinutes = accountedDays.Sum(x => x.BalanceMinutes),
                AbsenceDays = accountedDays.Count(x => x.Absences.Count > 0),
                UnjustifiedAbsenceDays = accountedDays.Count(x => x.UnjustifiedAbsence != null)
            };

            summary.WorkedLabel = FormatMinutes(summary.WorkedMinutes);
            summary.ExpectedLabel = FormatMinutes(summary.ExpectedMinutes);
            summary.BalanceLabel = FormatSignedMinutes(summary.BalanceMinutes);
            summary.AccumulatedBalanceMinutes = await CalculateAccumulatedBalanceAsync(
                userId,
                schedule,
                globalSettings.ToleranceMinutes,
                GetBusinessToday());
            summary.AccumulatedBalanceLabel = FormatSignedMinutes(summary.AccumulatedBalanceMinutes);

            return new TimeClockCalendarDto
            {
                Year = year,
                Month = month,
                MonthLabel = monthStart.ToString("MMMM yyyy", new CultureInfo("pt-BR")),
                Schedule = MapSchedule(schedule),
                GlobalSettings = new GlobalTimeClockSettingDto { ToleranceMinutes = globalSettings.ToleranceMinutes },
                Days = days,
                Summary = summary
            };
        }

        private async Task<int> CalculateAccumulatedBalanceAsync(
            string userId,
            UserWorkScheduleSetting schedule,
            int toleranceMinutes,
            DateTime throughDate)
        {
            var throughDateUtc = DateTime.SpecifyKind(throughDate.Date, DateTimeKind.Utc);
            var firstPunchDate = await _context.TimeClockPunches
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.PunchDate)
                .Select(x => (DateTime?)x.PunchDate)
                .FirstOrDefaultAsync();
            var firstAbsenceDate = await _context.TimeClockAbsences
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.StartDate)
                .Select(x => (DateTime?)x.StartDate)
                .FirstOrDefaultAsync();
            var firstUnjustifiedAbsenceDate = await _context.TimeClockUnjustifiedAbsences
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.AbsenceDate)
                .Select(x => (DateTime?)x.AbsenceDate)
                .FirstOrDefaultAsync();

            var startDates = new[] { firstPunchDate, firstAbsenceDate, firstUnjustifiedAbsenceDate }
                .Where(x => x.HasValue)
                .Select(x => x!.Value.Date)
                .ToList();
            if (startDates.Count == 0)
            {
                return 0;
            }

            var startDate = startDates.Min();
            if (startDate > throughDateUtc)
            {
                return 0;
            }

            var punches = await _context.TimeClockPunches
                .Where(x => x.UserId == userId && x.PunchDate >= startDate && x.PunchDate <= throughDateUtc)
                .ToListAsync();
            var absences = await _context.TimeClockAbsences
                .Include(x => x.Attachments)
                .Where(x => x.UserId == userId && x.StartDate <= throughDateUtc && x.EndDate >= startDate)
                .ToListAsync();
            var unjustifiedAbsences = await _context.TimeClockUnjustifiedAbsences
                .Where(x => x.UserId == userId && x.AbsenceDate >= startDate && x.AbsenceDate <= throughDateUtc)
                .ToListAsync();

            var totalDays = (throughDateUtc.Date - startDate.Date).Days + 1;
            return Enumerable.Range(0, totalDays)
                .Select(offset => BuildDay(
                    startDate.AddDays(offset),
                    schedule,
                    toleranceMinutes,
                    punches,
                    absences,
                    unjustifiedAbsences))
                .Sum(day => day.BalanceMinutes);
        }

        private async Task<TimeClockDayDto> BuildDayAsync(string userId, DateTime date)
        {
            var schedule = await GetOrCreateUserScheduleAsync(userId);
            var globalSettings = await GetOrCreateGlobalSettingsAsync();

            var punches = await _context.TimeClockPunches
                .Where(x => x.UserId == userId && x.PunchDate == date)
                .ToListAsync();

            var unjustifiedAbsences = await _context.TimeClockUnjustifiedAbsences
                .Where(x => x.UserId == userId && x.AbsenceDate == date)
                .ToListAsync();

            var absences = await _context.TimeClockAbsences
                .Include(x => x.Attachments)
                .Where(x => x.UserId == userId && x.StartDate <= date && x.EndDate >= date)
                .ToListAsync();

            return BuildDay(date, schedule, globalSettings.ToleranceMinutes, punches, absences, unjustifiedAbsences);
        }

        private static TimeClockDayDto BuildDay(
            DateTime date,
            UserWorkScheduleSetting schedule,
            int toleranceMinutes,
            List<TimeClockPunch> monthPunches,
            List<TimeClockAbsence> monthAbsences,
            List<TimeClockUnjustifiedAbsence> monthUnjustifiedAbsences)
        {
            var dayPunches = monthPunches
                .Where(x => x.PunchDate.Date == date.Date)
                .OrderBy(x => GetPunchTypeSequence(x.Type))
                .ThenBy(x => x.PunchTime)
                .ToList();

            var dayAbsences = monthAbsences
                .Where(x => x.StartDate.Date <= date.Date && x.EndDate.Date >= date.Date)
                .ToList();

            var unjustified = monthUnjustifiedAbsences
                .FirstOrDefault(x => x.AbsenceDate.Date == date.Date);

            var isFuture = date.Date > GetBusinessToday();
            var hasTimeClockEvent = dayPunches.Count > 0 || dayAbsences.Count > 0 || unjustified != null;
            var expectedBaseMinutes = IsWeekend(date) || !hasTimeClockEvent
                ? 0
                : Math.Max(0, CalculateMinutesBetween(schedule.EntryTime, schedule.ExitTime) - schedule.LunchIntervalMinutes);

            var hasFullDayJustifiedAbsence = dayAbsences.Any(x => x.PeriodType == TimeClockAbsencePeriodType.FullDay);
            // A justified full-day absence always takes precedence over a prior absence flag.
            if (hasFullDayJustifiedAbsence)
            {
                unjustified = null;
            }

            var workedMinutes = CalculateWorkedMinutes(dayPunches);
            var lunchMinutes = CalculateLunchMinutes(dayPunches);
            var absenceCoverageMinutes = CalculateJustifiedAbsenceCoverageMinutes(dayAbsences, expectedBaseMinutes);
            var expectedMinutes = Math.Max(0, expectedBaseMinutes - absenceCoverageMinutes);
            var balanceMinutes = workedMinutes - expectedMinutes;

            if (hasFullDayJustifiedAbsence)
            {
                balanceMinutes = 0;
                workedMinutes = 0;
                expectedMinutes = 0;
                lunchMinutes = 0;
            }

            var unjustifiedDto = unjustified == null
                ? null
                : MapUnjustifiedAbsence(unjustified, expectedBaseMinutes);

            if (unjustified != null)
            {
                var penalty = unjustified.Type == TimeClockUnjustifiedAbsenceType.FullDay
                    ? expectedBaseMinutes
                    : CalculateMinutesBetween(unjustified.StartTime ?? TimeSpan.Zero, unjustified.EndTime ?? TimeSpan.Zero);

                balanceMinutes = Math.Min(balanceMinutes, -penalty);
            }

            if (isFuture)
            {
                expectedMinutes = 0;
                balanceMinutes = 0;
            }

            balanceMinutes = ApplyTolerance(balanceMinutes, toleranceMinutes);

            var nextPunchType = GetNextPunchType(dayPunches);

            return new TimeClockDayDto
            {
                Date = FormatDate(date),
                Day = date.Day,
                IsFuture = isFuture,
                IsWeekend = IsWeekend(date),
                IsHoliday = false,
                HolidayName = null,
                Punches = dayPunches.Select(MapPunch).ToList(),
                Absences = dayAbsences.Select(MapAbsence).ToList(),
                UnjustifiedAbsence = unjustifiedDto,
                WorkedMinutes = workedMinutes,
                ExpectedMinutes = expectedMinutes,
                BalanceMinutes = balanceMinutes,
                LunchMinutes = lunchMinutes,
                WorkedLabel = FormatMinutes(workedMinutes),
                ExpectedLabel = FormatMinutes(expectedMinutes),
                BalanceLabel = FormatSignedMinutes(balanceMinutes),
                LunchLabel = FormatMinutes(lunchMinutes),
                NextPunchType = nextPunchType?.ToString(),
                NextPunchTypeLabel = nextPunchType.HasValue ? GetPunchTypeLabel(nextPunchType.Value) : null
            };
        }

        private async Task<UserWorkScheduleSetting> GetOrCreateUserScheduleAsync(string userId)
        {
            var setting = await _context.UserWorkScheduleSettings
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (setting != null)
            {
                return setting;
            }

            setting = new UserWorkScheduleSetting
            {
                UserId = userId,
                EntryTime = new TimeSpan(8, 0, 0),
                ExitTime = new TimeSpan(17, 0, 0),
                LunchIntervalMinutes = 60
            };

            _context.UserWorkScheduleSettings.Add(setting);
            await _context.SaveChangesAsync();

            return setting;
        }

        private async Task<GlobalTimeClockSetting> GetOrCreateGlobalSettingsAsync()
        {
            var setting = await _context.GlobalTimeClockSettings
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefaultAsync();

            if (setting != null)
            {
                return setting;
            }

            setting = new GlobalTimeClockSetting { ToleranceMinutes = 10 };
            _context.GlobalTimeClockSettings.Add(setting);
            await _context.SaveChangesAsync();

            return setting;
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value;
        }

        private async Task<bool> IsCurrentUserAdminAsync()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            var roles = await _userManager.GetRolesAsync(user);
            return roles.Any(role =>
                role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ||
                role.Equals("Administrador", StringComparison.OrdinalIgnoreCase) ||
                role.Contains("admin", StringComparison.OrdinalIgnoreCase));
        }

        private static UserWorkScheduleSettingDto MapSchedule(UserWorkScheduleSetting setting)
        {
            var expectedMinutes = Math.Max(0, CalculateMinutesBetween(setting.EntryTime, setting.ExitTime) - setting.LunchIntervalMinutes);

            return new UserWorkScheduleSettingDto
            {
                EntryTime = FormatTime(setting.EntryTime),
                ExitTime = FormatTime(setting.ExitTime),
                LunchIntervalMinutes = setting.LunchIntervalMinutes,
                ShowWorkPackagesInCalendar = setting.ShowWorkPackagesInCalendar,
                ExpectedDailyMinutes = expectedMinutes,
                ExpectedDailyLabel = FormatMinutes(expectedMinutes)
            };
        }

        private static TimeClockPunchDto MapPunch(TimeClockPunch punch)
        {
            return new TimeClockPunchDto
            {
                Id = punch.Id,
                Date = FormatDate(punch.PunchDate),
                Time = FormatTime(punch.PunchTime),
                Type = punch.Type.ToString(),
                TypeLabel = GetPunchTypeLabel(punch.Type),
                TypeIcon = GetPunchTypeIcon(punch.Type),
                Sequence = GetPunchTypeSequence(punch.Type),
                Nsr = punch.Nsr,
                Observation = punch.Observation,
                ExternalRecordId = punch.ExternalRecordId,
                ImportFileName = punch.ImportFileName,
                SourceCreatedAt = punch.SourceCreatedAt?.ToString("O", CultureInfo.InvariantCulture),
                SourceUpdatedAt = punch.SourceUpdatedAt?.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private static TimeClockAbsenceDto MapAbsence(TimeClockAbsence absence)
        {
            return new TimeClockAbsenceDto
            {
                Id = absence.Id,
                UserId = absence.UserId,
                Type = absence.Type.ToString(),
                TypeLabel = GetAbsenceTypeLabel(absence.Type),
                PeriodType = absence.PeriodType.ToString(),
                PeriodTypeLabel = absence.PeriodType == TimeClockAbsencePeriodType.FullDay ? "Integral" : "Parcial",
                StartDate = FormatDate(absence.StartDate),
                EndDate = FormatDate(absence.EndDate),
                StartTime = absence.StartTime.HasValue ? FormatTime(absence.StartTime.Value) : null,
                EndTime = absence.EndTime.HasValue ? FormatTime(absence.EndTime.Value) : null,
                Observation = absence.Observation,
                Attachments = absence.Attachments.Select(attachment => new TimeClockAbsenceAttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    ContentType = attachment.ContentType,
                    Size = attachment.Size
                }).ToList()
            };
        }

        private static bool MatchesImportedAttachment(IFormFile file, string expectedName, string attachmentName)
        {
            var fileName = Path.GetFileName(file.FileName);
            return fileName.Equals(expectedName, StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(attachmentName, StringComparison.OrdinalIgnoreCase);
        }

        private static TimeClockAbsenceManagementItemDto MapManagementAbsence(TimeClockAbsence absence)
        {
            return new TimeClockAbsenceManagementItemDto
            {
                Id = absence.Id,
                Type = absence.Type.ToString(),
                TypeLabel = GetAbsenceTypeLabel(absence.Type),
                PeriodType = absence.PeriodType.ToString(),
                PeriodTypeLabel = absence.PeriodType == TimeClockAbsencePeriodType.FullDay ? "Integral" : "Parcial",
                StartDate = FormatDate(absence.StartDate), EndDate = FormatDate(absence.EndDate),
                StartTime = absence.StartTime.HasValue ? FormatTime(absence.StartTime.Value) : null,
                EndTime = absence.EndTime.HasValue ? FormatTime(absence.EndTime.Value) : null,
                Observation = absence.Observation, ExternalRecordId = absence.ExternalRecordId,
                SourceCreatedAt = absence.SourceCreatedAt?.ToString("O", CultureInfo.InvariantCulture),
                Attachments = absence.Attachments.Select(attachment => new TimeClockAbsenceAttachmentDto
                {
                    Id = attachment.Id, FileName = attachment.FileName, ContentType = attachment.ContentType, Size = attachment.Size
                }).ToList()
            };
        }

        private static TimeClockAbsenceManagementItemDto MapManagementUnjustifiedAbsence(TimeClockUnjustifiedAbsence absence)
        {
            return new TimeClockAbsenceManagementItemDto
            {
                Id = absence.Id, IsUnjustified = true, Type = absence.Type.ToString(),
                TypeLabel = absence.Type == TimeClockUnjustifiedAbsenceType.FullDay ? "Falta sem justificativa integral" : "Falta sem justificativa parcial",
                PeriodType = absence.Type.ToString(), PeriodTypeLabel = absence.Type == TimeClockUnjustifiedAbsenceType.FullDay ? "Integral" : "Parcial",
                StartDate = FormatDate(absence.AbsenceDate), EndDate = FormatDate(absence.AbsenceDate),
                StartTime = absence.StartTime.HasValue ? FormatTime(absence.StartTime.Value) : null,
                EndTime = absence.EndTime.HasValue ? FormatTime(absence.EndTime.Value) : null,
                Observation = absence.Observation, ExternalRecordId = absence.ExternalRecordId,
                SourceCreatedAt = absence.SourceCreatedAt?.ToString("O", CultureInfo.InvariantCulture)
            };
        }

        private static TimeClockUnjustifiedAbsenceDto MapUnjustifiedAbsence(TimeClockUnjustifiedAbsence absence, int expectedMinutes)
        {
            var penaltyMinutes = absence.Type == TimeClockUnjustifiedAbsenceType.FullDay
                ? expectedMinutes
                : CalculateMinutesBetween(absence.StartTime ?? TimeSpan.Zero, absence.EndTime ?? TimeSpan.Zero);

            return new TimeClockUnjustifiedAbsenceDto
            {
                Id = absence.Id,
                Date = FormatDate(absence.AbsenceDate),
                Type = absence.Type.ToString(),
                TypeLabel = absence.Type == TimeClockUnjustifiedAbsenceType.FullDay ? "Falta sem justificativa integral" : "Falta sem justificativa parcial",
                StartTime = absence.StartTime.HasValue ? FormatTime(absence.StartTime.Value) : null,
                EndTime = absence.EndTime.HasValue ? FormatTime(absence.EndTime.Value) : null,
                Observation = absence.Observation,
                PenaltyMinutes = penaltyMinutes,
                PenaltyLabel = FormatMinutes(penaltyMinutes)
            };
        }

        private static int CalculateWorkedMinutes(List<TimeClockPunch> punches)
        {
            var byType = punches.ToDictionary(x => x.Type, x => x.PunchTime);
            var minutes = 0;

            if (byType.TryGetValue(TimeClockPunchType.MorningEntry, out var morningEntry) &&
                byType.TryGetValue(TimeClockPunchType.MorningExit, out var morningExit))
            {
                minutes += CalculateMinutesBetween(morningEntry, morningExit);
            }

            if (byType.TryGetValue(TimeClockPunchType.AfternoonEntry, out var afternoonEntry) &&
                byType.TryGetValue(TimeClockPunchType.AfternoonExit, out var afternoonExit))
            {
                minutes += CalculateMinutesBetween(afternoonEntry, afternoonExit);
            }

            if (byType.TryGetValue(TimeClockPunchType.AdditionalEntry, out var additionalEntry) &&
                byType.TryGetValue(TimeClockPunchType.AdditionalExit, out var additionalExit))
            {
                minutes += CalculateMinutesBetween(additionalEntry, additionalExit);
            }

            return minutes;
        }

        private static int CalculateLunchMinutes(List<TimeClockPunch> punches)
        {
            var byType = punches.ToDictionary(x => x.Type, x => x.PunchTime);

            return byType.TryGetValue(TimeClockPunchType.MorningExit, out var morningExit) &&
                   byType.TryGetValue(TimeClockPunchType.AfternoonEntry, out var afternoonEntry)
                ? CalculateMinutesBetween(morningExit, afternoonEntry)
                : 0;
        }

        private static int CalculateJustifiedAbsenceCoverageMinutes(List<TimeClockAbsence> absences, int expectedMinutes)
        {
            if (expectedMinutes <= 0 || absences.Count == 0)
            {
                return 0;
            }

            if (absences.Any(x => x.PeriodType == TimeClockAbsencePeriodType.FullDay))
            {
                return expectedMinutes;
            }

            var partialMinutes = absences
                .Where(x => x.PeriodType == TimeClockAbsencePeriodType.Partial && x.StartTime.HasValue && x.EndTime.HasValue)
                .Sum(x => CalculateMinutesBetween(x.StartTime!.Value, x.EndTime!.Value));

            return Math.Min(expectedMinutes, partialMinutes);
        }

        private static TimeClockPunchType? GetNextPunchType(List<TimeClockPunch> punches)
        {
            var usedTypes = punches.Select(x => x.Type).ToHashSet();
            foreach (var type in PunchSequence)
            {
                if (!usedTypes.Contains(type))
                {
                    return type;
                }
            }

            return null;
        }

        private static int CalculateMinutesBetween(TimeSpan start, TimeSpan end)
        {
            var difference = end - start;
            if (difference < TimeSpan.Zero)
            {
                difference += TimeSpan.FromDays(1);
            }

            return (int)Math.Round(difference.TotalMinutes, MidpointRounding.AwayFromZero);
        }

        private static int ApplyTolerance(int balanceMinutes, int toleranceMinutes)
        {
            return Math.Abs(balanceMinutes) <= toleranceMinutes ? 0 : balanceMinutes;
        }

        private static bool IsWeekend(DateTime date)
        {
            return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        }

        private static DateTime GetBusinessToday()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone).Date;
        }

        private static TimeZoneInfo ResolveBusinessTimeZone()
        {
            foreach (var timeZoneId in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
                }
                catch (TimeZoneNotFoundException)
                {
                    // Try the alternate Windows/IANA identifier.
                }
                catch (InvalidTimeZoneException)
                {
                    // Try the alternate Windows/IANA identifier.
                }
            }

            return TimeZoneInfo.Utc;
        }

        private static bool IsValidYearMonth(int year, int month)
        {
            return year is >= 2000 and <= 2100 && month is >= 1 and <= 12;
        }

        private static bool TryParseDate(string? value, out DateTime date)
        {
            if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                date = DateTime.SpecifyKind(parsedDate.Date, DateTimeKind.Utc);
                return true;
            }

            date = default;
            return false;
        }

        private static bool TryParseTime(string? value, out TimeSpan time)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                time = default;
                return false;
            }

            return TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time) ||
                   TimeSpan.TryParseExact(value, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out time);
        }

        private static bool TryParsePunchType(string? value, out TimeClockPunchType type)
        {
            return Enum.TryParse(value, true, out type);
        }

        private static bool TryParseAbsenceType(string? value, out TimeClockAbsenceType type)
        {
            return Enum.TryParse(value, true, out type);
        }

        private static bool TryParseAbsencePeriodType(string? value, out TimeClockAbsencePeriodType type)
        {
            return Enum.TryParse(value, true, out type);
        }

        private static bool TryParseUnjustifiedType(string? value, out TimeClockUnjustifiedAbsenceType type)
        {
            return Enum.TryParse(value, true, out type);
        }

        private static int GetPunchTypeSequence(TimeClockPunchType type)
        {
            var sequence = Array.IndexOf(PunchSequence, type);
            return sequence >= 0
                ? sequence
                : type switch
                {
                    TimeClockPunchType.AdditionalEntry => 4,
                    TimeClockPunchType.AdditionalExit => 5,
                    _ => int.MaxValue
                };
        }

        private static string GetPunchTypeLabel(TimeClockPunchType type)
        {
            return type switch
            {
                TimeClockPunchType.MorningEntry => "Entrada manhã",
                TimeClockPunchType.MorningExit => "Saída manhã",
                TimeClockPunchType.AfternoonEntry => "Entrada tarde",
                TimeClockPunchType.AfternoonExit => "Saída tarde",
                TimeClockPunchType.AdditionalEntry => "Entrada adicional",
                TimeClockPunchType.AdditionalExit => "Saída adicional",
                _ => type.ToString()
            };
        }

        private static string GetPunchTypeIcon(TimeClockPunchType type)
        {
            return type switch
            {
                TimeClockPunchType.MorningEntry => "ki-entrance-right",
                TimeClockPunchType.MorningExit => "ki-exit-right",
                TimeClockPunchType.AfternoonEntry => "ki-entrance-left",
                TimeClockPunchType.AfternoonExit => "ki-exit-left",
                TimeClockPunchType.AdditionalEntry => "ki-plus-circle",
                TimeClockPunchType.AdditionalExit => "ki-minus-circle",
                _ => "ki-time"
            };
        }

        private static string GetAbsenceTypeLabel(TimeClockAbsenceType type)
        {
            return type switch
            {
                TimeClockAbsenceType.MedicalCertificate => "Atestado médico",
                TimeClockAbsenceType.Vacation => "Férias",
                TimeClockAbsenceType.MaternityLeave => "Licença maternidade",
                TimeClockAbsenceType.MarriageLeave => "Casamento",
                TimeClockAbsenceType.MilitaryService => "Alistamento militar",
                TimeClockAbsenceType.Bereavement => "Luto",
                TimeClockAbsenceType.HomeOffice => "Home office",
                TimeClockAbsenceType.Other => "Outra ausência",
                _ => type.ToString()
            };
        }

        private static string BuildAuditDescription(TimeClockDayDto day)
        {
            if (day.UnjustifiedAbsence != null)
            {
                return day.UnjustifiedAbsence.TypeLabel;
            }

            if (day.Absences.Count > 0)
            {
                return string.Join(", ", day.Absences.Select(x => x.TypeLabel).Distinct());
            }

            return day.Punches.Count > 0 ? $"{day.Punches.Count} registro(s) de ponto" : "Dia sem registro";
        }

        private static string FormatDate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormatTime(TimeSpan time)
        {
            return time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
        }

        private static string FormatMinutes(int minutes)
        {
            var signlessMinutes = Math.Abs(minutes);
            return $"{signlessMinutes / 60}h{signlessMinutes % 60:00}";
        }

        private static string FormatSignedMinutes(int minutes)
        {
            if (minutes == 0)
            {
                return "0h00";
            }

            return $"{(minutes > 0 ? "+" : "-")}{FormatMinutes(minutes)}";
        }
    }
}
