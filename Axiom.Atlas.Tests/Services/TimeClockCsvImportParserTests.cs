using System.Text;
using Axiom.Atlas.Application.Services.TimeClock;
using Axiom.Atlas.Domain.Enums;

namespace Axiom.Atlas.Tests.Services;

public class TimeClockCsvImportParserTests
{
    private readonly TimeClockCsvImportParser _parser = new();

    [Fact]
    public void Parse_ValidLegacyCsv_MapsRowsAndAdditionalPunches()
    {
        var result = _parser.Parse(Encoding.UTF8.GetBytes("""
            ID;Usuário (ID);NSR;Data;Hora;Tipo;Observação;Criado em;Atualizado em
            first;legacy-user;000084901;2024-06-13;08:00:00;entrada_manha;Importado via AFD;2026-04-08T14:00:51.013Z;2026-04-08T14:00:51.013Z
            second;legacy-user;000084902;2024-06-13;12:00:00;saida_manha;;2026-04-08T14:00:51.013Z;2026-04-08T14:00:51.013Z
            third;legacy-user;000084903;2024-06-13;14:20:00;outro;;2026-04-08T14:00:51.013Z;2026-04-08T14:00:51.013Z
            fourth;legacy-user;000084904;2024-06-13;18:02:00;outro;;2026-04-08T14:00:51.013Z;2026-04-08T14:00:51.013Z
            """), "registros.csv");

        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Rows.Count);
        Assert.Equal("legacy-user", result.ExternalUserId);
        Assert.Equal(new DateOnly(2024, 6, 13), result.DateStart);
        Assert.Equal(TimeClockPunchType.AdditionalEntry, result.Rows[2].Type);
        Assert.Equal(TimeClockPunchType.AdditionalExit, result.Rows[3].Type);
        Assert.Equal("000084901", result.Rows[0].Nsr);
        Assert.NotNull(result.Rows[0].SourceCreatedAt);
    }

    [Fact]
    public void Parse_DuplicateStandardPunchType_ReturnsError()
    {
        var result = _parser.Parse(Encoding.UTF8.GetBytes("""
            ID;Usuário (ID);NSR;Data;Hora;Tipo;Observação;Criado em;Atualizado em
            first;legacy-user;000084901;2024-06-13;08:00:00;entrada_manha;;;
            second;legacy-user;000084902;2024-06-13;08:01:00;entrada_manha;;;
            """), "registros.csv");

        Assert.Contains(result.Errors, error => error.Line == 3 && error.Message.Contains("repetido", StringComparison.OrdinalIgnoreCase));
    }
}
