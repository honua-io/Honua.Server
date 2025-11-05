// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using System.Net;
using System.Net.Http.Json;

namespace Honua.Admin.Blazor.Tests.Integration;

/// <summary>
/// Integration tests for authentication flows.
/// These tests demonstrate testing against a real API server (when available).
/// Note: These require the backend API to be running or use WebApplicationFactory.
/// </summary>
public class AuthenticationFlowTests
{
    [Fact(Skip = "Integration test - requires backend API")]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        using var client = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
        var loginRequest = new LoginRequest
        {
            Username = "admin",
            Password = "password"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/tokens/generate", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokenResponse = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokenResponse.Should().NotBeNull();
        tokenResponse!.Token.Should().NotBeNullOrEmpty();
        tokenResponse.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        using var client = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
        var loginRequest = new LoginRequest
        {
            Username = "admin",
            Password = "wrongpassword"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/tokens/generate", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task AccessProtectedEndpoint_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        using var client = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };

        // Act
        var response = await client.GetAsync("/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(Skip = "Integration test - requires backend API")]
    public async Task AccessProtectedEndpoint_WithValidToken_ReturnsOk()
    {
        // Arrange
        using var client = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };

        // First, login to get token
        var loginRequest = new LoginRequest
        {
            Username = "admin",
            Password = "password"
        };

        var loginResponse = await client.PostAsJsonAsync("/api/tokens/generate", loginRequest);
        var tokenResponse = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        // Add bearer token to request
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenResponse!.Token}");

        // Act
        var response = await client.GetAsync("/admin/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// Models for integration tests
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
