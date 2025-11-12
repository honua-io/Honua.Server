// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Models;
using HonuaField.Services;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the feature detail page
/// Displays feature properties, geometry, and attachments with edit/delete capabilities
/// </summary>
public partial class FeatureDetailViewModel : BaseViewModel, IDisposable
{
	private readonly IFeaturesService _featuresService;
	private readonly INavigationService _navigationService;

	[ObservableProperty]
	private string _featureId = string.Empty;

	[ObservableProperty]
	private Feature? _feature;

	[ObservableProperty]
	private ObservableCollection<Attachment> _attachments = new();

	[ObservableProperty]
	private ObservableCollection<FeatureProperty> _properties = new();

	[ObservableProperty]
	private string _geometryType = string.Empty;

	[ObservableProperty]
	private string _createdDate = string.Empty;

	[ObservableProperty]
	private string _modifiedDate = string.Empty;

	[ObservableProperty]
	private long _attachmentsTotalSize;

	[ObservableProperty]
	private int _attachmentsCount;

	[ObservableProperty]
	private bool _hasPendingChanges;

	public FeatureDetailViewModel(
		IFeaturesService featuresService,
		INavigationService navigationService)
	{
		_featuresService = featuresService;
		_navigationService = navigationService;

		Title = "Feature Details";
	}

	/// <summary>
	/// Initialize with feature ID
	/// </summary>
	public async Task InitializeAsync(string featureId)
	{
		FeatureId = featureId;
		await LoadFeatureAsync();
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();

		// Reload if we already have a feature
		if (!string.IsNullOrEmpty(FeatureId))
		{
			await LoadFeatureAsync();
		}
	}

