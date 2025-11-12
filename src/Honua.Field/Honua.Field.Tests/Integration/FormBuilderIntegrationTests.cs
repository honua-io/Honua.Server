// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Models.FormBuilder;
using HonuaField.Services;
using HonuaField.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for dynamic form builder workflows
/// Tests real FormBuilderService with collection schemas and feature properties
/// </summary>
public class FormBuilderIntegrationTests : IntegrationTestBase
{
	private IFormBuilderService _formBuilderService = null!;
	private ICollectionsService _collectionsService = null!;

	protected override void ConfigureServices(IServiceCollection services)
	{
		base.ConfigureServices(services);

		services.AddSingleton<IFormBuilderService, FormBuilderService>();
		services.AddSingleton<ICollectionsService, CollectionsService>();
	}

	protected override async Task OnInitializeAsync()
	{
		_formBuilderService = ServiceProvider.GetRequiredService<IFormBuilderService>();
		_collectionsService = ServiceProvider.GetRequiredService<ICollectionsService>();
		await base.OnInitializeAsync();
	}

	[Fact]
	public void ParseSchema_WithBasicProperties_ShouldGenerateFormFields()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new
				{
					type = "string",
					title = "Name",
					description = "Enter the name"
				},
				age = new
				{
					type = "number",
					title = "Age",
					minimum = 0,
					maximum = 120
				},
				active = new
				{
					type = "boolean",
					title = "Active"
				}
			},
			required = new[] { "name" }
		});

		// Act
		var fields = _formBuilderService.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(3);
		fields.Should().Contain(f => f.Name == "name" && f.IsRequired);
		fields.Should().Contain(f => f.Name == "age" && !f.IsRequired);
		fields.Should().Contain(f => f.Name == "active" && !f.IsRequired);
	}

	[Fact]
	public void ParseSchema_WithEnumField_ShouldCreateChoiceField()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				category = new
				{
					type = "string",
					title = "Category",
					@enum = new[] { "Residential", "Commercial", "Industrial" }
				}
			}
		});

		// Act
		var fields = _formBuilderService.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		var choiceField = fields[0] as ChoiceFormField;
		choiceField.Should().NotBeNull();
		choiceField!.Options.Should().HaveCount(3);
		choiceField.Options.Should().Contain("Residential");
	}

	[Fact]
	public void ParseSchemaWithValues_ShouldPopulateFieldValues()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				value = new { type = "number", title = "Value" }
			}
		});

		var properties = JsonSerializer.Serialize(new
		{
			name = "Test Building",
			value = 12345
		});

		// Act
		var fields = _formBuilderService.ParseSchemaWithValues(schema, properties);

		// Assert
		fields.Should().HaveCount(2);
		var nameField = fields.First(f => f.Name == "name");
		nameField.Value.Should().Be("Test Building");

		var valueField = fields.First(f => f.Name == "value");
		valueField.Value.Should().Be(12345);
	}

	[Fact]
	public void ValidateForm_WithRequiredFieldEmpty_ShouldReturnFalse()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				email = new { type = "string", title = "Email" }
			},
			required = new[] { "name", "email" }
		});

		var fields = _formBuilderService.ParseSchema(schema);
		fields[0].Value = "John Doe"; // name is filled
		fields[1].Value = ""; // email is empty

		// Act
		var isValid = _formBuilderService.ValidateForm(fields);

		// Assert
		isValid.Should().BeFalse();
	}

	[Fact]
	public void ValidateForm_WithAllRequiredFieldsFilled_ShouldReturnTrue()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				email = new { type = "string", title = "Email" }
			},
			required = new[] { "name", "email" }
		});

		var fields = _formBuilderService.ParseSchema(schema);
		fields[0].Value = "John Doe";
		fields[1].Value = "john@example.com";

		// Act
		var isValid = _formBuilderService.ValidateForm(fields);

		// Assert
		isValid.Should().BeTrue();
	}

	[Fact]
	public void GetValidationErrors_WithInvalidFields_ShouldReturnErrors()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				age = new { type = "number", title = "Age" }
			},
			required = new[] { "name", "age" }
		});

		var fields = _formBuilderService.ParseSchema(schema);
		fields[0].Value = ""; // name is empty
		fields[1].Value = null; // age is null

		// Act
		_formBuilderService.ValidateForm(fields);
		var errors = _formBuilderService.GetValidationErrors(fields);

		// Assert
		errors.Should().HaveCount(2);
		errors.Should().ContainKey("name");
		errors.Should().ContainKey("age");
	}

	[Fact]
	public void SerializeForm_ToJsonString_ShouldGenerateValidJson()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				value = new { type = "number", title = "Value" },
				active = new { type = "boolean", title = "Active" }
			}
		});

		var fields = _formBuilderService.ParseSchema(schema);
		fields[0].Value = "Test Feature";
		fields[1].Value = 123.45;
		fields[2].Value = true;

		// Act
		var json = _formBuilderService.SerializeForm(fields);

		// Assert
		json.Should().NotBeNullOrEmpty();
		var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
		dict.Should().NotBeNull();
		dict!["name"].GetString().Should().Be("Test Feature");
		dict["value"].GetDouble().Should().BeApproximately(123.45, 0.01);
		dict["active"].GetBoolean().Should().BeTrue();
	}

	[Fact]
	public void SerializeFormToDictionary_ShouldReturnTypedDictionary()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new { type = "string", title = "Name" },
				count = new { type = "number", title = "Count" }
			}
		});

		var fields = _formBuilderService.ParseSchema(schema);
		fields[0].Value = "Item";
		fields[1].Value = 42;

		// Act
		var dict = _formBuilderService.SerializeFormToDictionary(fields);

		// Assert
		dict.Should().HaveCount(2);
		dict["name"].Should().Be("Item");
		dict["count"].Should().Be(42);
	}

	[Fact]
	public async Task IntegrationWithCollection_ParseSchemaAndCreateFeature_ShouldWork()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var schema = collection.Schema;

		// Act - Parse schema to form
		var fields = _formBuilderService.ParseSchema(schema);
		fields[0].Value = "Test Building";
		fields[1].Value = "A test building created via form";

		// Validate and serialize
		var isValid = _formBuilderService.ValidateForm(fields);
		var properties = _formBuilderService.SerializeForm(fields);

		// Create feature with form data
		var feature = DataBuilder.CreateTestFeature(collection.Id);
		feature.Properties = properties;
		await FeatureRepository.InsertAsync(feature);

		// Assert
		isValid.Should().BeTrue();

		var savedFeature = await FeatureRepository.GetByIdAsync(feature.Id);
		savedFeature.Should().NotBeNull();
		savedFeature!.Properties.Should().Contain("Test Building");
	}

	[Fact]
	public async Task EditExistingFeature_LoadValuesUpdateAndSave_ShouldPersist()
	{
		// Arrange
		var collection = DataBuilder.CreateTestCollection();
		await CollectionRepository.InsertAsync(collection);

		var initialProps = new Dictionary<string, object>
		{
			{ "name", "Original Name" },
			{ "description", "Original Description" }
		};

		var feature = DataBuilder.CreateTestFeature(
			collection.Id,
			properties: initialProps);
		await FeatureRepository.InsertAsync(feature);

		// Act - Load feature into form
		var fields = _formBuilderService.ParseSchemaWithValues(
			collection.Schema,
			feature.Properties);

		// Update form values
		fields.First(f => f.Name == "name").Value = "Updated Name";

		// Validate and save
		var isValid = _formBuilderService.ValidateForm(fields);
		var updatedProperties = _formBuilderService.SerializeForm(fields);

		feature.Properties = updatedProperties;
		await FeatureRepository.UpdateAsync(feature);

		// Assert
		isValid.Should().BeTrue();

		var savedFeature = await FeatureRepository.GetByIdAsync(feature.Id);
		savedFeature.Should().NotBeNull();
		savedFeature!.GetPropertiesDict()["name"].ToString().Should().Be("Updated Name");
		savedFeature.GetPropertiesDict()["description"].ToString().Should().Be("Original Description");
	}

	[Fact]
	public void ParseSchema_WithDateFields_ShouldCreateDateFormFields()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				install_date = new
				{
					type = "string",
					format = "date",
					title = "Installation Date"
				},
				inspection_datetime = new
				{
					type = "string",
					format = "date-time",
					title = "Inspection Date/Time"
				}
			}
		});

		// Act
		var fields = _formBuilderService.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(2);
		fields[0].Should().BeOfType<DateFormField>();
		fields[1].Should().BeOfType<DateTimeFormField>();
	}

	[Fact]
	public void ParseSchema_WithComplexValidation_ShouldApplyConstraints()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				name = new
				{
					type = "string",
					title = "Name",
					minLength = 3,
					maxLength = 50
				},
				age = new
				{
					type = "number",
					title = "Age",
					minimum = 0,
					maximum = 120
				}
			},
			required = new[] { "name" }
		});

		// Act
		var fields = _formBuilderService.ParseSchema(schema);

		// Assert - Check that fields were created with constraints
		var nameField = fields.First(f => f.Name == "name") as TextFormField;
		nameField.Should().NotBeNull();
		nameField!.IsRequired.Should().BeTrue();

		var ageField = fields.First(f => f.Name == "age") as NumberFormField;
		ageField.Should().NotBeNull();
	}

	[Fact]
	public void SetFieldValue_WithTypeConversion_ShouldConvertTypes()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				count = new { type = "number", title = "Count" }
			}
		});

		var fields = _formBuilderService.ParseSchema(schema);
		var field = fields[0];

		// Act - Set string value, should convert to number
		_formBuilderService.SetFieldValue(field, "42");

		// Assert
		var typedValue = _formBuilderService.GetFieldValue(field);
		typedValue.Should().BeOfType<double>().And.Be(42);
	}

	[Fact]
	public void ParseSchema_WithMultilineText_ShouldCreateMultilineField()
	{
		// Arrange
		var schema = JsonSerializer.Serialize(new
		{
			type = "object",
			properties = new
			{
				description = new
				{
					type = "string",
					title = "Description",
					format = "textarea"
				}
			}
		});

		// Act
		var fields = _formBuilderService.ParseSchema(schema);

		// Assert
		fields.Should().HaveCount(1);
		fields[0].Should().BeOfType<MultilineTextFormField>();
	}
}
