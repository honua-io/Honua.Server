namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Multiline text form field for long text values
/// Rendered as Editor (textarea)
/// </summary>
public partial class MultilineTextFormField : FormField
{
	/// <summary>
	/// Minimum length constraint
	/// </summary>
	public int? MinLength { get; set; }

	/// <summary>
	/// Maximum length constraint
	/// </summary>
	public int? MaxLength { get; set; }

	/// <summary>
	/// Number of rows to display (UI hint)
	/// </summary>
	public int Rows { get; set; } = 4;

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
