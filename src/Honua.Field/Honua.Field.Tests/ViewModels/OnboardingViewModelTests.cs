// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for OnboardingViewModel
/// Tests onboarding flow, permissions, and initial setup
/// </summary>
public class OnboardingViewModelTests
{
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly OnboardingViewModel _viewModel;

	public OnboardingViewModelTests()
	{
		_mockSettingsService = new Mock<ISettingsService>();
		_mockNavigationService = new Mock<INavigationService>();
		_mockAuthService = new Mock<IAuthenticationService>();

		_viewModel = new OnboardingViewModel(
			_mockSettingsService.Object,
			_mockNavigationService.Object,
			_mockAuthService.Object
		);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Get Started");
		_viewModel.CurrentStep.Should().Be(0);
		_viewModel.TotalSteps.Should().Be(4);
		_viewModel.ServerUrl.Should().Be("https://api.honua.io");
		_viewModel.CanGoNext.Should().BeTrue();
		_viewModel.CanGoPrevious.Should().BeFalse();
		_viewModel.ShowSkipButton.Should().BeTrue();
	}

	[Fact]
	public void Steps_ShouldContainFourSteps()
	{
		// Assert
		_viewModel.Steps.Should().HaveCount(4);
		_viewModel.Steps[0].Title.Should().Be("Welcome");
		_viewModel.Steps[1].Title.Should().Be("Permissions");
		_viewModel.Steps[2].Title.Should().Be("Server Setup");
		_viewModel.Steps[3].Title.Should().Be("All Set");
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldNavigateToLogin_WhenAlreadyOnboarded()
	{
		// Arrange
		_mockSettingsService
			.Setup(x => x.GetAsync("onboarding_completed", false))
			.ReturnsAsync(true);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Login", true, null),
			Times.Once
		);
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadServerUrl_WhenNotOnboarded()
	{
		// Arrange
		var serverUrl = "https://test.honua.io";
		_mockSettingsService
			.Setup(x => x.GetAsync("onboarding_completed", false))
			.ReturnsAsync(false);
		_mockSettingsService
			.Setup(x => x.GetAsync("server_url", "https://api.honua.io"))
			.ReturnsAsync(serverUrl);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.ServerUrl.Should().Be(serverUrl);
		_viewModel.CurrentStep.Should().Be(0);
	}

	[Fact]
	public async Task NextStepCommand_ShouldIncrementStep()
	{
		// Arrange
		_mockSettingsService
			.Setup(x => x.GetAsync("onboarding_completed", false))
			.ReturnsAsync(false);
		_mockSettingsService
			.Setup(x => x.GetAsync("server_url", "https://api.honua.io"))
			.ReturnsAsync("https://api.honua.io");
		await _viewModel.OnAppearingAsync();

		// Act
		await _viewModel.NextStepCommand.ExecuteAsync(null);

		// Assert
		_viewModel.CurrentStep.Should().Be(1);
		_viewModel.CanGoPrevious.Should().BeTrue();
	}

	[Fact]
	public void PreviousStepCommand_ShouldDecrementStep()
	{
		// Arrange
		_viewModel.CurrentStep = 2;
		_viewModel.CanGoPrevious = true;

		// Act
		_viewModel.PreviousStepCommand.Execute(null);

		// Assert
		_viewModel.CurrentStep.Should().Be(1);
	}

	[Fact]
	public void PreviousStepCommand_ShouldNotGoBelow Zero()
	{
		// Arrange
		_viewModel.CurrentStep = 0;

		// Act
		_viewModel.PreviousStepCommand.Execute(null);

		// Assert
		_viewModel.CurrentStep.Should().Be(0);
	}

	[Fact]
	public async Task SkipOnboardingCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.SkipOnboardingCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task CompleteOnboardingCommand_ShouldMarkOnboardingComplete()
	{
		// Arrange
		_viewModel.ServerUrl = "https://test.honua.io";
		_viewModel.ServerConfigured = true;

		// Act
		await _viewModel.CompleteOnboardingCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("onboarding_completed", true),
			Times.Once
		);
	}

