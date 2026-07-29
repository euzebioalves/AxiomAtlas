using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Axiom.Atlas.Domain.Enums;

namespace Axiom.Atlas.Application.Services.TimeClock;

public sealed class TimeClockCsvImportParser
{
    private static readonly string[] RequiredHeaders =
    [
        "id", "usuarioid", "nsr", "data", "hora", "tipo", "observacao", "criadoem", "atualizadoem"
    ];

    public TimeClockCsvParseResult Parse(byte[] content, string fileName)
    {
        var result = new TimeClockCsvParseResult
        {
            FileName = Path.GetFileName(fileName),
            FileHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant()
        };

        if (content.Length == 0)
        {
            result.Errors.Add(new TimeClockCsvIssue(null, "O arquivo CSV está vazio."));
            return result;
        }

        var records = ReadRecords(Encoding.UTF8.GetString(content), result.Errors);
        if (records.Count == 0)
        {
            result.Errors.Add(new TimeClockCsvIssue(null, "O arquivo CSV não possui cabeçalho."));
            return result;
        }

        var header = records[0].Fields
            .Select((value, index) => new { Name = Normalize(value), Index = index })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.Name)
            .ToDictionary(group => group.Key, group => group.First().Index);

        foreach (var requiredHeader in RequiredHeaders)
        {
            if (!header.ContainsKey(requiredHeader))
            {
                result.Errors.Add(new TimeClockCsvIssue(records[0].Line, $"Coluna obrigatória não encontrada: {GetHeaderLabel(requiredHeader)}."));
            }
        }

        if (result.Errors.Count > 0)
        {
            return result;
        }

        var externalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var externalUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var additionalTypesByDate = new Dictionary<DateOnly, int>();
        var typesByDate = new HashSet<(DateOnly Date, TimeClockPunchType Type)>();
        result.TotalRows = records.Count - 1;

