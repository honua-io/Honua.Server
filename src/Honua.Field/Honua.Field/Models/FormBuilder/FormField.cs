// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HonuaField.Models.FormBuilder;

/// <summary>
/// Base class for all form field types
/// Provides common properties and validation support
/// </summary>
public abstract partial class FormField : ObservableObject
{
	/// <summary>
	/// Property name in the JSON schema
	/// </summary>
	[ObservableProperty]
	private string _name = string.Empty;

	/// <summary>
	/// Display label for the field
	/// </summary>
	[ObservableProperty]
	private string _label = string.Empty;

	/// <summary>
	/// Help text or description for the field
	/// </summary>
	[ObservableProperty]
	private string? _helpText;

	/// <summary>
	/// Whether this field is required
	/// </summary>
	[ObservableProperty]
	private bool _isRequired;

	/// <summary>
	/// Whether this field is read-only
	/// </summary>
	[ObservableProperty]
	private bool _isReadOnly;

	/// <summary>
	/// Order/position in the form
	/// </summary>
	[ObservableProperty]
	private int _order;

	/// <summary>
	/// Field value (type varies by field type)
	/// </summary>
	[ObservableProperty]
	private object? _value;

	/// <summary>
	/// Validation error message (null if valid)
	/// </summary>
	[ObservableProperty]
	private string? _errorMessage;

	/// <summary>
	/// Whether this field has validation errors
	/// </summary>
	[ObservableProperty]
	private bool _hasError;

	/// <summary>
	/// JSON Schema type for this field
	/// </summary>
	public abstract string SchemaType { get; }

	/// <summary>
	/// Validate the field value
	/// Returns true if valid, false otherwise
	/// Sets ErrorMessage if invalid
	/// </summary>
	public virtual bool Validate()
	{
		ErrorMessage = null;
		HasError = false;

		// Check required
		if (IsRequired && (Value == null || string.IsNullOrWhiteSpace(Value.ToString())))
		{
			ErrorMessage = $"{Label} is required";
			HasError = true;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Get the typed value from this field
	/// </summary>
	public virtual object? GetTypedValue()
	{
		return Value;
	}

	/// <summary>
	/// Set the value with type conversion
	/// </summary>
	public virtual void SetValue(object? value)
	{
		Value = value;
	}

	/// <summary>
	/// Clear validation errors
	/// </summary>
	public void ClearError()
	{
		ErrorMessage = null;
		HasError = false;
	}
}
