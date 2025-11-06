// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using System.Net;
using Moq;

namespace Honua.Admin.Blazor.Tests.Services;

/// <summary>
/// Tests for UserApiClient.
/// Demonstrates testing API clients with mocked HTTP responses.
/// </summary>
public class UserApiClientTests
{
    [Fact]
    public async Task ListUsersAsync_Success_ReturnsUserList()
    {
        // Arrange
        var expectedUsers = new UserListResponse
        {
            Users = new List<UserResponse>
            {
                new UserResponse
                {
                    Id = "user1",
                    Username = "testuser",
                    DisplayName = "Test User",
                    Email = "test@example.com",
                    Roles = new List<string> { "administrator" },
                    IsEnabled = true,
                    IsLockedOut = false,
                    FailedLoginAttempts = 0,
                    LastLogin = DateTimeOffset.UtcNow
                }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 50
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/users?page=1&pageSize=50", expectedUsers);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.ListUsersAsync();

        // Assert
        result.Should().NotBeNull();
        result.Users.Should().HaveCount(1);
        result.Users[0].Username.Should().Be("testuser");
        result.Users[0].DisplayName.Should().Be("Test User");
        result.Users[0].Roles.Should().Contain("administrator");
    }

    [Fact]
    public async Task GetUserAsync_Success_ReturnsUser()
    {
        // Arrange
        var expectedUser = new UserResponse
        {
            Id = "user1",
            Username = "testuser",
            DisplayName = "Test User",
            Email = "test@example.com",
            Roles = new List<string> { "datapublisher", "viewer" },
            IsEnabled = true,
            IsLockedOut = false,
            FailedLoginAttempts = 0,
            LastLogin = DateTimeOffset.UtcNow
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/users/testuser", expectedUser);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetUserAsync("testuser");

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("testuser");
        result.Roles.Should().HaveCount(2);
        result.Roles.Should().Contain("datapublisher");
        result.Roles.Should().Contain("viewer");
    }

    [Fact]
    public async Task CreateUserAsync_Success_ReturnsCreatedUser()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Username = "newuser",
            DisplayName = "New User",
            Email = "new@example.com",
            Password = "SecurePassword123",
            Roles = new List<string> { "viewer" },
            IsEnabled = true
        };

        var expectedResponse = new UserResponse
        {
            Id = "user2",
            Username = "newuser",
            DisplayName = "New User",
            Email = "new@example.com",
            Roles = new List<string> { "viewer" },
            IsEnabled = true,
            IsLockedOut = false,
            FailedLoginAttempts = 0,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/users", expectedResponse, HttpStatusCode.OK);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.CreateUserAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("newuser");
        result.DisplayName.Should().Be("New User");
        result.Email.Should().Be("new@example.com");
        result.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateUserAsync_Success_ReturnsUpdatedUser()
    {
        // Arrange
        var request = new UpdateUserRequest
        {
            DisplayName = "Updated Name",
            Email = "updated@example.com",
            Roles = new List<string> { "administrator", "datapublisher" },
            IsEnabled = true
        };

        var expectedResponse = new UserResponse
        {
            Id = "user1",
            Username = "testuser",
            DisplayName = "Updated Name",
            Email = "updated@example.com",
            Roles = new List<string> { "administrator", "datapublisher" },
            IsEnabled = true,
            IsLockedOut = false
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPutJson("/admin/users/testuser", expectedResponse);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.UpdateUserAsync("testuser", request);

        // Assert
        result.Should().NotBeNull();
        result.DisplayName.Should().Be("Updated Name");
        result.Email.Should().Be("updated@example.com");
        result.Roles.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteUserAsync_Success_ReturnsTrue()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockDelete("/admin/users/testuser", HttpStatusCode.OK);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.DeleteUserAsync("testuser");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ReturnsTrue()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            NewPassword = "NewSecurePassword123"
        };

        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/users/testuser/password", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.ChangePasswordAsync("testuser", request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetStatisticsAsync_Success_ReturnsStatistics()
    {
        // Arrange
        var expectedStats = new UserStatistics
        {
            TotalUsers = 10,
            ActiveUsers = 8,
            LockedUsers = 1,
            DisabledUsers = 1
        };

        var mockFactory = new MockHttpClientFactory()
            .MockGetJson("/admin/users/statistics", expectedStats);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.GetStatisticsAsync();

        // Assert
        result.Should().NotBeNull();
        result.TotalUsers.Should().Be(10);
        result.ActiveUsers.Should().Be(8);
        result.LockedUsers.Should().Be(1);
        result.DisabledUsers.Should().Be(1);
    }

    [Fact]
    public async Task ListUsersAsync_ServerError_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/users?page=1&pageSize=50", HttpStatusCode.InternalServerError);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.ListUsersAsync();

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetUserAsync_NotFound_ThrowsHttpRequestException()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Get, "/admin/users/nonexistent", HttpStatusCode.NotFound);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.GetUserAsync("nonexistent");

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task CreateUserAsync_ValidationError_ThrowsHttpRequestException()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Username = "", // Invalid: empty username
            DisplayName = "Test",
            Email = "invalid-email", // Invalid email format
            Password = "123" // Too short
        };

        var mockFactory = new MockHttpClientFactory()
            .MockError(HttpMethod.Post, "/admin/users", HttpStatusCode.BadRequest, "Validation failed");

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var act = async () => await apiClient.CreateUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task UnlockUserAsync_Success_ReturnsTrue()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/users/testuser/unlock", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.UnlockUserAsync("testuser");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EnableUserAsync_Success_ReturnsTrue()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/users/testuser/enable", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.EnableUserAsync("testuser");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DisableUserAsync_Success_ReturnsTrue()
    {
        // Arrange
        var mockFactory = new MockHttpClientFactory()
            .MockPostJson("/admin/users/testuser/disable", new { success = true });

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient("AdminApi")).Returns(mockFactory.CreateClient());
        var logger = Mock.Of<ILogger<UserApiClient>>();
        var apiClient = new UserApiClient(httpClientFactory.Object, logger);

        // Act
        var result = await apiClient.DisableUserAsync("testuser");

        // Assert
        result.Should().BeTrue();
    }
}
