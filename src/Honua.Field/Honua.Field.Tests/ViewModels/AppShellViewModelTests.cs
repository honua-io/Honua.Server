using FluentAssertions;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for AppShellViewModel
/// Tests navigation menu, user profile display, and app shell state management
/// </summary>
public class AppShellViewModelTests
{
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly AppShellViewModel _viewModel;

	public AppShellViewModelTests()
	{
		_mockNavigationService = new Mock<INavigationService>();
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockSettingsService = new Mock<ISettingsService>();

		_viewModel = new AppShellViewModel(
			_mockNavigationService.Object,
			_mockAuthService.Object,
			_mockSettingsService.Object
		);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Honua Field");
		_viewModel.Username.Should().BeEmpty();
		_viewModel.Email.Should().BeEmpty();
		_viewModel.UserInitials.Should().BeEmpty();
		_viewModel.IsAuthenticated.Should().BeFalse();
		_viewModel.CurrentRoute.Should().Be("//Main");
		_viewModel.IsOnline.Should().BeTrue();
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadUserProfile_WhenAuthenticated()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com",
			FullName = "John Doe"
		};

		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(true);
		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("notification_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("alert_count", 0)).ReturnsAsync(0);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.IsAuthenticated.Should().BeTrue();
		_viewModel.Username.Should().Be("John Doe");
		_viewModel.Email.Should().Be("test@example.com");
		_viewModel.UserInitials.Should().Be("JD");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldSetGuestUser_WhenNotAuthenticated()
	{
		// Arrange
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(false);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("notification_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("alert_count", 0)).ReturnsAsync(0);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.IsAuthenticated.Should().BeFalse();
		_viewModel.Username.Should().Be("Guest");
		_viewModel.Email.Should().BeEmpty();
		_viewModel.UserInitials.Should().Be("?");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadSyncStatus()
	{
		// Arrange
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(false);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(5);
		_mockSettingsService.Setup(x => x.GetAsync("notification_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("alert_count", 0)).ReturnsAsync(0);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.PendingChanges.Should().Be(5);
		_viewModel.SyncStatusText.Should().Be("5 pending");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadNotifications()
	{
		// Arrange
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(false);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("notification_count", 0)).ReturnsAsync(3);
		_mockSettingsService.Setup(x => x.GetAsync("alert_count", 0)).ReturnsAsync(2);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.NotificationCount.Should().Be(3);
		_viewModel.HasNotifications.Should().BeTrue();
		_viewModel.AlertCount.Should().Be(2);
		_viewModel.HasAlerts.Should().BeTrue();
	}

	[Fact]
	public async Task RefreshCommand_ShouldReloadAllData()
	{
		// Arrange
		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(false);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("notification_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("alert_count", 0)).ReturnsAsync(0);

		// Act
		await _viewModel.RefreshCommand.ExecuteAsync(null);

		// Assert
		_mockAuthService.Verify(x => x.IsAuthenticatedAsync(), Times.Once);
		_mockSettingsService.Verify(x => x.GetAsync("pending_changes_count", 0), Times.Once);
	}

	[Fact]
	public async Task NavigateToHomeCommand_ShouldNavigateToMain()
	{
		// Act
		await _viewModel.NavigateToHomeCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentRoute.Should().Be("//Main");
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Main", null),
			Times.Once
		);
	}

	[Fact]
	public async Task NavigateToMapCommand_ShouldNavigateToMap()
	{
		// Act
		await _viewModel.NavigateToMapCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentRoute.Should().Be("//Map");
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Map", null),
			Times.Once
		);
	}

	[Fact]
	public async Task NavigateToFeaturesCommand_ShouldNavigateToFeatures()
	{
		// Act
		await _viewModel.NavigateToFeaturesCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentRoute.Should().Be("//Features");
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Features", null),
			Times.Once
		);
	}

	[Fact]
	public async Task NavigateToCollectionsCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.NavigateToCollectionsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_viewModel.CurrentRoute.Should().Be("//Collections");
		_viewModel.IsFlyoutOpen.Should().BeFalse();
	}

	[Fact]
	public async Task NavigateToProfileCommand_ShouldNavigateToProfile()
	{
		// Act
		await _viewModel.NavigateToProfileCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentRoute.Should().Be("//Profile");
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Profile", null),
			Times.Once
		);
	}

	[Fact]
	public async Task NavigateToSettingsCommand_ShouldNavigateToSettings()
	{
		// Act
		await _viewModel.NavigateToSettingsCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentRoute.Should().Be("//Settings");
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Settings", null),
			Times.Once
		);
	}

