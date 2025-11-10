// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Services;
using System.Collections.ObjectModel;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the main dashboard/home screen
/// Displays quick stats, recent activity, and provides quick actions
/// </summary>
public partial class MainViewModel : BaseViewModel
{
	private readonly INavigationService _navigationService;
	private readonly IAuthenticationService _authService;
	private readonly ISettingsService _settingsService;

	#region Observable Properties - User Info

	[ObservableProperty]
	private string _welcomeMessage = "Welcome";

	[ObservableProperty]
	private string _username = string.Empty;

	#endregion

	#region Observable Properties - Quick Stats

	[ObservableProperty]
	private int _totalFeatures;

	[ObservableProperty]
	private int _totalCollections;

	[ObservableProperty]
	private int _pendingChanges;

	[ObservableProperty]
	private string _lastSyncTime = "Never";

	#endregion

	#region Observable Properties - Sync Status

	[ObservableProperty]
	private bool _isSyncing;

	[ObservableProperty]
	private string _syncStatus = "Up to date";

	[ObservableProperty]
	private bool _isOnline = true;

	[ObservableProperty]
	private string _connectionStatus = "Online";

	#endregion

	#region Observable Properties - Storage

	[ObservableProperty]
	private string _storageUsed = "0 MB";

	[ObservableProperty]
	private int _offlineMapsCount;

	#endregion

	public ObservableCollection<RecentActivityItem> RecentActivities { get; } = new();

	public MainViewModel(
		INavigationService navigationService,
		IAuthenticationService authService,
		ISettingsService settingsService)
	{
		_navigationService = navigationService;
		_authService = authService;
		_settingsService = settingsService;

		Title = "Home";
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();
		await LoadDashboardDataAsync();
	}

