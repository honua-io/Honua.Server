// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Number input form field for integer or decimal values
/// Supports min/max value constraints
/// </summary>
public partial class NumberFormField : FormField
{
	/// <summary>
	/// Whether this is an integer field (vs decimal)
	/// </summary>
	public bool IsInteger { get; set; }

	/// <summary>
	/// Minimum value constraint
	/// </summary>
	public double? Minimum { get; set; }

	/// <summary>
	/// Maximum value constraint
	/// </summary>
	public double? Maximum { get; set; }

	/// <summary>
	/// Whether minimum is exclusive (vs inclusive)
	/// </summary>
	public bool ExclusiveMinimum { get; set; }

	/// <summary>
	/// Whether maximum is exclusive (vs inclusive)
	/// </summary>
	public bool ExclusiveMaximum { get; set; }

	/// <summary>
	/// Multiple of constraint (number must be multiple of this value)
	/// </summary>
	public double? MultipleOf { get; set; }

	/// <summary>
	/// Placeholder text for empty field
	/// </summary>
	public string? Placeholder { get; set; }

	/// <inheritdoc/>
	public override string SchemaType => IsInteger ? "integer" : "number";

	/// <inheritdoc/>
	public override bool Validate()
	{
		// Base validation (required check)
		if (!base.Validate())
			return false;

		// No value, no further validation needed
		if (Value == null || string.IsNullOrWhiteSpace(Value.ToString()))
			return true;

		// Parse as number
		if (!double.TryParse(Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
		{
			ErrorMessage = $"{Label} must be a valid number";
			HasError = true;
			return false;
		}

		// Integer validation
		if (IsInteger && numericValue != Math.Floor(numericValue))
		{
			ErrorMessage = $"{Label} must be an integer";
			HasError = true;
			return false;
		}

		// Minimum validation
		if (Minimum.HasValue)
		{
			if (ExclusiveMinimum && numericValue <= Minimum.Value)
			{
				ErrorMessage = $"{Label} must be greater than {Minimum.Value}";
				HasError = true;
				return false;
			}
			else if (!ExclusiveMinimum && numericValue < Minimum.Value)
			{
				ErrorMessage = $"{Label} must be at least {Minimum.Value}";
				HasError = true;
				return false;
			}
		}

		// Maximum validation
		if (Maximum.HasValue)
		{
			if (ExclusiveMaximum && numericValue >= Maximum.Value)
			{
				ErrorMessage = $"{Label} must be less than {Maximum.Value}";
				HasError = true;
				return false;
			}
			else if (!ExclusiveMaximum && numericValue > Maximum.Value)
			{
				ErrorMessage = $"{Label} must not exceed {Maximum.Value}";
				HasError = true;
				return false;
			}
		}

		// Multiple of validation
		if (MultipleOf.HasValue && MultipleOf.Value > 0)
		{
			var remainder = numericValue % MultipleOf.Value;
			if (Math.Abs(remainder) > 0.0001) // Allow small floating point errors
			{
				ErrorMessage = $"{Label} must be a multiple of {MultipleOf.Value}";
				HasError = true;
				return false;
			}
		}

		return true;
	}

	/// <inheritdoc/>
	public override object? GetTypedValue()
	{
		if (Value == null || string.IsNullOrWhiteSpace(Value.ToString()))
			return null;

		if (double.TryParse(Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
		{
			return IsInteger ? (int)numericValue : numericValue;
		}

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

		// Handle various numeric types
		if (value is int || value is long || value is short || value is byte)
		{
			Value = value.ToString();
		}
		else if (value is double || value is float || value is decimal)
		{
			Value = Convert.ToDouble(value).ToString(CultureInfo.InvariantCulture);
		}
		else if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var numericValue))
		{
			Value = numericValue.ToString(CultureInfo.InvariantCulture);
		}
		else
		{
			Value = value.ToString();
		}
	}
}
