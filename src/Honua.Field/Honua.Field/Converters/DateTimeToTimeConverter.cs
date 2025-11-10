using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converter between DateTime and TimeSpan for TimePicker
/// </summary>
public class DateTimeToTimeConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is DateTime dateTime)
		{
			return dateTime.TimeOfDay;
		}

		if (value is string stringValue && DateTime.TryParse(stringValue, out var parsedDateTime))
		{
			return parsedDateTime.TimeOfDay;
		}

		return TimeSpan.Zero;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		// This converter is used with DatePicker, so we need to combine date and time
		// The actual combination happens in the control's code-behind or ViewModel
		if (value is TimeSpan timeSpan)
		{
			return DateTime.Today.Add(timeSpan);
		}

		return null;
	}
}
