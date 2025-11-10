using FluentAssertions;
using HonuaField.Models.FormBuilder;
using HonuaField.Services;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Comprehensive tests for FormBuilderService
/// Tests JSON Schema parsing, validation, and serialization
/// </summary>
public class FormBuilderServiceTests
{
	private readonly FormBuilderService _service;

	public FormBuilderServiceTests()
	{
		_service = new FormBuilderService();
	}

	#region ParseSchema Tests

	[Fact]
	public void ParseSchema_WithValidSchema_ReturnsFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""name"": {
					""type"": ""string"",
					""title"": ""Name"",
					""description"": ""Enter your name""
				},
				""age"": {
					""type"": ""integer"",
					""title"": ""Age""
				}
			},
			""required"": [""name""]
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(2);
		fields[0].Should().BeOfType<TextFormField>();
		fields[0].Name.Should().Be("name");
		fields[0].Label.Should().Be("Name");
		fields[0].IsRequired.Should().BeTrue();
		fields[1].Should().BeOfType<NumberFormField>();
		fields[1].Name.Should().Be("age");
		fields[1].IsRequired.Should().BeFalse();
	}

	[Fact]
	public void ParseSchema_WithNullSchema_ThrowsArgumentException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _service.ParseSchema(null!));
	}

	[Fact]
	public void ParseSchema_WithEmptySchema_ThrowsArgumentException()
	{
		// Act & Assert
		Assert.Throws<ArgumentException>(() => _service.ParseSchema(""));
	}

	[Fact]
	public void ParseSchema_WithInvalidJson_ThrowsArgumentException()
	{
		// Arrange
		var invalidJson = "{ invalid json }";

		// Act & Assert
		Assert.Throws<ArgumentException>(() => _service.ParseSchema(invalidJson));
	}

	[Fact]
	public void ParseSchema_WithoutPropertiesObject_ThrowsArgumentException()
	{
		// Arrange
		var schema = @"{ ""type"": ""object"" }";

		// Act & Assert
		Assert.Throws<ArgumentException>(() => _service.ParseSchema(schema));
	}

	[Fact]
	public void ParseSchema_WithTextFields_CreatesTextFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""description"": {
					""type"": ""string"",
					""title"": ""Description"",
					""minLength"": 10,
					""maxLength"": 500,
					""pattern"": ""^[A-Za-z0-9 ]+$""
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var field = fields[0] as TextFormField;
		field.Should().NotBeNull();
		field!.MinLength.Should().Be(10);
		field.MaxLength.Should().Be(500);
		field.Pattern.Should().Be("^[A-Za-z0-9 ]+$");
	}

	[Fact]
	public void ParseSchema_WithMultilineText_CreatesMultilineTextFormField()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""notes"": {
					""type"": ""string"",
					""title"": ""Notes"",
					""multiline"": true,
					""rows"": 6
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var field = fields[0] as MultilineTextFormField;
		field.Should().NotBeNull();
		field!.Rows.Should().Be(6);
	}

	[Fact]
	public void ParseSchema_WithNumberFields_CreatesNumberFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""price"": {
					""type"": ""number"",
					""title"": ""Price"",
					""minimum"": 0,
					""maximum"": 1000,
					""multipleOf"": 0.01
				},
				""count"": {
					""type"": ""integer"",
					""title"": ""Count"",
					""minimum"": 1,
					""exclusiveMinimum"": true
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(2);

		var priceField = fields[0] as NumberFormField;
		priceField.Should().NotBeNull();
		priceField!.IsInteger.Should().BeFalse();
		priceField.Minimum.Should().Be(0);
		priceField.Maximum.Should().Be(1000);
		priceField.MultipleOf.Should().Be(0.01);

		var countField = fields[1] as NumberFormField;
		countField.Should().NotBeNull();
		countField!.IsInteger.Should().BeTrue();
		countField.ExclusiveMinimum.Should().BeTrue();
	}

	[Fact]
	public void ParseSchema_WithBooleanFields_CreatesBooleanFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""active"": {
					""type"": ""boolean"",
					""title"": ""Active"",
					""default"": true
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var field = fields[0] as BooleanFormField;
		field.Should().NotBeNull();
		field!.Value.Should().Be(true);
	}

	[Fact]
	public void ParseSchema_WithEnumFields_CreatesChoiceFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""status"": {
					""type"": ""string"",
					""title"": ""Status"",
					""enum"": [""active"", ""inactive"", ""pending""],
					""enumLabels"": [""Active"", ""Inactive"", ""Pending""]
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var field = fields[0] as ChoiceFormField;
		field.Should().NotBeNull();
		field!.Choices.Should().HaveCount(3);
		field.Choices.Should().Contain("active");
		field.ChoiceLabels.Should().NotBeNull();
		field.ChoiceLabels!["active"].Should().Be("Active");
	}

	[Fact]
	public void ParseSchema_WithDateFields_CreatesDateFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""startDate"": {
					""type"": ""string"",
					""format"": ""date"",
					""title"": ""Start Date"",
					""minimum"": ""2024-01-01"",
					""maximum"": ""2024-12-31""
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var field = fields[0] as DateFormField;
		field.Should().NotBeNull();
		field!.MinimumDate.Should().Be(new DateTime(2024, 1, 1));
		field.MaximumDate.Should().Be(new DateTime(2024, 12, 31));
	}

	[Fact]
	public void ParseSchema_WithDateTimeFields_CreatesDateTimeFormFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""timestamp"": {
					""type"": ""string"",
					""format"": ""date-time"",
					""title"": ""Timestamp""
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var field = fields[0] as DateTimeFormField;
		field.Should().NotBeNull();
	}

	[Fact]
	public void ParseSchema_WithDefaultValues_SetsFieldValues()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""name"": {
					""type"": ""string"",
					""default"": ""John Doe""
				},
				""age"": {
					""type"": ""integer"",
					""default"": 30
				},
				""active"": {
					""type"": ""boolean"",
					""default"": true
				}
			}
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields[0].Value.Should().Be("John Doe");
		fields[1].Value.Should().Be("30");
		fields[2].Value.Should().Be(true);
	}

	#endregion

	#region ParseSchemaWithValues Tests

	[Fact]
	public void ParseSchemaWithValues_WithExistingValues_PopulatesFields()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""name"": { ""type"": ""string"", ""title"": ""Name"" },
				""age"": { ""type"": ""integer"", ""title"": ""Age"" }
			}
		}";

		var properties = @"{
			""name"": ""Jane Doe"",
			""age"": 25
		}";

		// Act
		var fields = _service.ParseSchemaWithValues(schema, properties);

		// Assert
		fields.Should().HaveCount(2);
		fields[0].Value.Should().Be("Jane Doe");
		fields[1].Value.Should().Be(25.0);
	}

	[Fact]
	public void ParseSchemaWithValues_WithNullProperties_CreatesFieldsWithDefaults()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""name"": { ""type"": ""string"", ""default"": ""Default"" }
			}
		}";

		// Act
		var fields = _service.ParseSchemaWithValues(schema, null!);

		// Assert
		fields.Should().HaveCount(1);
		fields[0].Value.Should().Be("Default");
	}

	#endregion

	#region Validation Tests

	[Fact]
	public void ValidateForm_WithValidFields_ReturnsTrue()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField { Name = "name", Label = "Name", IsRequired = true, Value = "John" },
			new NumberFormField { Name = "age", Label = "Age", IsRequired = false, IsInteger = true, Value = "30" }
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public void ValidateForm_WithMissingRequiredField_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField { Name = "name", Label = "Name", IsRequired = true, Value = null }
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
		fields[0].ErrorMessage.Should().NotBeEmpty();
	}

	[Fact]
	public void ValidateForm_WithInvalidNumberField_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new NumberFormField
			{
				Name = "age",
				Label = "Age",
				IsInteger = true,
				Value = "not a number"
			}
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
	}

	[Fact]
	public void ValidateForm_WithMinLengthViolation_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField
			{
				Name = "name",
				Label = "Name",
				MinLength = 5,
				Value = "Joe"
			}
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
	}

	[Fact]
	public void ValidateForm_WithMaxLengthViolation_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField
			{
				Name = "name",
				Label = "Name",
				MaxLength = 5,
				Value = "Jonathan"
			}
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
	}

	[Fact]
	public void ValidateForm_WithPatternViolation_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField
			{
				Name = "email",
				Label = "Email",
				Pattern = @"^[^@]+@[^@]+\.[^@]+$",
				Value = "invalid-email"
			}
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
	}

	[Fact]
	public void ValidateForm_WithNumberMinimumViolation_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new NumberFormField
			{
				Name = "age",
				Label = "Age",
				Minimum = 18,
				Value = "15"
			}
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
	}

	[Fact]
	public void ValidateForm_WithNumberMaximumViolation_ReturnsFalse()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new NumberFormField
			{
				Name = "score",
				Label = "Score",
				Maximum = 100,
				Value = "150"
			}
		};

		// Act
		var result = _service.ValidateForm(fields);

		// Assert
		result.Should().BeFalse();
		fields[0].HasError.Should().BeTrue();
	}

	[Fact]
	public void GetValidationErrors_WithMultipleErrors_ReturnsAllErrors()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField { Name = "name", Label = "Name", IsRequired = true, Value = null },
			new NumberFormField { Name = "age", Label = "Age", Minimum = 18, Value = "15" }
		};

		// Act
		_service.ValidateForm(fields);
		var errors = _service.GetValidationErrors(fields);

		// Assert
		errors.Should().HaveCount(2);
		errors.Should().ContainKey("name");
		errors.Should().ContainKey("age");
	}

	#endregion

	#region Serialization Tests

	[Fact]
	public void SerializeForm_WithValidFields_ReturnsJson()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField { Name = "name", Value = "John Doe" },
			new NumberFormField { Name = "age", IsInteger = true, Value = "30" },
			new BooleanFormField { Name = "active", Value = true }
		};

		// Act
		var json = _service.SerializeForm(fields);

		// Assert
		json.Should().NotBeEmpty();
		json.Should().Contain("\"name\"");
		json.Should().Contain("\"age\"");
		json.Should().Contain("\"active\"");
	}

	[Fact]
	public void SerializeFormToDictionary_WithValidFields_ReturnsDictionary()
	{
		// Arrange
		var fields = new List<FormField>
		{
			new TextFormField { Name = "name", Value = "John Doe" },
			new NumberFormField { Name = "age", IsInteger = true, Value = "30" },
			new BooleanFormField { Name = "active", Value = true }
		};

		// Act
		var dict = _service.SerializeFormToDictionary(fields);

		// Assert
		dict.Should().HaveCount(3);
		dict["name"].Should().Be("John Doe");
		dict["age"].Should().Be(30);
		dict["active"].Should().Be(true);
	}

	[Fact]
	public void SerializeForm_WithNullFields_ReturnsEmptyObject()
	{
		// Act
		var json = _service.SerializeForm(null!);

		// Assert
		json.Should().Be("{}");
	}

	#endregion

	#region GetFieldValue and SetFieldValue Tests

	[Fact]
	public void GetFieldValue_WithTextField_ReturnsString()
	{
		// Arrange
		var field = new TextFormField { Name = "name", Value = "John Doe" };

		// Act
		var value = _service.GetFieldValue(field);

		// Assert
		value.Should().Be("John Doe");
	}

	[Fact]
	public void GetFieldValue_WithNumberField_ReturnsNumber()
	{
		// Arrange
		var field = new NumberFormField { Name = "age", IsInteger = true, Value = "30" };

		// Act
		var value = _service.GetFieldValue(field);

		// Assert
		value.Should().Be(30);
	}

	[Fact]
	public void GetFieldValue_WithBooleanField_ReturnsBoolean()
	{
		// Arrange
		var field = new BooleanFormField { Name = "active", Value = true };

		// Act
		var value = _service.GetFieldValue(field);

		// Assert
		value.Should().Be(true);
	}

	[Fact]
	public void SetFieldValue_WithTextField_SetsValue()
	{
		// Arrange
		var field = new TextFormField { Name = "name" };

		// Act
		_service.SetFieldValue(field, "Jane Doe");

		// Assert
		field.Value.Should().Be("Jane Doe");
	}

	[Fact]
	public void SetFieldValue_WithNumberField_SetsValue()
	{
		// Arrange
		var field = new NumberFormField { Name = "age", IsInteger = true };

		// Act
		_service.SetFieldValue(field, 25);

		// Assert
		field.Value.Should().Be("25");
	}

	[Fact]
	public void SetFieldValue_WithBooleanField_SetsValue()
	{
		// Arrange
		var field = new BooleanFormField { Name = "active" };

		// Act
		_service.SetFieldValue(field, true);

		// Assert
		field.Value.Should().Be(true);
	}

	#endregion

	#region CreateFieldFromSchema Tests

	[Fact]
	public void CreateFieldFromSchema_WithStringType_CreatesTextField()
	{
		// Arrange
		var propertySchema = System.Text.Json.JsonDocument.Parse(@"{
			""type"": ""string"",
			""title"": ""Name"",
			""description"": ""Enter your name""
		}").RootElement;

		// Act
		var field = _service.CreateFieldFromSchema("name", propertySchema, true);

		// Assert
		field.Should().BeOfType<TextFormField>();
		field.Name.Should().Be("name");
		field.Label.Should().Be("Name");
		field.HelpText.Should().Be("Enter your name");
		field.IsRequired.Should().BeTrue();
	}

	[Fact]
	public void CreateFieldFromSchema_WithIntegerType_CreatesNumberField()
	{
		// Arrange
		var propertySchema = System.Text.Json.JsonDocument.Parse(@"{
			""type"": ""integer"",
			""title"": ""Age""
		}").RootElement;

		// Act
		var field = _service.CreateFieldFromSchema("age", propertySchema, false);

		// Assert
		field.Should().BeOfType<NumberFormField>();
		var numberField = field as NumberFormField;
		numberField!.IsInteger.Should().BeTrue();
	}

	[Fact]
	public void CreateFieldFromSchema_WithBooleanType_CreatesBooleanField()
	{
		// Arrange
		var propertySchema = System.Text.Json.JsonDocument.Parse(@"{
			""type"": ""boolean"",
			""title"": ""Active""
		}").RootElement;

		// Act
		var field = _service.CreateFieldFromSchema("active", propertySchema, false);

		// Assert
		field.Should().BeOfType<BooleanFormField>();
	}

	[Fact]
	public void CreateFieldFromSchema_WithEnumType_CreatesChoiceField()
	{
		// Arrange
		var propertySchema = System.Text.Json.JsonDocument.Parse(@"{
			""type"": ""string"",
			""enum"": [""option1"", ""option2""]
		}").RootElement;

		// Act
		var field = _service.CreateFieldFromSchema("choice", propertySchema, false);

		// Assert
		field.Should().BeOfType<ChoiceFormField>();
		var choiceField = field as ChoiceFormField;
		choiceField!.Choices.Should().HaveCount(2);
	}

	#endregion

	#region Complex Schema Tests

	[Fact]
	public void ParseSchema_WithComplexSchema_ParsesCorrectly()
	{
		// Arrange
		var schema = @"{
			""properties"": {
				""title"": {
					""type"": ""string"",
					""title"": ""Title"",
					""minLength"": 3,
					""maxLength"": 100
				},
				""description"": {
					""type"": ""string"",
					""title"": ""Description"",
					""multiline"": true
				},
				""quantity"": {
					""type"": ""integer"",
					""title"": ""Quantity"",
					""minimum"": 1,
					""maximum"": 100
				},
				""price"": {
					""type"": ""number"",
					""title"": ""Price"",
					""minimum"": 0
				},
				""inStock"": {
					""type"": ""boolean"",
					""title"": ""In Stock"",
					""default"": true
				},
				""category"": {
					""type"": ""string"",
					""title"": ""Category"",
					""enum"": [""electronics"", ""clothing"", ""food""]
				},
				""releaseDate"": {
					""type"": ""string"",
					""format"": ""date"",
					""title"": ""Release Date""
				}
			},
			""required"": [""title"", ""quantity"", ""price""]
		}";

		// Act
		var fields = _service.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(7);
		fields.Count(f => f.IsRequired).Should().Be(3);
		fields.Should().ContainSingle(f => f is TextFormField && f.Name == "title");
		fields.Should().ContainSingle(f => f is MultilineTextFormField);
		fields.Should().ContainSingle(f => f is BooleanFormField);
		fields.Should().ContainSingle(f => f is ChoiceFormField);
		fields.Should().ContainSingle(f => f is DateFormField);
	}

	#endregion
}
