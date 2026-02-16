using System.Globalization;
using System.Windows.Data;

namespace AgentLoop.UI.Converters;

public class SingleLineConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // Replace newlines with spaces and trim extra spaces
            return s.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
