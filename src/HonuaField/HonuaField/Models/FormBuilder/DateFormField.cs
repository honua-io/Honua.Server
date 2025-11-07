using System.Globalization;

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Date form field for date-only values (no time)
/// Rendered as DatePicker
/// </summary>
public partial class DateFormField : FormField
{
	/// <summary>
	/// Minimum allowed date
	/// </summary>
	public DateTime? MinimumDate { get; set; }

	/// <summary>
	/// Maximum allowed date
	/// </summary>
	public DateTime? MaximumDate { get; set; }

	/// <summary>
	/// Date format for display (default: "yyyy-MM-dd")
	/// </summary>
	public string DateFormat { get; set; } = "yyyy-MM-dd";

	/// <inheritdoc/>
	public override string SchemaType => "string"; // ISO 8601 date format

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
		DateTime dateValue;
		if (Value is DateTime dt)
		{
			dateValue = dt;
		}
		else if (!DateTime.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dateValue))
		{
			ErrorMessage = $"{Label} must be a valid date";
			HasError = true;
			return false;
		}

		// Minimum date validation
		if (MinimumDate.HasValue && dateValue.Date < MinimumDate.Value.Date)
		{
			ErrorMessage = $"{Label} must be on or after {MinimumDate.Value:yyyy-MM-dd}";
			HasError = true;
			return false;
		}

		// Maximum date validation
		if (MaximumDate.HasValue && dateValue.Date > MaximumDate.Value.Date)
		{
			ErrorMessage = $"{Label} must be on or before {MaximumDate.Value:yyyy-MM-dd}";
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
			return dt.Date;

		if (DateTime.TryParse(Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
			return dateValue.Date;

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
			Value = dt.Date;
		}
		else if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
		{
			Value = dateValue.Date;
		}
		else
		{
			Value = value;
		}
	}
}
