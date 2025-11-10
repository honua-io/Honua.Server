// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using HonuaField.Models.FormBuilder;
using System.Text.Json;
using System.Globalization;

namespace HonuaField.Services;

/// <summary>
/// Implementation of form builder service
/// Supports JSON Schema Draft 7 standard for dynamic form generation
/// </summary>
public class FormBuilderService : IFormBuilderService
{
	private readonly JsonSerializerOptions _jsonOptions;

	public FormBuilderService()
	{
		_jsonOptions = new JsonSerializerOptions
		{
			PropertyNameCaseInsensitive = true,
			WriteIndented = false
		};
	}

	/// <inheritdoc/>
	public List<FormField> ParseSchema(string jsonSchema)
	{
		if (string.IsNullOrWhiteSpace(jsonSchema))
		{
			throw new ArgumentException("JSON schema cannot be null or empty", nameof(jsonSchema));
		}

		try
		{
			var schema = JsonDocument.Parse(jsonSchema);
			return ParseSchemaDocument(schema.RootElement, null);
		}
		catch (JsonException ex)
		{
			throw new ArgumentException($"Invalid JSON schema: {ex.Message}", nameof(jsonSchema), ex);
		}
	}

	/// <inheritdoc/>
	public List<FormField> ParseSchemaWithValues(string jsonSchema, string properties)
	{
		if (string.IsNullOrWhiteSpace(jsonSchema))
		{
			throw new ArgumentException("JSON schema cannot be null or empty", nameof(jsonSchema));
		}

		try
		{
			var schema = JsonDocument.Parse(jsonSchema);

			Dictionary<string, JsonElement>? propertiesDict = null;
			if (!string.IsNullOrWhiteSpace(properties))
			{
				var propertiesDoc = JsonDocument.Parse(properties);
				propertiesDict = new Dictionary<string, JsonElement>();

				foreach (var prop in propertiesDoc.RootElement.EnumerateObject())
				{
					propertiesDict[prop.Name] = prop.Value;
				}
			}

			return ParseSchemaDocument(schema.RootElement, propertiesDict);
		}
		catch (JsonException ex)
		{
			throw new ArgumentException($"Invalid JSON: {ex.Message}", ex);
		}
	}

	/// <inheritdoc/>
	public bool ValidateForm(IEnumerable<FormField> fields)
	{
		if (fields == null)
			return true;

		var isValid = true;
		foreach (var field in fields)
		{
			if (!field.Validate())
			{
				isValid = false;
			}
		}

		return isValid;
	}

	/// <inheritdoc/>
	public Dictionary<string, string> GetValidationErrors(IEnumerable<FormField> fields)
	{
		var errors = new Dictionary<string, string>();

		if (fields == null)
			return errors;

		foreach (var field in fields)
		{
			if (!field.Validate() && !string.IsNullOrEmpty(field.ErrorMessage))
			{
				errors[field.Name] = field.ErrorMessage;
			}
		}

		return errors;
	}

	/// <inheritdoc/>
	public string SerializeForm(IEnumerable<FormField> fields)
	{
		var dict = SerializeFormToDictionary(fields);
		return JsonSerializer.Serialize(dict, _jsonOptions);
	}

	/// <inheritdoc/>
	public Dictionary<string, object?> SerializeFormToDictionary(IEnumerable<FormField> fields)
	{
		var dict = new Dictionary<string, object?>();

		if (fields == null)
			return dict;

		foreach (var field in fields)
		{
			dict[field.Name] = field.GetTypedValue();
		}

		return dict;
	}

	/// <inheritdoc/>
	public object? GetFieldValue(FormField field)
	{
		if (field == null)
			return null;

		return field.GetTypedValue();
	}

	/// <inheritdoc/>
	public void SetFieldValue(FormField field, object? value)
	{
		if (field == null)
			return;

		field.SetValue(value);
	}

	/// <inheritdoc/>
	public FormField CreateFieldFromSchema(string propertyName, JsonElement propertySchema, bool isRequired)
	{
		// Determine field type from schema
		var schemaType = GetSchemaType(propertySchema);
		var format = GetPropertyString(propertySchema, "format");
		var isMultiline = GetPropertyBool(propertySchema, "multiline");

		FormField field;

		// Create appropriate field type based on schema
		if (propertySchema.TryGetProperty("enum", out var enumProperty) && enumProperty.ValueKind == JsonValueKind.Array)
		{
			// Choice field (enum)
			field = CreateChoiceField(propertyName, propertySchema, isRequired, enumProperty);
		}
		else if (schemaType == "boolean")
		{
			field = CreateBooleanField(propertyName, propertySchema, isRequired);
		}
		else if (schemaType == "integer" || schemaType == "number")
		{
			field = CreateNumberField(propertyName, propertySchema, isRequired, schemaType == "integer");
		}
		else if (schemaType == "string")
		{
			// Check format for special types
			if (format == "date")
			{
				field = CreateDateField(propertyName, propertySchema, isRequired);
			}
			else if (format == "date-time" || format == "datetime")
			{
				field = CreateDateTimeField(propertyName, propertySchema, isRequired);
			}
			else if (isMultiline)
			{
				field = CreateMultilineTextField(propertyName, propertySchema, isRequired);
			}
			else
			{
				field = CreateTextField(propertyName, propertySchema, isRequired);
			}
		}
		else
		{
			// Default to text field
			field = CreateTextField(propertyName, propertySchema, isRequired);
		}

		return field;
	}

