using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Services;
using NetTopologySuite.Geometries;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the feature editor page
/// Handles creating and editing features with property validation and attachment management
/// </summary>
public partial class FeatureEditorViewModel : BaseViewModel, IDisposable
{
	private readonly IFeaturesService _featuresService;
	private readonly INavigationService _navigationService;
	private readonly ICollectionRepository _collectionRepository;
	private readonly IAuthenticationService _authenticationService;

	[ObservableProperty]
	private string _mode = "create"; // "create" or "edit"

	[ObservableProperty]
	private string _featureId = string.Empty;

	[ObservableProperty]
	private string _collectionId = string.Empty;

	[ObservableProperty]
	private Collection? _collection;

	[ObservableProperty]
	private Feature? _feature;

	[ObservableProperty]
	private ObservableCollection<EditableProperty> _properties = new();

	[ObservableProperty]
	private ObservableCollection<Attachment> _attachments = new();

	[ObservableProperty]
	private Geometry? _geometry;

	[ObservableProperty]
	private string _geometryType = "Point";

	[ObservableProperty]
	private bool _isDirty;

	[ObservableProperty]
	private bool _hasValidationErrors;

	[ObservableProperty]
	private bool _canSave = true;

	[ObservableProperty]
	private string _validationMessage = string.Empty;

	private List<Attachment> _attachmentsToDelete = new();
	private Dictionary<string, object?> _originalProperties = new();

	public FeatureEditorViewModel(
		IFeaturesService featuresService,
		INavigationService navigationService,
		ICollectionRepository collectionRepository,
		IAuthenticationService authenticationService)
	{
		_featuresService = featuresService;
		_navigationService = navigationService;
		_collectionRepository = collectionRepository;
		_authenticationService = authenticationService;

		Title = "Edit Feature";
	}

	/// <summary>
	/// Initialize for creating a new feature
	/// </summary>
	public async Task InitializeForCreateAsync(string collectionId, Geometry? geometry = null)
	{
		Mode = "create";
		CollectionId = collectionId;
		Geometry = geometry;
		Title = "New Feature";

		await LoadCollectionSchemaAsync();
		InitializeProperties();
	}

	/// <summary>
	/// Initialize for editing an existing feature
	/// </summary>
	public async Task InitializeForEditAsync(string featureId)
	{
		Mode = "edit";
		FeatureId = featureId;
		Title = "Edit Feature";

		await LoadFeatureAsync();
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();

		// Reload if editing
		if (Mode == "edit" && !string.IsNullOrEmpty(FeatureId))
		{
			await LoadFeatureAsync();
		}
	}

