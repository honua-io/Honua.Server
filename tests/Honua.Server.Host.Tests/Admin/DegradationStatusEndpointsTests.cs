using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Features;
using Honua.Server.Host.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Admin;

/// <summary>
/// Tests for DegradationStatusEndpoints to ensure proper authorization and functionality
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Unit")]
public sealed class DegradationStatusEndpointsTests
{
    [Fact]
    public async Task GetAllFeatureStatus_RequiresAuthorization()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetFeatureStatus_RequiresAuthorization()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/status/test-feature");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DisableFeature_RequiresAuthorization()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        var request = new { Reason = "Testing" };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/features/status/test-feature/disable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EnableFeature_RequiresAuthorization()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/admin/features/status/test-feature/enable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CheckFeatureHealth_RequiresAuthorization()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/admin/features/status/test-feature/check-health", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetActiveDegradations_RequiresAuthorization()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/degradations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAllFeatureStatus_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.GetAllFeatureStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, FeatureStatus>
            {
                ["feature1"] = new FeatureStatus(
                    FeatureDegradationState.Healthy,
                    100,
                    null,
                    null,
                    DateTimeOffset.UtcNow,
                    null)
            });

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FeatureStatusResponse>();
        result.Should().NotBeNull();
        result!.Features.Should().HaveCount(1);
        result.Features[0].Name.Should().Be("feature1");
    }

    [Fact]
    public async Task GetFeatureStatus_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.GetFeatureStatusAsync("test-feature", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureStatus(
                FeatureDegradationState.Healthy,
                100,
                null,
                null,
                DateTimeOffset.UtcNow,
                null));

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/status/test-feature");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<FeatureStatusResponse>();
        result.Should().NotBeNull();
        result!.Features.Should().HaveCount(1);
        result.Features[0].Name.Should().Be("test-feature");
    }

    [Fact]
    public async Task GetFeatureStatus_NotFound_ReturnsNotFound()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.GetFeatureStatusAsync("unknown-feature", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureStatus(
                FeatureDegradationState.Unavailable,
                0,
                null,
                "Feature not registered",
                DateTimeOffset.UtcNow,
                null));

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/status/unknown-feature");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DisableFeature_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.DisableFeatureAsync("test-feature", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockFeatureManagement
            .Setup(m => m.GetFeatureStatusAsync("test-feature", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureStatus(
                FeatureDegradationState.Degraded,
                0,
                FeatureDegradationType.ManuallyDisabled,
                "Manually disabled via API",
                DateTimeOffset.UtcNow,
                null));

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new { Reason = "Testing" };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/features/status/test-feature/disable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockFeatureManagement.Verify(
            m => m.DisableFeatureAsync("test-feature", "Testing", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DisableFeature_WithoutReason_UsesDefaultReason()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.DisableFeatureAsync("test-feature", "Manually disabled via API", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockFeatureManagement
            .Setup(m => m.GetFeatureStatusAsync("test-feature", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureStatus(
                FeatureDegradationState.Degraded,
                0,
                FeatureDegradationType.ManuallyDisabled,
                "Manually disabled via API",
                DateTimeOffset.UtcNow,
                null));

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        var request = new { };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/features/status/test-feature/disable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockFeatureManagement.Verify(
            m => m.DisableFeatureAsync("test-feature", "Manually disabled via API", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnableFeature_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.EnableFeatureAsync("test-feature", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockFeatureManagement
            .Setup(m => m.GetFeatureStatusAsync("test-feature", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureStatus(
                FeatureDegradationState.Healthy,
                100,
                null,
                null,
                DateTimeOffset.UtcNow,
                null));

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/admin/features/status/test-feature/enable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockFeatureManagement.Verify(
            m => m.EnableFeatureAsync("test-feature", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CheckFeatureHealth_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.CheckFeatureHealthAsync("test-feature", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FeatureStatus(
                FeatureDegradationState.Healthy,
                100,
                null,
                null,
                DateTimeOffset.UtcNow,
                null));

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/admin/features/status/test-feature/check-health", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        mockFeatureManagement.Verify(
            m => m.CheckFeatureHealthAsync("test-feature", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetActiveDegradations_WithAdminAuth_ReturnsOk()
    {
        // Arrange
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        mockFeatureManagement
            .Setup(m => m.GetAllFeatureStatusesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, FeatureStatus>
            {
                ["healthy-feature"] = new FeatureStatus(
                    FeatureDegradationState.Healthy,
                    100,
                    null,
                    null,
                    DateTimeOffset.UtcNow,
                    null),
                ["degraded-feature"] = new FeatureStatus(
                    FeatureDegradationState.Degraded,
                    50,
                    FeatureDegradationType.HighErrorRate,
                    "Error rate exceeded threshold",
                    DateTimeOffset.UtcNow.AddMinutes(-5),
                    DateTimeOffset.UtcNow.AddMinutes(5)),
                ["unavailable-feature"] = new FeatureStatus(
                    FeatureDegradationState.Unavailable,
                    0,
                    FeatureDegradationType.CircuitBreakerOpen,
                    "Circuit breaker is open",
                    DateTimeOffset.UtcNow.AddMinutes(-10),
                    DateTimeOffset.UtcNow.AddMinutes(10))
            });

        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: false);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/admin/features/degradations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DegradationSummaryResponse>();
        result.Should().NotBeNull();
        result!.TotalFeatures.Should().Be(3);
        result.HealthyFeatures.Should().Be(1);
        result.DegradedFeatures.Should().Be(1);
        result.UnavailableFeatures.Should().Be(1);
        result.ActiveDegradations.Should().HaveCount(2); // degraded + unavailable
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotDisableFeature()
    {
        // Arrange - This test verifies that unauthenticated callers cannot disable features
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        var request = new { Reason = "Malicious disable attempt" };

        // Act
        var response = await client.PostAsJsonAsync("/api/admin/features/status/critical-feature/disable", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify the service was never called
        mockFeatureManagement.Verify(
            m => m.DisableFeatureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnauthenticatedUser_CannotEnableFeature()
    {
        // Arrange - This test verifies that unauthenticated callers cannot enable features
        var mockFeatureManagement = new Mock<IFeatureManagementService>();
        await using var factory = CreateTestFactory(mockFeatureManagement.Object, requireAuth: true);
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/admin/features/status/critical-feature/enable", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify the service was never called
        mockFeatureManagement.Verify(
            m => m.EnableFeatureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static WebApplicationFactory<DegradationStatusTestStartup> CreateTestFactory(
        IFeatureManagementService featureManagement,
        bool requireAuth)
    {
        return new WebApplicationFactory<DegradationStatusTestStartup>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton(featureManagement);

                    if (requireAuth)
                    {
                        // Configure to require authentication
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("RequireAdministrator", policy =>
                                policy.RequireAuthenticatedUser()
                                      .RequireRole("Administrator"));
                        });

                        services.AddAuthentication("Test")
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
                    }
                    else
                    {
                        // Configure to allow all (simulate admin user)
                        services.AddAuthorization(options =>
                        {
                            options.AddPolicy("RequireAdministrator", policy =>
                                policy.RequireAssertion(_ => true));
                        });
                    }
                });
            });
    }
}

/// <summary>
/// Test authentication handler that always rejects authentication
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Always fail authentication
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

/// <summary>
/// Minimal test startup for DegradationStatusEndpoints testing
/// </summary>
internal sealed class DegradationStatusTestStartup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapDegradationStatusEndpoints();
        });
    }
}
