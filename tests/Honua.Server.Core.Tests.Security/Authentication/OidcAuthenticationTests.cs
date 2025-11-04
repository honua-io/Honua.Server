using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Moq.Protected;

namespace Honua.Server.Core.Tests.Security.Authentication;

/// <summary>
/// Comprehensive tests for OIDC authentication integration.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class OidcAuthenticationTests
{
    [Fact]
    public void JwtBearerOptionsConfigurator_ConfiguresOidcMode_Correctly()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com",
                Audience = "honua-api",
                RequireHttpsMetadata = true,
                RoleClaimPath = "roles"
            }
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);
        var signingKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateMockServiceProvider();
        var environment = new FakeWebHostEnvironment { EnvironmentName = Environments.Production };
        var logger = NullLogger<JwtBearerOptionsConfigurator>.Instance;

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            signingKeyProvider,
            serviceProvider,
            environment,
            logger);

        var jwtOptions = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert
        jwtOptions.Authority.Should().Be("https://auth.example.com");
        jwtOptions.Audience.Should().Be("honua-api");
        jwtOptions.RequireHttpsMetadata.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidAudience.Should().Be("honua-api");
        jwtOptions.TokenValidationParameters.RoleClaimType.Should().Be("roles");
    }

    [Fact]
    public void JwtBearerOptionsConfigurator_ConfiguresLocalMode_Correctly()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);
        var signingKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateMockServiceProvider();
        var environment = new FakeWebHostEnvironment { EnvironmentName = Environments.Development };
        var logger = NullLogger<JwtBearerOptionsConfigurator>.Instance;

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            signingKeyProvider,
            serviceProvider,
            environment,
            logger);

        var jwtOptions = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert
        jwtOptions.Authority.Should().BeNull();
        jwtOptions.Audience.Should().Be(LocalAuthenticationDefaults.Audience);
        jwtOptions.RequireHttpsMetadata.Should().BeFalse();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be(LocalAuthenticationDefaults.Issuer);
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
    }

    [Fact]
    public void JwtBearerOptionsConfigurator_ConfiguresQuickStartMode_Correctly()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.QuickStart
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);
        var signingKeyProvider = CreateMockSigningKeyProvider();
        var serviceProvider = CreateMockServiceProvider();
        var environment = new FakeWebHostEnvironment { EnvironmentName = Environments.Development };
        var logger = NullLogger<JwtBearerOptionsConfigurator>.Instance;

        var configurator = new JwtBearerOptionsConfigurator(
            optionsMonitor,
            signingKeyProvider,
            serviceProvider,
            environment,
            logger);

        var jwtOptions = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions();

        // Act
        configurator.Configure(jwtOptions);

        // Assert
        jwtOptions.Authority.Should().BeNull();
        jwtOptions.Audience.Should().BeNull();
        jwtOptions.RequireHttpsMetadata.Should().BeFalse();
        jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeFalse();
        jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeFalse();
        jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeFalse();
    }

    [Fact]
    public async Task OidcDiscoveryHealthCheck_WhenOidcNotEnabled_ReturnsHealthy()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var healthCheck = new Honua.Server.Host.Health.OidcDiscoveryHealthCheck(
            optionsMonitor,
            httpClientFactory.Object,
            NullLogger<Honua.Server.Host.Health.OidcDiscoveryHealthCheck>.Instance,
            cache);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Be("OIDC mode not enabled");
    }

    [Fact]
    public async Task OidcDiscoveryHealthCheck_WhenAuthorityNotConfigured_ReturnsDegraded()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = null
            }
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        var healthCheck = new Honua.Server.Host.Health.OidcDiscoveryHealthCheck(
            optionsMonitor,
            httpClientFactory.Object,
            NullLogger<Honua.Server.Host.Health.OidcDiscoveryHealthCheck>.Instance,
            cache);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("OIDC authority not configured");
    }

    [Fact]
    public async Task OidcDiscoveryHealthCheck_WhenDiscoveryEndpointAccessible_ReturnsHealthy()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com"
            }
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"issuer\":\"https://auth.example.com\"}")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var healthCheck = new Honua.Server.Host.Health.OidcDiscoveryHealthCheck(
            optionsMonitor,
            httpClientFactory.Object,
            NullLogger<Honua.Server.Host.Health.OidcDiscoveryHealthCheck>.Instance,
            cache);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("OIDC discovery endpoint accessible");
    }

    [Fact]
    public async Task OidcDiscoveryHealthCheck_WhenDiscoveryEndpointUnreachable_ReturnsDegraded()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com"
            }
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var healthCheck = new Honua.Server.Host.Health.OidcDiscoveryHealthCheck(
            optionsMonitor,
            httpClientFactory.Object,
            NullLogger<Honua.Server.Host.Health.OidcDiscoveryHealthCheck>.Instance,
            cache);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Be("OIDC discovery endpoint unreachable");
    }

    [Fact]
    public async Task OidcDiscoveryHealthCheck_CachesSuccessfulResults()
    {
        // Arrange
        var authOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com"
            }
        };

        var optionsMonitor = new TestOptionsMonitor(authOptions);

        var callCount = 0;
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"issuer\":\"https://auth.example.com\"}")
                };
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var cache = new MemoryCache(new MemoryCacheOptions());

        var healthCheck = new Honua.Server.Host.Health.OidcDiscoveryHealthCheck(
            optionsMonitor,
            httpClientFactory.Object,
            NullLogger<Honua.Server.Host.Health.OidcDiscoveryHealthCheck>.Instance,
            cache);

        // Act - Check health twice
        var result1 = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        var result2 = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result1.Status.Should().Be(HealthStatus.Healthy);
        result2.Status.Should().Be(HealthStatus.Healthy);
        callCount.Should().Be(1, "Second call should use cached result");
    }

    [Fact]
    public void HonuaAuthenticationOptionsValidator_WithValidOidcConfig_Succeeds()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com",
                Audience = "honua-api"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public void HonuaAuthenticationOptionsValidator_WithMissingAuthority_Fails()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = null,
                Audience = "honua-api"
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Jwt Authority must be provided when authentication mode is Oidc.");
    }

    [Fact]
    public void HonuaAuthenticationOptionsValidator_WithMissingAudience_Fails()
    {
        // Arrange
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var validator = new HonuaAuthenticationOptionsValidator(environment, Options.Create(new ConnectionStringOptions()));
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc,
            Jwt = new HonuaAuthenticationOptions.JwtOptions
            {
                Authority = "https://auth.example.com",
                Audience = null
            }
        };

        // Act
        var result = validator.Validate(null, options);

        // Assert
        result.Failed.Should().BeTrue();
        result.Failures.Should().Contain("Jwt Audience must be provided when authentication mode is Oidc.");
    }

    [Fact]
    public void OidcToken_WithValidClaims_CanBeExtracted()
    {
        // Arrange - Create a mock OIDC token
        var signingKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var securityKey = new SymmetricSecurityKey(signingKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "user-123"),
            new Claim(JwtRegisteredClaimNames.Email, "user@example.com"),
            new Claim("roles", "admin"),
            new Claim("roles", "user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: "https://auth.example.com",
            audience: "honua-api",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        // Act - Read the token
        var readToken = tokenHandler.ReadJwtToken(tokenString);

        // Assert
        readToken.Subject.Should().Be("user-123");
        readToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "user@example.com");
        readToken.Claims.Where(c => c.Type == "roles").Select(c => c.Value).Should().BeEquivalentTo(new[] { "admin", "user" });
    }

    [Fact]
    public void OidcToken_Validation_RequiresCorrectIssuer()
    {
        // Arrange
        var signingKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var securityKey = new SymmetricSecurityKey(signingKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://auth.example.com",
            audience: "honua-api",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "user-123") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://wrong-issuer.com",
            ValidateAudience = true,
            ValidAudience = "honua-api",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateLifetime = true
        };

        // Act & Assert
        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            tokenHandler.ValidateToken(tokenString, validationParameters, out _));
    }

    [Fact]
    public void OidcToken_Validation_RequiresCorrectAudience()
    {
        // Arrange
        var signingKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var securityKey = new SymmetricSecurityKey(signingKey);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "https://auth.example.com",
            audience: "honua-api",
            claims: new[] { new Claim(JwtRegisteredClaimNames.Sub, "user-123") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "https://auth.example.com",
            ValidateAudience = true,
            ValidAudience = "wrong-audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = securityKey,
            ValidateLifetime = true
        };

        // Act & Assert
        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            tokenHandler.ValidateToken(tokenString, validationParameters, out _));
    }

    private static ILocalSigningKeyProvider CreateMockSigningKeyProvider()
    {
        var mock = new Mock<ILocalSigningKeyProvider>();
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        mock.Setup(p => p.GetSigningKey()).Returns(key);
        mock.Setup(p => p.GetSigningKeyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);
        return mock.Object;
    }

    private static IServiceProvider CreateMockServiceProvider()
    {
        var mock = new Mock<IServiceProvider>();
        return mock.Object;
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private readonly HonuaAuthenticationOptions _value;

        public TestOptionsMonitor(HonuaAuthenticationOptions value)
        {
            _value = value;
        }

        public HonuaAuthenticationOptions CurrentValue => _value;
        public HonuaAuthenticationOptions Get(string? name) => _value;
        public IDisposable OnChange(Action<HonuaAuthenticationOptions, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Honua.Server.Core.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Honua.Server.Core.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    }
}
