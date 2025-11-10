using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Services;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the user profile page
/// Displays user information, account details, and storage statistics
/// </summary>
public partial class ProfileViewModel : BaseViewModel
{
	private readonly IAuthenticationService _authService;
	private readonly ISettingsService _settingsService;
	private readonly INavigationService _navigationService;

	#region Observable Properties - User Info

	[ObservableProperty]
	private string _username = string.Empty;

	[ObservableProperty]
	private string _email = string.Empty;

	[ObservableProperty]
	private string _fullName = string.Empty;

	[ObservableProperty]
	private string _userId = string.Empty;

	[ObservableProperty]
	private string _organizationId = string.Empty;

	#endregion

	#region Observable Properties - Storage & Sync

	[ObservableProperty]
	private string _storageUsed = "0 MB";

	[ObservableProperty]
	private string _storageAvailable = "100 MB";

	[ObservableProperty]
	private int _storagePercentage;

	[ObservableProperty]
	private string _lastSyncTime = "Never";

	[ObservableProperty]
	private bool _isSyncing;

	[ObservableProperty]
	private string _syncStatus = "Up to date";

	[ObservableProperty]
	private int _pendingChanges;

	#endregion

	#region Observable Properties - Offline Capabilities

	[ObservableProperty]
	private bool _offlineModeEnabled;

	[ObservableProperty]
	private int _offlineFeaturesCount;

	[ObservableProperty]
	private int _offlineMapsCount;

	#endregion

	#region Observable Properties - Account Status

	[ObservableProperty]
	private bool _isAccountVerified = true;

	[ObservableProperty]
	private string _accountType = "Standard";

	[ObservableProperty]
	private DateTime _accountCreatedDate = DateTime.Now;

	#endregion

	public ProfileViewModel(
		IAuthenticationService authService,
		ISettingsService settingsService,
		INavigationService navigationService)
	{
		_authService = authService;
		_settingsService = settingsService;
		_navigationService = navigationService;

		Title = "Profile";
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();
		await LoadProfileDataAsync();
	}

