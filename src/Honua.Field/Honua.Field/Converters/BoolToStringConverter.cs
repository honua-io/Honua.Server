using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converter that converts a boolean to a string
/// Parameter format: "TrueString|FalseString"
/// </summary>
public class BoolToStringConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool boolValue && parameter is string paramString)
		{
			var parts = paramString.Split('|');
			if (parts.Length == 2)
			{
				return boolValue ? parts[0] : parts[1];
			}
		}

		return value?.ToString() ?? string.Empty;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
