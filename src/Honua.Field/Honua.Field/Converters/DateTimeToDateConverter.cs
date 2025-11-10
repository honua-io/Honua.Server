using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converter between DateTime and DateTime.Date for DatePicker
/// </summary>
public class DateTimeToDateConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is DateTime dateTime)
		{
			return dateTime.Date;
		}

		if (value is string stringValue && DateTime.TryParse(stringValue, out var parsedDateTime))
		{
			return parsedDateTime.Date;
		}

		return DateTime.Today;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is DateTime dateTime)
		{
			return dateTime;
		}

		return null;
	}
}