	/// <summary>
	/// Load user profile data from authentication service and settings
	/// </summary>
	private async Task LoadProfileDataAsync()
	{
		try
		{
			IsBusy = true;

			// Load user information
			var user = await _authService.GetCurrentUserAsync();
			if (user != null)
			{
				Username = user.Username;
				Email = user.Email;
				FullName = user.FullName ?? string.Empty;
				UserId = user.Id;
				OrganizationId = user.OrganizationId ?? "None";
			}

			// Load storage statistics
			await LoadStorageStatisticsAsync();

			// Load sync status
			await LoadSyncStatusAsync();

			// Load offline capabilities
			await LoadOfflineStatusAsync();

			// Load account info
			await LoadAccountInfoAsync();
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to load profile data");
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Load storage usage statistics
	/// </summary>
	private async Task LoadStorageStatisticsAsync()
	{
		// This would calculate actual storage usage from database/file system
		// For now, using placeholder values
		await Task.CompletedTask;

		StorageUsed = "2.5 MB";
		StorageAvailable = "97.5 MB";
		StoragePercentage = 3;
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
				LastSyncTime = $"{(int)timeSpan.TotalMinutes} minutes ago";
			else if (timeSpan.TotalDays < 1)
				LastSyncTime = $"{(int)timeSpan.TotalHours} hours ago";
			else
				LastSyncTime = $"{(int)timeSpan.TotalDays} days ago";
		}
		else
		{
			LastSyncTime = "Never";
		}

		PendingChanges = await _settingsService.GetAsync("pending_changes_count", 0);
		SyncStatus = PendingChanges > 0 ? $"{PendingChanges} pending changes" : "Up to date";
	}

	/// <summary>
	/// Load offline capabilities status
	/// </summary>
	private async Task LoadOfflineStatusAsync()
	{
		OfflineModeEnabled = await _settingsService.GetAsync("offline_mode_enabled", true);
		OfflineFeaturesCount = await _settingsService.GetAsync("offline_features_count", 0);
		OfflineMapsCount = await _settingsService.GetAsync("offline_maps_count", 0);
	}

	/// <summary>
	/// Load account information
	/// </summary>
	private async Task LoadAccountInfoAsync()
	{
		IsAccountVerified = await _settingsService.GetAsync("account_verified", true);
		AccountType = await _settingsService.GetAsync("account_type", "Standard");
		AccountCreatedDate = await _settingsService.GetAsync("account_created_date", DateTime.Now.AddMonths(-6));
	}

	/// <summary>
	/// Refresh profile data
	/// </summary>
	[RelayCommand]
	private async Task RefreshAsync()
	{
		await LoadProfileDataAsync();
	}

	/// <summary>
	/// Edit user profile
	/// </summary>
	[RelayCommand]
	private async Task EditProfileAsync()
	{
		await ShowAlertAsync(
			"Edit Profile",
			"Profile editing will be available in a future update. Please contact your administrator to update your profile information.");
	}

	/// <summary>
	/// Change password
	/// </summary>
	[RelayCommand]
	private async Task ChangePasswordAsync()
	{
		await ShowAlertAsync(
			"Change Password",
			"Password changes must be done through your organization's identity provider. Please contact your administrator for assistance.");
	}

	/// <summary>
	/// Manage storage
	/// </summary>
	[RelayCommand]
	private async Task ManageStorageAsync()
	{
		await _navigationService.NavigateToAsync("//Settings");
	}

	/// <summary>
	/// Trigger manual sync
	/// </summary>
	[RelayCommand]
	private async Task SyncNowAsync()
	{
		if (IsSyncing)
			return;

		try
		{
			IsSyncing = true;
			SyncStatus = "Syncing...";

			// Simulate sync operation
			await Task.Delay(2000);

			// Update last sync time
			await _settingsService.SetAsync("last_sync_time", DateTime.Now);
			await _settingsService.SetAsync("pending_changes_count", 0);

			await LoadSyncStatusAsync();
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
	/// View sync history
	/// </summary>
	[RelayCommand]
	private async Task ViewSyncHistoryAsync()
	{
		await ShowAlertAsync(
			"Sync History",
			"Sync history feature will be available in a future update.");
	}

	/// <summary>
	/// Toggle offline mode
	/// </summary>
	[RelayCommand]
	private async Task ToggleOfflineModeAsync()
	{
		try
		{
			await _settingsService.SetAsync("offline_mode_enabled", OfflineModeEnabled);

			var message = OfflineModeEnabled
				? "Offline mode enabled. You can now work without an internet connection."
				: "Offline mode disabled. App will require internet connection.";

			await ShowAlertAsync("Offline Mode", message);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to toggle offline mode");
			OfflineModeEnabled = !OfflineModeEnabled;
		}
	}

	/// <summary>
	/// Download offline data
	/// </summary>
	[RelayCommand]
	private async Task DownloadOfflineDataAsync()
	{
		await ShowAlertAsync(
			"Download Offline Data",
			"This feature allows you to download maps and data for offline use. Implementation coming soon.");
	}

	/// <summary>
	/// Verify account
	/// </summary>
	[RelayCommand]
	private async Task VerifyAccountAsync()
	{
		if (IsAccountVerified)
		{
			await ShowAlertAsync("Account Verified", "Your account is already verified.");
			return;
		}

		await ShowAlertAsync(
			"Verify Account",
			"Please check your email for verification instructions.");
	}

	/// <summary>
	/// View account details
	/// </summary>
	[RelayCommand]
	private async Task ViewAccountDetailsAsync()
	{
		var details = $"User ID: {UserId}\n" +
		              $"Username: {Username}\n" +
		              $"Email: {Email}\n" +
		              $"Organization: {OrganizationId}\n" +
		              $"Account Type: {AccountType}\n" +
		              $"Created: {AccountCreatedDate:MMM dd, yyyy}\n" +
		              $"Verified: {(IsAccountVerified ? "Yes" : "No")}";

		await ShowAlertAsync("Account Details", details);
	}
}
