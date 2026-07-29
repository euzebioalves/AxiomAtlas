using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Axiom.Atlas.Domain.Enums;

namespace Axiom.Atlas.Application.Services.TimeClock;

public sealed class TimeClockAbsenceCsvImportParser
{
    private static readonly string[] RequiredHeaders =
    [
        "id", "usuarioid", "tipodeausencia", "abrangencia", "datainicial", "datafinal",
        "horainicial", "horafinal", "descricao", "status", "anexonome", "anexotipo",
        "anexocaminho", "criadoporid", "criadoem", "atualizadoem"
    ];

    public TimeClockAbsenceCsvParseResult Parse(byte[] content, string fileName)
    {
        var result = new TimeClockAbsenceCsvParseResult
        {
            FileName = Path.GetFileName(fileName),
            FileHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        };

        var records = ReadRecords(Encoding.UTF8.GetString(content), result.Errors);
        if (records.Count == 0)
        {
            result.Errors.Add(new TimeClockCsvIssue(null, "O arquivo CSV está vazio ou não possui cabeçalho."));
            return result;
        }

        var headers = records[0].Fields.Select((value, index) => new { Name = Normalize(value), Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name)
            .ToDictionary(group => group.Key, group => group.First().Index);
        foreach (var header in RequiredHeaders.Where(header => !headers.ContainsKey(header)))
        {
            result.Errors.Add(new TimeClockCsvIssue(records[0].Line, $"Coluna obrigatória não encontrada: {header}."));
        }

        if (result.Errors.Count > 0) return result;

        var externalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var externalUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Skip(1))
        {
            if (record.Fields.All(string.IsNullOrWhiteSpace)) continue;
            string Value(string name) => headers[name] < record.Fields.Count ? record.Fields[headers[name]].Trim() : string.Empty;
            var errorsBefore = result.Errors.Count;
            var externalId = Value("id");
            var externalUserId = Value("usuarioid");
            if (string.IsNullOrWhiteSpace(externalId) || externalId.Length > 100)
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O ID da ausência é obrigatório e deve ter no máximo 100 caracteres."));
            else if (!externalIds.Add(externalId))
                result.Errors.Add(new TimeClockCsvIssue(record.Line, $"O ID externo {externalId} está duplicado no arquivo."));
            if (string.IsNullOrWhiteSpace(externalUserId) || externalUserId.Length > 100)
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O Usuário (ID) é obrigatório e deve ter no máximo 100 caracteres."));
            else externalUsers.Add(externalUserId);

            var startDateIsValid = TryParseDate(Value("datainicial"), out var startDate);
            var endDateIsValid = TryParseDate(Value("datafinal"), out var endDate);
            if (!startDateIsValid || !endDateIsValid || endDate < startDate)
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O período da ausência é inválido."));
            var isUnjustified = Normalize(Value("tipodeausencia")) == "unjustifiedabsence";
            var period = ParsePeriod(Value("abrangencia"), record.Line, result.Errors);
            var type = isUnjustified ? null : ParseType(Value("tipodeausencia"), record.Line, result.Errors);
            if (!Normalize(Value("status")).Equals("active", StringComparison.Ordinal))
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "Somente ausências com status 'active' podem ser importadas."));

            TimeOnly? startTime = null;
            TimeOnly? endTime = null;
            if (period == TimeClockAbsencePeriodType.Partial)
            {
                if (!TryParseTime(Value("horainicial"), out var parsedStart) || !TryParseTime(Value("horafinal"), out var parsedEnd) || parsedEnd <= parsedStart || startDate != endDate)
                    result.Errors.Add(new TimeClockCsvIssue(record.Line, "Ausência parcial requer início, fim e uma única data válidos."));
                else { startTime = parsedStart; endTime = parsedEnd; }
            }
            var createdAt = ParseTimestamp(Value("criadoem"), "Criado em", record.Line, result.Errors);
            var updatedAt = ParseTimestamp(Value("atualizadoem"), "Atualizado em", record.Line, result.Errors);
            if (result.Errors.Count != errorsBefore || period is null || (!isUnjustified && type is null)) continue;

