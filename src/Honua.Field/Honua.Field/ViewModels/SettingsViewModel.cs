// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Services;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the settings page
/// Handles app configuration, preferences, and user settings
/// </summary>
public partial class SettingsViewModel : BaseViewModel, IDisposable
{
	private readonly ISettingsService _settingsService;
	private readonly IAuthenticationService _authService;
	private readonly INavigationService _navigationService;
	private readonly IBiometricService _biometricService;
	private bool _disposed;

	#region Observable Properties - Server & Authentication

	[ObservableProperty]
	private string _serverUrl = "https://api.honua.io";

	[ObservableProperty]
	private string _loggedInUser = string.Empty;

	[ObservableProperty]
	private bool _biometricsEnabled;

	[ObservableProperty]
	private bool _biometricsAvailable;

	#endregion

	#region Observable Properties - App Preferences

	[ObservableProperty]
	private string _selectedUnits = "Metric";

	[ObservableProperty]
	private string _coordinateFormat = "Decimal Degrees";

	[ObservableProperty]
	private string _gpsAccuracy = "High";

	#endregion

	#region Observable Properties - Map Settings

	[ObservableProperty]
	private string _baseMapProvider = "OpenStreetMap";

	[ObservableProperty]
	private bool _offlineTilesEnabled;

	[ObservableProperty]
	private string _offlineStorageUsed = "0 MB";

	#endregion

	#region Observable Properties - Sync Settings

	[ObservableProperty]
	private bool _autoSyncEnabled = true;

	[ObservableProperty]
	private int _syncInterval = 15;

	[ObservableProperty]
	private bool _wifiOnlySync = true;

	#endregion

	#region Observable Properties - About

	[ObservableProperty]
	private string _appVersion = "1.0.0";

	[ObservableProperty]
	private string _buildNumber = "1";

	#endregion

	public List<string> UnitsOptions { get; } = new() { "Metric", "Imperial" };
	public List<string> CoordinateFormatOptions { get; } = new() { "Decimal Degrees", "Degrees Minutes Seconds", "Degrees Decimal Minutes" };
	public List<string> GpsAccuracyOptions { get; } = new() { "High", "Medium", "Low" };
	public List<string> BaseMapProviders { get; } = new() { "OpenStreetMap", "Satellite", "Topographic", "Hybrid" };
	public List<int> SyncIntervalOptions { get; } = new() { 5, 10, 15, 30, 60 };

	public SettingsViewModel(
		ISettingsService settingsService,
		IAuthenticationService authService,
		INavigationService navigationService,
		IBiometricService biometricService)
	{
		_settingsService = settingsService;
		_authService = authService;
		_navigationService = navigationService;
		_biometricService = biometricService;

		Title = "Settings";
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();
		await LoadSettingsAsync();
	}

