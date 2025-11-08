using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converts DateTime to formatted string
/// </summary>
public class DateTimeToStringConverter : IValueConverter
{
	public string Format { get; set; } = "MMM dd, yyyy hh:mm tt";

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is DateTime dateTime)
		{
			return dateTime.ToString(Format, culture);
		}
		if (value is DateTimeOffset dateTimeOffset)
		{
			return dateTimeOffset.ToString(Format, culture);
		}
		return string.Empty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string str && DateTime.TryParse(str, culture, DateTimeStyles.None, out var dateTime))
		{
			return dateTime;
		}
		return DateTime.MinValue;
	}
}
