using AgentLoop.Data.Models;
using System.Text;

namespace AgentLoop.Core.Helpers;

public static class ScheduleDescriptionHelper
{
    public static string GetDescription(ScheduleModel schedule)
    {
        if (schedule == null) return "Unknown";

        return schedule.Type switch
        {
            ScheduleType.Minute => $"Every {schedule.Interval} minute(s)",

            ScheduleType.Hourly => GetHourlyDescription(schedule),

            ScheduleType.Daily => $"Daily at {FormatTime(schedule.RunTime)}",

            ScheduleType.DailyRecurring => $"Every {schedule.Interval} hour(s) starting at {FormatTime(schedule.StartTime ?? TimeSpan.Zero)}",

            ScheduleType.Weekly => GetWeeklyDescription(schedule),

            ScheduleType.MonthlyDates => GetMonthlyDescription(schedule),

            _ => "Unknown schedule"
        };
    }

    private static string GetHourlyDescription(ScheduleModel schedule)
    {
        if (schedule.HasTimeRestrictions && schedule.TimeWindowStart.HasValue && schedule.TimeWindowEnd.HasValue)
        {
            return $"Every {schedule.Interval} hour(s) from {FormatTime(schedule.TimeWindowStart.Value)} to {FormatTime(schedule.TimeWindowEnd.Value)}";
        }
        return $"Every {schedule.Interval} hour(s)";
    }

    private static string GetWeeklyDescription(ScheduleModel schedule)
    {
        if (schedule.DaysOfWeek == null || schedule.DaysOfWeek.Count == 0)
            return $"Weekly (No days selected) at {FormatTime(schedule.RunTime)}";

        var days = string.Join(", ", schedule.DaysOfWeek.Select(d => d.ToString()[..3]));
        return $"Weekly on {days} at {FormatTime(schedule.RunTime)}";
    }

    private static string GetMonthlyDescription(ScheduleModel schedule)
    {
        if (schedule.MonthDays == null || schedule.MonthDays.Count == 0)
            return $"Monthly (No days selected) at {FormatTime(schedule.RunTime)}";

        var days = string.Join(", ", schedule.MonthDays.Select(d => d == 32 ? "Last" : d.ToString()));
        return $"Monthly on day(s) {days} at {FormatTime(schedule.RunTime)}";
    }

    private static string FormatTime(TimeSpan time)
    {
        return time.ToString(@"hh\:mm");
    }
}
