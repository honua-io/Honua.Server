using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converter that returns true if value is not null
/// </summary>
public class IsNotNullConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		return value != null;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
