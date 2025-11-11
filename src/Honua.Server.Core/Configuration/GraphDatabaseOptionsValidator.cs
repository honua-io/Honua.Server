// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates graph database configuration options.
/// </summary>
public sealed class GraphDatabaseOptionsValidator : IValidateOptions<GraphDatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, GraphDatabaseOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("GraphDatabaseOptions cannot be null");
        }

        var failures = new List<string>();

        // Skip validation if graph database is disabled
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        // Validate graph name
        if (string.IsNullOrWhiteSpace(options.DefaultGraphName))
        {
            failures.Add("GraphDatabase DefaultGraphName is required when GraphDatabase is enabled. Set 'GraphDatabase:DefaultGraphName'. Example: 'honua_graph'");
        }
        else if (options.DefaultGraphName.Length > 63)
        {
            failures.Add($"GraphDatabase DefaultGraphName '{options.DefaultGraphName}' exceeds maximum length of 63 characters. Set 'GraphDatabase:DefaultGraphName' to a shorter name.");
        }
        else if (!System.Text.RegularExpressions.Regex.IsMatch(options.DefaultGraphName, @"^[a-z][a-z0-9_]*$"))
        {
            failures.Add($"GraphDatabase DefaultGraphName '{options.DefaultGraphName}' is invalid. Must start with lowercase letter and contain only lowercase letters, digits, and underscores. Set 'GraphDatabase:DefaultGraphName'. Example: 'honua_graph'");
        }

        // Validate command timeout
        if (options.CommandTimeoutSeconds <= 0)
        {
            failures.Add($"GraphDatabase CommandTimeoutSeconds must be positive. Current: {options.CommandTimeoutSeconds}. Set 'GraphDatabase:CommandTimeoutSeconds' to a value greater than 0. Example: 30");
        }
        else if (options.CommandTimeoutSeconds > 3600)
        {
            failures.Add($"GraphDatabase CommandTimeoutSeconds ({options.CommandTimeoutSeconds}) exceeds maximum of 3600 seconds (1 hour). Long timeouts can cause resource exhaustion. Reduce 'GraphDatabase:CommandTimeoutSeconds'.");
        }

        // Validate retry attempts
        if (options.MaxRetryAttempts < 0)
        {
            failures.Add($"GraphDatabase MaxRetryAttempts cannot be negative. Current: {options.MaxRetryAttempts}. Set 'GraphDatabase:MaxRetryAttempts' to 0 (no retries) or positive value.");
        }
        else if (options.MaxRetryAttempts > 10)
        {
            failures.Add($"GraphDatabase MaxRetryAttempts ({options.MaxRetryAttempts}) exceeds maximum of 10. Excessive retries can cause cascading failures. Reduce 'GraphDatabase:MaxRetryAttempts'.");
        }

        // Validate cache expiration
        if (options.EnableQueryCache)
        {
            if (options.QueryCacheExpirationMinutes <= 0)
            {
                failures.Add($"GraphDatabase QueryCacheExpirationMinutes must be positive when query cache is enabled. Current: {options.QueryCacheExpirationMinutes}. Set 'GraphDatabase:QueryCacheExpirationMinutes' to a value greater than 0.");
            }
            else if (options.QueryCacheExpirationMinutes > 1440)
            {
                failures.Add($"GraphDatabase QueryCacheExpirationMinutes ({options.QueryCacheExpirationMinutes}) exceeds maximum of 1440 minutes (24 hours). Long cache times can cause stale data issues. Reduce 'GraphDatabase:QueryCacheExpirationMinutes'.");
            }
        }

        // Validate traversal depth
        if (options.MaxTraversalDepth <= 0)
        {
            failures.Add($"GraphDatabase MaxTraversalDepth must be positive. Current: {options.MaxTraversalDepth}. Set 'GraphDatabase:MaxTraversalDepth' to a value greater than 0. Example: 10");
        }
        else if (options.MaxTraversalDepth > 100)
        {
            failures.Add($"GraphDatabase MaxTraversalDepth ({options.MaxTraversalDepth}) exceeds maximum of 100. Deep traversals can cause performance issues and infinite loops. Reduce 'GraphDatabase:MaxTraversalDepth'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