	/// <summary>
	/// Load all dashboard data
	/// </summary>
	private async Task LoadDashboardDataAsync()
	{
		try
		{
			IsBusy = true;

			// Load user info
			await LoadUserInfoAsync();

			// Load statistics
			await LoadStatisticsAsync();

			// Load sync status
			await LoadSyncStatusAsync();

			// Load recent activities
			await LoadRecentActivitiesAsync();

			// Update connection status
			UpdateConnectionStatus();
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to load dashboard data");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Load user information
	/// </summary>
	private async Task LoadUserInfoAsync()
	{
		var user = await _authService.GetCurrentUserAsync();
		if (user != null)
		{
			Username = user.FullName ?? user.Username;
			WelcomeMessage = GetGreeting() + ", " + Username.Split(' ')[0];
		}
	}

	/// <summary>
	/// Get time-appropriate greeting
	/// </summary>
	private string GetGreeting()
	{
		var hour = DateTime.Now.Hour;
		if (hour < 12)
			return "Good morning";
		else if (hour < 18)
			return "Good afternoon";
		else
			return "Good evening";
	}

	/// <summary>
	/// Load dashboard statistics
	/// </summary>
	private async Task LoadStatisticsAsync()
	{
		// In a real implementation, this would query the local database
		// For now, using placeholder values from settings
		TotalFeatures = await _settingsService.GetAsync("total_features_count", 0);
		TotalCollections = await _settingsService.GetAsync("total_collections_count", 0);
		PendingChanges = await _settingsService.GetAsync("pending_changes_count", 0);
		OfflineMapsCount = await _settingsService.GetAsync("offline_maps_count", 0);
		StorageUsed = await _settingsService.GetAsync("storage_used", "0 MB");
	}

	/// <summary>
	/// Load synchronization status
	/// </summary>
	private async Task LoadSyncStatusAsync()
	{
		var lastSync = await _settingsService.GetAsync<DateTime?>("last_sync_time", null);
		if (lastSync.HasValue)
		{
			var timeSpan = DateTime.Now - lastSync.Value;
			if (timeSpan.TotalMinutes < 1)
				LastSyncTime = "Just now";
			else if (timeSpan.TotalHours < 1)
				LastSyncTime = $"{(int)timeSpan.TotalMinutes}m ago";
			else if (timeSpan.TotalDays < 1)
				LastSyncTime = $"{(int)timeSpan.TotalHours}h ago";
			else
				LastSyncTime = $"{(int)timeSpan.TotalDays}d ago";
		}

		SyncStatus = PendingChanges > 0 ? $"{PendingChanges} pending" : "Up to date";
	}

	/// <summary>
	/// Load recent activities
	/// </summary>
	private async Task LoadRecentActivitiesAsync()
	{
		await Task.CompletedTask;

		// In a real implementation, this would query the local database
		// For now, creating sample data
		RecentActivities.Clear();

		// Sample activities (would come from database)
		if (TotalFeatures > 0)
		{
			RecentActivities.Add(new RecentActivityItem
			{
				Title = "Feature created",
				Description = "New point feature added to collection",
				Timestamp = DateTime.Now.AddHours(-2),
				Icon = "add_circle"
			});
		}

		if (PendingChanges > 0)
		{
			RecentActivities.Add(new RecentActivityItem
			{
				Title = $"{PendingChanges} pending changes",
				Description = "Waiting for sync",
				Timestamp = DateTime.Now.AddHours(-5),
				Icon = "sync"
			});
		}
	}

	/// <summary>
	/// Update connection status
	/// </summary>
	private void UpdateConnectionStatus()
	{
		// In a real implementation, this would check actual network connectivity
		// For now, assume online
		IsOnline = true;
		ConnectionStatus = IsOnline ? "Online" : "Offline";
	}

	/// <summary>
	/// Refresh dashboard data
	/// </summary>
	[RelayCommand]
	private async Task RefreshAsync()
	{
		await LoadDashboardDataAsync();
	}

	/// <summary>
	/// Navigate to map view
	/// </summary>
	[RelayCommand]
	private async Task ViewMapAsync()
	{
		await _navigationService.NavigateToAsync("//Map");
	}

	/// <summary>
	/// Navigate to feature list
	/// </summary>
	[RelayCommand]
	private async Task ViewFeaturesAsync()
	{
		await _navigationService.NavigateToAsync("//Features");
	}

	/// <summary>
	/// Navigate to create new feature
	/// </summary>
	[RelayCommand]
	private async Task CreateFeatureAsync()
	{
		await _navigationService.NavigateToAsync("//Features/New");
	}

	/// <summary>
	/// Trigger manual sync
	/// </summary>
	[RelayCommand]
	private async Task SyncNowAsync()
	{
		if (IsSyncing)
			return;

		if (!IsOnline)
		{
			await ShowAlertAsync("Offline", "Cannot sync while offline. Please connect to the internet.");
			return;
		}

		try
		{
			IsSyncing = true;
			SyncStatus = "Syncing...";

			// Simulate sync operation
			await Task.Delay(2000);

			// Update last sync time
			await _settingsService.SetAsync("last_sync_time", DateTime.Now);
			await _settingsService.SetAsync("pending_changes_count", 0);

			// Reload data
			await LoadStatisticsAsync();
			await LoadSyncStatusAsync();
			await LoadRecentActivitiesAsync();

			await ShowAlertAsync("Success", "Sync completed successfully");
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
	/// Navigate to settings
	/// </summary>
	[RelayCommand]
	private async Task OpenSettingsAsync()
	{
		await _navigationService.NavigateToAsync("//Settings");
	}

	/// <summary>
	/// Navigate to profile
	/// </summary>
	[RelayCommand]
	private async Task OpenProfileAsync()
	{
		await _navigationService.NavigateToAsync("//Profile");
	}

	/// <summary>
	/// Navigate to collections
	/// </summary>
	[RelayCommand]
	private async Task ViewCollectionsAsync()
	{
		await ShowAlertAsync("Collections", "Collections view will be available in a future update.");
	}

	/// <summary>
	/// Navigate to offline maps
	/// </summary>
	[RelayCommand]
	private async Task ManageOfflineMapsAsync()
	{
		await ShowAlertAsync("Offline Maps", "Offline maps management will be available in a future update.");
	}

	/// <summary>
	/// View activity details
	/// </summary>
	[RelayCommand]
	private async Task ViewActivityAsync(RecentActivityItem activity)
	{
		if (activity == null)
			return;

		await ShowAlertAsync(activity.Title, activity.Description);
	}
}

/// <summary>
/// Represents a recent activity item
/// </summary>
public class RecentActivityItem
{
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public DateTime Timestamp { get; set; }
	public string Icon { get; set; } = "info";
	public string TimestampDisplay => GetTimestampDisplay();

	private string GetTimestampDisplay()
	{
		var timeSpan = DateTime.Now - Timestamp;
		if (timeSpan.TotalMinutes < 1)
			return "Just now";
		else if (timeSpan.TotalHours < 1)
			return $"{(int)timeSpan.TotalMinutes}m ago";
		else if (timeSpan.TotalDays < 1)
			return $"{(int)timeSpan.TotalHours}h ago";
		else if (timeSpan.TotalDays < 7)
			return $"{(int)timeSpan.TotalDays}d ago";
		else
			return Timestamp.ToString("MMM dd");
	}
}
