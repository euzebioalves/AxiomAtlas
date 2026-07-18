using Axiom.Atlas.Application.Services;

namespace Axiom.Atlas.Tests.Services;

public class TimeConverterServiceTests
{
    private readonly TimeConverterService _service = new();

    [Theory]
    [InlineData("1h30m", 1.50)]
    [InlineData("2h", 2.00)]
    [InlineData("45m", 0.75)]
    [InlineData("3", 3.00)]
    [InlineData("", 0.00)]
    public void ParseStringToDecimal_ConvertsOpenProjectDurations(string input, decimal expected)
    {
        var result = _service.ParseStringToDecimal(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseStringToDecimal_RoundsMinuteFractionsToTwoDecimalPlaces()
    {
        var result = _service.ParseStringToDecimal("1h20m");

        Assert.Equal(1.33m, result);
    }
}