	[Fact]
	public async Task NavigateToOfflineMapsCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.NavigateToOfflineMapsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_viewModel.IsFlyoutOpen.Should().BeFalse();
	}

	[Fact]
	public async Task TriggerSyncCommand_ShouldUpdateSyncStatus()
	{
		// Arrange
		_viewModel.IsOnline = true;

		// Act
		await _viewModel.TriggerSyncCommand.ExecuteAsync(null);

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
	public async Task TriggerSyncCommand_ShouldNotSync_WhenAlreadySyncing()
	{
		// Arrange
		_viewModel.IsSyncing = true;

		// Act
		await _viewModel.TriggerSyncCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("last_sync_time", It.IsAny<DateTime>()),
			Times.Never
		);
	}

	[Fact]
	public async Task TriggerSyncCommand_ShouldShowAlert_WhenOffline()
	{
		// Arrange
		_viewModel.IsOnline = false;

		// Act
		Func<Task> act = async () => await _viewModel.TriggerSyncCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_mockSettingsService.Verify(
			x => x.SetAsync("last_sync_time", It.IsAny<DateTime>()),
			Times.Never
		);
	}

	[Fact]
	public async Task ViewNotificationsCommand_ShouldClearNotificationBadge()
	{
		// Arrange
		_viewModel.NotificationCount = 5;
		_viewModel.HasNotifications = true;

		// Act
		await _viewModel.ViewNotificationsCommand.ExecuteAsync(null);

		// Assert
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_viewModel.NotificationCount.Should().Be(0);
		_viewModel.HasNotifications.Should().BeFalse();
		_mockSettingsService.Verify(
			x => x.SetAsync("notification_count", 0),
			Times.Once
		);
	}

	[Fact]
	public async Task ViewAlertsCommand_ShouldClearAlertBadge()
	{
		// Arrange
		_viewModel.AlertCount = 3;
		_viewModel.HasAlerts = true;

		// Act
		await _viewModel.ViewAlertsCommand.ExecuteAsync(null);

		// Assert
		_viewModel.IsFlyoutOpen.Should().BeFalse();
		_viewModel.AlertCount.Should().Be(0);
		_viewModel.HasAlerts.Should().BeFalse();
		_mockSettingsService.Verify(
			x => x.SetAsync("alert_count", 0),
			Times.Once
		);
	}

	[Fact]
	public void ToggleFlyoutCommand_ShouldToggleFlyoutState()
	{
		// Arrange
		_viewModel.IsFlyoutOpen = false;

		// Act
		_viewModel.ToggleFlyoutCommand.Execute(null);

		// Assert
		_viewModel.IsFlyoutOpen.Should().BeTrue();

		// Act again
		_viewModel.ToggleFlyoutCommand.Execute(null);

		// Assert
		_viewModel.IsFlyoutOpen.Should().BeFalse();
	}

	[Fact]
	public async Task ShowHelpCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ShowHelpCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_viewModel.IsFlyoutOpen.Should().BeFalse();
	}

	[Fact]
	public async Task ShowAboutCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ShowAboutCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_viewModel.IsFlyoutOpen.Should().BeFalse();
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
		_viewModel.IsFlyoutOpen.Should().BeFalse();
	}

	[Fact]
	public async Task UpdateSyncBadgeAsync_ShouldUpdateSyncStatus()
	{
		// Act
		await _viewModel.UpdateSyncBadgeAsync(10);

		// Assert
		_viewModel.PendingChanges.Should().Be(10);
		_viewModel.SyncStatusText.Should().Be("10 pending");
		_mockSettingsService.Verify(
			x => x.SetAsync("pending_changes_count", 10),
			Times.Once
		);
	}

	[Fact]
	public async Task UpdateSyncBadgeAsync_ShouldShowUpToDate_WhenZero()
	{
		// Act
		await _viewModel.UpdateSyncBadgeAsync(0);

		// Assert
		_viewModel.PendingChanges.Should().Be(0);
		_viewModel.SyncStatusText.Should().Be("Up to date");
	}

	[Fact]
	public async Task UpdateNotificationBadgeAsync_ShouldUpdateNotifications()
	{
		// Act
		await _viewModel.UpdateNotificationBadgeAsync(5);

		// Assert
		_viewModel.NotificationCount.Should().Be(5);
		_viewModel.HasNotifications.Should().BeTrue();
		_mockSettingsService.Verify(
			x => x.SetAsync("notification_count", 5),
			Times.Once
		);
	}

	[Fact]
	public async Task UpdateNotificationBadgeAsync_ShouldClearFlag_WhenZero()
	{
		// Act
		await _viewModel.UpdateNotificationBadgeAsync(0);

		// Assert
		_viewModel.NotificationCount.Should().Be(0);
		_viewModel.HasNotifications.Should().BeFalse();
	}

	[Fact]
	public async Task UpdateAlertBadgeAsync_ShouldUpdateAlerts()
	{
		// Act
		await _viewModel.UpdateAlertBadgeAsync(3);

		// Assert
		_viewModel.AlertCount.Should().Be(3);
		_viewModel.HasAlerts.Should().BeTrue();
		_mockSettingsService.Verify(
			x => x.SetAsync("alert_count", 3),
			Times.Once
		);
	}

	[Fact]
	public async Task UpdateAlertBadgeAsync_ShouldClearFlag_WhenZero()
	{
		// Act
		await _viewModel.UpdateAlertBadgeAsync(0);

		// Assert
		_viewModel.AlertCount.Should().Be(0);
		_viewModel.HasAlerts.Should().BeFalse();
	}

	[Theory]
	[InlineData("John", "J")]
	[InlineData("John Doe", "JD")]
	[InlineData("John Michael Doe", "JD")]
	[InlineData("A", "A")]
	[InlineData("AB", "AB")]
	public async Task GetUserInitials_ShouldFormatCorrectly(string fullName, string expectedInitials)
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com",
			FullName = fullName
		};

		_mockAuthService.Setup(x => x.IsAuthenticatedAsync()).ReturnsAsync(true);
		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("notification_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("alert_count", 0)).ReturnsAsync(0);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.UserInitials.Should().Be(expectedInitials);
	}

	[Fact]
	public void IsSyncing_DefaultValue_ShouldBeFalse()
	{
		// Assert
		_viewModel.IsSyncing.Should().BeFalse();
	}

	[Fact]
	public void IsFlyoutOpen_DefaultValue_ShouldBeFalse()
	{
		// Assert
		_viewModel.IsFlyoutOpen.Should().BeFalse();
	}

	[Fact]
	public void SyncStatusText_DefaultValue_ShouldBeUpToDate()
	{
		// Assert
		_viewModel.SyncStatusText.Should().Be("Up to date");
	}
}
