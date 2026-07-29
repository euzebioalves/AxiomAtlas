namespace Axiom.Atlas.Application.DTOs.TimeClock
{
    public class UserWorkScheduleSettingDto
    {
        public string EntryTime { get; set; } = "08:00";
        public string ExitTime { get; set; } = "17:00";
        public int LunchIntervalMinutes { get; set; } = 60;
        public bool ShowWorkPackagesInCalendar { get; set; } = true;
        public int ExpectedDailyMinutes { get; set; } = 480;
        public string ExpectedDailyLabel { get; set; } = "8h00";
    }

    public class SaveUserWorkScheduleSettingRequest
    {
        public string EntryTime { get; set; } = string.Empty;
        public string ExitTime { get; set; } = string.Empty;
        public int LunchIntervalMinutes { get; set; }
    }

    public class SaveTimeClockCalendarPreferenceRequest
    {
        public bool ShowWorkPackagesInCalendar { get; set; }
    }

    public class GlobalTimeClockSettingDto
    {
        public int ToleranceMinutes { get; set; } = 10;
    }

    public class SaveGlobalTimeClockSettingRequest
    {
        public int ToleranceMinutes { get; set; }
    }

    public class TimeClockPunchDto
    {
        public Guid Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Time { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string TypeIcon { get; set; } = string.Empty;
        public int Sequence { get; set; }
        public string? Nsr { get; set; }
        public string? Observation { get; set; }
        public string? ExternalRecordId { get; set; }
        public string? ImportFileName { get; set; }
        public string? SourceCreatedAt { get; set; }
        public string? SourceUpdatedAt { get; set; }
    }

    public class SaveTimeClockPunchRequest
    {
        public Guid? Id { get; set; }
        public string Time { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Nsr { get; set; }
        public string? Observation { get; set; }
    }

    public class SaveTimeClockPunchesRequest
    {
        public string Date { get; set; } = string.Empty;
        public List<SaveTimeClockPunchRequest> Punches { get; set; } = new();
    }

    public class TimeClockImportResultDto
    {
        public int ImportedPunches { get; set; }
        public int ReplacedPunches { get; set; }
        public string DateStart { get; set; } = string.Empty;
        public string DateEnd { get; set; } = string.Empty;
        public string ExternalUserId { get; set; } = string.Empty;
    }

    public class TimeClockUnjustifiedAbsenceDto
    {
        public Guid Id { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Observation { get; set; }
        public int PenaltyMinutes { get; set; }
        public string PenaltyLabel { get; set; } = "0h00";
    }

    public class SaveTimeClockUnjustifiedAbsenceRequest
    {
        public string Date { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Observation { get; set; }
    }

    public class TimeClockAbsenceAttachmentDto
    {
        public Guid Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public class TimeClockAbsenceManagementItemDto
    {
        public Guid Id { get; set; }
        public bool IsUnjustified { get; set; }
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string PeriodType { get; set; } = string.Empty;
        public string PeriodTypeLabel { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Observation { get; set; }
        public string? ExternalRecordId { get; set; }
        public string? SourceCreatedAt { get; set; }
        public List<TimeClockAbsenceAttachmentDto> Attachments { get; set; } = new();
    }

    public class TimeClockAbsenceImportResultDto
    {
        public int ImportedAbsences { get; set; }
        public int ImportedUnjustifiedAbsences { get; set; }
        public int ImportedAttachments { get; set; }
        public int ReplacedAbsences { get; set; }
        public int ReplacedUnjustifiedAbsences { get; set; }
    }

    public class TimeClockAbsenceDto
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string TypeLabel { get; set; } = string.Empty;
        public string PeriodType { get; set; } = string.Empty;
        public string PeriodTypeLabel { get; set; } = string.Empty;
        public string StartDate { get; set; } = string.Empty;
        public string EndDate { get; set; } = string.Empty;
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Observation { get; set; }
        public List<TimeClockAbsenceAttachmentDto> Attachments { get; set; } = new();
    }

    public class TimeClockDayDto
    {
        public string Date { get; set; } = string.Empty;
        public int Day { get; set; }
        public bool IsCurrentMonth { get; set; } = true;
        public bool IsFuture { get; set; }
        public bool IsWeekend { get; set; }
        public bool IsHoliday { get; set; }
        public string? HolidayName { get; set; }
        public List<TimeClockPunchDto> Punches { get; set; } = new();
        public List<TimeClockWorkPackageDto> WorkPackages { get; set; } = new();
        public decimal WorkPackageHours { get; set; }
        public List<TimeClockAbsenceDto> Absences { get; set; } = new();
        public TimeClockUnjustifiedAbsenceDto? UnjustifiedAbsence { get; set; }
        public int WorkedMinutes { get; set; }
        public int ExpectedMinutes { get; set; }
        public int BalanceMinutes { get; set; }
        public int LunchMinutes { get; set; }
        public string WorkedLabel { get; set; } = "0h00";
        public string ExpectedLabel { get; set; } = "0h00";
        public string BalanceLabel { get; set; } = "0h00";
        public string LunchLabel { get; set; } = "0h00";
        public string? NextPunchType { get; set; }
        public string? NextPunchTypeLabel { get; set; }
    }

    public class TimeClockWorkPackageDto
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        public string? Url { get; set; }
        public string SyncStatus { get; set; } = "pending";
    }

    public class TimeClockSummaryDto
    {
        public int WorkedMinutes { get; set; }
        public int ExpectedMinutes { get; set; }
        public int BalanceMinutes { get; set; }
        public int AccumulatedBalanceMinutes { get; set; }
        public int AbsenceDays { get; set; }
        public int UnjustifiedAbsenceDays { get; set; }
        public string WorkedLabel { get; set; } = "0h00";
        public string ExpectedLabel { get; set; } = "0h00";
        public string BalanceLabel { get; set; } = "0h00";
        public string AccumulatedBalanceLabel { get; set; } = "0h00";
    }

    public class TimeClockCalendarDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthLabel { get; set; } = string.Empty;
        public UserWorkScheduleSettingDto Schedule { get; set; } = new();
        public GlobalTimeClockSettingDto GlobalSettings { get; set; } = new();
        public List<TimeClockDayDto> Days { get; set; } = new();
        public TimeClockSummaryDto Summary { get; set; } = new();
    }

    public class TimeClockAuditEntryDto
    {
        public string Date { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public List<TimeClockPunchDto> Punches { get; set; } = new();
        public List<TimeClockAbsenceDto> Absences { get; set; } = new();
        public TimeClockUnjustifiedAbsenceDto? UnjustifiedAbsence { get; set; }
        public int WorkedMinutes { get; set; }
        public int ExpectedMinutes { get; set; }
        public int BalanceMinutes { get; set; }
        public string WorkedLabel { get; set; } = "0h00";
        public string ExpectedLabel { get; set; } = "0h00";
        public string BalanceLabel { get; set; } = "0h00";
    }

    public class TimeClockAuditDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int BalanceMinutes { get; set; }
        public string BalanceLabel { get; set; } = "0h00";
        public List<TimeClockAuditEntryDto> Entries { get; set; } = new();
    }
}
