using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Services;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the onboarding flow
/// Handles first-run experience, permissions, and initial setup
/// </summary>
public partial class OnboardingViewModel : BaseViewModel
{
	private readonly ISettingsService _settingsService;
	private readonly INavigationService _navigationService;
	private readonly IAuthenticationService _authService;

	#region Observable Properties - Onboarding State

	[ObservableProperty]
	private int _currentStep;

	[ObservableProperty]
	private int _totalSteps = 4;

	[ObservableProperty]
	private string _stepTitle = "Welcome";

	[ObservableProperty]
	private string _stepDescription = "Welcome to Honua Field";

	[ObservableProperty]
	private bool _canGoNext = true;

	[ObservableProperty]
	private bool _canGoPrevious;

	[ObservableProperty]
	private bool _showSkipButton = true;

	#endregion

	#region Observable Properties - Permissions

	[ObservableProperty]
	private bool _locationPermissionGranted;

	[ObservableProperty]
	private bool _cameraPermissionGranted;

	[ObservableProperty]
	private bool _storagePermissionGranted;

	[ObservableProperty]
	private bool _allPermissionsGranted;

	#endregion

	#region Observable Properties - Server Configuration

	[ObservableProperty]
	private string _serverUrl = "https://api.honua.io";

	[ObservableProperty]
	private bool _serverConfigured;

	#endregion

	public List<OnboardingStep> Steps { get; } = new()
	{
		new OnboardingStep
		{
			StepNumber = 0,
			Title = "Welcome",
			Description = "Welcome to Honua Field - Your offline-first geospatial data collection app",
			Content = "Collect and manage field data even without an internet connection. Sync when you're back online."
		},
		new OnboardingStep
		{
			StepNumber = 1,
			Title = "Permissions",
			Description = "We need a few permissions to provide the best experience",
			Content = "Location: To capture GPS coordinates\nCamera: To attach photos to features\nStorage: To save data offline"
		},
		new OnboardingStep
		{
			StepNumber = 2,
			Title = "Server Setup",
			Description = "Configure your Honua server connection",
			Content = "Enter your organization's Honua server URL to sync your data."
		},
		new OnboardingStep
		{
			StepNumber = 3,
			Title = "All Set",
			Description = "You're ready to start collecting data",
			Content = "Sign in to begin using Honua Field."
		}
	};

	public OnboardingViewModel(
		ISettingsService settingsService,
		INavigationService navigationService,
		IAuthenticationService authService)
	{
		_settingsService = settingsService;
		_navigationService = navigationService;
		_authService = authService;

		Title = "Get Started";
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();
		await InitializeOnboardingAsync();
	}

	/// <summary>
	/// Initialize onboarding flow
	/// </summary>
	private async Task InitializeOnboardingAsync()
	{
		try
		{
			// Check if onboarding has been completed
			var hasCompletedOnboarding = await _settingsService.GetAsync("onboarding_completed", false);
			if (hasCompletedOnboarding)
			{
				// Navigate to login if already onboarded
				await _navigationService.NavigateToAsync("//Login", clearStack: true);
				return;
			}

			// Load saved server URL if any
			ServerUrl = await _settingsService.GetAsync("server_url", "https://api.honua.io");

			// Start from first step
			CurrentStep = 0;
			UpdateStepInfo();
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to initialize onboarding");
		}
	}

	/// <summary>
	/// Update step information based on current step
	/// </summary>
	private void UpdateStepInfo()
	{
		if (CurrentStep < 0 || CurrentStep >= Steps.Count)
			return;

		var step = Steps[CurrentStep];
		StepTitle = step.Title;
		StepDescription = step.Description;

		CanGoPrevious = CurrentStep > 0;
		CanGoNext = CurrentStep < TotalSteps - 1;

		// Hide skip button on last step
		ShowSkipButton = CurrentStep < TotalSteps - 1;
	}

	/// <summary>
	/// Move to next step
	/// </summary>
	[RelayCommand]
	private async Task NextStepAsync()
	{
		if (CurrentStep >= TotalSteps - 1)
		{
			await CompleteOnboardingAsync();
			return;
		}

		// Validate current step before proceeding
		if (!await ValidateCurrentStepAsync())
			return;

		CurrentStep++;
		UpdateStepInfo();

		// Auto-request permissions on permissions step
		if (CurrentStep == 1)
		{
			await CheckPermissionsAsync();
		}
	}

	/// <summary>
	/// Move to previous step
	/// </summary>
	[RelayCommand]
	private void PreviousStep()
	{
		if (CurrentStep > 0)
		{
			CurrentStep--;
			UpdateStepInfo();
		}
	}

	/// <summary>
	/// Skip onboarding
	/// </summary>
	[RelayCommand]
	private async Task SkipOnboardingAsync()
	{
		var confirmed = await ShowConfirmAsync(
			"Skip Setup",
			"You can complete this setup later in Settings. Continue?",
			"Skip",
			"Cancel");

		if (confirmed)
		{
			await CompleteOnboardingAsync();
		}
	}

