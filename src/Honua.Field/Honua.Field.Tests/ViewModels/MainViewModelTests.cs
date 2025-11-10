using FluentAssertions;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for MainViewModel
/// Tests dashboard functionality, statistics, and quick actions
/// </summary>
public class MainViewModelTests
{
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly MainViewModel _viewModel;

	public MainViewModelTests()
	{
		_mockNavigationService = new Mock<INavigationService>();
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockSettingsService = new Mock<ISettingsService>();

		_viewModel = new MainViewModel(
			_mockNavigationService.Object,
			_mockAuthService.Object,
			_mockSettingsService.Object
		);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Home");
		_viewModel.WelcomeMessage.Should().Be("Welcome");
		_viewModel.LastSyncTime.Should().Be("Never");
		_viewModel.SyncStatus.Should().Be("Up to date");
		_viewModel.IsOnline.Should().BeTrue();
		_viewModel.ConnectionStatus.Should().Be("Online");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadDashboardData()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com",
			FullName = "Test User"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync("total_features_count", 0)).ReturnsAsync(10);
		_mockSettingsService.Setup(x => x.GetAsync("total_collections_count", 0)).ReturnsAsync(3);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(5);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(2);
		_mockSettingsService.Setup(x => x.GetAsync("storage_used", "0 MB")).ReturnsAsync("15 MB");
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.Username.Should().Be("Test User");
		_viewModel.TotalFeatures.Should().Be(10);
		_viewModel.TotalCollections.Should().Be(3);
		_viewModel.PendingChanges.Should().Be(5);
		_viewModel.OfflineMapsCount.Should().Be(2);
		_viewModel.StorageUsed.Should().Be("15 MB");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldSetWelcomeMessage_BasedOnTimeOfDay()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com",
			FullName = "John Doe"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync("total_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("total_collections_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("storage_used", "0 MB")).ReturnsAsync("0 MB");
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.WelcomeMessage.Should().Contain("John");
		_viewModel.WelcomeMessage.Should().MatchRegex("Good (morning|afternoon|evening), John");
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
		_mockSettingsService.Setup(x => x.GetAsync("total_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("total_collections_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("storage_used", "0 MB")).ReturnsAsync("0 MB");
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync(lastSync);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.LastSyncTime.Should().Be("30m ago");
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
		_mockSettingsService.Setup(x => x.GetAsync("total_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("total_collections_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(7);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("storage_used", "0 MB")).ReturnsAsync("0 MB");
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.PendingChanges.Should().Be(7);
		_viewModel.SyncStatus.Should().Be("7 pending");
	}

	[Fact]
	public async Task RefreshCommand_ShouldReloadDashboardData()
	{
		// Arrange
		var user = new UserInfo
		{
			Id = "1",
			Username = "testuser",
			Email = "test@example.com"
		};

		_mockAuthService.Setup(x => x.GetCurrentUserAsync()).ReturnsAsync(user);
		_mockSettingsService.Setup(x => x.GetAsync("total_features_count", 0)).ReturnsAsync(5);
		_mockSettingsService.Setup(x => x.GetAsync("total_collections_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("storage_used", "0 MB")).ReturnsAsync("0 MB");
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);

		// Act
		await _viewModel.RefreshCommand.ExecuteAsync(null);

		// Assert
		_mockAuthService.Verify(x => x.GetCurrentUserAsync(), Times.Once);
		_viewModel.TotalFeatures.Should().Be(5);
	}

	[Fact]
	public async Task ViewMapCommand_ShouldNavigateToMap()
	{
		// Act
		await _viewModel.ViewMapCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Map", null),
			Times.Once
		);
	}

	[Fact]
	public async Task ViewFeaturesCommand_ShouldNavigateToFeatures()
	{
		// Act
		await _viewModel.ViewFeaturesCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Features", null),
			Times.Once
		);
	}

