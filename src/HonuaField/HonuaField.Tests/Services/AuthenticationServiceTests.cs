using FluentAssertions;
using HonuaField.Services;
using Moq;
using Xunit;

namespace HonuaField.Tests.Services;

/// <summary>
/// Unit tests for AuthenticationService
/// Tests OAuth 2.0 Authorization Code + PKCE flow
/// </summary>
public class AuthenticationServiceTests
{
	private readonly Mock<IApiClient> _mockApiClient;
	private readonly Mock<ISettingsService> _mockSettingsService;
	private readonly AuthenticationService _authService;

	public AuthenticationServiceTests()
	{
		_mockApiClient = new Mock<IApiClient>();
		_mockSettingsService = new Mock<ISettingsService>();
		_authService = new AuthenticationService(_mockApiClient.Object, _mockSettingsService.Object);
	}

	[Fact]
	public async Task LoginAsync_ShouldReturnTrue_WhenCredentialsAreValid()
	{
		// Arrange
		var username = "testuser";
		var password = "testpass123";

		// Mock authorization code response
		_mockApiClient
			.Setup(x => x.PostAsync<dynamic>(
				It.Is<string>(s => s.Contains("/oauth/authorize")),
				It.IsAny<object>(),
				null))
			.ReturnsAsync(new { code = "auth_code_123" });

		// Mock token response
		var tokenResponse = new
		{
			access_token = "access_token_123",
			refresh_token = "refresh_token_123",
			expires_in = 3600,
			token_type = "Bearer"
		};

		_mockApiClient
			.Setup(x => x.PostAsync<object>(
				It.Is<string>(s => s.Contains("/oauth/token")),
				It.IsAny<object>(),
				null))
			.ReturnsAsync(tokenResponse);

		// Act
		var result = await _authService.LoginAsync(username, password);

		// Assert
		result.Should().BeTrue();
		_mockApiClient.Verify(x => x.PostAsync<dynamic>(
			It.Is<string>(s => s.Contains("/oauth/authorize")),
			It.IsAny<object>(),
			null), Times.Once);
		_mockApiClient.Verify(x => x.PostAsync<object>(
			It.Is<string>(s => s.Contains("/oauth/token")),
			It.IsAny<object>(),
			null), Times.Once);
	}

	[Fact]
	public async Task LoginAsync_ShouldReturnFalse_WhenAuthorizationCodeIsNull()
	{
		// Arrange
		var username = "testuser";
		var password = "wrongpass";

		// Mock authorization code response with null code
		_mockApiClient
			.Setup(x => x.PostAsync<dynamic>(
				It.Is<string>(s => s.Contains("/oauth/authorize")),
				It.IsAny<object>(),
				null))
			.ReturnsAsync(new { code = (string?)null });

		// Act
		var result = await _authService.LoginAsync(username, password);

		// Assert
		result.Should().BeFalse();
		_mockApiClient.Verify(x => x.PostAsync<object>(
			It.Is<string>(s => s.Contains("/oauth/token")),
			It.IsAny<object>(),
			null), Times.Never);
	}

	[Fact]
	public async Task LoginAsync_ShouldStorTokens_WhenLoginIsSuccessful()
	{
		// Arrange
		var username = "testuser";
		var password = "testpass123";
		var accessToken = "access_token_123";
		var refreshToken = "refresh_token_123";

		_mockApiClient
			.Setup(x => x.PostAsync<dynamic>(
				It.Is<string>(s => s.Contains("/oauth/authorize")),
				It.IsAny<object>(),
				null))
			.ReturnsAsync(new { code = "auth_code_123" });

		var tokenResponse = new
		{
			access_token = accessToken,
			refresh_token = refreshToken,
			expires_in = 3600,
			token_type = "Bearer"
		};

		_mockApiClient
			.Setup(x => x.PostAsync<object>(
				It.Is<string>(s => s.Contains("/oauth/token")),
				It.IsAny<object>(),
				null))
			.ReturnsAsync(tokenResponse);

		// Act
		await _authService.LoginAsync(username, password);

		// Assert
		_mockSettingsService.Verify(x => x.SetAsync(
			"access_token",
			It.IsAny<string>()), Times.Once);
		_mockSettingsService.Verify(x => x.SetAsync(
			"refresh_token",
			It.IsAny<string>()), Times.Once);
	}

	[Fact]
	public async Task IsAuthenticatedAsync_ShouldReturnTrue_WhenAccessTokenExists()
	{
		// Arrange
		_mockSettingsService
			.Setup(x => x.GetAsync<string>("access_token", null))
			.ReturnsAsync("valid_token");

		// Act
		var result = await _authService.IsAuthenticatedAsync();

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task IsAuthenticatedAsync_ShouldReturnFalse_WhenAccessTokenIsNull()
	{
		// Arrange
		_mockSettingsService
			.Setup(x => x.GetAsync<string>("access_token", null))
			.ReturnsAsync((string?)null);

		// Act
		var result = await _authService.IsAuthenticatedAsync();

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task LogoutAsync_ShouldClearTokens()
	{
		// Act
		await _authService.LogoutAsync();

		// Assert
		_mockSettingsService.Verify(x => x.RemoveAsync("access_token"), Times.Once);
		_mockSettingsService.Verify(x => x.RemoveAsync("refresh_token"), Times.Once);
		_mockSettingsService.Verify(x => x.RemoveAsync("user_data"), Times.Once);
	}

	[Fact]
	public async Task RefreshTokenAsync_ShouldReturnTrue_WhenRefreshIsSuccessful()
	{
		// Arrange
		var refreshToken = "refresh_token_123";
		_mockSettingsService
			.Setup(x => x.GetAsync<string>("refresh_token", null))
			.ReturnsAsync(refreshToken);

		var tokenResponse = new
		{
			access_token = "new_access_token",
			refresh_token = "new_refresh_token",
			expires_in = 3600,
			token_type = "Bearer"
		};

		_mockApiClient
			.Setup(x => x.PostAsync<object>(
				It.Is<string>(s => s.Contains("/oauth/token")),
				It.IsAny<object>(),
				null))
			.ReturnsAsync(tokenResponse);

		// Act
		var result = await _authService.RefreshTokenAsync();

		// Assert
		result.Should().BeTrue();
		_mockApiClient.Verify(x => x.PostAsync<object>(
			It.Is<string>(s => s.Contains("/oauth/token")),
			It.Is<object>(o => o.ToString()!.Contains("refresh_token")),
			null), Times.Once);
	}

	[Fact]
	public async Task RefreshTokenAsync_ShouldReturnFalse_WhenRefreshTokenIsNull()
	{
		// Arrange
		_mockSettingsService
			.Setup(x => x.GetAsync<string>("refresh_token", null))
			.ReturnsAsync((string?)null);

		// Act
		var result = await _authService.RefreshTokenAsync();

		// Assert
		result.Should().BeFalse();
		_mockApiClient.Verify(x => x.PostAsync<object>(
			It.IsAny<string>(),
			It.IsAny<object>(),
			null), Times.Never);
	}
}
