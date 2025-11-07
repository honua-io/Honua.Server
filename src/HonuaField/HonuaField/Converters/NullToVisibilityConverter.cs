using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converts null to visibility (false = collapsed, true = visible)
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; }

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		bool isNull = value == null;

		if (Invert)
			isNull = !isNull;

		return !isNull;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