            result.Rows.Add(new TimeClockAbsenceCsvRow
            {
                SourceLine = record.Line, ExternalRecordId = externalId, ExternalUserId = externalUserId,
                IsUnjustified = isUnjustified, Type = type, PeriodType = period.Value, StartDate = startDate, EndDate = endDate,
                StartTime = startTime, EndTime = endTime, Observation = NullIfWhiteSpace(Value("descricao")),
                AttachmentName = NullIfWhiteSpace(Value("anexonome")), AttachmentContentType = NullIfWhiteSpace(Value("anexotipo")),
                SourceCreatedAt = createdAt, SourceUpdatedAt = updatedAt
            });
        }

        if (externalUsers.Count > 1) result.Errors.Add(new TimeClockCsvIssue(null, "O arquivo contém ausências de mais de um usuário."));
        result.ExternalUserId = externalUsers.Count == 1 ? externalUsers.Single() : null;
        return result;
    }

    private static TimeClockAbsenceType? ParseType(string value, int line, List<TimeClockCsvIssue> errors) => Normalize(value) switch
    {
        "medicalcertificate" => TimeClockAbsenceType.MedicalCertificate,
        "vacation" => TimeClockAbsenceType.Vacation,
        "maternityleave" => TimeClockAbsenceType.MaternityLeave,
        "marriageleave" => TimeClockAbsenceType.MarriageLeave,
        "militaryservice" => TimeClockAbsenceType.MilitaryService,
        "bereavement" => TimeClockAbsenceType.Bereavement,
        "remotework" => TimeClockAbsenceType.HomeOffice,
        "other" => TimeClockAbsenceType.Other,
        _ => Invalid<TimeClockAbsenceType>(value, line, "Tipo de ausência", errors)
    };

    private static TimeClockAbsencePeriodType? ParsePeriod(string value, int line, List<TimeClockCsvIssue> errors) => Normalize(value) switch
    {
        "full" => TimeClockAbsencePeriodType.FullDay,
        "partial" => TimeClockAbsencePeriodType.Partial,
        _ => Invalid<TimeClockAbsencePeriodType>(value, line, "Abrangência", errors)
    };

    private static T? Invalid<T>(string value, int line, string label, List<TimeClockCsvIssue> errors) where T : struct
    {
        errors.Add(new TimeClockCsvIssue(line, $"{label} inválido: '{value}'."));
        return null;
    }

    private static List<CsvRecord> ReadRecords(string text, List<TimeClockCsvIssue> errors)
    {
        var records = new List<CsvRecord>(); var fields = new List<string>(); var value = new StringBuilder(); var quoted = false; var line = 1; var recordLine = 1;
        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"') { if (quoted && index + 1 < text.Length && text[index + 1] == '"') { value.Append('"'); index++; } else quoted = !quoted; continue; }
            if (character == ';' && !quoted) { fields.Add(value.ToString()); value.Clear(); continue; }
            if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
                fields.Add(value.ToString()); value.Clear(); records.Add(new CsvRecord(recordLine, [.. fields])); fields.Clear(); line++; recordLine = line; continue;
            }
            if (character == '\n') line++;
            value.Append(character);
        }
        if (quoted) errors.Add(new TimeClockCsvIssue(recordLine, "Há um campo entre aspas que não foi encerrado corretamente."));
        if (value.Length > 0 || fields.Count > 0) { fields.Add(value.ToString()); records.Add(new CsvRecord(recordLine, [.. fields])); }
        return records;
    }

    private static bool TryParseDate(string value, out DateOnly date) => DateOnly.TryParseExact(value, ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    private static bool TryParseTime(string value, out TimeOnly time) => TimeOnly.TryParseExact(value, ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    private static DateTime? ParseTimestamp(string value, string label, int line, List<TimeClockCsvIssue> errors)
    { if (string.IsNullOrWhiteSpace(value)) return null; if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp)) return timestamp.UtcDateTime; errors.Add(new TimeClockCsvIssue(line, $"{label} inválido: '{value}'.")); return null; }
    private static string Normalize(string value)
    { var decomposed = value.Trim().TrimStart('\uFEFF').Normalize(NormalizationForm.FormD); return new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)).Select(char.ToLowerInvariant).ToArray()); }
    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record CsvRecord(int Line, List<string> Fields);
}

public sealed class TimeClockAbsenceCsvParseResult
{
    public string FileName { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public string? ExternalUserId { get; set; }
    public List<TimeClockAbsenceCsvRow> Rows { get; } = [];
    public List<TimeClockCsvIssue> Errors { get; } = [];
}

public sealed class TimeClockAbsenceCsvRow
{
    public int SourceLine { get; init; }
    public string ExternalRecordId { get; init; } = string.Empty;
    public string ExternalUserId { get; init; } = string.Empty;
    public bool IsUnjustified { get; init; }
    public TimeClockAbsenceType? Type { get; init; }
    public TimeClockAbsencePeriodType PeriodType { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public TimeOnly? StartTime { get; init; }
    public TimeOnly? EndTime { get; init; }
    public string? Observation { get; init; }
    public string? AttachmentName { get; init; }
    public string? AttachmentContentType { get; init; }
    public DateTime? SourceCreatedAt { get; init; }
    public DateTime? SourceUpdatedAt { get; init; }
}
