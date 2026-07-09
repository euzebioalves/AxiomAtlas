using Axiom.Atlas.Application.Interfaces;
using System.Text.RegularExpressions;

namespace Axiom.Atlas.Application.Services
{
    public class TimeConverterService : ITimeConverterService
    {
        public decimal ParseStringToDecimal(string timeInput)
        {
            if (string.IsNullOrWhiteSpace(timeInput)) return 0;

            int totalMinutes = 0;

            var hourMatch = Regex.Match(timeInput, @"(\d+)\s*h", RegexOptions.IgnoreCase);
            if (hourMatch.Success)
                totalMinutes += int.Parse(hourMatch.Groups[1].Value) * 60;

            var minuteMatch = Regex.Match(timeInput, @"(\d+)\s*m", RegexOptions.IgnoreCase);
            if (minuteMatch.Success)
                totalMinutes += int.Parse(minuteMatch.Groups[1].Value);

            if (!hourMatch.Success && !minuteMatch.Success && int.TryParse(timeInput, out int hours))
                return hours;

            return Math.Round((decimal)totalMinutes / 60, 2);
        }
    }
}