	[Fact]
	public async Task CreateFeatureCommand_ShouldNavigateToNewFeature()
	{
		// Act
		await _viewModel.CreateFeatureCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Features/New", null),
			Times.Once
		);
	}

	[Fact]
	public async Task SyncNowCommand_ShouldUpdateSyncStatus()
	{
		// Arrange
		_mockSettingsService.Setup(x => x.GetAsync<DateTime?>("last_sync_time", null)).ReturnsAsync((DateTime?)null);
		_mockSettingsService.Setup(x => x.GetAsync("pending_changes_count", 0)).ReturnsAsync(3);
		_mockSettingsService.Setup(x => x.GetAsync("total_features_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("total_collections_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("offline_maps_count", 0)).ReturnsAsync(0);
		_mockSettingsService.Setup(x => x.GetAsync("storage_used", "0 MB")).ReturnsAsync("0 MB");

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
	public async Task SyncNowCommand_ShouldShowAlert_WhenOffline()
	{
		// Arrange
		_viewModel.IsOnline = false;

		// Act
		Func<Task> act = async () => await _viewModel.SyncNowCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_mockSettingsService.Verify(
			x => x.SetAsync("last_sync_time", It.IsAny<DateTime>()),
			Times.Never
		);
	}

	[Fact]
	public async Task OpenSettingsCommand_ShouldNavigateToSettings()
	{
		// Act
		await _viewModel.OpenSettingsCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Settings", null),
			Times.Once
		);
	}

	[Fact]
	public async Task OpenProfileCommand_ShouldNavigateToProfile()
	{
		// Act
		await _viewModel.OpenProfileCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Profile", null),
			Times.Once
		);
	}

	[Fact]
	public async Task ViewCollectionsCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ViewCollectionsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ManageOfflineMapsCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ManageOfflineMapsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ViewActivityCommand_ShouldExecuteWithoutError_WhenActivityProvided()
	{
		// Arrange
		var activity = new RecentActivityItem
		{
			Title = "Test Activity",
			Description = "Test Description",
			Timestamp = DateTime.Now
		};

		// Act
		Func<Task> act = async () => await _viewModel.ViewActivityCommand.ExecuteAsync(activity);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ViewActivityCommand_ShouldNotThrow_WhenActivityIsNull()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ViewActivityCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public void RecentActivities_ShouldBeInitialized()
	{
		// Assert
		_viewModel.RecentActivities.Should().NotBeNull();
		_viewModel.RecentActivities.Should().BeEmpty();
	}

	[Fact]
	public void ConnectionStatus_ShouldBeOnline_Initially()
	{
		// Assert
		_viewModel.ConnectionStatus.Should().Be("Online");
		_viewModel.IsOnline.Should().BeTrue();
	}

	[Fact]
	public void IsSyncing_ShouldBeFalse_Initially()
	{
		// Assert
		_viewModel.IsSyncing.Should().BeFalse();
	}
}

/// <summary>
/// Tests for RecentActivityItem class
/// </summary>
public class RecentActivityItemTests
{
	[Fact]
	public void RecentActivityItem_ShouldInitializeProperties()
	{
		// Arrange & Act
		var activity = new RecentActivityItem
		{
			Title = "Test Title",
			Description = "Test Description",
			Timestamp = DateTime.Now.AddMinutes(-30),
			Icon = "test_icon"
		};

		// Assert
		activity.Title.Should().Be("Test Title");
		activity.Description.Should().Be("Test Description");
		activity.Icon.Should().Be("test_icon");
		activity.Timestamp.Should().BeCloseTo(DateTime.Now.AddMinutes(-30), TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void TimestampDisplay_ShouldFormatRecent_AsJustNow()
	{
		// Arrange
		var activity = new RecentActivityItem
		{
			Timestamp = DateTime.Now
		};

		// Act
		var display = activity.TimestampDisplay;

		// Assert
		display.Should().Be("Just now");
	}

	[Fact]
	public void TimestampDisplay_ShouldFormatMinutes_Correctly()
	{
		// Arrange
		var activity = new RecentActivityItem
		{
			Timestamp = DateTime.Now.AddMinutes(-30)
		};

		// Act
		var display = activity.TimestampDisplay;

		// Assert
		display.Should().Be("30m ago");
	}

	[Fact]
	public void TimestampDisplay_ShouldFormatHours_Correctly()
	{
		// Arrange
		var activity = new RecentActivityItem
		{
			Timestamp = DateTime.Now.AddHours(-3)
		};

		// Act
		var display = activity.TimestampDisplay;

		// Assert
		display.Should().Be("3h ago");
	}

	[Fact]
	public void TimestampDisplay_ShouldFormatDays_Correctly()
	{
		// Arrange
		var activity = new RecentActivityItem
		{
			Timestamp = DateTime.Now.AddDays(-2)
		};

		// Act
		var display = activity.TimestampDisplay;

		// Assert
		display.Should().Be("2d ago");
	}

	[Fact]
	public void TimestampDisplay_ShouldFormatOldDates_AsMonthDay()
	{
		// Arrange
		var date = new DateTime(2024, 1, 15);
		var activity = new RecentActivityItem
		{
			Timestamp = date
		};

		// Act
		var display = activity.TimestampDisplay;

		// Assert
		display.Should().Be("Jan 15");
	}

	[Fact]
	public void DefaultIcon_ShouldBeInfo()
	{
		// Arrange & Act
		var activity = new RecentActivityItem();

		// Assert
		activity.Icon.Should().Be("info");
	}

	[Fact]
	public void DefaultTitle_ShouldBeEmpty()
	{
		// Arrange & Act
		var activity = new RecentActivityItem();

		// Assert
		activity.Title.Should().BeEmpty();
		activity.Description.Should().BeEmpty();
	}
}
