// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data.ConnectionTesting;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Security;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Honua.Server.Enterprise.Data.ConnectionTesting;

/// <summary>
/// Connection tester for MongoDB databases.
/// Tests connectivity and retrieves version and cluster information.
/// </summary>
public sealed class MongoDbConnectionTester : IConnectionTester
{
    private readonly IConnectionStringEncryptionService? _encryptionService;
    private readonly ILogger<MongoDbConnectionTester> _logger;

    public MongoDbConnectionTester(
        IConnectionStringEncryptionService? encryptionService = null,
        ILogger<MongoDbConnectionTester>? logger = null)
    {
        _encryptionService = encryptionService;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MongoDbConnectionTester>.Instance;
    }

    public string ProviderType => "mongodb";

    public async Task<ConnectionTestResult> TestConnectionAsync(
        DataSourceDefinition dataSource,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(dataSource.ConnectionString))
            {
                return new ConnectionTestResult
                {
                    Success = false,
                    Message = "Connection string is empty",
                    ErrorDetails = "Data source configuration is missing a connection string",
                    ErrorType = "configuration",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Decrypt connection string if needed
            var connectionString = _encryptionService?.DecryptConnectionString(dataSource.ConnectionString)
                ?? dataSource.ConnectionString;

            // Create MongoDB client with timeout settings
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            settings.ConnectTimeout = TimeSpan.FromSeconds(10);

            var client = new MongoClient(settings);

            // Test connection by pinging the server
            var adminDb = client.GetDatabase("admin");
            var pingCommand = new BsonDocument("ping", 1);
            await adminDb.RunCommandAsync<BsonDocument>(pingCommand, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Get server version and build info
            var buildInfoCommand = new BsonDocument("buildInfo", 1);
            var buildInfo = await adminDb.RunCommandAsync<BsonDocument>(buildInfoCommand, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var version = buildInfo.GetValue("version", "unknown").ToString();
            var gitVersion = buildInfo.GetValue("gitVersion", "").ToString();

            // Get cluster description for additional info
            var cluster = client.Cluster;
            var description = cluster.Description;

            stopwatch.Stop();

            var metadata = new Dictionary<string, object?>
            {
                ["version"] = version,
                ["clusterType"] = description.Type.ToString(),
                ["serverCount"] = description.Servers.Count
            };

            if (!string.IsNullOrEmpty(gitVersion))
            {
                metadata["gitVersion"] = gitVersion;
            }

            // Add database name if available from connection string
            var mongoUrl = MongoUrl.Create(connectionString);
            if (!string.IsNullOrEmpty(mongoUrl.DatabaseName))
            {
                metadata["database"] = mongoUrl.DatabaseName;
            }

            // Add replica set name if applicable
            if (description.Type == MongoDB.Driver.Core.Clusters.ClusterType.ReplicaSet)
            {
                metadata["replicaSetName"] = description.Servers.FirstOrDefault()?.ReplicaSetConfig?.Name ?? "unknown";
            }

            return new ConnectionTestResult
            {
                Success = true,
                Message = description.Type == MongoDB.Driver.Core.Clusters.ClusterType.ReplicaSet
                    ? "Successfully connected to MongoDB replica set"
                    : "Successfully connected to MongoDB database",
                ResponseTime = stopwatch.Elapsed,
                Metadata = metadata
            };
        }
        catch (MongoAuthenticationException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "MongoDB authentication failed for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Authentication failed",
                ErrorDetails = $"MongoDB authentication failed: {ex.Message}. Check username and password.",
                ErrorType = "authentication",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (MongoConnectionException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "MongoDB connection failed for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection failed",
                ErrorDetails = $"Could not connect to MongoDB server: {ex.Message}. Check host, port, and network connectivity.",
                ErrorType = "network",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "MongoDB connection test timed out for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection attempt timed out",
                ErrorDetails = "The MongoDB server did not respond within the timeout period. Check network connectivity and server availability.",
                ErrorType = "timeout",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection test was cancelled",
                ErrorDetails = "The connection test was cancelled before completion",
                ErrorType = "cancelled",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (MongoConfigurationException ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex,
                "MongoDB configuration error for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Configuration error",
                ErrorDetails = $"MongoDB configuration error: {ex.Message}. Check the connection string format.",
                ErrorType = "configuration",
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Unexpected error during MongoDB connection test for data source {DataSourceId}",
                dataSource.Id);

            return new ConnectionTestResult
            {
                Success = false,
                Message = "Connection test failed with unexpected error",
                ErrorDetails = ex.Message,
                ErrorType = "unknown",
                ResponseTime = stopwatch.Elapsed
            };
        }
    }
}