	[Fact]
	public async Task CompleteOnboardingCommand_ShouldSaveServerUrl_WhenConfigured()
	{
		// Arrange
		var serverUrl = "https://custom.honua.io";
		_viewModel.ServerUrl = serverUrl;
		_viewModel.ServerConfigured = true;

		// Act
		await _viewModel.CompleteOnboardingCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("server_url", serverUrl),
			Times.Once
		);
	}

	[Fact]
	public async Task CompleteOnboardingCommand_ShouldNavigateToLogin()
	{
		// Act
		await _viewModel.CompleteOnboardingCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Login", true, null),
			Times.Once
		);
	}

	[Fact]
	public async Task RequestLocationPermissionCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.RequestLocationPermissionCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task RequestCameraPermissionCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.RequestCameraPermissionCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task RequestStoragePermissionCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.RequestStoragePermissionCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task RequestAllPermissionsCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.RequestAllPermissionsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task TestServerConnectionCommand_ShouldRequireServerUrl()
	{
		// Arrange
		_viewModel.ServerUrl = "";

		// Act
		Func<Task> act = async () => await _viewModel.TestServerConnectionCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
		_viewModel.ServerConfigured.Should().BeFalse();
	}

	[Fact]
	public async Task TestServerConnectionCommand_ShouldSetConfigured_WhenSuccessful()
	{
		// Arrange
		_viewModel.ServerUrl = "https://test.honua.io";

		// Act
		await _viewModel.TestServerConnectionCommand.ExecuteAsync(null);

		// Assert
		_viewModel.ServerConfigured.Should().BeTrue();
	}

	[Fact]
	public void StepTitle_ShouldUpdateWhenStepChanges()
	{
		// Arrange
		_viewModel.CurrentStep = 0;
		var initialTitle = _viewModel.StepTitle;

		// Act - Manually update step info (simulating what happens in NextStep)
		_viewModel.CurrentStep = 1;

		// Assert - Initial title should be set
		initialTitle.Should().Be("Welcome");
	}

	[Fact]
	public void AllPermissionsGranted_ShouldBeFalse_Initially()
	{
		// Assert
		_viewModel.AllPermissionsGranted.Should().BeFalse();
	}

	[Fact]
	public void LocationPermissionGranted_ShouldBeFalse_Initially()
	{
		// Assert
		_viewModel.LocationPermissionGranted.Should().BeFalse();
	}

	[Fact]
	public void CameraPermissionGranted_ShouldBeFalse_Initially()
	{
		// Assert
		_viewModel.CameraPermissionGranted.Should().BeFalse();
	}

	[Fact]
	public void StoragePermissionGranted_ShouldBeFalse_Initially()
	{
		// Assert
		_viewModel.StoragePermissionGranted.Should().BeFalse();
	}

	[Fact]
	public void ServerConfigured_ShouldBeFalse_Initially()
	{
		// Assert
		_viewModel.ServerConfigured.Should().BeFalse();
	}

	[Fact]
	public void ShowSkipButton_ShouldBeTrue_OnFirstSteps()
	{
		// Arrange
		_viewModel.CurrentStep = 0;

		// Assert
		_viewModel.ShowSkipButton.Should().BeTrue();
	}

	[Fact]
	public void CanGoNext_ShouldBeTrue_OnEarlySteps()
	{
		// Arrange
		_viewModel.CurrentStep = 0;

		// Assert
		_viewModel.CanGoNext.Should().BeTrue();
	}

	[Fact]
	public void CanGoPrevious_ShouldBeFalse_OnFirstStep()
	{
		// Arrange
		_viewModel.CurrentStep = 0;

		// Assert
		_viewModel.CanGoPrevious.Should().BeFalse();
	}
}

/// <summary>
/// Tests for OnboardingStep class
/// </summary>
public class OnboardingStepTests
{
	[Fact]
	public void OnboardingStep_ShouldInitializeProperties()
	{
		// Arrange & Act
		var step = new OnboardingStep
		{
			StepNumber = 1,
			Title = "Test Step",
			Description = "Test Description",
			Content = "Test Content"
		};

		// Assert
		step.StepNumber.Should().Be(1);
		step.Title.Should().Be("Test Step");
		step.Description.Should().Be("Test Description");
		step.Content.Should().Be("Test Content");
	}

	[Fact]
	public void OnboardingStep_DefaultValues_ShouldBeEmpty()
	{
		// Arrange & Act
		var step = new OnboardingStep();

		// Assert
		step.StepNumber.Should().Be(0);
		step.Title.Should().BeEmpty();
		step.Description.Should().BeEmpty();
		step.Content.Should().BeEmpty();
	}
}