	/// <summary>
	/// Load collection schema for property definitions
	/// </summary>
	private async Task LoadCollectionSchemaAsync()
	{
		try
		{
			Collection = await _collectionRepository.GetByIdAsync(CollectionId);

			if (Collection == null)
			{
				throw new InvalidOperationException($"Collection {CollectionId} not found");
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading collection schema: {ex.Message}");
			throw;
		}
	}

	/// <summary>
	/// Load feature for editing
	/// </summary>
	private async Task LoadFeatureAsync()
	{
		if (IsBusy || string.IsNullOrEmpty(FeatureId))
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			Feature = await _featuresService.GetFeatureByIdAsync(FeatureId);

			if (Feature == null)
			{
				throw new InvalidOperationException("Feature not found");
			}

			CollectionId = Feature.CollectionId;
			Geometry = Feature.GetGeometry();
			GeometryType = Geometry?.GeometryType ?? "Point";

			await LoadCollectionSchemaAsync();
			LoadPropertiesFromFeature();
			await LoadAttachmentsAsync();

			// Save original state for dirty checking
			_originalProperties = GetPropertiesDictionary();
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to load feature");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Initialize properties from collection schema
	/// </summary>
	private void InitializeProperties()
	{
		Properties.Clear();

		if (Collection == null)
			return;

		try
		{
			// Parse schema to get property definitions
			var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Collection.Schema);

			if (schema != null && schema.ContainsKey("properties"))
			{
				var propertiesSchema = schema["properties"];

				foreach (var prop in propertiesSchema.EnumerateObject())
				{
					var propertyName = prop.Name;
					var propertyDef = prop.Value;

					var editableProperty = new EditableProperty
					{
						Name = propertyName,
						DisplayName = GetDisplayName(propertyDef, propertyName),
						Type = GetPropertyType(propertyDef),
						IsRequired = GetIsRequired(schema, propertyName),
						Value = GetDefaultValue(propertyDef)
					};

					Properties.Add(editableProperty);
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing properties: {ex.Message}");
		}
	}

	/// <summary>
	/// Load properties from existing feature
	/// </summary>
	private void LoadPropertiesFromFeature()
	{
		InitializeProperties();

		if (Feature == null)
			return;

		try
		{
			var propertiesDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Feature.Properties);

			if (propertiesDict != null)
			{
				foreach (var property in Properties)
				{
					if (propertiesDict.ContainsKey(property.Name))
					{
						var value = propertiesDict[property.Name];
						property.Value = value.ValueKind switch
						{
							JsonValueKind.String => value.GetString(),
							JsonValueKind.Number => value.GetDouble(),
							JsonValueKind.True => true,
							JsonValueKind.False => false,
							_ => null
						};
					}
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading properties from feature: {ex.Message}");
		}
	}

	/// <summary>
	/// Load feature attachments
	/// </summary>
	private async Task LoadAttachmentsAsync()
	{
		try
		{
			Attachments.Clear();

			var attachments = await _featuresService.GetFeatureAttachmentsAsync(FeatureId);

			foreach (var attachment in attachments)
			{
				Attachments.Add(attachment);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading attachments: {ex.Message}");
		}
	}

	/// <summary>
	/// Save feature (create or update)
	/// </summary>
	[RelayCommand]
	private async Task SaveAsync()
	{
		if (IsBusy)
			return;

		// Validate
		if (!ValidateProperties())
		{
			await ShowAlertAsync("Validation Error", ValidationMessage);
			return;
		}

		if (Geometry == null)
		{
			await ShowAlertAsync("Validation Error", "Please add geometry to the feature");
			return;
		}

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			if (Mode == "create")
			{
				await CreateFeatureAsync();
			}
			else
			{
				await UpdateFeatureAsync();
			}

			IsDirty = false;
			await _navigationService.GoBackAsync();
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to save feature");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Create new feature
	/// </summary>
	private async Task CreateFeatureAsync()
	{
		var user = await _authenticationService.GetCurrentUserAsync();

		var feature = new Feature
		{
			CollectionId = CollectionId,
			Properties = SerializeProperties(),
			CreatedBy = user?.Username ?? "unknown"
		};

		feature.SetGeometry(Geometry!);

		var featureId = await _featuresService.CreateFeatureAsync(feature);

		// Add attachments
		foreach (var attachment in Attachments)
		{
			attachment.FeatureId = featureId;
			await _featuresService.AddAttachmentAsync(attachment);
		}

		await ShowAlertAsync("Success", "Feature created successfully");
	}

	/// <summary>
	/// Update existing feature
	/// </summary>
	private async Task UpdateFeatureAsync()
	{
		if (Feature == null)
			return;

		Feature.Properties = SerializeProperties();
		Feature.SetGeometry(Geometry!);

		await _featuresService.UpdateFeatureAsync(Feature);

		// Delete removed attachments
		foreach (var attachment in _attachmentsToDelete)
		{
			await _featuresService.DeleteAttachmentAsync(attachment.Id);
		}

		// Add new attachments (those without IDs or with empty IDs)
		foreach (var attachment in Attachments.Where(a => string.IsNullOrEmpty(a.Id) || a.Id == Guid.Empty.ToString()))
		{
			attachment.FeatureId = Feature.Id;
			await _featuresService.AddAttachmentAsync(attachment);
		}

		await ShowAlertAsync("Success", "Feature updated successfully");
	}

	/// <summary>
	/// Cancel editing with dirty check
	/// </summary>
	[RelayCommand]
	private async Task CancelAsync()
	{
		if (IsDirty)
		{
			var confirmed = await ShowConfirmAsync(
				"Discard Changes",
				"You have unsaved changes. Are you sure you want to discard them?",
				"Discard",
				"Continue Editing");

			if (!confirmed)
				return;
		}

		await _navigationService.GoBackAsync();
	}

	/// <summary>
	/// Add photo attachment
	/// </summary>
	[RelayCommand]
	private async Task AddPhotoAsync()
	{
		try
		{
			// TODO: Implement photo capture/selection when Media API is available
			await ShowAlertAsync("Add Photo", "Photo capture will be available soon");
			IsDirty = true;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to add photo");
		}
	}

	/// <summary>
	/// Add video attachment
	/// </summary>
	[RelayCommand]
	private async Task AddVideoAsync()
	{
		try
		{
			// TODO: Implement video capture/selection
			await ShowAlertAsync("Add Video", "Video capture will be available soon");
			IsDirty = true;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to add video");
		}
	}

	/// <summary>
	/// Add audio attachment
	/// </summary>
	[RelayCommand]
	private async Task AddAudioAsync()
	{
		try
		{
			// TODO: Implement audio recording
			await ShowAlertAsync("Add Audio", "Audio recording will be available soon");
			IsDirty = true;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to add audio");
		}
	}

	/// <summary>
	/// Remove attachment
	/// </summary>
	[RelayCommand]
	private async Task RemoveAttachmentAsync(Attachment attachment)
	{
		if (attachment == null)
			return;

		try
		{
			var confirmed = await ShowConfirmAsync(
				"Remove Attachment",
				$"Remove {attachment.Filename}?",
				"Remove",
				"Cancel");

			if (!confirmed)
				return;

			Attachments.Remove(attachment);

			// Track for deletion if it's an existing attachment
			if (!string.IsNullOrEmpty(attachment.Id) && attachment.Id != Guid.Empty.ToString())
			{
				_attachmentsToDelete.Add(attachment);
			}

			IsDirty = true;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to remove attachment");
		}
	}

	/// <summary>
	/// Update geometry from map
	/// </summary>
	public void UpdateGeometry(Geometry geometry)
	{
		Geometry = geometry;
		GeometryType = geometry.GeometryType;
		IsDirty = true;
	}

	/// <summary>
	/// Property value changed - mark as dirty
	/// </summary>
	public void OnPropertyValueChanged()
	{
		IsDirty = true;
		ValidateProperties();
	}

	/// <summary>
	/// Validate all properties
	/// </summary>
	private bool ValidateProperties()
	{
		HasValidationErrors = false;
		ValidationMessage = string.Empty;
		CanSave = true;

		foreach (var property in Properties)
		{
			if (property.IsRequired && (property.Value == null || string.IsNullOrWhiteSpace(property.Value.ToString())))
			{
				HasValidationErrors = true;
				ValidationMessage = $"{property.DisplayName} is required";
				CanSave = false;
				return false;
			}

			// Type validation
			if (property.Value != null)
			{
				var isValid = property.Type switch
				{
					"number" => double.TryParse(property.Value.ToString(), out _),
					"integer" => int.TryParse(property.Value.ToString(), out _),
					"boolean" => bool.TryParse(property.Value.ToString(), out _),
					_ => true
				};

				if (!isValid)
				{
					HasValidationErrors = true;
					ValidationMessage = $"{property.DisplayName} must be a valid {property.Type}";
					CanSave = false;
					return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	/// Serialize properties to JSON
	/// </summary>
	private string SerializeProperties()
	{
		var propertiesDict = GetPropertiesDictionary();
		return JsonSerializer.Serialize(propertiesDict);
	}

	/// <summary>
	/// Get properties as dictionary
	/// </summary>
	private Dictionary<string, object?> GetPropertiesDictionary()
	{
		var dict = new Dictionary<string, object?>();

		foreach (var property in Properties)
		{
			dict[property.Name] = property.Value;
		}

		return dict;
	}

	#region Schema Helpers

	private string GetDisplayName(JsonElement propertyDef, string fallback)
	{
		if (propertyDef.TryGetProperty("title", out var title))
		{
			return title.GetString() ?? fallback;
		}
		return fallback;
	}

	private string GetPropertyType(JsonElement propertyDef)
	{
		if (propertyDef.TryGetProperty("type", out var type))
		{
			return type.GetString() ?? "string";
		}
		return "string";
	}

	private bool GetIsRequired(Dictionary<string, JsonElement> schema, string propertyName)
	{
		if (schema.ContainsKey("required"))
		{
			var required = schema["required"];
			if (required.ValueKind == JsonValueKind.Array)
			{
				foreach (var item in required.EnumerateArray())
				{
					if (item.GetString() == propertyName)
					{
						return true;
					}
				}
			}
		}
		return false;
	}

	private object? GetDefaultValue(JsonElement propertyDef)
	{
		if (propertyDef.TryGetProperty("default", out var defaultValue))
		{
			return defaultValue.ValueKind switch
			{
				JsonValueKind.String => defaultValue.GetString(),
				JsonValueKind.Number => defaultValue.GetDouble(),
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				_ => null
			};
		}
		return null;
	}

	#endregion

	public void Dispose()
	{
		Properties.Clear();
		Attachments.Clear();
		_attachmentsToDelete.Clear();
		_originalProperties.Clear();
	}
}

/// <summary>
/// Helper class for editable property
/// </summary>
public partial class EditableProperty : ObservableObject
{
	[ObservableProperty]
	private string _name = string.Empty;

	[ObservableProperty]
	private string _displayName = string.Empty;

	[ObservableProperty]
	private string _type = "string";

	[ObservableProperty]
	private bool _isRequired;

	[ObservableProperty]
	private object? _value;
}
