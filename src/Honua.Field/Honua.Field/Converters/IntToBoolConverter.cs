// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converter that converts an integer to a boolean
/// Returns true if the integer is greater than 0, false otherwise
/// </summary>
public class IntToBoolConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is int intValue)
		{
			return intValue > 0;
		}

		return false;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
