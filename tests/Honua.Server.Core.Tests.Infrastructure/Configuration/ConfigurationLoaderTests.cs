using System;
using System.IO;
using FluentAssertions;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Configuration;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public class ConfigurationLoaderTests
{
    [Fact]
    public void Load_ShouldDeserializeHonuaConfigurationFromJson()
    {
        var loader = new ConfigurationLoader();
        var configPath = Path.Combine(TestEnvironment.SolutionRoot, "samples", "sample-config.json");

        var configuration = loader.Load(configPath);

        configuration.Should().NotBeNull();
        configuration.Metadata.Provider.Should().Be("json");
        configuration.Metadata.Path.Should().EndWith(Path.Combine("samples", "metadata", "sample-metadata.json"));
    }

    [Fact]
    public void Load_ShouldThrowWhenFileMissing()
    {
        var loader = new ConfigurationLoader();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");

        var act = () => loader.Load(path);

        act.Should().Throw<FileNotFoundException>();
    }

}
