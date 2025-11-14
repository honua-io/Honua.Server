// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Services;
using Microsoft.Extensions.Logging;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the app shell
/// Manages navigation menu, user profile display, and global app state
/// </summary>
public partial class AppShellViewModel : BaseViewModel
{
	private readonly INavigationService _navigationService;
	private readonly IAuthenticationService _authService;
	private readonly ISettingsService _settingsService;

	#region Observable Properties - User Profile

	[ObservableProperty]
	private string _username = string.Empty;

	[ObservableProperty]
	private string _email = string.Empty;

	[ObservableProperty]
	private string _userInitials = string.Empty;

	[ObservableProperty]
	private bool _isAuthenticated;

	#endregion

	#region Observable Properties - Sync Status

	[ObservableProperty]
	private bool _isSyncing;

	[ObservableProperty]
	private int _pendingChanges;

	[ObservableProperty]
	private string _syncStatusText = "Up to date";

	[ObservableProperty]
	private bool _isOnline = true;

	#endregion

	#region Observable Properties - Notifications

	[ObservableProperty]
	private int _notificationCount;

	[ObservableProperty]
	private bool _hasNotifications;

	[ObservableProperty]
	private int _alertCount;

	[ObservableProperty]
	private bool _hasAlerts;

	#endregion

	#region Observable Properties - Menu State

	[ObservableProperty]
	private bool _isFlyoutOpen;

	[ObservableProperty]
	private string _currentRoute = "//Main";

	#endregion

	public AppShellViewModel(
		INavigationService navigationService,
		IAuthenticationService authService,
		ISettingsService settingsService)
	{
		_navigationService = navigationService;
		_authService = authService;
		_settingsService = settingsService;

		Title = "Honua Field";
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();
		await InitializeAsync();
	}

	/// <summary>
	/// Initialize app shell data
	/// </summary>
	private async Task InitializeAsync()
	{
		try
		{
			await LoadUserProfileAsync();
			await LoadSyncStatusAsync();
			await LoadNotificationsAsync();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error initializing AppShell");
		}
	}

	/// <summary>
	/// Load user profile information for menu header
	/// </summary>
	private async Task LoadUserProfileAsync()
	{
		IsAuthenticated = await _authService.IsAuthenticatedAsync();

		if (IsAuthenticated)
		{
			var user = await _authService.GetCurrentUserAsync();
			if (user != null)
			{
				Username = user.FullName ?? user.Username;
				Email = user.Email;
				UserInitials = GetUserInitials(Username);
			}
		}
		else
		{
			Username = "Guest";
			Email = string.Empty;
			UserInitials = "?";
		}
	}

	/// <summary>
	/// Get user initials from name
	/// </summary>
	private string GetUserInitials(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
			return "?";

		var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0)
			return "?";

		if (parts.Length == 1)
			return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();