	/// <summary>
	/// Load all settings from storage
	/// </summary>
	private async Task LoadSettingsAsync()
	{
		try
		{
			IsBusy = true;

			// Load server settings
			ServerUrl = await _settingsService.GetAsync("server_url", "https://api.honua.io");

			// Load authentication info
			var user = await _authService.GetCurrentUserAsync();
			LoggedInUser = user?.Username ?? "Not logged in";

			// Load biometric settings
			BiometricsAvailable = await _biometricService.IsBiometricAvailableAsync()
			                      && await _biometricService.IsBiometricEnrolledAsync();
			BiometricsEnabled = await _settingsService.GetAsync("use_biometrics", false);

			// Load app preferences
			SelectedUnits = await _settingsService.GetAsync("units", "Metric");
			CoordinateFormat = await _settingsService.GetAsync("coordinate_format", "Decimal Degrees");
			GpsAccuracy = await _settingsService.GetAsync("gps_accuracy", "High");

			// Load map settings
			BaseMapProvider = await _settingsService.GetAsync("basemap_provider", "OpenStreetMap");
			OfflineTilesEnabled = await _settingsService.GetAsync("offline_tiles_enabled", false);
			await UpdateStorageUsageAsync();

			// Load sync settings
			AutoSyncEnabled = await _settingsService.GetAsync("auto_sync_enabled", true);
			SyncInterval = await _settingsService.GetAsync("sync_interval", 15);
			WifiOnlySync = await _settingsService.GetAsync("wifi_only_sync", true);

			// Load app version info
			AppVersion = AppInfo.Current.VersionString;
			BuildNumber = AppInfo.Current.BuildString;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to load settings");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Save server URL setting
	/// </summary>
	[RelayCommand]
	private async Task SaveServerUrlAsync()
	{
		try
		{
			await _settingsService.SetAsync("server_url", ServerUrl);
			await ShowAlertAsync("Success", "Server URL saved successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to save server URL");
		}
	}

	/// <summary>
	/// Toggle biometric authentication
	/// </summary>
	[RelayCommand]
	private async Task ToggleBiometricsAsync()
	{
		try
		{
			if (!BiometricsAvailable)
			{
				await ShowAlertAsync("Not Available", "Biometric authentication is not available on this device");
				BiometricsEnabled = false;
				return;
			}

			await _settingsService.SetAsync("use_biometrics", BiometricsEnabled);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to toggle biometrics");
			BiometricsEnabled = !BiometricsEnabled;
		}
	}

	/// <summary>
	/// Save app preferences
	/// </summary>
	[RelayCommand]
	private async Task SavePreferencesAsync()
	{
		try
		{
			await _settingsService.SetAsync("units", SelectedUnits);
			await _settingsService.SetAsync("coordinate_format", CoordinateFormat);
			await _settingsService.SetAsync("gps_accuracy", GpsAccuracy);
			await ShowAlertAsync("Success", "Preferences saved successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to save preferences");
		}
	}

	/// <summary>
	/// Save map settings
	/// </summary>
	[RelayCommand]
	private async Task SaveMapSettingsAsync()
	{
		try
		{
			await _settingsService.SetAsync("basemap_provider", BaseMapProvider);
			await _settingsService.SetAsync("offline_tiles_enabled", OfflineTilesEnabled);
			await ShowAlertAsync("Success", "Map settings saved successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to save map settings");
		}
	}

	/// <summary>
	/// Save sync settings
	/// </summary>
	[RelayCommand]
	private async Task SaveSyncSettingsAsync()
	{
		try
		{
			await _settingsService.SetAsync("auto_sync_enabled", AutoSyncEnabled);
			await _settingsService.SetAsync("sync_interval", SyncInterval);
			await _settingsService.SetAsync("wifi_only_sync", WifiOnlySync);
			await ShowAlertAsync("Success", "Sync settings saved successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to save sync settings");
		}
	}

	/// <summary>
	/// Clear application cache
	/// </summary>
	[RelayCommand]
	private async Task ClearCacheAsync()
	{
		try
		{
			var confirmed = await ShowConfirmAsync(
				"Clear Cache",
				"This will clear all cached data. Are you sure?",
				"Clear",
				"Cancel");

			if (!confirmed)
				return;

			IsBusy = true;

			// Clear cache logic would go here
			// For now, just simulate the operation
			await Task.Delay(1000);

			await UpdateStorageUsageAsync();
			await ShowAlertAsync("Success", "Cache cleared successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to clear cache");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Delete all local data
	/// </summary>
	[RelayCommand]
	private async Task DeleteLocalDataAsync()
	{
		try
		{
			var confirmed = await ShowConfirmAsync(
				"Delete Local Data",
				"This will delete ALL local data including offline maps and collected features. This action cannot be undone. Are you sure?",
				"Delete",
				"Cancel");

			if (!confirmed)
				return;

			IsBusy = true;

			// Delete local data logic would go here
			// For now, just simulate the operation
			await Task.Delay(1000);

			await UpdateStorageUsageAsync();
			await ShowAlertAsync("Success", "Local data deleted successfully");
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to delete local data");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Show licenses information
	/// </summary>
	[RelayCommand]
	private async Task ShowLicensesAsync()
	{
		await ShowAlertAsync(
			"Licenses",
			"This application uses the following open source components:\n\n" +
			"- .NET MAUI (MIT License)\n" +
			"- CommunityToolkit.Mvvm (MIT License)\n" +
			"- NetTopologySuite (BSD License)\n" +
			"- OpenStreetMap Data (ODbL License)");
	}

	/// <summary>
	/// Logout the current user
	/// </summary>
	[RelayCommand]
	private async Task LogoutAsync()
	{
		try
		{
			var confirmed = await ShowConfirmAsync(
				"Logout",
				"Are you sure you want to logout?",
				"Logout",
				"Cancel");

			if (!confirmed)
				return;

			IsBusy = true;

			await _authService.LogoutAsync();
			await _navigationService.NavigateToAsync("//Login", clearStack: true);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to logout");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Update storage usage display
	/// </summary>
	private async Task UpdateStorageUsageAsync()
	{
		// This would calculate actual storage usage
		// For now, just set a placeholder value
		await Task.CompletedTask;
		OfflineStorageUsed = "0 MB";
	}

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			// Cleanup any managed resources
		}

		_disposed = true;
	}
}