	#region Private Helper Methods

	private List<FormField> ParseSchemaDocument(JsonElement schemaRoot, Dictionary<string, JsonElement>? existingValues)
	{
		var fields = new List<FormField>();

		// Check if schema has "properties" object
		if (!schemaRoot.TryGetProperty("properties", out var propertiesElement))
		{
			throw new ArgumentException("Schema must contain a 'properties' object");
		}

		// Get required fields list
		var requiredFields = new HashSet<string>();
		if (schemaRoot.TryGetProperty("required", out var requiredElement) && requiredElement.ValueKind == JsonValueKind.Array)
		{
			foreach (var item in requiredElement.EnumerateArray())
			{
				if (item.ValueKind == JsonValueKind.String)
				{
					requiredFields.Add(item.GetString()!);
				}
			}
		}

		// Parse each property
		var order = 0;
		foreach (var property in propertiesElement.EnumerateObject())
		{
			var propertyName = property.Name;
			var propertySchema = property.Value;
			var isRequired = requiredFields.Contains(propertyName);

			try
			{
				var field = CreateFieldFromSchema(propertyName, propertySchema, isRequired);
				field.Order = order++;

				// Set existing value if available
				if (existingValues != null && existingValues.TryGetValue(propertyName, out var existingValue))
				{
					SetFieldValueFromJson(field, existingValue);
				}

				fields.Add(field);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"Error parsing property '{propertyName}': {ex.Message}");
				// Continue with other properties
			}
		}

