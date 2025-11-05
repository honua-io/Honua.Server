using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HonuaField.Services;

namespace HonuaField.ViewModels;

/// <summary>
/// ViewModel for the login page
/// Handles user authentication with OAuth 2.0 Authorization Code + PKCE flow
/// </summary>
public partial class LoginViewModel : BaseViewModel
{
	private readonly IAuthenticationService _authService;
	private readonly INavigationService _navigationService;
	private readonly ISettingsService _settingsService;

	[ObservableProperty]
	private string _username = string.Empty;

	[ObservableProperty]
	private string _password = string.Empty;

	[ObservableProperty]
	private bool _rememberMe = true;

	[ObservableProperty]
	private bool _useBiometrics;

	[ObservableProperty]
	private bool _isBiometricsAvailable;

	[ObservableProperty]
	private string _serverUrl = "https://api.honua.io";

	public LoginViewModel(
		IAuthenticationService authService,
		INavigationService navigationService,
		ISettingsService settingsService)
	{
		_authService = authService;
		_navigationService = navigationService;
		_settingsService = settingsService;

		Title = "Sign In";
	}

	public override async Task OnAppearingAsync()
	{
		await base.OnAppearingAsync();

		// Load saved settings
		Username = await _settingsService.GetAsync("last_username", string.Empty);
		RememberMe = await _settingsService.GetAsync("remember_me", true);
		ServerUrl = await _settingsService.GetAsync("server_url", "https://api.honua.io");

		// Check if biometrics are available and enabled
		await CheckBiometricsAvailabilityAsync();

		// Auto-login with biometrics if enabled
		if (UseBiometrics && IsBiometricsAvailable)
		{
			await LoginWithBiometricsCommand.ExecuteAsync(null);
		}
	}

	[RelayCommand]
	private async Task LoginAsync()
	{
		if (IsBusy)
			return;

		// Validate inputs
		if (string.IsNullOrWhiteSpace(Username))
		{
			await ShowAlertAsync("Error", "Please enter your username");
			return;
		}

		if (string.IsNullOrWhiteSpace(Password))
		{
			await ShowAlertAsync("Error", "Please enter your password");
			return;
		}

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			// Attempt login using OAuth 2.0 Authorization Code + PKCE flow
			var success = await _authService.LoginAsync(Username, Password);

			if (success)
			{
				// Save settings if Remember Me is checked
				if (RememberMe)
				{
					await _settingsService.SetAsync("last_username", Username);
					await _settingsService.SetAsync("remember_me", true);
				}
				else
				{
					await _settingsService.RemoveAsync("last_username");
				}

				await _settingsService.SetAsync("server_url", ServerUrl);

				// Setup biometrics if requested
				if (UseBiometrics && IsBiometricsAvailable)
				{
					await SetupBiometricsAsync();
				}

				// Navigate to main app
				await _navigationService.NavigateToAsync("//Main", clearStack: true);
			}
			else
			{
				ErrorMessage = "Invalid username or password";
				await ShowAlertAsync("Login Failed", "Please check your credentials and try again.");
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Login failed");
		}
		finally
		{
			IsBusy = false;
			Password = string.Empty; // Clear password for security
		}
	}

	[RelayCommand]
	private async Task LoginWithBiometricsAsync()
	{
		if (!IsBiometricsAvailable || IsBusy)
			return;

		IsBusy = true;
		ErrorMessage = string.Empty;

		try
		{
			// Authenticate with biometrics
			var authenticated = await AuthenticateWithBiometricsAsync();

			if (authenticated)
			{
				// Retrieve stored credentials
				var storedUsername = await _settingsService.GetAsync<string>("biometric_username");
				var storedPassword = await _settingsService.GetAsync<string>("biometric_password");

				if (!string.IsNullOrEmpty(storedUsername) && !string.IsNullOrEmpty(storedPassword))
				{
					Username = storedUsername;
					Password = storedPassword;

					// Use regular login flow
					await LoginAsync();
				}
				else
				{
					await ShowAlertAsync("Error", "Biometric credentials not found. Please login with username and password.");
				}
			}
		}
		catch (Exception ex)
		{
			await HandleErrorAsync(ex, "Biometric authentication failed");
		}
		finally
		{
			IsBusy = false;
		}
	}

	[RelayCommand]
	private async Task ForgotPasswordAsync()
	{
		await ShowAlertAsync(
			"Reset Password",
			"Please contact your system administrator to reset your password.");
	}

	[RelayCommand]
	private async Task ShowServerSettingsAsync()
	{
		// TODO: Navigate to server settings page
		await ShowAlertAsync("Server Settings", $"Current server: {ServerUrl}");
	}

	private async Task CheckBiometricsAvailabilityAsync()
	{
		try
		{
			// Check if biometric authentication is available on this device
			// This is a placeholder - actual implementation would check platform capabilities
			IsBiometricsAvailable = false; // Will be implemented with platform-specific code

			// Check if user has enabled biometrics
			UseBiometrics = await _settingsService.GetAsync("use_biometrics", false);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error checking biometrics: {ex.Message}");
			IsBiometricsAvailable = false;
		}
	}

	private async Task<bool> AuthenticateWithBiometricsAsync()
	{
		try
		{
			// TODO: Implement platform-specific biometric authentication
			// For now, return false as placeholder
			await Task.Delay(100); // Simulate biometric check
			return false;
		}
		catch
		{
			return false;
		}
	}

	private async Task SetupBiometricsAsync()
	{
		try
		{
			// Store credentials securely for biometric login
			await _settingsService.SetAsync("use_biometrics", true);
			await _settingsService.SetAsync("biometric_username", Username);
			await _settingsService.SetAsync("biometric_password", Password);
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Error setting up biometrics: {ex.Message}");
		}
	}
}