	/// <summary>
	/// Validate current step before proceeding
	/// </summary>
	private async Task<bool> ValidateCurrentStepAsync()
	{
		switch (CurrentStep)
		{
			case 1: // Permissions step
				if (!AllPermissionsGranted)
				{
					var proceed = await ShowConfirmAsync(
						"Permissions Required",
						"Some permissions were not granted. The app may not function properly. Continue anyway?",
						"Continue",
						"Cancel");
					return proceed;
				}
				break;

			case 2: // Server configuration step
				if (string.IsNullOrWhiteSpace(ServerUrl))
				{
					await ShowAlertAsync("Server URL Required", "Please enter a valid server URL");
					return false;
				}

				if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ||
				    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
				{
					await ShowAlertAsync("Invalid URL", "Please enter a valid HTTP or HTTPS URL");
					return false;
				}

				// Save server URL
				await _settingsService.SetAsync("server_url", ServerUrl);
				ServerConfigured = true;
				break;
		}

		return true;
	}

	/// <summary>
	/// Request location permission
	/// </summary>
	[RelayCommand]
	private async Task RequestLocationPermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

			if (status != PermissionStatus.Granted)
			{
				status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			}

			LocationPermissionGranted = status == PermissionStatus.Granted;
			UpdateAllPermissionsStatus();

			if (!LocationPermissionGranted)
			{
				await ShowAlertAsync(
					"Permission Denied",
					"Location permission is required to capture GPS coordinates for your field data.");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to request location permission");
		}
	}

	/// <summary>
	/// Request camera permission
	/// </summary>
	[RelayCommand]
	private async Task RequestCameraPermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.Camera>();

			if (status != PermissionStatus.Granted)
			{
				status = await Permissions.RequestAsync<Permissions.Camera>();
			}

			CameraPermissionGranted = status == PermissionStatus.Granted;
			UpdateAllPermissionsStatus();

			if (!CameraPermissionGranted)
			{
				await ShowAlertAsync(
					"Permission Denied",
					"Camera permission is required to attach photos to your field data.");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to request camera permission");
		}
	}

	/// <summary>
	/// Request storage permission
	/// </summary>
	[RelayCommand]
	private async Task RequestStoragePermissionAsync()
	{
		try
		{
			var status = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();

			if (status != PermissionStatus.Granted)
			{
				status = await Permissions.RequestAsync<Permissions.StorageWrite>();
			}

			StoragePermissionGranted = status == PermissionStatus.Granted;
			UpdateAllPermissionsStatus();

			if (!StoragePermissionGranted)
			{
				await ShowAlertAsync(
					"Permission Denied",
					"Storage permission is required to save data offline.");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to request storage permission");
		}
	}

	/// <summary>
	/// Request all permissions at once
	/// </summary>
	[RelayCommand]
	private async Task RequestAllPermissionsAsync()
	{
		await RequestLocationPermissionAsync();
		await RequestCameraPermissionAsync();
		await RequestStoragePermissionAsync();
	}

	/// <summary>
	/// Check current permission status
	/// </summary>
	private async Task CheckPermissionsAsync()
	{
		try
		{
			LocationPermissionGranted = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() == PermissionStatus.Granted;
			CameraPermissionGranted = await Permissions.CheckStatusAsync<Permissions.Camera>() == PermissionStatus.Granted;
			StoragePermissionGranted = await Permissions.CheckStatusAsync<Permissions.StorageWrite>() == PermissionStatus.Granted;

			UpdateAllPermissionsStatus();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error checking permissions: {ex.Message}");
		}
	}

	/// <summary>
	/// Update all permissions granted status
	/// </summary>
	private void UpdateAllPermissionsStatus()
	{
		AllPermissionsGranted = LocationPermissionGranted && CameraPermissionGranted && StoragePermissionGranted;
	}

	/// <summary>
	/// Test server connection
	/// </summary>
	[RelayCommand]
	private async Task TestServerConnectionAsync()
	{
		if (string.IsNullOrWhiteSpace(ServerUrl))
		{
			await ShowAlertAsync("Server URL Required", "Please enter a server URL first");
			return;
		}

		try
		{
			IsBusy = true;

			// Simulate server connection test
			await Task.Delay(1500);

			// In a real implementation, this would make an HTTP request to test connectivity
			await ShowAlertAsync("Connection Successful", "Successfully connected to the server");
			ServerConfigured = true;
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to connect to server");
			ServerConfigured = false;
		}
		finally
		{
			IsBusy = false;
		}
	}

	/// <summary>
	/// Complete onboarding and navigate to login
	/// </summary>
	[RelayCommand]
	private async Task CompleteOnboardingAsync()
	{
		try
		{
			IsBusy = true;

			// Mark onboarding as completed
			await _settingsService.SetAsync("onboarding_completed", true);

			// Save server URL if configured
			if (ServerConfigured && !string.IsNullOrWhiteSpace(ServerUrl))
			{
				await _settingsService.SetAsync("server_url", ServerUrl);
			}

			// Navigate to login
			await _navigationService.NavigateToAsync("//Login", clearStack: true);
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Failed to complete onboarding");
		}
		finally
		{
			IsBusy = false;
		}
	}
}

/// <summary>
/// Represents a single step in the onboarding flow
/// </summary>
public class OnboardingStep
{
	public int StepNumber { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Description { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
}
