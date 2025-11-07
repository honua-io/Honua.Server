using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converts empty string to visibility (false = collapsed, true = visible)
/// </summary>
public class EmptyStringToVisibilityConverter : IValueConverter
{
	public bool Invert { get; set; }

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		bool isEmpty = value == null || string.IsNullOrWhiteSpace(value.ToString());

		if (Invert)
			isEmpty = !isEmpty;

		return !isEmpty;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
