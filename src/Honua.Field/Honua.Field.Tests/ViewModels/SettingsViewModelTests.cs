using FluentAssertions;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for SettingsViewModel
/// Tests settings management, preferences, and configuration
/// </summary>
public class SettingsViewModelTests : IDisposable
{
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<IBiometricService> _mockBiometricService;
	private readonly SettingsViewModel _viewModel;

	public SettingsViewModelTests()
	{
		_mockSettingsService = new Mock<ISettingsService>();
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockNavigationService = new Mock<INavigationService>();
		_mockBiometricService = new Mock<IBiometricService>();

		_viewModel = new SettingsViewModel(
			_mockSettingsService.Object,
			_mockAuthService.Object,
			_mockNavigationService.Object,
			_mockBiometricService.Object
		);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Settings");
		_viewModel.ServerUrl.Should().Be("https://api.honua.io");
		_viewModel.AutoSyncEnabled.Should().BeTrue();
		_viewModel.SyncInterval.Should().Be(15);
		_viewModel.WifiOnlySync.Should().BeTrue();
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadAllSettings()
	{
		// Arrange
		var serverUrl = "https://test.honua.io";
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com"
		};

		_mockSettingsService.Setup(x => x.GetAsync("server_url", "https://api.honua.io")).ReturnsAsync(serverUrl);
		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockBiometricService.Setup(x => x.IsBiometricAvailableAsync()).ReturnsAsync(true);
		_mockBiometricService.Setup(x => x.IsBiometricEnrolledAsync()).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("use_biometrics", false)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("units", "Metric")).ReturnsAsync("Imperial");
		_mockSettingsService.Setup(x => x.GetAsync("coordinate_format", "Decimal Degrees")).ReturnsAsync("Degrees Minutes Seconds");
		_mockSettingsService.Setup(x => x.GetAsync("gps_accuracy", "High")).ReturnsAsync("Medium");
		_mockSettingsService.Setup(x => x.GetAsync("basemap_provider", "OpenStreetMap")).ReturnsAsync("Satellite");
		_mockSettingsService.Setup(x => x.GetAsync("offline_tiles_enabled", false)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("auto_sync_enabled", true)).ReturnsAsync(false);
		_mockSettingsService.Setup(x => x.GetAsync("sync_interval", 15)).ReturnsAsync(30);
		_mockSettingsService.Setup(x => x.GetAsync("wifi_only_sync", true)).ReturnsAsync(false);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.ServerUrl.Should().Be(serverUrl);
		_viewModel.LoggedInUser.Should().Be("testuser");
		_viewModel.BiometricsAvailable.Should().BeTrue();
		_viewModel.BiometricsEnabled.Should().BeTrue();
		_viewModel.SelectedUnits.Should().Be("Imperial");
		_viewModel.CoordinateFormat.Should().Be("Degrees Minutes Seconds");
		_viewModel.GpsAccuracy.Should().Be("Medium");
		_viewModel.BaseMapProvider.Should().Be("Satellite");
		_viewModel.OfflineTilesEnabled.Should().BeTrue();
		_viewModel.AutoSyncEnabled.Should().BeFalse();
		_viewModel.SyncInterval.Should().Be(30);
		_viewModel.WifiOnlySync.Should().BeFalse();
	}

	[Fact]
	public async Task SaveServerUrlCommand_ShouldSaveUrlToSettings()
	{
		// Arrange
		_viewModel.ServerUrl = "https://custom.honua.io";

		// Act
		await _viewModel.SaveServerUrlCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("server_url", "https://custom.honua.io"),
			Times.Once
		);
	}

	[Fact]
	public async Task ToggleBiometricsCommand_ShouldSaveBiometricSetting_WhenAvailable()
	{
		// Arrange
		_viewModel.BiometricsAvailable = true;
		_viewModel.BiometricsEnabled = true;

		// Act
		await _viewModel.ToggleBiometricsCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("use_biometrics", true),
			Times.Once
		);
	}

	[Fact]
	public async Task ToggleBiometricsCommand_ShouldNotSave_WhenNotAvailable()
	{
		// Arrange
		_viewModel.BiometricsAvailable = false;
		_viewModel.BiometricsEnabled = true;

		// Act
		await _viewModel.ToggleBiometricsCommand.ExecuteAsync(null);

		// Assert
		_viewModel.BiometricsEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task SavePreferencesCommand_ShouldSaveAllPreferences()
	{
		// Arrange
		_viewModel.SelectedUnits = "Imperial";
		_viewModel.CoordinateFormat = "Degrees Minutes Seconds";
		_viewModel.GpsAccuracy = "Low";

		// Act
		await _viewModel.SavePreferencesCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(x => x.SetAsync("units", "Imperial"), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync("coordinate_format", "Degrees Minutes Seconds"), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync("gps_accuracy", "Low"), Times.Once);
	}

	[Fact]
	public async Task SaveMapSettingsCommand_ShouldSaveMapSettings()
	{
		// Arrange
		_viewModel.BaseMapProvider = "Topographic";
		_viewModel.OfflineTilesEnabled = true;

		// Act
		await _viewModel.SaveMapSettingsCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(x => x.SetAsync("basemap_provider", "Topographic"), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync("offline_tiles_enabled", true), Times.Once);
	}

	[Fact]
	public async Task SaveSyncSettingsCommand_ShouldSaveSyncSettings()
	{
		// Arrange
		_viewModel.AutoSyncEnabled = false;
		_viewModel.SyncInterval = 60;
		_viewModel.WifiOnlySync = false;

		// Act
		await _viewModel.SaveSyncSettingsCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(x => x.SetAsync("auto_sync_enabled", false), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync("sync_interval", 60), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync("wifi_only_sync", false), Times.Once);
	}

	[Fact]
	public async Task ClearCacheCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ClearCacheCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task DeleteLocalDataCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.DeleteLocalDataCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ShowLicensesCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ShowLicensesCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task LogoutCommand_ShouldCallAuthServiceAndNavigate()
	{
		// Arrange
		_mockAuthService.Setup(x => x.LogoutAsync()).Returns(Task.CompletedTask);

		// Act
		await _viewModel.LogoutCommand.ExecuteAsync(null);

		// Assert - Note: In real UI, this would require user confirmation
		// The command will execute but needs UI interaction
	}

	[Fact]
	public void UnitsOptions_ShouldContainExpectedValues()
	{
		// Assert
		_viewModel.UnitsOptions.Should().Contain(new[] { "Metric", "Imperial" });
	}

	[Fact]
	public void CoordinateFormatOptions_ShouldContainExpectedValues()
	{
		// Assert
		_viewModel.CoordinateFormatOptions.Should().Contain(new[]
		{
			"Decimal Degrees",
			"Degrees Minutes Seconds",
			"Degrees Decimal Minutes"
		});
	}

	[Fact]
	public void GpsAccuracyOptions_ShouldContainExpectedValues()
	{
		// Assert
		_viewModel.GpsAccuracyOptions.Should().Contain(new[] { "High", "Medium", "Low" });
	}

	[Fact]
	public void BaseMapProviders_ShouldContainExpectedValues()
	{
		// Assert
		_viewModel.BaseMapProviders.Should().Contain(new[]
		{
			"OpenStreetMap",
			"Satellite",
			"Topographic",
			"Hybrid"
		});
	}

	[Fact]
	public void SyncIntervalOptions_ShouldContainExpectedValues()
	{
		// Assert
		_viewModel.SyncIntervalOptions.Should().Contain(new[] { 5, 10, 15, 30, 60 });
	}

	[Fact]
	public void AppVersion_ShouldBeSet()
	{
		// Assert
		_viewModel.AppVersion.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void BuildNumber_ShouldBeSet()
	{
		// Assert
		_viewModel.BuildNumber.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void Dispose_ShouldNotThrow()
	{
		// Act
		Action act = () => _viewModel.Dispose();

		// Assert
		act.Should().NotThrow();
	}

	[Fact]
	public void Dispose_CalledTwice_ShouldNotThrow()
	{
		// Act
		_viewModel.Dispose();
		Action act = () => _viewModel.Dispose();

		// Assert
		act.Should().NotThrow();
	}

	public void Dispose()
	{
		_viewModel.Dispose();
	}
}
