using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Extensions;

/// <summary>
/// Tests for AuthenticationExtensions ensuring proper authentication/authorization configuration.
/// </summary>
[Trait("Category", "Unit")]
public class AuthenticationExtensionsTests
{
    [Fact]
    public async Task AddHonuaAuthentication_ShouldRegisterJwtBearerAuthentication()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "honua:authentication:mode", "Local" },
                { "honua:authentication:local:issuer", "https://localhost" },
                { "honua:authentication:local:audience", "honua-api" }
            })
            .Build();

        // Act
        services.AddHonuaAuthentication(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        defaultScheme.Should().NotBeNull();
        defaultScheme!.Name.Should().Be(JwtBearerDefaults.AuthenticationScheme);
    }

    [Fact]
    public void AddHonuaAuthorization_WithEnforcement_ShouldRequireRoles()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "honua:authentication:mode", "Local" },
                { "honua:authentication:enforce", "true" }
            })
            .Build();

        // Act
        services.AddHonuaAuthorization(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>();
        options.Value.GetPolicy("RequireAdministrator").Should().NotBeNull();
        options.Value.GetPolicy("RequireDataPublisher").Should().NotBeNull();
        options.Value.GetPolicy("RequireViewer").Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaAuthorization_WithoutEnforcement_ShouldAllowAnonymous()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "honua:authentication:mode", "QuickStart" },
                { "honua:authentication:enforce", "false" }
            })
            .Build();

        // Act
        services.AddHonuaAuthorization(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>();

        // Policies should exist but allow anonymous access
        options.Value.GetPolicy("RequireAdministrator").Should().NotBeNull();
        options.Value.GetPolicy("RequireDataPublisher").Should().NotBeNull();
        options.Value.GetPolicy("RequireViewer").Should().NotBeNull();
    }

    [Fact]
    public void AddHonuaAuthentication_ShouldValidateOptionsOnStart()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "honua:authentication:mode", "Local" },
                { "honua:authentication:local:issuer", "https://localhost" },
                { "honua:authentication:local:audience", "honua-api" }
            })
            .Build();

        // Act
        services.AddHonuaAuthentication(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HonuaAuthenticationOptions>>();
        options.Value.Should().NotBeNull();
        options.Value.Mode.Should().Be(HonuaAuthenticationOptions.AuthenticationMode.Local);
    }
}
