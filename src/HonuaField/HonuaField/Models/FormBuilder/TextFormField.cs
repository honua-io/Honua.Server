using System.Text.RegularExpressions;

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Text input form field for single-line string values
/// Supports pattern validation, min/max length
/// </summary>
public partial class TextFormField : FormField
{
	/// <summary>
	/// Regex pattern for validation (optional)
	/// </summary>
	public string? Pattern { get; set; }

	/// <summary>
	/// Minimum length constraint
	/// </summary>
	public int? MinLength { get; set; }

	/// <summary>
	/// Maximum length constraint
	/// </summary>
	public int? MaxLength { get; set; }

	/// <summary>
	/// Placeholder text for empty field
	/// </summary>
	public string? Placeholder { get; set; }

	/// <inheritdoc/>
	public override string SchemaType => "string";

	/// <inheritdoc/>
	public override bool Validate()
	{
		// Base validation (required check)
		if (!base.Validate())
			return false;

		// No value, no further validation needed
		if (Value == null || string.IsNullOrEmpty(Value.ToString()))
			return true;

		var stringValue = Value.ToString()!;

		// Min length validation
		if (MinLength.HasValue && stringValue.Length < MinLength.Value)
		{
			ErrorMessage = $"{Label} must be at least {MinLength.Value} characters";
			HasError = true;
			return false;
		}

		// Max length validation
		if (MaxLength.HasValue && stringValue.Length > MaxLength.Value)
		{
			ErrorMessage = $"{Label} must not exceed {MaxLength.Value} characters";
			HasError = true;
			return false;
		}

		// Pattern validation
		if (!string.IsNullOrEmpty(Pattern))
		{
			try
			{
				var regex = new Regex(Pattern);
				if (!regex.IsMatch(stringValue))
				{
					ErrorMessage = $"{Label} format is invalid";
					HasError = true;
					return false;
				}
			}
			catch (Exception)
			{
				// Invalid regex pattern - log but don't fail validation
				System.Diagnostics.Debug.WriteLine($"Invalid regex pattern for field {Name}: {Pattern}");
			}
		}

		return true;
	}

	/// <inheritdoc/>
	public override object? GetTypedValue()
	{
		return Value?.ToString();
	}

	/// <inheritdoc/>
	public override void SetValue(object? value)
	{
		Value = value?.ToString();
	}
}
