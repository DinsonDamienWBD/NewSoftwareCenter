using System;
using System.Linq;

namespace SoftwareCenter.Kernel
{
    /// <summary>
    /// A lightweight parser for standard 5-part CRON expressions.
    /// Format: [Minute] [Hour] [DayOfMonth] [Month] [DayOfWeek]
    /// Supports: * (all), comma lists (1,2,3), and step values (*/5).
    /// </summary>
    public class SimpleCronParser
    {
        private readonly string _expression;

        public SimpleCronParser(string expression)
        {
            _expression = expression;
        }

        public bool IsMatch(DateTimeOffset time)
        {
            var parts = _expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5) return false; // Invalid Cron

            var minute = parts[0];
            var hour = parts[1];
            var dayOfMonth = parts[2];
            var month = parts[3];
            var dayOfWeek = parts[4];

            return MatchPart(minute, time.Minute) &&
                   MatchPart(hour, time.Hour) &&
                   MatchPart(dayOfMonth, time.Day) &&
                   MatchPart(month, time.Month) &&
                   MatchPart(dayOfWeek, (int)time.DayOfWeek);
        }

        private bool MatchPart(string cronPart, int value)
        {
            if (cronPart == "*") return true;

            if (cronPart.Contains("/"))
            {
                var steps = cronPart.Split('/');
                if (steps.Length == 2 && int.TryParse(steps[1], out int step))
                {
                    return value % step == 0;
                }
            }

            if (cronPart.Contains(","))
            {
                var list = cronPart.Split(',');
                return list.Any(s => int.TryParse(s, out int item) && item == value);
            }

            if (int.TryParse(cronPart, out int exact))
            {
                return exact == value;
            }

            return false;
        }
    }
}