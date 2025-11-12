// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Choice form field for selecting from predefined options
/// Rendered as picker/dropdown or radio buttons
/// </summary>
public partial class ChoiceFormField : FormField
{
	/// <summary>
	/// Available choices for selection
	/// </summary>
	public List<string> Choices { get; set; } = new();

	/// <summary>
	/// Display labels for choices (if different from values)
	/// </summary>
	public Dictionary<string, string>? ChoiceLabels { get; set; }

	/// <summary>
	/// Whether to allow custom values (not in the choices list)
	/// </summary>
	public bool AllowCustomValue { get; set; }

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
		if (Value == null || string.IsNullOrWhiteSpace(Value.ToString()))
			return true;

		var stringValue = Value.ToString()!;

		// Check if value is in allowed choices (unless custom values allowed)
		if (!AllowCustomValue && !Choices.Contains(stringValue))
		{
			ErrorMessage = $"{Label} must be one of the allowed values";
			HasError = true;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Get display label for a choice value
	/// </summary>
	public string GetChoiceLabel(string value)
	{
		if (ChoiceLabels != null && ChoiceLabels.TryGetValue(value, out var label))
		{
			return label;
		}
		return value;
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
