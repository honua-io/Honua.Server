// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration.V2;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2;

public sealed class HonuaConfigLoaderTests : IDisposable
{
    private readonly string _tempDirectory;

    public HonuaConfigLoaderTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"honua_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public void Load_HclFile_Success()
    {
        // Arrange
        var hclContent = @"
honua {
    version = ""1.0""
    environment = ""test""
}

data_source ""test-db"" {
    provider = ""sqlite""
    connection = ""Data Source=:memory:""
}
";

        var filePath = Path.Combine(_tempDirectory, "test.hcl");
        File.WriteAllText(filePath, hclContent);

        // Act
        var config = HonuaConfigLoader.Load(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
        Assert.Equal("test", config.Honua.Environment);
        Assert.Single(config.DataSources);
    }

    [Fact]
    public void Load_HonuaFile_Success()
    {
        // Arrange
        var hclContent = @"
honua {
    version = ""1.0""
    environment = ""test""
}
";

        var filePath = Path.Combine(_tempDirectory, "config.honua");
        File.WriteAllText(filePath, hclContent);

        // Act
        var config = HonuaConfigLoader.Load(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
    }

    [Fact]
    public void Load_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent.hcl");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => HonuaConfigLoader.Load(filePath));
    }

    [Fact]
    public void Load_NullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => HonuaConfigLoader.Load(null!));
    }

    [Fact]
    public void Load_EmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => HonuaConfigLoader.Load(""));
    }

    [Fact]
    public void Load_UnsupportedFormat_ThrowsNotSupportedException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "config.yaml");
        File.WriteAllText(filePath, "test: value");

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => HonuaConfigLoader.Load(filePath));
    }

    [Fact]
    public async Task LoadAsync_HclFile_Success()
    {
        // Arrange
        var hclContent = @"
honua {
    version = ""1.0""
    environment = ""test""
}
";

        var filePath = Path.Combine(_tempDirectory, "async_test.hcl");
        await File.WriteAllTextAsync(filePath, hclContent);

        // Act
        var config = await HonuaConfigLoader.LoadAsync(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
    }

    [Fact]
    public async Task LoadAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var filePath = Path.Combine(_tempDirectory, "nonexistent_async.hcl");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => HonuaConfigLoader.LoadAsync(filePath));
    }

    [Fact]
    public void Load_WithEnvironmentVariableInterpolation_Success()
    {
        // Arrange
        var testVarName = "TEST_DB_CONNECTION";
        var testVarValue = "Data Source=./prod.db";
        Environment.SetEnvironmentVariable(testVarName, testVarValue);

        try
        {
            var hclContent = @"
data_source ""prod-db"" {
    provider = ""sqlite""
    connection = ""${env:TEST_DB_CONNECTION}""
}
";

            var filePath = Path.Combine(_tempDirectory, "env_test.hcl");
            File.WriteAllText(filePath, hclContent);

            // Act
            var config = HonuaConfigLoader.Load(filePath);

            // Assert
            Assert.Single(config.DataSources);
            Assert.Equal(testVarValue, config.DataSources["prod-db"].Connection);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [Fact]
    public void Load_WithVariableInterpolation_Success()
    {
        // Arrange
        var hclContent = @"
variable ""db_name"" = ""honua_test""

data_source ""test-db"" {
    provider = ""sqlite""
    connection = ""Data Source=./var.db_name.db""
}
";

        var filePath = Path.Combine(_tempDirectory, "var_test.hcl");
        File.WriteAllText(filePath, hclContent);

        // Act
        var config = HonuaConfigLoader.Load(filePath);

        // Assert
        Assert.Single(config.DataSources);
        Assert.Contains("honua_test", config.DataSources["test-db"].Connection);
    }

    [Fact]
    public void Load_ComplexConfiguration_Success()
    {
        // Arrange
        var hclContent = @"
honua {
    version = ""1.0""
    environment = ""test""
    log_level = ""debug""

    cors = {
        allow_any_origin = false
        allowed_origins = [""http://localhost:3000""]
    }
}

data_source ""sqlite-test"" {
    provider = ""sqlite""
    connection = ""Data Source=:memory:""
}

service ""odata"" {
    enabled = true
    allow_writes = false
}

service ""ogc_api"" {
    enabled = true
}

layer ""test-layer"" {
    title = ""Test Layer""
    data_source = ""sqlite-test""
    table = ""test_table""
    id_field = ""id""
    introspect_fields = true

    geometry = {
        column = ""geom""
        type = ""Point""
        srid = 4326
    }

    services = [""odata"", ""ogc_api""]
}

cache ""memory"" {
    type = ""memory""
    enabled = true
}

rate_limit {
    enabled = true
    store = ""memory""

    rules = {
        default = {
            requests = 100
            window = ""1m""
        }
    }
}
";

        var filePath = Path.Combine(_tempDirectory, "complex.hcl");
        File.WriteAllText(filePath, hclContent);

        // Act
        var config = HonuaConfigLoader.Load(filePath);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
        Assert.Equal("test", config.Honua.Environment);
        Assert.Equal("debug", config.Honua.LogLevel);
        Assert.NotNull(config.Honua.Cors);
        Assert.Single(config.DataSources);
        Assert.Equal(2, config.Services.Count);
        Assert.Single(config.Layers);
        Assert.Single(config.Caches);
        Assert.NotNull(config.RateLimit);

        var layer = config.Layers["test-layer"];
        Assert.Equal("Test Layer", layer.Title);
        Assert.Equal(2, layer.Services.Count);
    }

    [Fact]
    public void Load_InvalidHcl_ThrowsParseException()
    {
        // Arrange
        var hclContent = @"
honua {
    version = ""1.0""
    invalid_syntax_without_equals ""test""
}
";

        var filePath = Path.Combine(_tempDirectory, "invalid.hcl");
        File.WriteAllText(filePath, hclContent);

        // Act & Assert
        Assert.Throws<ParseException>(() => HonuaConfigLoader.Load(filePath));
    }
}
