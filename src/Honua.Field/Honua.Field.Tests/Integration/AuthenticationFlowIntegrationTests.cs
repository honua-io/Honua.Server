// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using HonuaField.Services;
using HonuaField.Tests.Integration.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Net;
using Xunit;

namespace HonuaField.Tests.Integration;

/// <summary>
/// Integration tests for authentication workflows
/// Tests real AuthenticationService with mocked OAuth server and biometric service
/// </summary>
public class AuthenticationFlowIntegrationTests : IntegrationTestBase
{
	private MockHttpMessageHandler _mockHttpHandler = null!;
	private Mock<IBiometricService> _mockBiometricService = null!;
	private IAuthenticationService _authService = null!;
	private ISettingsService _settingsService = null!;

	protected override void ConfigureServices(IServiceCollection services)
	{
		base.ConfigureServices(services);

		// Create mock HTTP handler for OAuth server
		_mockHttpHandler = new MockHttpMessageHandler();
		var httpClient = _mockHttpHandler.CreateClient("https://auth.honua.io/");

		// Create mock biometric service
		_mockBiometricService = new Mock<IBiometricService>();

		// Create real settings service
		_settingsService = new SettingsService();

		// Create API client with mock HTTP
		var apiClient = new ApiClient(httpClient, null!);

		// Register services
		services.AddSingleton<IApiClient>(apiClient);
		services.AddSingleton<ISettingsService>(_settingsService);
		services.AddSingleton(_mockBiometricService.Object);
		services.AddSingleton<IAuthenticationService>(sp =>
			new AuthenticationService(
				sp.GetRequiredService<IApiClient>(),
				sp.GetRequiredService<ISettingsService>()));
	}

	protected override async Task OnInitializeAsync()
	{
		_authService = ServiceProvider.GetRequiredService<IAuthenticationService>();
		await base.OnInitializeAsync();
	}

	[Fact]
	public async Task LoginWithOAuth_ValidCredentials_ShouldStoreTokens()
	{
		// Arrange
		var username = "testuser";
		var password = "TestPass@123";

		// Mock OAuth authorization response
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new
		{
			code = "auth_code_123",
			state = "test_state"
		});

