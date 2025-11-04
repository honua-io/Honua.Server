// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Centralized service for logging and validating configuration changes during hot reload.
/// Provides thread-safe configuration change tracking, validation, and rollback support.
/// </summary>
public sealed class ConfigurationChangeNotificationService
{
    private readonly ILogger<ConfigurationChangeNotificationService> _logger;
    private readonly ConcurrentDictionary<string, ConfigurationSnapshot> _previousConfigurations;
    private readonly ConcurrentDictionary<string, int> _changeCounters;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationChangeNotificationService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public ConfigurationChangeNotificationService(ILogger<ConfigurationChangeNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _previousConfigurations = new ConcurrentDictionary<string, ConfigurationSnapshot>();
        _changeCounters = new ConcurrentDictionary<string, int>();
    }

    /// <summary>
    /// Validates configuration options using data annotations.
    /// </summary>
    /// <typeparam name="TOptions">Type of options to validate.</typeparam>
    /// <param name="options">Options instance to validate.</param>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <returns>Validation result.</returns>
    public ValidationResult ValidateConfiguration<TOptions>(TOptions options, string configurationName)
        where TOptions : class
    {
        if (options == null)
        {
            return new ValidationResult($"Configuration '{configurationName}' is null");
        }

        var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var validationContext = new ValidationContext(options);

        if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
        {
            var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
            _logger.LogError(
                "Configuration validation failed for {Configuration}: {Errors}",
                configurationName,
                errors);

            return new ValidationResult(false, errors);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Logs configuration change and stores snapshot for rollback.
    /// </summary>
    /// <typeparam name="TOptions">Type of options.</typeparam>
    /// <param name="options">New configuration options.</param>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <param name="changes">Dictionary of changed properties and their new values.</param>
    public void NotifyConfigurationChange<TOptions>(
        TOptions options,
        string configurationName,
        Dictionary<string, object?> changes)
        where TOptions : class
    {
        var changeCount = _changeCounters.AddOrUpdate(configurationName, 1, (_, count) => count + 1);

        _logger.LogInformation(
            "Configuration hot reload detected for {Configuration} (reload #{Count}). Changes: {Changes}",
            configurationName,
            changeCount,
            string.Join(", ", changes.Select(kvp => $"{kvp.Key}={kvp.Value}")));

        // Store snapshot for potential rollback
        var snapshot = new ConfigurationSnapshot
        {
            Timestamp = DateTimeOffset.UtcNow,
            ConfigurationName = configurationName,
            Configuration = options,
            ChangeCount = changeCount
        };

        _previousConfigurations.AddOrUpdate(configurationName, snapshot, (_, _) => snapshot);
    }

    /// <summary>
    /// Gets the previous configuration snapshot for rollback.
    /// </summary>
    /// <typeparam name="TOptions">Type of options.</typeparam>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <returns>Previous configuration snapshot, or null if not available.</returns>
    public TOptions? GetPreviousConfiguration<TOptions>(string configurationName)
        where TOptions : class
    {
        if (_previousConfigurations.TryGetValue(configurationName, out var snapshot))
        {
            return snapshot.Configuration as TOptions;
        }

        return null;
    }

    /// <summary>
    /// Gets the number of times a configuration has been reloaded.
    /// </summary>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <returns>Number of reloads.</returns>
    public int GetReloadCount(string configurationName)
    {
        return _changeCounters.TryGetValue(configurationName, out var count) ? count : 0;
    }

    /// <summary>
    /// Logs a validation error for configuration hot reload.
    /// </summary>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <param name="validationResult">Validation result containing errors.</param>
    public void NotifyValidationFailure(string configurationName, ValidationResult validationResult)
    {
        _logger.LogError(
            "Configuration hot reload validation failed for {Configuration}. Using previous configuration. Errors: {Errors}",
            configurationName,
            validationResult.ErrorMessage);
    }

    /// <summary>
    /// Logs successful application of configuration change.
    /// </summary>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <param name="appliedChanges">Description of what was changed.</param>
    public void NotifyChangeApplied(string configurationName, string appliedChanges)
    {
        _logger.LogInformation(
            "Configuration change successfully applied for {Configuration}: {Changes}",
            configurationName,
            appliedChanges);
    }

    /// <summary>
    /// Logs rollback of configuration change.
    /// </summary>
    /// <param name="configurationName">Name of the configuration section.</param>
    /// <param name="reason">Reason for rollback.</param>
    public void NotifyRollback(string configurationName, string reason)
    {
        _logger.LogWarning(
            "Configuration rolled back for {Configuration}. Reason: {Reason}",
            configurationName,
            reason);
    }

    private sealed class ConfigurationSnapshot
    {
        public required DateTimeOffset Timestamp { get; init; }
        public required string ConfigurationName { get; init; }
        public required object Configuration { get; init; }
        public required int ChangeCount { get; init; }
    }
}

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether validation succeeded.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets a successful validation result.
    /// </summary>
    public static ValidationResult Success { get; } = new ValidationResult(true, null);

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class.
    /// </summary>
    /// <param name="isValid">Whether validation succeeded.</param>
    /// <param name="errorMessage">Error message if validation failed.</param>
    public ValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationResult"/> class for a failure.
    /// </summary>
    /// <param name="errorMessage">Error message.</param>
    public ValidationResult(string errorMessage)
        : this(false, errorMessage)
    {
    }
}
