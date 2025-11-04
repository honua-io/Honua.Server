using FluentAssertions;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Configuration;

public sealed class HonuaAuthenticationOptionsValidatorTests
{
    private readonly IHostEnvironment _environment = new FakeHostEnvironment("Development");

    [Fact]
    public void Validate_PostgresProviderWithoutConnectionString_Fails()
    {
        var connectionStrings = Options.Create(new ConnectionStringOptions());
        var validator = new HonuaAuthenticationOptionsValidator(_environment, connectionStrings);

        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                Provider = "postgres"
            }
        };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public void Validate_PostgresProviderWithConnectionString_Succeeds()
    {
        var connectionStrings = Options.Create(new ConnectionStringOptions
        {
            Postgres = "Host=localhost;Database=honua;Username=test;Password=test"
        });

        var validator = new HonuaAuthenticationOptionsValidator(_environment, connectionStrings);

        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                Provider = "postgres"
            }
        };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void Validate_SqliteProvider_AllowsMissingConnectionString()
    {
        var connectionStrings = Options.Create(new ConnectionStringOptions());
        var validator = new HonuaAuthenticationOptionsValidator(_environment, connectionStrings);

        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                Provider = "sqlite",
                StorePath = "data/auth/auth.db"
            }
        };

        var result = validator.Validate(null, options);

        result.Failed.Should().BeFalse();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
            ContentRootPath = ".";
            ApplicationName = "Honua.Tests";
            ContentRootFileProvider = new NullFileProvider();
        }

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