		// Mock OAuth token response
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "access_token_abc123",
			refresh_token = "refresh_token_xyz789",
			expires_in = 3600,
			token_type = "Bearer"
		});

		// Act
		var success = await _authService.LoginAsync(username, password);

		// Assert
		success.Should().BeTrue();

		var isAuthenticated = await _authService.IsAuthenticatedAsync();
		isAuthenticated.Should().BeTrue();

		var accessToken = await _authService.GetAccessTokenAsync();
		accessToken.Should().Be("access_token_abc123");
	}

	[Fact]
	public async Task LoginWithOAuth_InvalidCredentials_ShouldFail()
	{
		// Arrange
		var username = "testuser";
		var password = "wrongpassword";

		// Mock OAuth authorization failure
		_mockHttpHandler.ConfigureResponse("/oauth/authorize", HttpStatusCode.Unauthorized);

		// Act
		var success = await _authService.LoginAsync(username, password);

		// Assert
		success.Should().BeFalse();

		var isAuthenticated = await _authService.IsAuthenticatedAsync();
		isAuthenticated.Should().BeFalse();
	}

	[Fact]
	public async Task StoreAndRetrieveAccessToken_ShouldPersist()
	{
		// Arrange
		var testToken = "test_access_token_123";

		// Act - Store token
		await _settingsService.SetAsync("access_token", testToken);

		// Retrieve token
		var retrievedToken = await _settingsService.GetAsync<string>("access_token");

		// Assert
		retrievedToken.Should().Be(testToken);
	}

	[Fact]
	public async Task RefreshExpiredToken_ShouldGetNewToken()
	{
		// Arrange
		// First login to get tokens
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new { code = "auth_code" });
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "initial_token",
			refresh_token = "refresh_token",
			expires_in = 3600
		});

		await _authService.LoginAsync("testuser", "password");

		// Mock refresh token response
		_mockHttpHandler.ClearResponses();
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "new_refreshed_token",
			refresh_token = "new_refresh_token",
			expires_in = 3600
		});

		// Act
		var refreshed = await _authService.RefreshTokenAsync();

		// Assert
		refreshed.Should().BeTrue();

		var newToken = await _authService.GetAccessTokenAsync();
		newToken.Should().Be("new_refreshed_token");
	}

	[Fact]
	public async Task EnableBiometricAuth_ShouldStoreBiometricFlag()
	{
		// Arrange
		_mockBiometricService.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
		_mockBiometricService.Setup(x => x.AuthenticateAsync(It.IsAny<string>()))
			.ReturnsAsync(new BiometricAuthResult { Success = true });

		// Act
		var enabled = await _authService.EnableBiometricAuthAsync();

		// Assert
		enabled.Should().BeTrue();

		var isBiometricEnabled = await _settingsService.GetAsync<bool>("biometric_enabled");
		isBiometricEnabled.Should().BeTrue();
	}

	[Fact]
	public async Task BiometricAuth_WhenNotAvailable_ShouldFail()
	{
		// Arrange
		_mockBiometricService.Setup(x => x.IsAvailableAsync()).ReturnsAsync(false);

		// Act
		var enabled = await _authService.EnableBiometricAuthAsync();

		// Assert
		enabled.Should().BeFalse();
	}

	[Fact]
	public async Task AuthenticateWithBiometric_ShouldSucceed()
	{
		// Arrange
		// First enable biometric
		_mockBiometricService.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
		_mockBiometricService.Setup(x => x.AuthenticateAsync(It.IsAny<string>()))
			.ReturnsAsync(new BiometricAuthResult { Success = true });

		await _authService.EnableBiometricAuthAsync();

		// Store credentials for biometric unlock
		await _settingsService.SetAsync("stored_refresh_token", "refresh_token_for_biometric");

		// Act
		var authResult = await _authService.AuthenticateWithBiometricAsync();

		// Assert
		authResult.Should().BeTrue();
	}

	[Fact]
	public async Task Logout_ShouldClearAllCredentials()
	{
		// Arrange
		// Login first
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new { code = "code" });
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "token",
			refresh_token = "refresh",
			expires_in = 3600
		});

		await _authService.LoginAsync("testuser", "password");

		// Act
		await _authService.LogoutAsync();

		// Assert
		var isAuthenticated = await _authService.IsAuthenticatedAsync();
		isAuthenticated.Should().BeFalse();

		var accessToken = await _authService.GetAccessTokenAsync();
		accessToken.Should().BeNullOrEmpty();
	}

	[Fact]
	public async Task GetUserInfo_WhenAuthenticated_ShouldReturnUserData()
	{
		// Arrange
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new { code = "code" });
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "token",
			refresh_token = "refresh",
			expires_in = 3600
		});

		await _authService.LoginAsync("testuser", "password");

		// Mock user info response
		_mockHttpHandler.ConfigureJsonResponse("/api/user/me", new
		{
			id = "user123",
			username = "testuser",
			email = "test@example.com",
			name = "Test User"
		});

		// Act
		var userInfo = await _authService.GetUserInfoAsync();

		// Assert
		userInfo.Should().NotBeNull();
		userInfo!.Username.Should().Be("testuser");
		userInfo.Email.Should().Be("test@example.com");
	}

	[Fact]
	public async Task CheckTokenExpiration_ExpiredToken_ShouldAutoRefresh()
	{
		// Arrange
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new { code = "code" });
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "initial_token",
			refresh_token = "refresh_token",
			expires_in = 1 // Expires in 1 second
		});

		await _authService.LoginAsync("testuser", "password");

		// Wait for token to expire
		await Task.Delay(1100);

		// Mock refresh response
		_mockHttpHandler.ClearResponses();
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "auto_refreshed_token",
			refresh_token = "new_refresh_token",
			expires_in = 3600
		});

		// Act
		var isAuthenticated = await _authService.IsAuthenticatedAsync();

		// Assert
		// Authentication service should auto-refresh
		isAuthenticated.Should().BeTrue();
	}

	[Fact]
	public async Task MultipleLogins_ShouldReplaceTokens()
	{
		// Arrange
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new { code = "code1" });
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "token1",
			refresh_token = "refresh1",
			expires_in = 3600
		});

		await _authService.LoginAsync("user1", "password1");
		var firstToken = await _authService.GetAccessTokenAsync();

		// Second login
		_mockHttpHandler.ClearResponses();
		_mockHttpHandler.ConfigureJsonResponse("/oauth/authorize", new { code = "code2" });
		_mockHttpHandler.ConfigureJsonResponse("/oauth/token", new
		{
			access_token = "token2",
			refresh_token = "refresh2",
			expires_in = 3600
		});

		// Act
		await _authService.LoginAsync("user2", "password2");
		var secondToken = await _authService.GetAccessTokenAsync();

		// Assert
		firstToken.Should().NotBe(secondToken);
		secondToken.Should().Be("token2");
	}

	[Fact]
	public async Task DisableBiometric_ShouldRemoveSettings()
	{
		// Arrange
		_mockBiometricService.Setup(x => x.IsAvailableAsync()).ReturnsAsync(true);
		_mockBiometricService.Setup(x => x.AuthenticateAsync(It.IsAny<string>()))
			.ReturnsAsync(new BiometricAuthResult { Success = true });

		await _authService.EnableBiometricAuthAsync();

		// Act
		await _authService.DisableBiometricAuthAsync();

		// Assert
		var isBiometricEnabled = await _settingsService.GetAsync<bool>("biometric_enabled");
		isBiometricEnabled.Should().BeFalse();
	}

	[Fact]
	public async Task NetworkError_DuringLogin_ShouldHandleGracefully()
	{
		// Arrange
		_mockHttpHandler.ConfigureNetworkFailure("/oauth/authorize", "Network timeout");

		// Act
		var success = await _authService.LoginAsync("testuser", "password");

		// Assert
		success.Should().BeFalse();

		var isAuthenticated = await _authService.IsAuthenticatedAsync();
		isAuthenticated.Should().BeFalse();
	}
}

/// <summary>
/// Biometric authentication result
/// </summary>
public class BiometricAuthResult
{
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
}

/// <summary>
/// User information
/// </summary>
public class UserInfo
{
	public required string Id { get; init; }
	public required string Username { get; init; }
	public required string Email { get; init; }
	public string? Name { get; init; }
}