		return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
	}

	/// <summary>
	/// Load sync status
	/// </summary>
	private async Task LoadSyncStatusAsync()
	{
		PendingChanges = await _settingsService.GetAsync("pending_changes_count", 0);
		SyncStatusText = PendingChanges > 0 ? $"{PendingChanges} pending" : "Up to date";

		// Check if currently syncing
		IsSyncing = false; // Would be updated by sync service

		// Update online status
		UpdateOnlineStatus();
	}

	/// <summary>
	/// Load notifications and alerts
	/// </summary>
	private async Task LoadNotificationsAsync()
	{
		NotificationCount = await _settingsService.GetAsync("notification_count", 0);
		HasNotifications = NotificationCount > 0;

		AlertCount = await _settingsService.GetAsync("alert_count", 0);
		HasAlerts = AlertCount > 0;
	}

	/// <summary>
	/// Update online status
	/// </summary>
	private void UpdateOnlineStatus()
	{
		// In a real implementation, this would check network connectivity
		IsOnline = true;
	}

	/// <summary>
	/// Refresh all shell data
	/// </summary>
	[RelayCommand]
	private async Task RefreshAsync()
	{
		await InitializeAsync();
	}

	/// <summary>
	/// Navigate to Home
	/// </summary>
	[RelayCommand]
	private async Task NavigateToHomeAsync()
	{
		CurrentRoute = "//Main";
		IsFlyoutOpen = false;
		await _navigationService.NavigateToAsync("//Main");
	}

	/// <summary>
	/// Navigate to Map
	/// </summary>
	[RelayCommand]
	private async Task NavigateToMapAsync()
	{
		CurrentRoute = "//Map";
		IsFlyoutOpen = false;
		await _navigationService.NavigateToAsync("//Map");
	}

	/// <summary>
	/// Navigate to Features
	/// </summary>
	[RelayCommand]
	private async Task NavigateToFeaturesAsync()
	{
		CurrentRoute = "//Features";
		IsFlyoutOpen = false;
		await _navigationService.NavigateToAsync("//Features");
	}

	/// <summary>
	/// Navigate to Collections
	/// </summary>
	[RelayCommand]
	private async Task NavigateToCollectionsAsync()
	{
		CurrentRoute = "//Collections";
		IsFlyoutOpen = false;
		await ShowAlertAsync("Collections", "Collections view will be available in a future update.");
	}

	/// <summary>
	/// Navigate to Profile
	/// </summary>
	[RelayCommand]
	private async Task NavigateToProfileAsync()
	{
		CurrentRoute = "//Profile";
		IsFlyoutOpen = false;
		await _navigationService.NavigateToAsync("//Profile");
	}

	/// <summary>
	/// Navigate to Settings
	/// </summary>
	[RelayCommand]
	private async Task NavigateToSettingsAsync()
	{
		CurrentRoute = "//Settings";
		IsFlyoutOpen = false;
		await _navigationService.NavigateToAsync("//Settings");
	}

	/// <summary>
	/// Navigate to Offline Maps
	/// </summary>
	[RelayCommand]
	private async Task NavigateToOfflineMapsAsync()
	{
		IsFlyoutOpen = false;
		await ShowAlertAsync("Offline Maps", "Offline maps management will be available in a future update.");
	}

	/// <summary>
	/// Trigger sync
	/// </summary>
	[RelayCommand]
	private async Task TriggerSyncAsync()
	{
		if (IsSyncing)
			return;

		if (!IsOnline)
		{
			await ShowAlertAsync("Offline", "Cannot sync while offline.");
			return;
		}

		try
		{
			IsSyncing = true;
			SyncStatusText = "Syncing...";

			// Simulate sync
			await Task.Delay(2000);

			// Update sync status
			await _settingsService.SetAsync("last_sync_time", DateTime.Now);
			await _settingsService.SetAsync("pending_changes_count", 0);

			await LoadSyncStatusAsync();
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Sync failed");
		}
		finally
		{
			IsSyncing = false;
		}
	}

	/// <summary>
	/// View notifications
	/// </summary>
	[RelayCommand]
	private async Task ViewNotificationsAsync()
	{
		IsFlyoutOpen = false;
		await ShowAlertAsync("Notifications", "Notifications view will be available in a future update.");

		// Clear notification badge
		NotificationCount = 0;
		HasNotifications = false;
		await _settingsService.SetAsync("notification_count", 0);
	}

	/// <summary>
	/// View alerts
	/// </summary>
	[RelayCommand]
	private async Task ViewAlertsAsync()
	{
		IsFlyoutOpen = false;
		await ShowAlertAsync("Alerts", "Alerts view will be available in a future update.");

		// Clear alert badge
		AlertCount = 0;
		HasAlerts = false;
		await _settingsService.SetAsync("alert_count", 0);
	}

	/// <summary>
	/// Toggle flyout menu
	/// </summary>
	[RelayCommand]
	private void ToggleFlyout()
	{
		IsFlyoutOpen = !IsFlyoutOpen;
	}

	/// <summary>
	/// Show help
	/// </summary>
	[RelayCommand]
	private async Task ShowHelpAsync()
	{
		IsFlyoutOpen = false;
		await ShowAlertAsync(
			"Help",
			"Honua Field - Offline-first geospatial data collection\n\n" +
			"For support, please contact your system administrator or visit our documentation at https://docs.honua.io");
	}

	/// <summary>
	/// Show about
	/// </summary>
	[RelayCommand]
	private async Task ShowAboutAsync()
	{
		IsFlyoutOpen = false;
		var version = AppInfo.Current.VersionString;
		var build = AppInfo.Current.BuildString;

		await ShowAlertAsync(
			"About Honua Field",
			$"Version: {version} (Build {build})\n\n" +
			"Offline-first geospatial data collection for field teams.\n\n" +
			"Copyright © 2024 Honua.io");
	}

	/// <summary>
	/// Logout
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
			IsFlyoutOpen = false;

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
	/// Update sync badge count
	/// </summary>
	public async Task UpdateSyncBadgeAsync(int count)
	{
		PendingChanges = count;
		await _settingsService.SetAsync("pending_changes_count", count);
		SyncStatusText = count > 0 ? $"{count} pending" : "Up to date";
	}

	/// <summary>
	/// Update notification badge count
	/// </summary>
	public async Task UpdateNotificationBadgeAsync(int count)
	{
		NotificationCount = count;
		HasNotifications = count > 0;
		await _settingsService.SetAsync("notification_count", count);
	}

	/// <summary>
	/// Update alert badge count
	/// </summary>
	public async Task UpdateAlertBadgeAsync(int count)
	{
		AlertCount = count;
		HasAlerts = count > 0;
		await _settingsService.SetAsync("alert_count", count);
	}
}
