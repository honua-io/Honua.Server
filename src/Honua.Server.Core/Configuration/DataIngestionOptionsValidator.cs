// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates data ingestion configuration options.
/// </summary>
public sealed class DataIngestionOptionsValidator : IValidateOptions<DataIngestionOptions>
{
    public ValidateOptionsResult Validate(string? name, DataIngestionOptions options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("DataIngestionOptions cannot be null");
        }

        var failures = new List<string>();

        // Validate batch size
        if (options.BatchSize <= 0)
        {
            failures.Add($"DataIngestion BatchSize must be positive. Current: {options.BatchSize}. Set 'Honua:DataIngestion:BatchSize' to a value greater than 0. Example: 1000");
        }
        else if (options.BatchSize > 100000)
        {
            failures.Add($"DataIngestion BatchSize ({options.BatchSize}) exceeds recommended maximum of 100,000. Large batches can cause memory exhaustion. Reduce 'Honua:DataIngestion:BatchSize'.");
        }

        // Validate progress report interval
        if (options.ProgressReportInterval <= 0)
        {
            failures.Add($"DataIngestion ProgressReportInterval must be positive. Current: {options.ProgressReportInterval}. Set 'Honua:DataIngestion:ProgressReportInterval' to a value greater than 0.");
        }
        else if (options.ProgressReportInterval > options.BatchSize)
        {
            failures.Add($"DataIngestion ProgressReportInterval ({options.ProgressReportInterval}) should not exceed BatchSize ({options.BatchSize}). Set 'Honua:DataIngestion:ProgressReportInterval' to a value <= BatchSize.");
        }

        // Validate max retries
        if (options.MaxRetries < 0)
        {
            failures.Add($"DataIngestion MaxRetries cannot be negative. Current: {options.MaxRetries}. Set 'Honua:DataIngestion:MaxRetries' to 0 (no retries) or positive value.");
        }
        else if (options.MaxRetries > 10)
        {
            failures.Add($"DataIngestion MaxRetries ({options.MaxRetries}) exceeds recommended maximum of 10. Excessive retries can cause long delays. Reduce 'Honua:DataIngestion:MaxRetries'.");
        }

        // Validate batch timeout
        if (options.BatchTimeout <= TimeSpan.Zero)
        {
            failures.Add($"DataIngestion BatchTimeout must be positive. Current: {options.BatchTimeout}. Set 'Honua:DataIngestion:BatchTimeout' to a positive TimeSpan. Example: '00:05:00' (5 minutes)");
        }
        else if (options.BatchTimeout > TimeSpan.FromHours(1))
        {
            failures.Add($"DataIngestion BatchTimeout ({options.BatchTimeout}) exceeds recommended maximum of 1 hour. Long timeouts can cause resource exhaustion. Reduce 'Honua:DataIngestion:BatchTimeout'.");
        }

        // Validate transaction timeout
        if (options.TransactionTimeout <= TimeSpan.Zero)
        {
            failures.Add($"DataIngestion TransactionTimeout must be positive. Current: {options.TransactionTimeout}. Set 'Honua:DataIngestion:TransactionTimeout' to a positive TimeSpan. Example: '00:30:00' (30 minutes)");
        }
        else if (options.TransactionTimeout > TimeSpan.FromHours(4))
        {
            failures.Add($"DataIngestion TransactionTimeout ({options.TransactionTimeout}) exceeds recommended maximum of 4 hours. Long transactions can lock tables. Reduce 'Honua:DataIngestion:TransactionTimeout'.");
        }

        // Validate transaction timeout is greater than batch timeout
        if (options.UseTransactionalIngestion && options.TransactionTimeout < options.BatchTimeout)
        {
            failures.Add($"DataIngestion TransactionTimeout ({options.TransactionTimeout}) should be greater than BatchTimeout ({options.BatchTimeout}) when using transactional ingestion. Adjust 'Honua:DataIngestion:TransactionTimeout'.");
        }

        // Validate isolation level
        if (!Enum.IsDefined(typeof(IsolationLevel), options.TransactionIsolationLevel))
        {
            failures.Add($"DataIngestion TransactionIsolationLevel '{options.TransactionIsolationLevel}' is invalid. Valid values: ReadCommitted, RepeatableRead, Serializable. Set 'Honua:DataIngestion:TransactionIsolationLevel'.");
        }

        // Validate geometry options
        if (options.MaxGeometryCoordinates <= 0)
        {
            failures.Add($"DataIngestion MaxGeometryCoordinates must be positive. Current: {options.MaxGeometryCoordinates}. Set 'Honua:DataIngestion:MaxGeometryCoordinates' to a value greater than 0.");
        }
        else if (options.MaxGeometryCoordinates > 10_000_000)
        {
            failures.Add($"DataIngestion MaxGeometryCoordinates ({options.MaxGeometryCoordinates}) exceeds recommended maximum of 10,000,000. Large geometries can cause memory issues. Reduce 'Honua:DataIngestion:MaxGeometryCoordinates'.");
        }

        // Validate schema validation options
        if (options.MaxValidationErrors <= 0)
        {
            failures.Add($"DataIngestion MaxValidationErrors must be positive. Current: {options.MaxValidationErrors}. Set 'Honua:DataIngestion:MaxValidationErrors' to a value greater than 0.");
        }
        else if (options.MaxValidationErrors > 10000)
        {
            failures.Add($"DataIngestion MaxValidationErrors ({options.MaxValidationErrors}) exceeds recommended maximum of 10,000. Large error collections can consume memory. Reduce 'Honua:DataIngestion:MaxValidationErrors'.");
        }

        // Validate logical constraints
        if (!options.ValidateGeometry && options.RejectInvalidGeometries)
        {
            failures.Add("DataIngestion RejectInvalidGeometries requires ValidateGeometry to be enabled. Set 'Honua:DataIngestion:ValidateGeometry' to true or disable 'Honua:DataIngestion:RejectInvalidGeometries'.");
        }

        if (!options.ValidateGeometry && options.AutoRepairGeometries)
        {
            failures.Add("DataIngestion AutoRepairGeometries requires ValidateGeometry to be enabled. Set 'Honua:DataIngestion:ValidateGeometry' to true or disable 'Honua:DataIngestion:AutoRepairGeometries'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
