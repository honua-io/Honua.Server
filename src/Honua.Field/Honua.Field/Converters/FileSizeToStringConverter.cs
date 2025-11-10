using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converts file size in bytes to human-readable string
/// </summary>
public class FileSizeToStringConverter : IValueConverter
{
	private static readonly string[] Sizes = { "B", "KB", "MB", "GB", "TB" };

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value == null)
			return "0 B";

		double bytes = 0;
		if (value is long longValue)
			bytes = longValue;
		else if (value is int intValue)
			bytes = intValue;
		else if (value is double doubleValue)
			bytes = doubleValue;
		else
			return "0 B";

		int order = 0;
		while (bytes >= 1024 && order < Sizes.Length - 1)
		{
			order++;
			bytes = bytes / 1024;
		}

		return $"{bytes:0.##} {Sizes[order]}";
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
