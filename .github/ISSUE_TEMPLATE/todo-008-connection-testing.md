---
name: 'TODO-008: Implement Connection Testing for Data Sources'
about: Implement actual connection testing for various data source providers
title: '[P1] Implement Connection Testing for Data Sources'
labels: ['priority: high', 'feature', 'data-sources', 'todo-cleanup']
assignees: []
---

## Summary

The connection test endpoint currently returns stub responses instead of actually testing connections to data sources. Need provider-specific implementations for PostgreSQL, MySQL, SQL Server, Oracle, MongoDB, CosmosDB, Elasticsearch, and BigQuery.

**Priority:** P1 - High (Core Feature)
**Effort:** Large (1-2 weeks)
**Sprint Target:** Sprint 2-3

## Context

### Files Affected

- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/MetadataAdministrationEndpoints.cs:1385`

### Current Implementation

```csharp
// Line 1385
public static async Task<IResult> TestDataSourceConnection(
    string id,
    IServiceMetadataStore metadataStore,
    ILogger<MetadataAdministrationEndpoints> logger)
{
    var service = await metadataStore.GetServiceByIdAsync(id);

    if (service == null)
    {
        return Results.NotFound(new { error = "Service not found" });
    }

    // TODO: Implement actual connection test based on provider
    return Results.Ok(new ConnectionTestResponse
    {
        Success = true,
        Message = "Connection test successful (stub)",
        ResponseTime = TimeSpan.FromMilliseconds(50)
    });
}
```

### Problem

- Connection tests always succeed (stub implementation)
- Cannot verify data source configurations are correct
- Users cannot diagnose connection issues
- No provider-specific connection testing logic

## Expected Behavior

### 1. Define Provider-Specific Connection Testers

```csharp
public interface IDataSourceConnectionTester
{
    /// <summary>
    /// Provider type this tester supports.
    /// </summary>
    string ProviderType { get; }

    /// <summary>
    /// Tests connection to the data source.
    /// </summary>
    Task<ConnectionTestResult> TestConnectionAsync(
        ServiceMetadata service,
        CancellationToken cancellationToken = default);
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
```

### 2. Implement PostgreSQL Connection Tester

```csharp
public class PostgresConnectionTester : IDataSourceConnectionTester
{
    private readonly ILogger<PostgresConnectionTester> _logger;

    public string ProviderType => "postgres";

    public PostgresConnectionTester(ILogger<PostgresConnectionTester> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(
        ServiceMetadata service,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var connectionString = service.ConnectionString;

            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Test query
            await using var command = new NpgsqlCommand("SELECT version();", connection);
            var version = await command.ExecuteScalarAsync(cancellationToken) as string;

            // Check PostGIS extension
            await using var postgisCmd = new NpgsqlCommand(
                "SELECT PostGIS_Version();",
                connection);

            string? postgisVersion = null;
            try
            {
                postgisVersion = await postgisCmd.ExecuteScalarAsync(cancellationToken) as string;
            }
            catch
            {
                // PostGIS not installed
            }

            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Success = true,
                Message = "Successfully connected to PostgreSQL database",
                ResponseTime = stopwatch.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    ["version"] = version ?? "unknown",
                    ["postgis_version"] = postgisVersion ?? "not installed",
                    ["database"] = connection.Database,
                    ["host"] = connection.Host,
                    ["port"] = connection.Port
                }
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "PostgreSQL connection test failed for service {ServiceId}", service.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection failed",
                ErrorDetails = ex.Message,
                ResponseTime = stopwatch.Elapsed
            };
        }
    }
}
```

### 3. Implement Additional Testers

Create similar implementations for:

- **MySqlConnectionTester** - MySQL/MariaDB
- **SqlServerConnectionTester** - SQL Server
- **OracleConnectionTester** - Oracle
- **MongoDbConnectionTester** - MongoDB
- **CosmosDbConnectionTester** - Azure CosmosDB
- **ElasticsearchConnectionTester** - Elasticsearch
- **BigQueryConnectionTester** - Google BigQuery

### 4. Create Connection Tester Factory

```csharp
public class DataSourceConnectionTesterFactory
{
    private readonly IEnumerable<IDataSourceConnectionTester> _testers;

    public DataSourceConnectionTesterFactory(IEnumerable<IDataSourceConnectionTester> testers)
    {
        _testers = testers;
    }

