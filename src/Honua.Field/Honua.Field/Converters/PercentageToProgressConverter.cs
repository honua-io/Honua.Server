// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converter that converts a percentage (0-100) to progress value (0.0-1.0)
/// </summary>
public class PercentageToProgressConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int intValue)
		{
			return intValue / 100.0;
		}

		if (value is double doubleValue)
		{
			return doubleValue / 100.0;
		}

		return 0.0;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is double doubleValue)
		{
			return (int)(doubleValue * 100);
		}

		return 0;
	}
}
