using System;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class JwtBearerOptionsConfiguratorTests
{
    [Fact]
    public void Configure_LocalMode_SetsLocalValidationParameters()
    {
        var tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"honua-tests-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(tempRoot);
        var signingKeyPath = System.IO.Path.Combine(tempRoot, "signing.key");
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SigningKeyPath = signingKeyPath
            }
        };

        var monitor = new TestOptionsMonitor(options);
        var signingKeyProvider = new LocalSigningKeyProvider(monitor, NullLogger<LocalSigningKeyProvider>.Instance);
        var services = new ServiceCollection().BuildServiceProvider();
        var environment = new FakeWebHostEnvironment { EnvironmentName = Environments.Development };
        var logger = NullLogger<JwtBearerOptionsConfigurator>.Instance;
        var configurator = new JwtBearerOptionsConfigurator(monitor, signingKeyProvider, services, environment, logger);
        var jwtOptions = new JwtBearerOptions();

        try
        {
            configurator.Configure(jwtOptions);

            jwtOptions.TokenValidationParameters.ValidateIssuer.Should().BeTrue();
            jwtOptions.TokenValidationParameters.ValidIssuer.Should().Be(LocalAuthenticationDefaults.Issuer);
            jwtOptions.TokenValidationParameters.ValidateAudience.Should().BeTrue();
            jwtOptions.TokenValidationParameters.ValidAudience.Should().Be(LocalAuthenticationDefaults.Audience);
            jwtOptions.TokenValidationParameters.ValidateIssuerSigningKey.Should().BeTrue();
            jwtOptions.TokenValidationParameters.IssuerSigningKey.Should().BeOfType<SymmetricSecurityKey>();
            jwtOptions.TokenValidationParameters.RoleClaimType.Should().Be(LocalAuthenticationDefaults.RoleClaimType);
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(signingKeyPath))
                {
                    System.IO.File.Delete(signingKeyPath);
                }
            }
            finally
            {
                if (System.IO.Directory.Exists(tempRoot))
                {
                    System.IO.Directory.Delete(tempRoot, recursive: true);
                }
            }
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private HonuaAuthenticationOptions _value;

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
