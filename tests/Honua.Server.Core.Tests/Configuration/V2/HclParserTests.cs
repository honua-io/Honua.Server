// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Honua.Server.Core.Configuration.V2;
using Xunit;

namespace Honua.Server.Core.Tests.Configuration.V2;

public sealed class HclParserTests
{
    [Fact]
    public void Parse_MinimalHonuaBlock_Success()
    {
        // Arrange
        var hcl = @"
honua {
    version = ""1.0""
    environment = ""development""
    log_level = ""information""
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
        Assert.Equal("development", config.Honua.Environment);
        Assert.Equal("information", config.Honua.LogLevel);
    }

    [Fact]
    public void Parse_HonuaBlockWithCors_Success()
    {
        // Arrange
        var hcl = @"
honua {
    version = ""1.0""
    environment = ""production""

    cors = {
        allow_any_origin = false
        allowed_origins = [""https://example.com"", ""https://app.example.com""]
        allow_credentials = true
    }
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.NotNull(config.Honua.Cors);
        Assert.False(config.Honua.Cors.AllowAnyOrigin);
        Assert.Equal(2, config.Honua.Cors.AllowedOrigins.Count);
        Assert.Contains("https://example.com", config.Honua.Cors.AllowedOrigins);
        Assert.True(config.Honua.Cors.AllowCredentials);
    }

    [Fact]
    public void Parse_DataSourceBlock_Success()
    {
        // Arrange
        var hcl = @"
data_source ""sqlite-test"" {
    provider = ""sqlite""
    connection = ""Data Source=./test.db""
    health_check = ""SELECT 1""
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.Single(config.DataSources);
        var dataSource = config.DataSources["sqlite-test"];
        Assert.Equal("sqlite-test", dataSource.Id);
        Assert.Equal("sqlite", dataSource.Provider);
        Assert.Equal("Data Source=./test.db", dataSource.Connection);
        Assert.Equal("SELECT 1", dataSource.HealthCheck);
    }

    [Fact]
    public void Parse_DataSourceWithPool_Success()
    {
        // Arrange
        var hcl = @"
data_source ""postgres-prod"" {
    provider = ""postgresql""
    connection = ""Server=localhost;Database=honua;User=postgres""

    pool = {
        min_size = 5
        max_size = 20
        timeout = 30
    }
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        var dataSource = config.DataSources["postgres-prod"];
        Assert.NotNull(dataSource.Pool);
        Assert.Equal(5, dataSource.Pool.MinSize);
        Assert.Equal(20, dataSource.Pool.MaxSize);
        Assert.Equal(30, dataSource.Pool.Timeout);
    }

    [Fact]
    public void Parse_ServiceBlock_Success()
    {
        // Arrange
        var hcl = @"
service ""odata"" {
    enabled = true
    type = ""odata""
    allow_writes = true
    max_page_size = 1000
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.Single(config.Services);
        var service = config.Services["odata"];
        Assert.Equal("odata", service.Id);
        Assert.Equal("odata", service.Type);
        Assert.True(service.Enabled);
        Assert.True((bool)service.Settings["allow_writes"]!);
        Assert.Equal(1000, (int)service.Settings["max_page_size"]!);
    }

    [Fact]
    public void Parse_LayerBlockWithGeometry_Success()
    {
        // Arrange
        var hcl = @"
layer ""roads-primary"" {
    title = ""Primary Roads""
    data_source = ""sqlite-test""
    table = ""roads_primary""
    id_field = ""road_id""
    display_field = ""name""
    introspect_fields = true

    geometry = {
        column = ""geom""
        type = ""LineString""
        srid = 4326
    }

    services = [""odata"", ""ogc_api""]
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.Single(config.Layers);
        var layer = config.Layers["roads-primary"];
        Assert.Equal("roads-primary", layer.Id);
        Assert.Equal("Primary Roads", layer.Title);
        Assert.Equal("sqlite-test", layer.DataSource);
        Assert.Equal("roads_primary", layer.Table);
        Assert.Equal("road_id", layer.IdField);
        Assert.Equal("name", layer.DisplayField);
        Assert.True(layer.IntrospectFields);

        Assert.NotNull(layer.Geometry);
        Assert.Equal("geom", layer.Geometry.Column);
        Assert.Equal("LineString", layer.Geometry.Type);
        Assert.Equal(4326, layer.Geometry.Srid);

        Assert.Equal(2, layer.Services.Count);
        Assert.Contains("odata", layer.Services);
        Assert.Contains("ogc_api", layer.Services);
    }

    [Fact]
    public void Parse_LayerWithExplicitFields_Success()
    {
        // Arrange
        var hcl = @"
layer ""sensors"" {
    title = ""IoT Sensors""
    data_source = ""postgres-prod""
    table = ""sensors""
    id_field = ""sensor_id""
    introspect_fields = false

    geometry = {
        column = ""location""
        type = ""Point""
        srid = 4326
    }

    fields = {
        sensor_id = {
            type = ""int""
            nullable = false
        }
        name = {
            type = ""string""
            nullable = false
        }
        model = {
            type = ""string""
            nullable = true
        }
    }

    services = [""ogc_api""]
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        var layer = config.Layers["sensors"];
        Assert.False(layer.IntrospectFields);
        Assert.NotNull(layer.Fields);
        Assert.Equal(3, layer.Fields.Count);

        var sensorIdField = layer.Fields["sensor_id"];
        Assert.Equal("int", sensorIdField.Type);
        Assert.False(sensorIdField.Nullable);

        var modelField = layer.Fields["model"];
        Assert.Equal("string", modelField.Type);
        Assert.True(modelField.Nullable);
    }

    [Fact]
    public void Parse_CacheBlock_Success()
    {
        // Arrange
        var hcl = @"
cache ""redis"" {
    type = ""redis""
    enabled = true
    connection = ""localhost:6379""
    required_in = [""production"", ""staging""]
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.Single(config.Caches);
        var cache = config.Caches["redis"];
        Assert.Equal("redis", cache.Id);
        Assert.Equal("redis", cache.Type);
        Assert.True(cache.Enabled);
        Assert.Equal("localhost:6379", cache.Connection);
        Assert.Equal(2, cache.RequiredIn.Count);
        Assert.Contains("production", cache.RequiredIn);
        Assert.Contains("staging", cache.RequiredIn);
    }

    [Fact]
    public void Parse_RateLimitBlock_Success()
    {
        // Arrange
        var hcl = @"
rate_limit {
    enabled = true
    store = ""redis""

    rules = {
        default = {
            requests = 1000
            window = ""1m""
        }

        authenticated = {
            requests = 5000
            window = ""1m""
        }
    }
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.NotNull(config.RateLimit);
        Assert.True(config.RateLimit.Enabled);
        Assert.Equal("redis", config.RateLimit.Store);
        Assert.Equal(2, config.RateLimit.Rules.Count);

        var defaultRule = config.RateLimit.Rules["default"];
        Assert.Equal(1000, defaultRule.Requests);
        Assert.Equal("1m", defaultRule.Window);

        var authRule = config.RateLimit.Rules["authenticated"];
        Assert.Equal(5000, authRule.Requests);
    }

    [Fact]
    public void Parse_CompleteConfiguration_Success()
    {
        // Arrange
        var hcl = @"
honua {
    version = ""1.0""
    environment = ""development""
    log_level = ""information""

    cors = {
        allow_any_origin = false
        allowed_origins = [""http://localhost:3000""]
    }
}

data_source ""sqlite-test"" {
    provider = ""sqlite""
    connection = ""Data Source=./data/test.db""
    health_check = ""SELECT 1""
}

service ""odata"" {
    enabled = true
    allow_writes = true
    max_page_size = 1000
}

service ""ogc_api"" {
    enabled = true
}

layer ""roads_primary"" {
    title = ""Primary Roads""
    data_source = ""sqlite-test""
    table = ""roads_primary""

    geometry = {
        column = ""geom""
        type = ""LineString""
        srid = 4326
    }

    id_field = ""road_id""
    display_field = ""name""
    introspect_fields = true

    services = [""odata"", ""ogc_api""]
}

cache ""redis"" {
    type = ""redis""
    enabled = false
    connection = ""localhost:6379""
}

rate_limit {
    enabled = true
    store = ""memory""

    rules = {
        default = {
            requests = 1000
            window = ""1m""
        }
    }
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
        Assert.Single(config.DataSources);
        Assert.Equal(2, config.Services.Count);
        Assert.Single(config.Layers);
        Assert.Single(config.Caches);
        Assert.NotNull(config.RateLimit);
    }

    [Fact]
    public void Parse_WithLineComments_Success()
    {
        // Arrange
        var hcl = @"
# This is a comment
honua {
    version = ""1.0""  # Inline comment
    environment = ""development""
}

// This is also a comment
data_source ""test"" {
    provider = ""sqlite""  // Another inline comment
    connection = ""Data Source=:memory:""
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.NotNull(config);
        Assert.Single(config.DataSources);
    }

    [Fact]
    public void Parse_WithBlockComments_Success()
    {
        // Arrange
        var hcl = @"
/*
 * Multi-line block comment
 * describing the configuration
 */
honua {
    version = ""1.0""
    environment = ""development""
}
";

        var parser = new HclParser();

        // Act
        var config = parser.Parse(hcl);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("1.0", config.Honua.Version);
    }

    [Fact]
    public void Parse_EmptyString_ThrowsException()
    {
        // Arrange
        var parser = new HclParser();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => parser.Parse(""));
    }

    [Fact]
    public void Parse_InvalidSyntax_ThrowsParseException()
    {
        // Arrange
        var hcl = @"
honua {
    version = ""1.0""
    invalid_syntax_here
}
";

        var parser = new HclParser();

        // Act & Assert
        Assert.Throws<ParseException>(() => parser.Parse(hcl));
    }

    [Fact]
    public void Parse_UnterminatedString_ThrowsParseException()
    {
        // Arrange
        var hcl = @"
honua {
    version = ""unterminated
}
";

        var parser = new HclParser();

        // Act & Assert
        Assert.Throws<ParseException>(() => parser.Parse(hcl));
    }

    [Fact]
    public void Parse_UnterminatedBlock_ThrowsParseException()
    {
        // Arrange
        var hcl = @"
honua {
    version = ""1.0""
    # Missing closing brace
";

        var parser = new HclParser();

        // Act & Assert
        Assert.Throws<ParseException>(() => parser.Parse(hcl));
    }
}
