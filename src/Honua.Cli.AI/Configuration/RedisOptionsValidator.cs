// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Configuration;

/// <summary>
/// Validates Redis configuration options.
/// </summary>
public sealed class RedisOptionsValidator : IValidateOptions<RedisOptions>
{
    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        var failures = new List<string>();

        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        // Validate connection string
        if (options.ConnectionString.IsNullOrWhiteSpace())
        {
            failures.Add("Redis ConnectionString is required when Redis is enabled. Set 'Redis:ConnectionString'. Example: 'localhost:6379' or 'localhost:6379,password=secret,ssl=true'");
        }
        else if (!IsValidRedisConnectionString(options.ConnectionString))
        {
            failures.Add($"Redis ConnectionString '{options.ConnectionString}' is invalid. Expected format: 'host:port[,password=xxx][,ssl=true]'. Example: 'localhost:6379' or 'redis.example.com:6380,password=secret,ssl=true'");
        }

        // Validate key prefix
        if (options.KeyPrefix.IsNullOrWhiteSpace())
        {
            failures.Add("Redis KeyPrefix cannot be empty. Set 'Redis:KeyPrefix'. Example: 'honua:process:'");
        }

        // Validate TTL
        if (options.TtlSeconds <= 0)
        {
            failures.Add($"Redis TtlSeconds must be > 0. Current: {options.TtlSeconds}. Set 'Redis:TtlSeconds'.");
        }

        if (options.TtlSeconds > 2_592_000) // 30 days
        {
            failures.Add($"Redis TtlSeconds ({options.TtlSeconds}) exceeds recommended limit of 2592000 (30 days). This can cause memory bloat. Reduce 'Redis:TtlSeconds'.");
        }

        // Validate timeouts
        if (options.ConnectTimeoutMs <= 0)
        {
            failures.Add($"Redis ConnectTimeoutMs must be > 0. Current: {options.ConnectTimeoutMs}. Set 'Redis:ConnectTimeoutMs'.");
        }

        if (options.ConnectTimeoutMs > 60000)
        {
            failures.Add($"Redis ConnectTimeoutMs ({options.ConnectTimeoutMs}) exceeds recommended limit of 60000 (60 seconds). Long timeouts can cause application hangs. Reduce 'Redis:ConnectTimeoutMs'.");
        }

        if (options.SyncTimeoutMs <= 0)
        {
            failures.Add($"Redis SyncTimeoutMs must be > 0. Current: {options.SyncTimeoutMs}. Set 'Redis:SyncTimeoutMs'.");
        }

        if (options.SyncTimeoutMs > 30000)
        {
            failures.Add($"Redis SyncTimeoutMs ({options.SyncTimeoutMs}) exceeds recommended limit of 30000 (30 seconds). Long sync timeouts can cause performance issues. Reduce 'Redis:SyncTimeoutMs'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static bool IsValidRedisConnectionString(string connectionString)
    {
        if (connectionString.IsNullOrWhiteSpace())
            return false;

        // Split by comma to get connection parameters
        var parts = connectionString.Split(',');
        if (parts.Length == 0)
            return false;

        // First part should be host:port
        var hostPort = parts[0].Trim();
        if (!hostPort.Contains(':'))
            return false;

        // Validate port is numeric
        var portPart = hostPort.Split(':')[^1];
        if (!int.TryParse(portPart, out var port) || port <= 0 || port > 65535)
            return false;

        return true;
    }
}