        foreach (var record in records.Skip(1))
        {
            if (record.Fields.All(string.IsNullOrWhiteSpace))
            {
                result.TotalRows--;
                continue;
            }

            var rowErrorsBefore = result.Errors.Count;
            string Value(string name) => header[name] < record.Fields.Count ? record.Fields[header[name]].Trim() : string.Empty;

            var externalRecordId = Value("id");
            var externalUserId = Value("usuarioid");
            var nsr = Value("nsr");
            var observation = NullIfWhiteSpace(Value("observacao"));

            if (string.IsNullOrWhiteSpace(externalRecordId))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O ID do registro é obrigatório."));
            }
            else if (externalRecordId.Length > 100)
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O ID do registro excede 100 caracteres."));
            }
            else if (!externalIds.Add(externalRecordId))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, $"O ID externo {externalRecordId} está duplicado no arquivo."));
            }

            if (string.IsNullOrWhiteSpace(externalUserId))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O Usuário (ID) é obrigatório."));
            }
            else if (externalUserId.Length > 100)
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O Usuário (ID) excede 100 caracteres."));
            }
            else
            {
                externalUsers.Add(externalUserId);
            }

            if (!string.IsNullOrWhiteSpace(nsr) && (nsr.Length != 9 || !nsr.All(char.IsDigit)))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O NSR deve conter exatamente 9 dígitos numéricos."));
            }

            if (!TryParseDate(Value("data"), out var punchDate))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, $"Data inválida: '{Value("data")}'."));
            }

            if (!TryParseTime(Value("hora"), out var punchTime))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, $"Hora inválida: '{Value("hora")}'."));
            }

            var punchType = ParseType(Value("tipo"), punchDate, additionalTypesByDate, record.Line, result.Errors);
            var sourceCreatedAt = ParseTimestamp(Value("criadoem"), "Criado em", record.Line, result.Errors);
            var sourceUpdatedAt = ParseTimestamp(Value("atualizadoem"), "Atualizado em", record.Line, result.Errors);

            if (result.Errors.Count != rowErrorsBefore || punchType is null)
            {
                continue;
            }

            if (!typesByDate.Add((punchDate, punchType.Value)))
            {
                result.Errors.Add(new TimeClockCsvIssue(record.Line, "O tipo de registro está repetido na mesma data."));
                continue;
            }

            result.Rows.Add(new TimeClockCsvRow
            {
                SourceLine = record.Line,
                ExternalRecordId = externalRecordId,
                ExternalUserId = externalUserId,
                Nsr = NullIfWhiteSpace(nsr),
                PunchDate = punchDate,
                PunchTime = punchTime,
                Type = punchType.Value,
                Observation = observation,
                SourceCreatedAt = sourceCreatedAt,
                SourceUpdatedAt = sourceUpdatedAt
            });
        }

        if (externalUsers.Count > 1)
        {
            result.Errors.Add(new TimeClockCsvIssue(null, "O arquivo contém registros de mais de um usuário. Exporte e importe um usuário por vez."));
        }

        result.ExternalUserId = externalUsers.Count == 1 ? externalUsers.Single() : null;
        if (result.Rows.Count > 0)
        {
            result.DateStart = result.Rows.Min(row => row.PunchDate);
            result.DateEnd = result.Rows.Max(row => row.PunchDate);
        }

        return result;
    }

    private static List<CsvRecord> ReadRecords(string text, List<TimeClockCsvIssue> errors)
    {
        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        var line = 1;
        var recordLine = 1;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    value.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }

            if (character == ';' && !quoted)
            {
                fields.Add(value.ToString());
                value.Clear();
                continue;
            }

            if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                fields.Add(value.ToString());
                value.Clear();
                records.Add(new CsvRecord(recordLine, [.. fields]));
                fields.Clear();
                line++;
                recordLine = line;
                continue;
            }

            if (character == '\n')
            {
                line++;
            }

            value.Append(character);
        }

        if (quoted)
        {
            errors.Add(new TimeClockCsvIssue(recordLine, "Há um campo entre aspas que não foi encerrado corretamente."));
        }

        if (value.Length > 0 || fields.Count > 0)
        {
            fields.Add(value.ToString());
            records.Add(new CsvRecord(recordLine, [.. fields]));
        }

        return records;
    }

    private static TimeClockPunchType? ParseType(
        string value,
        DateOnly punchDate,
        Dictionary<DateOnly, int> additionalTypesByDate,
        int line,
        List<TimeClockCsvIssue> errors)
    {
        var normalized = Normalize(value);
        if (normalized == "outro")
        {
            var count = additionalTypesByDate.GetValueOrDefault(punchDate);
            additionalTypesByDate[punchDate] = count + 1;
            if (count == 0) return TimeClockPunchType.AdditionalEntry;
            if (count == 1) return TimeClockPunchType.AdditionalExit;

            errors.Add(new TimeClockCsvIssue(line, "Foram encontrados mais de dois registros do tipo 'outro' na mesma data."));
            return null;
        }

        return normalized switch
        {
            "entradamanha" => TimeClockPunchType.MorningEntry,
            "saidamanha" => TimeClockPunchType.MorningExit,
            "entradatarde" => TimeClockPunchType.AfternoonEntry,
            "saidatarde" => TimeClockPunchType.AfternoonExit,
            _ => AddInvalidType(value, line, errors)
        };
    }

    private static TimeClockPunchType? AddInvalidType(string value, int line, List<TimeClockCsvIssue> errors)
    {
        errors.Add(new TimeClockCsvIssue(line, $"Tipo de registro inválido: '{value}'."));
        return null;
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value.Trim(), ["yyyy-MM-dd", "dd/MM/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static bool TryParseTime(string value, out TimeOnly time) =>
        TimeOnly.TryParseExact(value.Trim(), ["HH:mm:ss", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static DateTime? ParseTimestamp(string value, string label, int line, List<TimeClockCsvIssue> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return timestamp.UtcDateTime;
        }

        errors.Add(new TimeClockCsvIssue(line, $"{label} inválido: '{value}'."));
        return null;
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Trim().TrimStart('\uFEFF').Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(character)) normalized.Append(char.ToLowerInvariant(character));
        }

        return normalized.ToString();
    }

    private static string? NullIfWhiteSpace(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string GetHeaderLabel(string normalized) => normalized switch
    {
        "usuarioid" => "Usuário (ID)",
        "criadoem" => "Criado em",
        "atualizadoem" => "Atualizado em",
        _ => CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(normalized)
    };

    private sealed record CsvRecord(int Line, List<string> Fields);
}

public sealed class TimeClockCsvParseResult
{
    public string FileName { get; init; } = string.Empty;
    public string FileHash { get; init; } = string.Empty;
    public int TotalRows { get; set; }
    public string? ExternalUserId { get; set; }
    public DateOnly? DateStart { get; set; }
    public DateOnly? DateEnd { get; set; }
    public List<TimeClockCsvRow> Rows { get; } = [];
    public List<TimeClockCsvIssue> Errors { get; } = [];
    public List<TimeClockCsvIssue> Warnings { get; } = [];
}

public sealed class TimeClockCsvRow
{
    public int SourceLine { get; init; }
    public string ExternalRecordId { get; init; } = string.Empty;
    public string ExternalUserId { get; init; } = string.Empty;
    public string? Nsr { get; init; }
    public DateOnly PunchDate { get; init; }
    public TimeOnly PunchTime { get; init; }
    public TimeClockPunchType Type { get; init; }
    public string? Observation { get; init; }
    public DateTime? SourceCreatedAt { get; init; }
    public DateTime? SourceUpdatedAt { get; init; }
}

public sealed record TimeClockCsvIssue(int? Line, string Message);
