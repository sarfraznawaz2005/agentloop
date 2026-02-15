namespace AgentLoop.Data.Models;

public class ScheduleModel
{
    public ScheduleType Type { get; set; } = ScheduleType.Daily;

    // Minute / Hourly / DailyRecurring interval
    public int Interval { get; set; } = 1;

    // Daily / Weekly / Monthly time
    public TimeSpan RunTime { get; set; } = new(9, 0, 0);

    // DailyRecurring start time
    public TimeSpan? StartTime { get; set; }

    // Weekly: days of week (0=Sunday .. 6=Saturday)
    public List<DayOfWeek> DaysOfWeek { get; set; } = [];

    // Monthly: specific dates (1-31), 32 = last day of month
    public List<int> MonthDays { get; set; } = [];

    // Time restrictions (Hourly / DailyRecurring)
    public bool HasTimeRestrictions { get; set; }
    public List<DayOfWeek> RestrictedDays { get; set; } = [];
    public TimeSpan? TimeWindowStart { get; set; }
    public TimeSpan? TimeWindowEnd { get; set; }
}
