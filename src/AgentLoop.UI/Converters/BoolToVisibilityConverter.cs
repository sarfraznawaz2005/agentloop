using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AgentLoop.UI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolValue = value is true;
        if (parameter is string s && s == "Invert")
            boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is not true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is not true;
    }
}

public class JobStatusToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Data.Models.JobStatus.Success ? "\u2705" : "\u274C";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt || dt == DateTime.MinValue)
            return "Never";

        var span = DateTime.Now - dt;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hour(s) ago";
        return $"{(int)span.TotalDays} day(s) ago";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CountdownConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt || dt == DateTime.MinValue)
            return "Not scheduled";

        var span = dt - DateTime.Now;
        if (span.TotalSeconds < 0) return "Overdue";
        if (span.TotalMinutes < 1) return "< 1 min";
        if (span.TotalMinutes < 60) return $"In {(int)span.TotalMinutes} min";
        if (span.TotalHours < 24) return $"In {(int)span.TotalHours}h {span.Minutes}m";
        return $"{dt:ddd MMM dd HH:mm}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;
        return value.Equals(parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter != null)
            return parameter;
        return Binding.DoNothing;
    }
}

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;
        return value.Equals(parameter) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var count = value is int i ? i : 0;
        var invert = parameter is string s && s == "Invert";
        var hasItems = count > 0;
        if (invert) hasItems = !hasItems;
        return hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class JobScheduleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Data.Models.ScheduleModel s)
            return string.Empty;

        var description = s.Type switch
        {
            Data.Models.ScheduleType.Minute => $"Every {s.Interval} min(s)",
            Data.Models.ScheduleType.Hourly => $"Every {s.Interval} hour(s)",
            Data.Models.ScheduleType.Daily => $"Daily at {s.RunTime:hh\\:mm}",
            Data.Models.ScheduleType.DailyRecurring => $"Every {s.Interval} hour(s) starting {s.StartTime:hh\\:mm}",
            Data.Models.ScheduleType.Weekly => $"Weekly ({string.Join(",", s.DaysOfWeek.Select(d => d.ToString()[..3]))}) at {s.RunTime:hh\\:mm}",
            Data.Models.ScheduleType.MonthlyDates => $"Monthly on {string.Join(",", s.MonthDays)} at {s.RunTime:hh\\:mm}",
            _ => "Unknown"
        };

        return description;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
