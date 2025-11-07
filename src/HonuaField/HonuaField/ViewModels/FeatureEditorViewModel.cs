using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Data.Repositories;
using HonuaField.Models;
using HonuaField.Models.FormBuilder;
using HonuaField.Services;
using NetTopologySuite.Geometries;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the feature editor page
/// Handles creating and editing features with dynamic form generation, validation and attachment management
/// </summary>
public partial class FeatureEditorViewModel : BaseViewModel, IDisposable
{
	private readonly IFeaturesService _featuresService;
	private readonly INavigationService _navigationService;
	private readonly ICollectionRepository _collectionRepository;
	private readonly IAuthenticationService _authenticationService;
	private readonly ICameraService _cameraService;
	private readonly IFormBuilderService _formBuilderService;

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
	private ObservableCollection<FormField> _formFields = new();

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
		IAuthenticationService authenticationService,
		ICameraService cameraService,
		IFormBuilderService formBuilderService)
	{
		_featuresService = featuresService;
		_navigationService = navigationService;
		_collectionRepository = collectionRepository;
		_authenticationService = authenticationService;
		_cameraService = cameraService;
		_formBuilderService = formBuilderService;

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
		InitializeFormFields();
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
			LoadFormFieldsFromFeature();
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
	/// Initialize form fields from collection schema
	/// </summary>
	private void InitializeFormFields()
	{
		FormFields.Clear();

		if (Collection == null)
			return;

		try
		{
			// Use FormBuilderService to parse schema and create form fields
			var fields = _formBuilderService.ParseSchema(Collection.Schema);

			foreach (var field in fields)
			{
				FormFields.Add(field);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error initializing form fields: {ex.Message}");
		}
	}

	/// <summary>
	/// Load form fields from existing feature
	/// </summary>
	private void LoadFormFieldsFromFeature()
	{
		FormFields.Clear();

		if (Feature == null || Collection == null)
			return;

		try
		{
			// Use FormBuilderService to parse schema with existing values
			var fields = _formBuilderService.ParseSchemaWithValues(Collection.Schema, Feature.Properties);

			foreach (var field in fields)
			{
				FormFields.Add(field);
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading form fields from feature: {ex.Message}");
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
		if (!ValidateForm())
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
	/// Take photo with camera
	/// </summary>
	[RelayCommand]
	private async Task TakePhotoAsync()
	{
		try
		{
			var result = await _cameraService.TakePhotoAsync();

			if (result != null && result.Success)
			{
				var attachment = CreateAttachmentFromCameraResult(result, AttachmentType.Photo);
				Attachments.Add(attachment);
				IsDirty = true;
				await ShowAlertAsync("Success", "Photo captured successfully");
			}
			else if (result != null && !result.Success)
			{
				await ShowAlertAsync("Error", result.ErrorMessage ?? "Failed to capture photo");
			}
			// If result is null, user cancelled - no message needed
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to capture photo");
		}
	}

	/// <summary>
	/// Pick photo from gallery
	/// </summary>
	[RelayCommand]
	private async Task PickPhotoAsync()
	{
		try
		{
			var result = await _cameraService.PickPhotoAsync();

			if (result != null && result.Success)
			{
				var attachment = CreateAttachmentFromCameraResult(result, AttachmentType.Photo);
				Attachments.Add(attachment);
				IsDirty = true;
				await ShowAlertAsync("Success", "Photo selected successfully");
			}
			else if (result != null && !result.Success)
			{
				await ShowAlertAsync("Error", result.ErrorMessage ?? "Failed to select photo");
			}
			// If result is null, user cancelled - no message needed
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to select photo");
		}
	}

	/// <summary>
	/// Add photo attachment (shows options for camera or gallery)
	/// </summary>
	[RelayCommand]
	private async Task AddPhotoAsync()
	{
		try
		{
			var choice = await ShowActionSheetAsync("Add Photo", "Cancel", null, "Take Photo", "Choose from Gallery");

			if (choice == "Take Photo")
			{
				await TakePhotoAsync();
			}
			else if (choice == "Choose from Gallery")
			{
				await PickPhotoAsync();
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to add photo");
		}
	}

	/// <summary>
	/// Record video with camera
	/// </summary>
	[RelayCommand]
	private async Task RecordVideoAsync()
	{
		try
		{
			var result = await _cameraService.RecordVideoAsync();

			if (result != null && result.Success)
			{
				var attachment = CreateAttachmentFromCameraResult(result, AttachmentType.Video);
				Attachments.Add(attachment);
				IsDirty = true;
				await ShowAlertAsync("Success", "Video recorded successfully");
			}
			else if (result != null && !result.Success)
			{
				await ShowAlertAsync("Error", result.ErrorMessage ?? "Failed to record video");
			}
			// If result is null, user cancelled - no message needed
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to record video");
		}
	}

	/// <summary>
	/// Pick video from gallery
	/// </summary>
	[RelayCommand]
	private async Task PickVideoAsync()
	{
		try
		{
			var result = await _cameraService.PickVideoAsync();

			if (result != null && result.Success)
			{
				var attachment = CreateAttachmentFromCameraResult(result, AttachmentType.Video);
				Attachments.Add(attachment);
				IsDirty = true;
				await ShowAlertAsync("Success", "Video selected successfully");
			}
			else if (result != null && !result.Success)
			{
				await ShowAlertAsync("Error", result.ErrorMessage ?? "Failed to select video");
			}
			// If result is null, user cancelled - no message needed
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to select video");
		}
	}

	/// <summary>
	/// Add video attachment (shows options for camera or gallery)
	/// </summary>
	[RelayCommand]
	private async Task AddVideoAsync()
	{
		try
		{
			var choice = await ShowActionSheetAsync("Add Video", "Cancel", null, "Record Video", "Choose from Gallery");

			if (choice == "Record Video")
			{
				await RecordVideoAsync();
			}
			else if (choice == "Choose from Gallery")
			{
				await PickVideoAsync();
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to add video");
		}
	}

	/// <summary>
	/// Record audio
	/// </summary>
	[RelayCommand]
	private async Task AddAudioAsync()
	{
		try
		{
			var result = await _cameraService.RecordAudioAsync();

			if (result != null && result.Success)
			{
				var attachment = CreateAttachmentFromCameraResult(result, AttachmentType.Audio);
				Attachments.Add(attachment);
				IsDirty = true;
				await ShowAlertAsync("Success", "Audio recorded successfully");
			}
			else if (result != null && !result.Success)
			{
				await ShowAlertAsync("Error", result.ErrorMessage ?? "Failed to record audio");
			}
			// If result is null, user cancelled - no message needed
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to record audio");
		}
	}

	/// <summary>
	/// Create attachment from camera result
	/// </summary>
	private Attachment CreateAttachmentFromCameraResult(CameraResult result, AttachmentType type)
	{
		var attachment = new Attachment
		{
			Id = Guid.NewGuid().ToString(),
			FeatureId = FeatureId, // Will be set when saving
			Type = type.ToString(),
			Filename = result.FileName,
			Filepath = result.FilePath,
			ContentType = result.ContentType,
			Size = result.FileSize,
			Thumbnail = result.ThumbnailPath,
			Metadata = result.Metadata != null ? JsonSerializer.Serialize(result.Metadata) : null,
			UploadStatus = UploadStatus.Pending.ToString()
		};

		return attachment;
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
	/// Form field value changed - mark as dirty
	/// </summary>
	public void OnFieldValueChanged()
	{
		IsDirty = true;
		ValidateForm();
	}

	/// <summary>
	/// Validate all form fields
	/// </summary>
	private bool ValidateForm()
	{
		HasValidationErrors = false;
		ValidationMessage = string.Empty;
		CanSave = true;

		// Use FormBuilderService to validate all fields
		var isValid = _formBuilderService.ValidateForm(FormFields);

		if (!isValid)
		{
			HasValidationErrors = true;
			var errors = _formBuilderService.GetValidationErrors(FormFields);

			// Get first error message
			if (errors.Count > 0)
			{
				var firstError = errors.First();
				ValidationMessage = firstError.Value;
			}

			CanSave = false;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Serialize form fields to JSON
	/// </summary>
	private string SerializeProperties()
	{
		return _formBuilderService.SerializeForm(FormFields);
	}

	/// <summary>
	/// Get form fields as dictionary
	/// </summary>
	private Dictionary<string, object?> GetPropertiesDictionary()
	{
		return _formBuilderService.SerializeFormToDictionary(FormFields);
	}

	public void Dispose()
	{
		FormFields.Clear();
		Attachments.Clear();
		_attachmentsToDelete.Clear();
		_originalProperties.Clear();
	}
}
