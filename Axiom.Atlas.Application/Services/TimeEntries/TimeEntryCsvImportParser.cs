using System.Globalization;
using System.Text;

namespace Axiom.Atlas.Application.Services.TimeEntries;

/// <summary>
/// Reads the legacy time-entry export and retains only values represented by the
/// current Axiom Atlas time-entry model.
/// </summary>
public sealed class TimeEntryCsvImportParser
{
    private static readonly string[] RequiredHeaders =
    [
        "data", "horainicial", "horafinal", "wpid", "atividadeid", "comentario", "openprojectid", "criadoem"
    ];

    public TimeEntryCsvParseResult Parse(byte[] content)
    {
        var result = new TimeEntryCsvParseResult();
        var records = ReadRecords(Encoding.UTF8.GetString(content), result.Errors);
        if (records.Count == 0)
        {
            result.Errors.Add(new TimeEntryCsvImportIssue(null, "O arquivo CSV está vazio ou não possui cabeçalho."));
            return result;
        }

        var headers = records[0].Fields.Select((value, index) => new { Name = Normalize(value), Index = index })
            .Where(x => !string.IsNullOrWhiteSpace(x.Name)).GroupBy(x => x.Name)
            .ToDictionary(group => group.Key, group => group.First().Index);
        foreach (var header in RequiredHeaders.Where(header => !headers.ContainsKey(header)))
            result.Errors.Add(new TimeEntryCsvImportIssue(records[0].Line, $"Coluna obrigatória não encontrada: {header}."));

        if (result.Errors.Count > 0) return result;

        foreach (var record in records.Skip(1))
        {
            if (record.Fields.All(string.IsNullOrWhiteSpace)) continue;
            string Value(string name) => headers[name] < record.Fields.Count ? record.Fields[headers[name]].Trim() : string.Empty;
            var errorsBefore = result.Errors.Count;

            if (!DateOnly.TryParseExact(Value("data"), ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var spentOn))
                result.Errors.Add(new TimeEntryCsvImportIssue(record.Line, "Data inválida."));
            var startTimeIsValid = TimeOnly.TryParseExact(Value("horainicial"), ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime);
            var endTimeIsValid = TimeOnly.TryParseExact(Value("horafinal"), ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime);
            if (!startTimeIsValid || !endTimeIsValid || endTime <= startTime)
                result.Errors.Add(new TimeEntryCsvImportIssue(record.Line, "Os horários de início e término são inválidos."));
            if (!int.TryParse(Value("wpid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var workPackageId) || workPackageId <= 0)
                result.Errors.Add(new TimeEntryCsvImportIssue(record.Line, "WP (ID) inválido."));

            var activityId = 0;
            if (!string.IsNullOrWhiteSpace(Value("atividadeid")) &&
                (!int.TryParse(Value("atividadeid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out activityId) || activityId < 0))
                result.Errors.Add(new TimeEntryCsvImportIssue(record.Line, "Atividade (ID) inválida."));

            int? openProjectTimeEntryId = null;
            if (!string.IsNullOrWhiteSpace(Value("openprojectid")))
            {
                if (!int.TryParse(Value("openprojectid"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedOpenProjectId) || parsedOpenProjectId <= 0)
                    result.Errors.Add(new TimeEntryCsvImportIssue(record.Line, "OpenProject (ID) inválido."));
                else openProjectTimeEntryId = parsedOpenProjectId;
            }

            DateTime? createdAt = null;
            if (!DateTimeOffset.TryParse(Value("criadoem"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedCreatedAt))
                result.Errors.Add(new TimeEntryCsvImportIssue(record.Line, "Criado em inválido."));
            else createdAt = parsedCreatedAt.UtcDateTime;

            if (result.Errors.Count != errorsBefore) continue;
            result.Rows.Add(new TimeEntryCsvImportRow
            {
                SourceLine = record.Line,
                SpentOn = spentOn,
                StartTime = startTime,
                EndTime = endTime,
                WorkPackageId = workPackageId,
                WorkPackageSubject = ValueOrNull(headers, record, "wpassunto"),
                ProjectId = ParseOptionalInt(headers, record, "projetoid"),
                ProjectName = ValueOrNull(headers, record, "projeto"),
                ActivityId = activityId,
                Comment = ValueOrNull(headers, record, "comentario"),
                OpenProjectTimeEntryId = openProjectTimeEntryId,
                CreatedAt = createdAt!.Value
            });
        }

        return result;
    }

    private static int? ParseOptionalInt(IReadOnlyDictionary<string, int> headers, CsvRecord record, string header)
    {
        var value = ValueOrNull(headers, record, header);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : null;
    }

    private static string? ValueOrNull(IReadOnlyDictionary<string, int> headers, CsvRecord record, string header)
    {
        if (!headers.TryGetValue(header, out var index) || index >= record.Fields.Count) return null;
        var value = record.Fields[index].Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static List<CsvRecord> ReadRecords(string text, List<TimeEntryCsvImportIssue> errors)
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
        if (quoted) errors.Add(new TimeEntryCsvImportIssue(recordLine, "Há um campo entre aspas que não foi encerrado corretamente."));
        if (value.Length > 0 || fields.Count > 0) { fields.Add(value.ToString()); records.Add(new CsvRecord(recordLine, [.. fields])); }
        return records;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().TrimStart('\uFEFF').Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character)).Select(char.ToLowerInvariant).ToArray());
    }

    private sealed record CsvRecord(int Line, List<string> Fields);
}

public sealed class TimeEntryCsvParseResult
{
    public List<TimeEntryCsvImportRow> Rows { get; } = [];
    public List<TimeEntryCsvImportIssue> Errors { get; } = [];
}

public sealed record TimeEntryCsvImportIssue(int? Line, string Message);

public sealed class TimeEntryCsvImportRow
{
    public int SourceLine { get; init; }
    public DateOnly SpentOn { get; init; }
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int WorkPackageId { get; init; }
    public string? WorkPackageSubject { get; init; }
    public int? ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public int ActivityId { get; init; }
    public string? Comment { get; init; }
    public int? OpenProjectTimeEntryId { get; init; }
    public DateTime CreatedAt { get; init; }
}