    public IDataSourceConnectionTester? GetTester(string providerType)
    {
        return _testers.FirstOrDefault(t =>
            t.ProviderType.Equals(providerType, StringComparison.OrdinalIgnoreCase));
    }
}
```

### 5. Update Endpoint

```csharp
public static async Task<IResult> TestDataSourceConnection(
    string id,
    IServiceMetadataStore metadataStore,
    DataSourceConnectionTesterFactory testerFactory,
    ILogger<MetadataAdministrationEndpoints> logger)
{
    try
    {
        var service = await metadataStore.GetServiceByIdAsync(id);

        if (service == null)
        {
            return Results.NotFound(new { error = "Service not found" });
        }

        var tester = testerFactory.GetTester(service.Type);

        if (tester == null)
        {
            return Results.BadRequest(new
            {
                error = "unsupported_provider",
                message = $"Connection testing not supported for provider type '{service.Type}'"
            });
        }

        logger.LogInformation(
            "Testing connection to data source {ServiceId} ({ServiceType})",
            id,
            service.Type);

        var result = await tester.TestConnectionAsync(
            service,
            CancellationToken.None);

        if (result.Success)
        {
            logger.LogInformation(
                "Connection test succeeded for {ServiceId} in {Duration}ms",
                id,
                result.ResponseTime.TotalMilliseconds);
        }
        else
        {
            logger.LogWarning(
                "Connection test failed for {ServiceId}: {Error}",
                id,
                result.ErrorDetails);
        }

        return Results.Ok(new ConnectionTestResponse
        {
            Success = result.Success,
            Message = result.Message,
            ErrorDetails = result.ErrorDetails,
            ResponseTime = result.ResponseTime,
            Metadata = result.Metadata
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Connection test failed for service {ServiceId}", id);
        return Results.Problem(
            title: "Connection test failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}
```

### 6. Register Services

```csharp
// ServiceCollectionExtensions.cs
services.AddSingleton<IDataSourceConnectionTester, PostgresConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, MySqlConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, SqlServerConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, OracleConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, MongoDbConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, CosmosDbConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, ElasticsearchConnectionTester>();
services.AddSingleton<IDataSourceConnectionTester, BigQueryConnectionTester>();

services.AddSingleton<DataSourceConnectionTesterFactory>();
```

## Acceptance Criteria

- [ ] Connection testers implemented for all supported providers
- [ ] Actual connections are tested (not stub responses)
- [ ] Connection errors include detailed error messages
- [ ] Response includes connection metadata (version, host, database name)
- [ ] Response time is measured accurately
- [ ] Connection timeouts are handled gracefully
- [ ] Credentials errors return appropriate error messages
- [ ] Network errors are distinguished from authentication errors
- [ ] Unit tests for each connection tester
- [ ] Integration tests with test databases

## Provider-Specific Checklist

### PostgreSQL ✅
- [ ] Connection string parsing
- [ ] PostgreSQL version detection
- [ ] PostGIS extension detection
- [ ] Database name, host, port extraction

### MySQL
- [ ] Connection string parsing
- [ ] MySQL/MariaDB version detection
- [ ] Spatial extension detection
- [ ] Database name, host, port extraction

### SQL Server
- [ ] Connection string parsing
- [ ] SQL Server version detection
- [ ] Spatial type support detection
- [ ] Database name, server extraction

### Oracle
- [ ] Connection string parsing
- [ ] Oracle version detection
- [ ] Spatial (SDO_GEOMETRY) support detection
- [ ] Database SID/service name extraction

### MongoDB
- [ ] Connection string parsing
- [ ] MongoDB version detection
- [ ] Database name, cluster info
- [ ] Authentication mechanism

### CosmosDB
- [ ] Connection string parsing
- [ ] Cosmos DB API version
- [ ] Account endpoint, database name
- [ ] Throughput/RU information

### Elasticsearch
- [ ] Connection URL parsing
- [ ] Elasticsearch version detection
- [ ] Cluster health status
- [ ] Index statistics

### BigQuery
- [ ] Project ID, dataset ID extraction
- [ ] BigQuery API connection test
- [ ] GCP authentication validation
- [ ] Dataset metadata

## Testing Checklist

### Unit Tests

```csharp
[Fact]
public async Task PostgresConnectionTester_WithValidConnection_ReturnsSuccess()
{
    // Arrange
    var service = new ServiceMetadata
    {
        Id = "test-db",
        Type = "postgres",
        ConnectionString = "Host=localhost;Database=testdb;Username=test;Password=test"
    };

    var tester = new PostgresConnectionTester(Mock.Of<ILogger<PostgresConnectionTester>>());

    // Act
    var result = await tester.TestConnectionAsync(service);

    // Assert
    Assert.True(result.Success);
    Assert.Contains("PostgreSQL", result.Message);
    Assert.True(result.ResponseTime > TimeSpan.Zero);
}

[Fact]
public async Task PostgresConnectionTester_WithInvalidCredentials_ReturnsFailure()
{
    // Arrange
    var service = new ServiceMetadata
    {
        Id = "test-db",
        Type = "postgres",
        ConnectionString = "Host=localhost;Database=testdb;Username=invalid;Password=wrong"
    };

    var tester = new PostgresConnectionTester(Mock.Of<ILogger<PostgresConnectionTester>>());

    // Act
    var result = await tester.TestConnectionAsync(service);

    // Assert
    Assert.False(result.Success);
    Assert.NotNull(result.ErrorDetails);
    Assert.Contains("password", result.ErrorDetails, StringComparison.OrdinalIgnoreCase);
}
```

## Related Files

- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/MetadataAdministrationEndpoints.cs:1385`
- Data store providers (for connection string formats)

## Related Issues

- #TBD-009 - Implement Table Discovery for Data Sources

## References

- [Npgsql (PostgreSQL)](https://www.npgsql.org/)
- [MySql.Data](https://dev.mysql.com/doc/connector-net/en/)
- [Microsoft.Data.SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/introduction-microsoft-data-sqlclient-namespace)
- [MongoDB.Driver](https://www.mongodb.com/docs/drivers/csharp/current/)
- [Azure.Cosmos](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/sdk-dotnet-v3)
- [NEST (Elasticsearch)](https://www.elastic.co/guide/en/elasticsearch/client/net-api/current/index.html)
- [Google.Cloud.BigQuery.V2](https://cloud.google.com/bigquery/docs/reference/libraries)
