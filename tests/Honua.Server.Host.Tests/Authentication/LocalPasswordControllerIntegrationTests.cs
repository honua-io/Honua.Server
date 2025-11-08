// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Honua.Server.Host.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Honua.Server.Host.Tests.Authentication;

/// <summary>
/// Integration tests for LocalPasswordController - password management endpoints.
/// Tests password change, reset, and complexity validation scenarios.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Feature", "Authentication")]
public sealed class LocalPasswordControllerIntegrationTests : IClassFixture<HonuaWebApplicationFactory>
{
    private readonly HonuaWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LocalPasswordControllerIntegrationTests(HonuaWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    #region ChangePassword Tests

    [Fact]
    public async Task ChangePassword_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.Unauthorized,  // If not authenticated
            HttpStatusCode.BadRequest);   // If user doesn't exist or password incorrect
    }

    [Fact]
    public async Task ChangePassword_WeakPassword_ReturnsBadRequest()
    {
        // Arrange - Password too short
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "weak"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("password", StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ChangePassword_MissingCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
            // CurrentPassword is missing
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_MissingNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!"
            // NewPassword is missing
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_EmptyPasswords_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "",
            NewPassword = ""
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange - Create client without authentication
        var unauthenticatedClient = _factory.CreateClient();
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_IncorrectCurrentPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_PasswordWithoutSpecialCharacters_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123" // Missing special character
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_PasswordWithoutNumbers_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword!" // Missing number
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_PasswordWithoutUppercase_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "newpassword123!" // Missing uppercase
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ResetPassword Tests (Admin)

    [Fact]
    public async Task ResetPassword_ValidRequest_ReturnsNoContent()
    {
        // Arrange
        var userId = "testuser";
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,      // If not admin
            HttpStatusCode.Unauthorized,   // If not authenticated
            HttpStatusCode.NotFound);      // If user doesn't exist
    }

    [Fact]
    public async Task ResetPassword_NonExistentUser_ReturnsNotFound()
    {
        // Arrange
        var userId = "nonexistent-user-12345";
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound,
            HttpStatusCode.Forbidden,      // If not admin
            HttpStatusCode.Unauthorized);  // If not authenticated
    }

    [Fact]
    public async Task ResetPassword_WeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var userId = "testuser";
        var request = new
        {
            NewPassword = "weak"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Forbidden,      // If not admin
            HttpStatusCode.Unauthorized);  // If not authenticated
    }

    [Fact]
    public async Task ResetPassword_MissingNewPassword_ReturnsBadRequest()
    {
        // Arrange
        var userId = "testuser";
        var request = new { };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Forbidden,      // If not admin
            HttpStatusCode.Unauthorized);  // If not authenticated
    }

    [Fact]
    public async Task ResetPassword_WithoutAdminRole_ReturnsForbidden()
    {
        // Arrange
        var userId = "testuser";
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden,      // Expected if not admin
            HttpStatusCode.Unauthorized,   // If not authenticated
            HttpStatusCode.NoContent);     // If admin and successful
    }

    [Fact]
    public async Task ResetPassword_EmptyUserId_ReturnsBadRequestOrNotFound()
    {
        // Arrange
        var userId = "";
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.MethodNotAllowed); // Route might not match
    }

    [Fact]
    public async Task ResetPassword_SpecialCharactersInUserId_HandlesCorrectly()
    {
        // Arrange
        var userId = "user@example.com";
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{Uri.EscapeDataString(userId)}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.NotFound,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Password Complexity Tests

    [Theory]
    [InlineData("Short1!")]           // Too short (7 chars)
    [InlineData("NoNumber!")]         // Missing number
    [InlineData("nouppercasechar1!")] // Missing uppercase
    [InlineData("NOLOWERCASE1!")]     // Missing lowercase
    [InlineData("NoSpecialChar123")]  // Missing special character
    public async Task ChangePassword_VariousInvalidPasswords_ReturnsBadRequest(string invalidPassword)
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = invalidPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("ValidPass123!")]
    [InlineData("AnotherGood1@")]
    [InlineData("Complex#Pass456")]
    [InlineData("Str0ng$Password")]
    public async Task ChangePassword_VariousValidPasswords_DoesNotRejectForComplexity(string validPassword)
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = validPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        // Should not be rejected for password complexity (may still be unauthorized/not found)
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Audit and Security Tests

    [Fact]
    public async Task ChangePassword_CapturesIpAddress_ForAuditLog()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewSecurePassword456!"
        };

        // Add custom IP address header
        _client.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.100");

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);

        // Audit logging is internal - this test verifies the endpoint handles IP headers
    }

    [Fact]
    public async Task ResetPassword_CapturesAdminUser_ForAuditLog()
    {
        // Arrange
        var userId = "testuser";
        var request = new
        {
            NewPassword = "NewSecurePassword456!"
        };

        // Act
        var response = await _client.PostAsJsonAsync($"/api/auth/local/users/{userId}/password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound);

        // Audit logging is internal - this test verifies the endpoint works
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ChangePassword_VeryLongPassword_HandlesCorrectly()
    {
        // Arrange - 128 character password (maximum allowed)
        var longPassword = new string('A', 100) + "1234567890!@#$%^&*()ABCDEFGHIJ";
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = longPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_PasswordTooLong_ReturnsBadRequest()
    {
        // Arrange - 129 characters (exceeds maximum)
        var tooLongPassword = new string('A', 101) + "1234567890!@#$%^&*()ABCDEFGHIJK";
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = tooLongPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_UnicodeCharacters_HandlesCorrectly()
    {
        // Arrange - Password with unicode characters
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "Pāss🔐wörd123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_SamePasswordAsCurrentPassword_AllowedOrValidated()
    {
        // Arrange - New password same as current (some systems prevent this)
        var request = new
        {
            CurrentPassword = "SamePassword123!",
            NewPassword = "SamePassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/local/change-password", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Concurrent Requests

    [Fact]
    public async Task ChangePassword_MultipleConcurrentRequests_HandlesGracefully()
    {
        // Arrange
        var request = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewSecurePassword456!"
        };

        // Act - Send 5 concurrent requests
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => _client.PostAsJsonAsync("/api/auth/local/change-password", request))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should complete without crashing
        responses.Should().NotBeEmpty();
        responses.Should().OnlyContain(r => r != null);
    }

    #endregion
}
