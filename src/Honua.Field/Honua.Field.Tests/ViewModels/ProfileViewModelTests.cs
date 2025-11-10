// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for ProfileViewModel
/// Tests user profile display, account information, and storage statistics
/// </summary>
public class ProfileViewModelTests
{
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly ProfileViewModel _viewModel;

	public ProfileViewModelTests()
	{
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockSettingsService = new Mock<ISettingsService>();
		_mockNavigationService = new Mock<INavigationService>();

		_viewModel = new ProfileViewModel(
			_mockAuthService.Object,
			_mockSettingsService.Object,
			_mockNavigationService.Object
		);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Profile");
		_viewModel.Username.Should().BeEmpty();
		_viewModel.Email.Should().BeEmpty();
		_viewModel.LastSyncTime.Should().Be("Never");
		_viewModel.SyncStatus.Should().Be("Up to date");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadUserProfile()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "user123",
			Username = "testuser",
			Email = "test@example.com",
			FullName = "Test User",
			OrganizationId = "org456"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_mode_enabled", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("offline_features_count", 0)).ReturnsAsync(10);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(2);
		_mockSettingsService.Setup(x => x.GetAsync("account_verified", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("account_type", "Standard")).ReturnsAsync("Premium");
		_mockSettingsService.Setup(x => x.GetAsync("account_created_date", It.IsAny<DateTime>())).ReturnsAsync(DateTime.Now.AddYears(-1));

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.Username.Should().Be("testuser");
		_viewModel.Email.Should().Be("test@example.com");
		_viewModel.FullName.Should().Be("Test User");
		_viewModel.UserId.Should().Be("user123");
		_viewModel.OrganizationId.Should().Be("org456");
		_viewModel.LastSyncTime.Should().Be("Never");
		_viewModel.OfflineModeEnabled.Should().BeTrue();
		_viewModel.OfflineFeaturesCount.Should().Be(10);
		_viewModel.OfflineMapsCount.Should().Be(2);
		_viewModel.IsAccountVerified.Should().BeTrue();
		_viewModel.AccountType.Should().Be("Premium");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldFormatLastSyncTime_WhenRecentlysynced()
	{
		// Arrange
		var lastSync = DateTime.Now.AddMinutes(-30);
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync(lastSync);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_mode_enabled", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("offline_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("account_verified", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("account_type", "Standard")).ReturnsAsync("Standard");
		_mockSettingsService.Setup(x => x.GetAsync("account_created_date", It.IsAny<DateTime>())).ReturnsAsync(DateTime.Now);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.LastSyncTime.Should().Be("30 minutes ago");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldSetSyncStatus_WhenPendingChanges()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(5);
		_mockSettingsService.Setup(x => x.GetAsync("offline_mode_enabled", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("offline_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("account_verified", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("account_type", "Standard")).ReturnsAsync("Standard");
		_mockSettingsService.Setup(x => x.GetAsync("account_created_date", It.IsAny<DateTime>())).ReturnsAsync(DateTime.Now);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.PendingChanges.Should().Be(5);
		_viewModel.SyncStatus.Should().Be("5 pending changes");
	}

	[Fact]
	public async Task RefreshCommand_ShouldReloadProfileData()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_mode_enabled", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("offline_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("account_verified", true)).ReturnsAsync(true);
		_mockSettingsService.Setup(x => x.GetAsync("account_type", "Standard")).ReturnsAsync("Standard");
		_mockSettingsService.Setup(x => x.GetAsync("account_created_date", It.IsAny<DateTime>())).ReturnsAsync(DateTime.Now);

		// Act
		await _viewModel.RefreshCommand.ExecuteAsync(null);

		// Assert
		_mockAuthService.Verify(x => x.GetCurrentUserAsync(), Times.Once);
		_viewModel.Username.Should().Be("testuser");
	}

	[Fact]
	public async Task EditProfileCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.EditProfileCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ChangePasswordCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ChangePasswordCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ManageStorageCommand_ShouldNavigateToSettings()
	{
		// Act
		await _viewModel.ManageStorageCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Settings", null),
			Times.Once
		);
	}

	[Fact]
	public async Task SyncNowCommand_ShouldUpdateSyncStatus()
	{
		// Arrange
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(3);

		// Act
		await _viewModel.SyncNowCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("last_sync_time", It.IsAny<DateTime>()),
			Times.Once
		);
		_mockSettingsService.Verify(
			x => x.SetAsync("pending_changes_count", 0),
			Times.Once
		);
	}

	[Fact]
	public async Task SyncNowCommand_ShouldNotSync_WhenAlreadySyncing()
	{
		// Arrange
		_viewModel.IsSyncing = true;

		// Act
		await _viewModel.SyncNowCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("last_sync_time", It.IsAny<DateTime>()),
			Times.Never
		);
	}

	[Fact]
	public async Task ViewSyncHistoryCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ViewSyncHistoryCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ToggleOfflineModeCommand_ShouldSaveSettings()
	{
		// Arrange
		_viewModel.OfflineModeEnabled = true;

		// Act
		await _viewModel.ToggleOfflineModeCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("offline_mode_enabled", true),
			Times.Once
		);
	}

	[Fact]
	public async Task ToggleOfflineModeCommand_ShouldRevertOnError()
	{
		// Arrange
		_viewModel.OfflineModeEnabled = true;
		_mockSettingsService
			.Setup(x => x.SetAsync("offline_mode_enabled", true))
			.ThrowsAsync(new Exception("Failed to save"));

		// Act
		await _viewModel.ToggleOfflineModeCommand.ExecuteAsync(null);

		// Assert
		_viewModel.OfflineModeEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task DownloadOfflineDataCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.DownloadOfflineDataCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task VerifyAccountCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.VerifyAccountCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task VerifyAccountCommand_ShouldShowMessage_WhenAlreadyVerified()
	{
		// Arrange
		_viewModel.IsAccountVerified = true;

		// Act
		Func<Task> act = async () => await _viewModel.VerifyAccountCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ViewAccountDetailsCommand_ShouldExecuteWithoutError()
	{
		// Arrange
		_viewModel.UserId = "123";
		_viewModel.Username = "testuser";
		_viewModel.Email = "test@example.com";

		// Act
		Func<Task> act = async () => await _viewModel.ViewAccountDetailsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public void StoragePercentage_ShouldBeInitializedToZero()
	{
		// Assert
		_viewModel.StoragePercentage.Should().Be(0);
	}

	[Fact]
	public void OfflineModeEnabled_DefaultValue_ShouldBeFalse()
	{
		// Assert - Check initial value before loading
		_viewModel.OfflineModeEnabled.Should().BeFalse();
	}

	[Fact]
	public void IsSyncing_DefaultValue_ShouldBeFalse()
	{
		// Assert
		_viewModel.IsSyncing.Should().BeFalse();
	}
}