		return fields.OrderBy(f => f.Order).ToList();
	}

	private TextFormField CreateTextField(string propertyName, JsonElement propertySchema, bool isRequired)
	{
		var field = new TextFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			Pattern = GetPropertyString(propertySchema, "pattern"),
			MinLength = GetPropertyInt(propertySchema, "minLength"),
			MaxLength = GetPropertyInt(propertySchema, "maxLength"),
			Placeholder = GetPropertyString(propertySchema, "placeholder")
		};

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue))
		{
			field.SetValue(defaultValue.GetString());
		}

		return field;
	}

	private MultilineTextFormField CreateMultilineTextField(string propertyName, JsonElement propertySchema, bool isRequired)
	{
		var field = new MultilineTextFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			MinLength = GetPropertyInt(propertySchema, "minLength"),
			MaxLength = GetPropertyInt(propertySchema, "maxLength"),
			Placeholder = GetPropertyString(propertySchema, "placeholder"),
			Rows = GetPropertyInt(propertySchema, "rows") ?? 4
		};

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue))
		{
			field.SetValue(defaultValue.GetString());
		}

		return field;
	}

	private NumberFormField CreateNumberField(string propertyName, JsonElement propertySchema, bool isRequired, bool isInteger)
	{
		var field = new NumberFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			IsInteger = isInteger,
			Minimum = GetPropertyDouble(propertySchema, "minimum"),
			Maximum = GetPropertyDouble(propertySchema, "maximum"),
			ExclusiveMinimum = GetPropertyBool(propertySchema, "exclusiveMinimum"),
			ExclusiveMaximum = GetPropertyBool(propertySchema, "exclusiveMaximum"),
			MultipleOf = GetPropertyDouble(propertySchema, "multipleOf"),
			Placeholder = GetPropertyString(propertySchema, "placeholder")
		};

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue))
		{
			if (defaultValue.ValueKind == JsonValueKind.Number)
			{
				field.SetValue(isInteger ? defaultValue.GetInt32() : defaultValue.GetDouble());
			}
		}

		return field;
	}

	private BooleanFormField CreateBooleanField(string propertyName, JsonElement propertySchema, bool isRequired)
	{
		var field = new BooleanFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			TrueLabel = GetPropertyString(propertySchema, "trueLabel"),
			FalseLabel = GetPropertyString(propertySchema, "falseLabel")
		};

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue))
		{
			if (defaultValue.ValueKind == JsonValueKind.True || defaultValue.ValueKind == JsonValueKind.False)
			{
				field.SetValue(defaultValue.GetBoolean());
			}
		}
		else
		{
			// Default to false for booleans
			field.SetValue(false);
		}

		return field;
	}

	private ChoiceFormField CreateChoiceField(string propertyName, JsonElement propertySchema, bool isRequired, JsonElement enumProperty)
	{
		var choices = new List<string>();
		var choiceLabels = new Dictionary<string, string>();

		// Parse enum values
		foreach (var enumValue in enumProperty.EnumerateArray())
		{
			if (enumValue.ValueKind == JsonValueKind.String)
			{
				var value = enumValue.GetString()!;
				choices.Add(value);
			}
		}

		// Parse enum labels if available (non-standard extension)
		if (propertySchema.TryGetProperty("enumLabels", out var enumLabels) && enumLabels.ValueKind == JsonValueKind.Array)
		{
			var labelsList = new List<string>();
			foreach (var label in enumLabels.EnumerateArray())
			{
				if (label.ValueKind == JsonValueKind.String)
				{
					labelsList.Add(label.GetString()!);
				}
			}

			// Map values to labels
			for (int i = 0; i < Math.Min(choices.Count, labelsList.Count); i++)
			{
				choiceLabels[choices[i]] = labelsList[i];
			}
		}

		var field = new ChoiceFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			Choices = choices,
			ChoiceLabels = choiceLabels.Count > 0 ? choiceLabels : null,
			Placeholder = GetPropertyString(propertySchema, "placeholder")
		};

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue))
		{
			field.SetValue(defaultValue.GetString());
		}

		return field;
	}

	private DateFormField CreateDateField(string propertyName, JsonElement propertySchema, bool isRequired)
	{
		var field = new DateFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			DateFormat = GetPropertyString(propertySchema, "dateFormat") ?? "yyyy-MM-dd"
		};

		// Parse min/max dates
		var minDateString = GetPropertyString(propertySchema, "minimum");
		if (!string.IsNullOrEmpty(minDateString) && DateTime.TryParse(minDateString, out var minDate))
		{
			field.MinimumDate = minDate;
		}

		var maxDateString = GetPropertyString(propertySchema, "maximum");
		if (!string.IsNullOrEmpty(maxDateString) && DateTime.TryParse(maxDateString, out var maxDate))
		{
			field.MaximumDate = maxDate;
		}

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.String)
		{
			var defaultString = defaultValue.GetString();
			if (!string.IsNullOrEmpty(defaultString) && DateTime.TryParse(defaultString, out var defaultDate))
			{
				field.SetValue(defaultDate);
			}
		}

		return field;
	}

	private DateTimeFormField CreateDateTimeField(string propertyName, JsonElement propertySchema, bool isRequired)
	{
		var field = new DateTimeFormField
		{
			Name = propertyName,
			Label = GetPropertyString(propertySchema, "title") ?? propertyName,
			HelpText = GetPropertyString(propertySchema, "description"),
			IsRequired = isRequired,
			DateTimeFormat = GetPropertyString(propertySchema, "dateTimeFormat") ?? "yyyy-MM-ddTHH:mm:ss"
		};

		// Parse min/max datetimes
		var minDateTimeString = GetPropertyString(propertySchema, "minimum");
		if (!string.IsNullOrEmpty(minDateTimeString) && DateTime.TryParse(minDateTimeString, out var minDateTime))
		{
			field.MinimumDateTime = minDateTime;
		}

		var maxDateTimeString = GetPropertyString(propertySchema, "maximum");
		if (!string.IsNullOrEmpty(maxDateTimeString) && DateTime.TryParse(maxDateTimeString, out var maxDateTime))
		{
			field.MaximumDateTime = maxDateTime;
		}

		// Set default value
		if (propertySchema.TryGetProperty("default", out var defaultValue) && defaultValue.ValueKind == JsonValueKind.String)
		{
			var defaultString = defaultValue.GetString();
			if (!string.IsNullOrEmpty(defaultString) && DateTime.TryParse(defaultString, out var defaultDateTime))
			{
				field.SetValue(defaultDateTime);
			}
		}

		return field;
	}

	private void SetFieldValueFromJson(FormField field, JsonElement value)
	{
		switch (value.ValueKind)
		{
			case JsonValueKind.String:
				field.SetValue(value.GetString());
				break;
			case JsonValueKind.Number:
				field.SetValue(value.GetDouble());
				break;
			case JsonValueKind.True:
			case JsonValueKind.False:
				field.SetValue(value.GetBoolean());
				break;
			case JsonValueKind.Null:
				field.SetValue(null);
				break;
			default:
				// For complex types, convert to string
				field.SetValue(value.ToString());
				break;
		}
	}

	private string GetSchemaType(JsonElement propertySchema)
	{
		if (propertySchema.TryGetProperty("type", out var typeProperty) && typeProperty.ValueKind == JsonValueKind.String)
		{
			return typeProperty.GetString()?.ToLowerInvariant() ?? "string";
		}
		return "string";
	}

	private string? GetPropertyString(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
		{
			return property.GetString();
		}
		return null;
	}

	private int? GetPropertyInt(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number)
		{
			return property.GetInt32();
		}
		return null;
	}

	private double? GetPropertyDouble(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number)
		{
			return property.GetDouble();
		}
		return null;
	}

	private bool GetPropertyBool(JsonElement element, string propertyName)
	{
		if (element.TryGetProperty(propertyName, out var property))
		{
			if (property.ValueKind == JsonValueKind.True)
				return true;
			if (property.ValueKind == JsonValueKind.False)
				return false;
		}
		return false;
	}

	#endregion
}
