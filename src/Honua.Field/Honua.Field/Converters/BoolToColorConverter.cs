// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace HonuaField.Converters;

/// <summary>
/// Converts boolean value to Color
/// </summary>
public class BoolToColorConverter : IValueConverter
{
	public Color TrueColor { get; set; } = Colors.Green;
	public Color FalseColor { get; set; } = Colors.Red;

	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is bool boolValue)
		{
			return boolValue ? TrueColor : FalseColor;
		}
		return FalseColor;
	}

	public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
