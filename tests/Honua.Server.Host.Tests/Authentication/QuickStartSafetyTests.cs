using FluentAssertions;
using Honua.Server.Core.Auth;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Host.Tests.Authentication;

/// <summary>
/// Tests for QuickStart authentication mode safety measures.
/// Ensures QuickStart is blocked in Production and allowed in Development/Testing.
/// </summary>
[Trait("Category", "Unit")]
public class QuickStartSafetyTests
{
    [Fact]
    public void ConfigureQuickStart_InProductionEnvironment_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };
        var optionsMonitor = CreateOptionsMonitor(authOptions);
        var mockEnvironment = CreateMockEnvironment(Environments.Production);
        var logger = NullLogger<JwtBearerOptionsConfigurator>.Instance;
        var mockSigningKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateServiceProvider();

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            mockSigningKeyProvider,
            serviceProvider,
            mockEnvironment,
            logger);

        var jwtOptions = new JwtBearerOptions();

        // Act
        var act = () => configurator.Configure(jwtOptions);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*QuickStart authentication mode is not allowed in Production environment*");
    }

    [Fact]
    public void ConfigureQuickStart_InDevelopmentEnvironment_ShouldConfigureSuccessfully()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };
        var optionsMonitor = CreateOptionsMonitor(authOptions);
        var mockEnvironment = CreateMockEnvironment(Environments.Development);
        var mockLogger = new Mock<ILogger<JwtBearerOptionsConfigurator>>();
        var mockSigningKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateServiceProvider();

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            mockSigningKeyProvider,
            serviceProvider,
            mockEnvironment,
            mockLogger.Object);

        var jwtOptions = new JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert
        jwtOptions.Authority.Should().BeNull();
        jwtOptions.Audience.Should().BeNull();
        jwtOptions.RequireHttpsMetadata.Should().BeFalse();
        jwtOptions.TokenValidationParameters.Should().NotBeNull();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeFalse();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeFalse();
    }

    [Fact]
    public void ConfigureQuickStart_InDevelopmentEnvironment_ShouldLogWarning()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };
        var optionsMonitor = CreateOptionsMonitor(authOptions);
        var mockEnvironment = CreateMockEnvironment(Environments.Development);
        var mockLogger = new Mock<ILogger<JwtBearerOptionsConfigurator>>();
        var mockSigningKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateServiceProvider();

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            mockSigningKeyProvider,
            serviceProvider,
            mockEnvironment,
            mockLogger.Object);

        var jwtOptions = new JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert - Verify warning was logged
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WARNING: QuickStart authentication mode is ENABLED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ConfigureQuickStart_InStagingEnvironment_ShouldConfigureWithWarning()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };
        var optionsMonitor = CreateOptionsMonitor(authOptions);
        var mockEnvironment = CreateMockEnvironment(Environments.Staging);
        var mockLogger = new Mock<ILogger<JwtBearerOptionsConfigurator>>();
        var mockSigningKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateServiceProvider();

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            mockSigningKeyProvider,
            serviceProvider,
            mockEnvironment,
            mockLogger.Object);

        var jwtOptions = new JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert - Should configure successfully (not Production)
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeFalse();

        // Assert - Should still log warning
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("WARNING: QuickStart authentication mode is ENABLED")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void ProductionSecurityValidator_WithQuickStartInProduction_ShouldThrowException()
    {
        // Arrange
        var logger = NullLogger<ProductionSecurityValidator>.Instance;
        var validator = new ProductionSecurityValidator(logger);
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };

        // Act
        var act = () => validator.ValidateProductionSecurity(authOptions, isProduction: true);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*QuickStart authentication mode is NOT allowed in Production environment*");
    }

    [Fact]
    public void ProductionSecurityValidator_WithQuickStartInDevelopment_ShouldNotThrow()
    {
        // Arrange
        var logger = NullLogger<ProductionSecurityValidator>.Instance;
        var validator = new ProductionSecurityValidator(logger);
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };

        // Act
        var act = () => validator.ValidateProductionSecurity(authOptions, isProduction: false);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ProductionSecurityValidator_WithLocalModeInProduction_ShouldNotThrow()
    {
        // Arrange
        var logger = NullLogger<ProductionSecurityValidator>.Instance;
        var validator = new ProductionSecurityValidator(logger);
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SessionLifetime = TimeSpan.FromHours(1),
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15),
                SigningKeyPath = "/app/keys/signing.key"
            }
        };

        // Act
        var act = () => validator.ValidateProductionSecurity(authOptions, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void ProductionSecurityValidator_WithOidcModeInProduction_ShouldNotThrow()
    {
        // Arrange
        var logger = NullLogger<ProductionSecurityValidator>.Instance;
        var validator = new ProductionSecurityValidator(logger);
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com",
                Audience = "honua-api"
            }
        };

        // Act
        var act = () => validator.ValidateProductionSecurity(authOptions, isProduction: true);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    [InlineData("QA")]
    public void ConfigureQuickStart_InNonProductionEnvironments_ShouldAllow(string environmentName)
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };
        var optionsMonitor = CreateOptionsMonitor(authOptions);
        var mockEnvironment = CreateMockEnvironment(environmentName);
        var mockLogger = new Mock<ILogger<JwtBearerOptionsConfigurator>>();
        var mockSigningKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateServiceProvider();

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            mockSigningKeyProvider,
            serviceProvider,
            mockEnvironment,
            mockLogger.Object);

        var jwtOptions = new JwtBearerOptions();

        // Act
        var act = () => configurator.Configure(jwtOptions);

        // Assert
        act.Should().NotThrow();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
    }

    [Fact]
    public void ConfigureQuickStart_WarningMessage_ShouldContainEnvironmentName()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };
        var optionsMonitor = CreateOptionsMonitor(authOptions);
        var mockEnvironment = CreateMockEnvironment(Environments.Development);
        var mockLogger = new Mock<ILogger<JwtBearerOptionsConfigurator>>();
        var mockSigningKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateServiceProvider();

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            mockSigningKeyProvider,
            serviceProvider,
            mockEnvironment,
            mockLogger.Object);

        var jwtOptions = new JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert - Verify environment name is in the warning
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Development")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // Helper methods
    private static IOptionsMonitor<HonuaAuthenticationOptions> CreateOptionsMonitor(HonuaAuthenticationOptions options)
    {
        var mock = new Mock<IOptionsMonitor<HonuaAuthenticationOptions>>();
        mock.Setup(x => x.CurrentValue).Returns(options);
        return mock.Object;
    }

    private static IWebHostEnvironment CreateMockEnvironment(string environmentName)
    {
        var mock = new Mock<IWebHostEnvironment>();
        mock.Setup(x => x.EnvironmentName).Returns(environmentName);
        return mock.Object;
    }

    private static ILocalSigningKeyProvider CreateMockSigningKeyProvider()
    {
        var mock = new Mock<ILocalSigningKeyProvider>();
        mock.Setup(x => x.GetSigningKey()).Returns(new byte[32]);
        return mock.Object;
    }

    private static IServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services.BuildServiceProvider();
    }
}
