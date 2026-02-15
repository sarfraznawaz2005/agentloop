using AgentLoop.Core.Helpers;
using AgentLoop.Data.Models;
using Xunit;

namespace AgentLoop.Tests;

public class ScheduleDescriptionHelperTests
{
    [Fact]
    public void GetDescription_Minute_Works()
    {
        var schedule = new ScheduleModel { Type = ScheduleType.Minute, Interval = 15 };
        var description = ScheduleDescriptionHelper.GetDescription(schedule);
        Assert.Equal("Every 15 minute(s)", description);
    }

    [Fact]
    public void GetDescription_Daily_Works()
    {
        var schedule = new ScheduleModel { Type = ScheduleType.Daily, RunTime = new TimeSpan(14, 30, 0) };
        var description = ScheduleDescriptionHelper.GetDescription(schedule);
        Assert.Equal("Daily at 14:30", description);
    }

    [Fact]
    public void GetDescription_Weekly_Works()
    {
        var schedule = new ScheduleModel
        {
            Type = ScheduleType.Weekly,
            RunTime = new TimeSpan(9, 0, 0),
            DaysOfWeek = [DayOfWeek.Monday, DayOfWeek.Wednesday]
        };
        var description = ScheduleDescriptionHelper.GetDescription(schedule);
        Assert.Equal("Weekly on Mon, Wed at 09:00", description);
    }

    [Fact]
    public void GetDescription_Hourly_WithRestrictions_Works()
    {
        var schedule = new ScheduleModel
        {
            Type = ScheduleType.Hourly,
            Interval = 2,
            HasTimeRestrictions = true,
            TimeWindowStart = new TimeSpan(8, 0, 0),
            TimeWindowEnd = new TimeSpan(18, 0, 0)
        };
        var description = ScheduleDescriptionHelper.GetDescription(schedule);
        Assert.Equal("Every 2 hour(s) from 08:00 to 18:00", description);
    }

    [Fact]
    public void GetDescription_Monthly_Works()
    {
        var schedule = new ScheduleModel
        {
            Type = ScheduleType.MonthlyDates,
            RunTime = new TimeSpan(23, 59, 0),
            MonthDays = [1, 15, 32]
        };
        var description = ScheduleDescriptionHelper.GetDescription(schedule);
        Assert.Equal("Monthly on day(s) 1, 15, Last at 23:59", description);
    }
}
