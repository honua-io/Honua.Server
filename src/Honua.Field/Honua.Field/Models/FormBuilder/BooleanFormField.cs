// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Boolean form field for true/false values
/// Rendered as switch or checkbox
/// </summary>
public partial class BooleanFormField : FormField
{
	/// <summary>
	/// Text to display when true
	/// </summary>
	public string? TrueLabel { get; set; }

	/// <summary>
	/// Text to display when false
	/// </summary>
	public string? FalseLabel { get; set; }

	/// <inheritdoc/>
	public override string SchemaType => "boolean";

	/// <inheritdoc/>
	public override bool Validate()
	{
		// Base validation (required check)
		// Note: For boolean fields, required means it must be explicitly set
		// A false value is still a valid value
		if (!base.Validate())
		{
			// Override error message for boolean - they can't really be "empty"
			if (IsRequired)
			{
				ErrorMessage = $"{Label} must be set";
				HasError = true;
				return false;
			}
		}

		// Boolean values are always valid if present
		return true;
	}

	/// <inheritdoc/>
	public override object? GetTypedValue()
	{
		if (Value == null)
			return false; // Default to false for boolean

		if (Value is bool boolValue)
			return boolValue;

		// Try to parse string values
		if (bool.TryParse(Value.ToString(), out var parsedValue))
			return parsedValue;

		// Default to false
		return false;
	}

	/// <inheritdoc/>
	public override void SetValue(object? value)
	{
		if (value == null)
		{
			Value = false;
			return;
		}

		if (value is bool boolValue)
		{
			Value = boolValue;
		}
		else if (bool.TryParse(value.ToString(), out var parsedValue))
		{
			Value = parsedValue;
		}
		else
		{
			// Try to interpret as truthy/falsy
			var stringValue = value.ToString()?.ToLowerInvariant();
			Value = stringValue is "1" or "yes" or "on" or "true";
		}
	}
}
