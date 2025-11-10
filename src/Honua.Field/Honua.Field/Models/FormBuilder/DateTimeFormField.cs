// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// DateTime form field for date and time values
/// Rendered as DatePicker + TimePicker
/// </summary>
public partial class DateTimeFormField : FormField
{
	/// <summary>
	/// Minimum allowed date/time
	/// </summary>
	public DateTime? MinimumDateTime { get; set; }

	/// <summary>
	/// Maximum allowed date/time
	/// </summary>
	public DateTime? MaximumDateTime { get; set; }

	/// <summary>
	/// DateTime format for display (default: ISO 8601)
	/// </summary>
	public string DateTimeFormat { get; set; } = "yyyy-MM-ddTHH:mm:ss";

	/// <inheritdoc/>
	public override string SchemaType => "string"; // ISO 8601 datetime format

	/// <inheritdoc/>
	public override bool Validate()
	{
		// Base validation (required check)
		if (!base.Validate())
			return false;

		// No value, no further validation needed
		if (Value == null || string.IsNullOrWhiteSpace(Value.ToString()))
			return true;

		// Try to parse as DateTime
		DateTime dateTimeValue;
		if (Value is DateTime dt)
		{
			dateTimeValue = dt;
		}
		else if (!DateTime.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTimeValue))
		{
			ErrorMessage = $"{Label} must be a valid date and time";
			HasError = true;
			return false;
		}

		// Minimum datetime validation
		if (MinimumDateTime.HasValue && dateTimeValue < MinimumDateTime.Value)
		{
			ErrorMessage = $"{Label} must be on or after {MinimumDateTime.Value:yyyy-MM-dd HH:mm}";
			HasError = true;
			return false;
		}

		// Maximum datetime validation
		if (MaximumDateTime.HasValue && dateTimeValue > MaximumDateTime.Value)
		{
			ErrorMessage = $"{Label} must be on or before {MaximumDateTime.Value:yyyy-MM-dd HH:mm}";
			HasError = true;
			return false;
		}

		return true;
	}

	/// <inheritdoc/>
	public override object? GetTypedValue()
	{
		if (Value == null)
			return null;

		if (Value is DateTime dt)
			return dt;

		if (DateTime.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeValue))
			return dateTimeValue;

		return null;
	}

	/// <inheritdoc/>
	public override void SetValue(object? value)
	{
		if (value == null)
		{
			Value = null;
			return;
		}

		if (value is DateTime dt)
		{
			Value = dt;
		}
		else if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeValue))
		{
			Value = dateTimeValue;
		}
		else
		{
			Value = value;
		}
	}
}
