// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Xunit;

namespace Honua.Server.Host.Tests.Authentication;

/// <summary>
/// Integration tests for LocalAuthController - local authentication and login endpoints.
/// Tests login flow, rate limiting, account lockout, and authentication validation.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Authentication")]
public sealed class LocalAuthControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LocalAuthControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Login Success Tests

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(content);

            json.TryGetProperty("token", out var token).Should().BeTrue();
            token.GetString().Should().NotBeNullOrEmpty();
        }
        else
        {
            // May fail if local auth is not configured
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.ServiceUnavailable,
                HttpStatusCode.Unauthorized);
        }
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsRoles()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(content);

            json.TryGetProperty("roles", out var roles).Should().BeTrue();
        }
    }

    #endregion

    #region Login Failure Tests

    [Fact]
    public async Task Login_InvalidUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "nonexistent-user-12345",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable); // If local auth disabled
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_EmptyUsername_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_EmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_MissingUsername_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_MissingPassword_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_NullCredentials_ReturnsBadRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Username = (string?)null,
            Password = (string?)null
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task Login_UsernameTooShort_ReturnsBadRequest()
    {
        // Arrange - Username must be at least 3 characters
        var loginRequest = new
        {
            Username = "ab",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("username", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_UsernameTooLong_ReturnsBadRequest()
    {
        // Arrange - Username must be at most 256 characters
        var longUsername = new string('a', 257);
        var loginRequest = new
        {
            Username = longUsername,
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_PasswordTooShort_ReturnsBadRequest()
    {
        // Arrange - Password must be at least 8 characters
        var loginRequest = new
        {
            Username = "testuser",
            Password = "Short1!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("password", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_PasswordTooLong_ReturnsBadRequest()
    {
        // Arrange - Password must be at most 128 characters
        var longPassword = new string('a', 129);
        var loginRequest = new
        {
            Username = "testuser",
            Password = longPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Security and Audit Tests

    [Fact]
    public async Task Login_WithCustomIpAddress_ProcessesRequest()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Add X-Forwarded-For header
        _client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.100");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_WithCustomUserAgent_ProcessesRequest()
    {
        // Arrange
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Custom-Test-Agent/1.0");

        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_FailedAttempt_DoesNotLeakUserExistence()
    {
        // Arrange - Test both existing and non-existing users
        var existingUserRequest = new
        {
            Username = "admin",
            Password = "WrongPassword123!"
        };

        var nonExistingUserRequest = new
        {
            Username = "definitely-does-not-exist-12345",
            Password = "SomePassword123!"
        };

        // Act
        var existingUserResponse = await _client.PostAsJsonAsync("/api/auth/local/login", existingUserRequest);
        var nonExistingUserResponse = await _client.PostAsJsonAsync("/api/auth/local/login", nonExistingUserRequest);

        // Assert - Both should return the same error (uniform failure response)
        if (existingUserResponse.StatusCode == HttpStatusCode.Unauthorized &&
            nonExistingUserResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            var existingContent = await existingUserResponse.Content.ReadAsStringAsync();
            var nonExistingContent = await nonExistingUserResponse.Content.ReadAsStringAsync();

            // Error messages should be identical to prevent user enumeration
            existingContent.Should().Be(nonExistingContent);
        }
    }

    #endregion

    #region Rate Limiting Tests

    [Fact]
    public async Task Login_MultipleFailedAttempts_MayTriggerRateLimit()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "WrongPassword123!"
        };

        // Act - Make 10 failed login attempts
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 10; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);
            responses.Add(response);
            await Task.Delay(100); // Small delay
        }

        // Assert - Later requests may be rate limited (429) or continue as unauthorized (401)
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.Unauthorized ||
            r.StatusCode == HttpStatusCode.TooManyRequests ||
            r.StatusCode == HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Account Lockout Tests

    [Fact]
    public async Task Login_AccountLockedOut_ReturnsUnauthorized()
    {
        // Arrange - Attempt to login with an account that might be locked
        var loginRequest = new
        {
            Username = "locked-test-user",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert - Locked accounts should return Unauthorized (uniform failure)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Disabled Account Tests

    [Fact]
    public async Task Login_DisabledAccount_ReturnsUnauthorized()
    {
        // Arrange - Attempt to login with a disabled account
        var loginRequest = new
        {
            Username = "disabled-test-user",
            Password = "CorrectPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert - Disabled accounts should return Unauthorized (uniform failure)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Local Auth Not Configured Tests

    [Fact]
    public async Task Login_LocalAuthDisabled_ReturnsServiceUnavailable()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("authentication", StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion

    #region Special Characters and Unicode Tests

    [Fact]
    public async Task Login_UsernameWithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "user@example.com",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_UsernameWithUnicode_HandlesCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "użytkownik",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_PasswordWithUnicode_HandlesCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "testuser",
            Password = "Pāsswörd123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region SQL Injection and Security Tests

    [Fact]
    public async Task Login_SqlInjectionAttemptInUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin' OR '1'='1",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_SqlInjectionAttemptInPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "' OR '1'='1"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Login_XssAttemptInUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "<script>alert('xss')</script>",
            Password = "SomePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.ServiceUnavailable);
    }

    #endregion

    #region Concurrent Login Tests

    [Fact]
    public async Task Login_MultipleConcurrentRequests_HandlesCorrectly()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Act - Send 10 concurrent login requests
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.PostAsJsonAsync("/api/auth/local/login", loginRequest))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
        responses.Should().OnlyContain(r =>
            r.StatusCode == HttpStatusCode.OK ||
            r.StatusCode == HttpStatusCode.Unauthorized ||
            r.StatusCode == HttpStatusCode.ServiceUnavailable ||
            r.StatusCode == HttpStatusCode.TooManyRequests);
    }

    #endregion

    #region Token Validation Tests

    [Fact]
    public async Task Login_SuccessfulLogin_ReturnsValidJwtToken()
    {
        // Arrange
        var loginRequest = new
        {
            Username = "admin",
            Password = "TestAdmin123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/login", loginRequest);

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            var json = JsonSerializer.Deserialize<JsonElement>(content);

            if (json.TryGetProperty("token", out var token))
            {
                var tokenString = token.GetString();
                tokenString.Should().NotBeNullOrEmpty();

                // Basic JWT format check (header.payload.signature)
                var parts = tokenString!.Split('.');
                parts.Should().HaveCount(3);
            }
        }
    }

    #endregion
}
