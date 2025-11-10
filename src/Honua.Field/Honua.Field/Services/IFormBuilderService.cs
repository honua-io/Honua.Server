// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models.FormBuilder;
using System.Text.Json;

namespace HonuaField.Services;

/// <summary>
/// Service for building dynamic forms from JSON schemas
/// Handles schema parsing, form generation, validation, and serialization
/// </summary>
public interface IFormBuilderService
{
	/// <summary>
	/// Parse a JSON schema and generate form fields
	/// Supports JSON Schema Draft 7 standard
	/// </summary>
	/// <param name="jsonSchema">JSON schema string (must contain "properties" object)</param>
	/// <returns>List of form fields ordered by schema definition</returns>
	/// <exception cref="ArgumentException">If schema is invalid or cannot be parsed</exception>
	List<FormField> ParseSchema(string jsonSchema);

	/// <summary>
	/// Parse a JSON schema with existing property values
	/// Populates form fields with values from properties dictionary
	/// </summary>
	/// <param name="jsonSchema">JSON schema string</param>
	/// <param name="properties">Existing property values</param>
	/// <returns>List of form fields with values populated</returns>
	List<FormField> ParseSchemaWithValues(string jsonSchema, string properties);

	/// <summary>
	/// Validate all fields in a form
	/// </summary>
	/// <param name="fields">Form fields to validate</param>
	/// <returns>True if all fields are valid, false otherwise</returns>
	bool ValidateForm(IEnumerable<FormField> fields);

	/// <summary>
	/// Get all validation errors from a form
	/// </summary>
	/// <param name="fields">Form fields to check</param>
	/// <returns>Dictionary mapping field names to error messages</returns>
	Dictionary<string, string> GetValidationErrors(IEnumerable<FormField> fields);

	/// <summary>
	/// Serialize form fields to JSON properties string
	/// Converts field values to appropriate JSON types
	/// </summary>
	/// <param name="fields">Form fields to serialize</param>
	/// <returns>JSON string representing properties</returns>
	string SerializeForm(IEnumerable<FormField> fields);

	/// <summary>
	/// Serialize form fields to a dictionary
	/// </summary>
	/// <param name="fields">Form fields to serialize</param>
	/// <returns>Dictionary of property names to typed values</returns>
	Dictionary<string, object?> SerializeFormToDictionary(IEnumerable<FormField> fields);

	/// <summary>
	/// Get typed value from a form field
	/// Performs type conversion based on field type
	/// </summary>
	/// <param name="field">Form field to get value from</param>
	/// <returns>Typed value or null</returns>
	object? GetFieldValue(FormField field);

	/// <summary>
	/// Set field value with type conversion
	/// </summary>
	/// <param name="field">Form field to set value on</param>
	/// <param name="value">Value to set</param>
	void SetFieldValue(FormField field, object? value);

	/// <summary>
	/// Create a form field from a schema property definition
	/// </summary>
	/// <param name="propertyName">Name of the property</param>
	/// <param name="propertySchema">JSON schema for the property</param>
	/// <param name="isRequired">Whether this property is required</param>
	/// <returns>Form field instance</returns>
	FormField CreateFieldFromSchema(string propertyName, JsonElement propertySchema, bool isRequired);
}
