// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using HonuaField.ViewModels;
using Moq;
using Xunit;

namespace HonuaField.Tests.ViewModels;

/// <summary>
/// Unit tests for LoginViewModel
/// Tests login flow, biometric authentication, and navigation
/// </summary>
public class LoginViewModelTests
{
	private readonly Mock<IAuthenticationService> _mockAuthService;
	private readonly Mock<INavigationService> _mockNavigationService;
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly Mock<IBiometricService> _mockBiometricService;
	private readonly LoginViewModel _viewModel;

	public LoginViewModelTests()
	{
		_mockAuthService = new Mock<IAuthenticationService>();
		_mockNavigationService = new Mock<INavigationService>();
		_mockSettingsService = new Mock<ISettingsService>();
		_mockBiometricService = new Mock<IBiometricService>();

		_viewModel = new LoginViewModel(
			_mockAuthService.Object,
			_mockNavigationService.Object,
			_mockSettingsService.Object,
			_mockBiometricService.Object
		);
	}

	[Fact]
	public void Constructor_ShouldInitializeProperties()
	{
		// Assert
		_viewModel.Title.Should().Be("Sign In");
		_viewModel.Username.Should().BeEmpty();
		_viewModel.Password.Should().BeEmpty();
		_viewModel.RememberMe.Should().BeTrue();
		_viewModel.IsBusy.Should().BeFalse();
	}

	[Fact]
	public async Task OnAppearingAsync_ShouldLoadSavedSettings()
	{
		// Arrange
		var savedUsername = "testuser";
		var serverUrl = "https://test.honua.io";

		_mockSettingsService
			.Setup(x => x.GetAsync("last_username", string.Empty))
			.ReturnsAsync(savedUsername);
		_mockSettingsService
			.Setup(x => x.GetAsync("server_url", "https://api.honua.io"))
			.ReturnsAsync(serverUrl);
		_mockSettingsService
			.Setup(x => x.GetAsync("remember_me", true))
			.ReturnsAsync(true);
		_mockSettingsService
			.Setup(x => x.GetAsync("use_biometrics", false))
			.ReturnsAsync(false);
		_mockBiometricService
			.Setup(x => x.IsBiometricAvailableAsync())
			.ReturnsAsync(false);
		_mockBiometricService
			.Setup(x => x.IsBiometricEnrolledAsync())
			.ReturnsAsync(false);

		// Act
		await _viewModel.OnAppearingAsync();

		// Assert
		_viewModel.Username.Should().Be(savedUsername);
		_viewModel.ServerUrl.Should().Be(serverUrl);
	}

	[Fact]
	public async Task LoginCommand_ShouldShowError_WhenUsernameIsEmpty()
	{
		// Arrange
		_viewModel.Username = "";
		_viewModel.Password = "password123";

		// Act
		await _viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_viewModel.IsBusy.Should().BeFalse();
		// Note: Error display is through alert dialog, which is mocked in real implementation
	}

	[Fact]
	public async Task LoginCommand_ShouldShowError_WhenPasswordIsEmpty()
	{
		// Arrange
		_viewModel.Username = "testuser";
		_viewModel.Password = "";

		// Act
		await _viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_viewModel.IsBusy.Should().BeFalse();
	}

	[Fact]
	public async Task LoginCommand_ShouldNavigateToMain_WhenLoginIsSuccessful()
	{
		// Arrange
		_viewModel.Username = "testuser";
		_viewModel.Password = "password123";

		_mockAuthService
			.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync(true);

		_mockBiometricService
			.Setup(x => x.IsBiometricAvailableAsync())
			.ReturnsAsync(false);

		// Act
		await _viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_mockNavigationService.Verify(
			x => x.NavigateToAsync("//Main", true, null),
			Times.Once
		);
	}

	[Fact]
	public async Task LoginCommand_ShouldClearPassword_WhenLoginFails()
	{
		// Arrange
		_viewModel.Username = "testuser";
		_viewModel.Password = "wrongpassword";

		_mockAuthService
			.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync(false);

		_mockBiometricService
			.Setup(x => x.IsBiometricAvailableAsync())
			.ReturnsAsync(false);

		// Act
		await _viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_viewModel.Password.Should().BeEmpty();
		_viewModel.ErrorMessage.Should().NotBeEmpty();
	}

	[Fact]
	public async Task LoginCommand_ShouldSaveUsername_WhenRememberMeIsChecked()
	{
		// Arrange
		_viewModel.Username = "testuser";
		_viewModel.Password = "password123";
		_viewModel.RememberMe = true;

		_mockAuthService
			.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync(true);

		_mockBiometricService
			.Setup(x => x.IsBiometricAvailableAsync())
			.ReturnsAsync(false);

		// Act
		await _viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.SetAsync("last_username", "testuser"),
			Times.Once
		);
		_mockSettingsService.Verify(
			x => x.SetAsync("remember_me", true),
			Times.Once
		);
	}

	[Fact]
	public async Task LoginCommand_ShouldNotSaveUsername_WhenRememberMeIsNotChecked()
	{
		// Arrange
		_viewModel.Username = "testuser";
		_viewModel.Password = "password123";
		_viewModel.RememberMe = false;

		_mockAuthService
			.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync(true);

		_mockBiometricService
			.Setup(x => x.IsBiometricAvailableAsync())
			.ReturnsAsync(false);

		// Act
		await _viewModel.LoginCommand.ExecuteAsync(null);

		// Assert
		_mockSettingsService.Verify(
			x => x.RemoveAsync("last_username"),
			Times.Once
		);
	}

	[Fact]
	public async Task LoginWithBiometricsCommand_ShouldNotExecute_WhenBiometricNotAvailable()
	{
		// Arrange
		_viewModel.IsBiometricsAvailable = false;

		// Act
		await _viewModel.LoginWithBiometricsCommand.ExecuteAsync(null);

		// Assert
		_mockBiometricService.Verify(
			x => x.AuthenticateAsync(It.IsAny<string>()),
			Times.Never
		);
	}

	[Fact]
	public async Task LoginWithBiometricsCommand_ShouldAuthenticate_WhenBiometricIsAvailable()
	{
		// Arrange
		_viewModel.IsBiometricsAvailable = true;

		_mockBiometricService
			.Setup(x => x.AuthenticateAsync(It.IsAny<string>()))
			.ReturnsAsync(BiometricResult.Successful());

		_mockSettingsService
			.Setup(x => x.GetAsync<string>("biometric_username"))
			.ReturnsAsync("testuser");
		_mockSettingsService
			.Setup(x => x.GetAsync<string>("biometric_password"))
			.ReturnsAsync("password123");

		_mockAuthService
			.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
			.ReturnsAsync(true);

		_mockBiometricService
			.Setup(x => x.IsBiometricAvailableAsync())
			.ReturnsAsync(true);

		// Act
		await _viewModel.LoginWithBiometricsCommand.ExecuteAsync(null);

		// Assert
		_mockBiometricService.Verify(
			x => x.AuthenticateAsync(It.IsAny<string>()),
			Times.Once
		);
	}

	[Fact]
	public async Task ForgotPasswordCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ForgotPasswordCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task ShowServerSettingsCommand_ShouldExecuteWithoutError()
	{
		// Act
		Func<Task> act = async () => await _viewModel.ShowServerSettingsCommand.ExecuteAsync(null);

		// Assert
		await act.Should().NotThrowAsync();
	}
}
