using System.Text;
using Axiom.Atlas.Application.Services.TimeEntries;

namespace Axiom.Atlas.Tests.Services;

public class TimeEntryCsvImportParserTests
{
    [Fact]
    public void Parse_ValidLegacyCsv_MapsOnlyFieldsSupportedByTimeEntry()
    {
        var parser = new TimeEntryCsvImportParser();
        var result = parser.Parse(Encoding.UTF8.GetBytes("""
            ID;Usuário (ID);Data;Hora Inicial;Hora Final;Projeto (ID);Projeto;WP (ID);WP (Assunto);Atividade (ID);Comentário;OpenProject (ID);Criado em
            external-entry;legacy-user;2025-06-30;09:00:00;10:30:00;5;Projeto Atlas;10698;Implementar recurso;2;"Comentário com; delimitador";543;2025-12-01T04:23:10.332Z
            """));

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(new DateOnly(2025, 6, 30), row.SpentOn);
        Assert.Equal(new TimeOnly(9, 0), row.StartTime);
        Assert.Equal(new TimeOnly(10, 30), row.EndTime);
        Assert.Equal(10698, row.WorkPackageId);
        Assert.Equal(5, row.ProjectId);
        Assert.Equal(2, row.ActivityId);
        Assert.Equal("Comentário com; delimitador", row.Comment);
        Assert.Equal(543, row.OpenProjectTimeEntryId);
    }

    [Fact]
    public void Parse_BlankActivity_UsesUnspecifiedActivityWithoutFailingImport()
    {
        var parser = new TimeEntryCsvImportParser();
        var result = parser.Parse(Encoding.UTF8.GetBytes("""
            Data;Hora Inicial;Hora Final;WP (ID);Atividade (ID);Comentário;OpenProject (ID);Criado em
            2025-06-30;09:00:00;10:00:00;10698;;;543;2025-12-01T04:23:10.332Z
            """));

        Assert.Empty(result.Errors);
        Assert.Equal(0, Assert.Single(result.Rows).ActivityId);
    }
}