	/// <summary>
	/// Load feature details
	/// </summary>
	[RelayCommand]
	private async Task LoadFeatureAsync()
	{
		if (IsBusy || string.IsNullOrEmpty(FeatureId))
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			// Load feature
			Feature = await _featuresService.GetFeatureByIdAsync(FeatureId);

			if (Feature == null)
			{
				ErrorMessage = "Feature not found";
				await ShowAlertAsync("Error", "Feature not found");
				await _navigationService.GoBackAsync();
				return;
			}

			// Parse and display properties
			LoadProperties();

			// Get geometry info
			var geometry = Feature.GetGeometry();
			GeometryType = geometry?.GeometryType ?? "Unknown";

			// Format dates
			CreatedDate = DateTimeOffset.FromUnixTimeSeconds(Feature.CreatedAt)
				.ToString("MMM dd, yyyy hh:mm tt");
			ModifiedDate = DateTimeOffset.FromUnixTimeSeconds(Feature.UpdatedAt)
				.ToString("MMM dd, yyyy hh:mm tt");

			// Check sync status
			HasPendingChanges = Feature.SyncStatus == SyncStatus.Pending.ToString();

			// Load attachments
			await LoadAttachmentsAsync();
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

			AttachmentsCount = Attachments.Count;
			AttachmentsTotalSize = await _featuresService.GetAttachmentsSizeAsync(FeatureId);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error loading attachments: {ex.Message}");
		}
	}

	/// <summary>
	/// Parse and load feature properties for display
	/// </summary>
	private void LoadProperties()
	{
		Properties.Clear();

		if (Feature == null || string.IsNullOrEmpty(Feature.Properties))
			return;

		try
		{
			var propertiesDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(Feature.Properties);

			if (propertiesDict != null)
			{
				foreach (var kvp in propertiesDict)
				{
					var value = kvp.Value.ValueKind switch
					{
						JsonValueKind.String => kvp.Value.GetString() ?? string.Empty,
						JsonValueKind.Number => kvp.Value.GetDouble().ToString(),
						JsonValueKind.True => "Yes",
						JsonValueKind.False => "No",
						JsonValueKind.Null => "(empty)",
						_ => kvp.Value.ToString()
					};

					Properties.Add(new FeatureProperty
					{
						Name = kvp.Key,
						Value = value
					});
				}
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error parsing properties: {ex.Message}");
		}
	}

	/// <summary>
	/// Navigate to edit feature
	/// </summary>
	[RelayCommand]
	private async Task EditFeatureAsync()
	{
		if (Feature == null)
			return;

		try
		{
			var parameters = new Dictionary<string, object>
			{
				{ "featureId", Feature.Id },
				{ "mode", "edit" }
			};

			await _navigationService.NavigateToAsync("FeatureEditor", parameters);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to navigate to editor");
		}
	}

	/// <summary>
	/// Delete feature with confirmation
	/// </summary>
	[RelayCommand]
	private async Task DeleteFeatureAsync()
	{
		if (Feature == null)
			return;

		try
		{
			var confirmed = await ShowConfirmAsync(
				"Delete Feature",
				"Are you sure you want to delete this feature? This action cannot be undone.",
				"Delete",
				"Cancel");

			if (!confirmed)
				return;

			IsBusy = true;

			var success = await _featuresService.DeleteFeatureAsync(Feature.Id);

			if (success)
			{
				await ShowAlertAsync("Success", "Feature deleted successfully");
				await _navigationService.GoBackAsync();
			}
			else
			{
				await ShowAlertAsync("Error", "Failed to delete feature");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to delete feature");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Share feature data
	/// </summary>
	[RelayCommand]
	private async Task ShareFeatureAsync()
	{
		if (Feature == null)
			return;

		try
		{
			// Format feature data for sharing
			var shareText = $"Feature: {Feature.Id}\n" +
			               $"Type: {GeometryType}\n" +
			               $"Created: {CreatedDate}\n" +
			               $"Properties: {Properties.Count}";

			// TODO: Implement actual sharing when Share API is available
			await ShowAlertAsync("Share", shareText);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to share feature");
		}
	}

	/// <summary>
	/// Show feature on map
	/// </summary>
	[RelayCommand]
	private async Task ShowOnMapAsync()
	{
		if (Feature == null)
			return;

		try
		{
			var parameters = new Dictionary<string, object>
			{
				{ "featureId", Feature.Id },
				{ "zoomToFeature", true }
			};

			await _navigationService.NavigateToAsync("Map", parameters);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to show feature on map");
		}
	}

	/// <summary>
	/// View attachment
	/// </summary>
	[RelayCommand]
	private async Task ViewAttachmentAsync(Attachment attachment)
	{
		if (attachment == null)
			return;

		try
		{
			// TODO: Implement attachment viewer
			await ShowAlertAsync("Attachment", $"{attachment.Filename}\nType: {attachment.Type}\nSize: {FormatFileSize(attachment.Size)}");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to view attachment");
		}
	}

	/// <summary>
	/// Delete attachment with confirmation
	/// </summary>
	[RelayCommand]
	private async Task DeleteAttachmentAsync(Attachment attachment)
	{
		if (attachment == null)
			return;

		try
		{
			var confirmed = await ShowConfirmAsync(
				"Delete Attachment",
				$"Are you sure you want to delete {attachment.Filename}?",
				"Delete",
				"Cancel");

			if (!confirmed)
				return;

			IsBusy = true;

			var success = await _featuresService.DeleteAttachmentAsync(attachment.Id);

			if (success)
			{
				Attachments.Remove(attachment);
				AttachmentsCount = Attachments.Count;
				AttachmentsTotalSize = await _featuresService.GetAttachmentsSizeAsync(FeatureId);
			}
			else
			{
				await ShowAlertAsync("Error", "Failed to delete attachment");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to delete attachment");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Format file size for display
	/// </summary>
	private string FormatFileSize(long bytes)
	{
		string[] sizes = { "B", "KB", "MB", "GB" };
		double len = bytes;
		int order = 0;

		while (len >= 1024 && order < sizes.Length - 1)
		{
			order++;
			len = len / 1024;
		}

		return $"{len:0.##} {sizes[order]}";
	}

	public void Dispose()
	{
		Attachments.Clear();
		Properties.Clear();
	}
}

/// <summary>
/// Helper class for displaying feature properties
/// </summary>
public class FeatureProperty
{
	public string Name { get; set; } = string.Empty;
	public string Value { get; set; } = string.Empty;
}
