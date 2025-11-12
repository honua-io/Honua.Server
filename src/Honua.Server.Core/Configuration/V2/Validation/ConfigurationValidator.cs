// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2.Validation;

/// <summary>
/// Composite validator that orchestrates syntax, semantic, and runtime validation.
/// </summary>
public sealed class ConfigurationValidator : IConfigurationValidator
{
    private readonly ValidationOptions _options;
    private readonly IDbConnectionFactory? _connectionFactory;

    public ConfigurationValidator(ValidationOptions? options = null, IDbConnectionFactory? connectionFactory = null)
    {
        _options = options ?? ValidationOptions.Default;
        _connectionFactory = connectionFactory;
    }

    public async Task<ValidationResult> ValidateAsync(HonuaConfig config, CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (config == null)
        {
            result.AddError("Configuration cannot be null");
            return result;
        }

        // Phase 1: Syntax Validation
        if (_options.ValidateSyntax)
        {
            var syntaxValidator = new SyntaxValidator();
            var syntaxResult = await syntaxValidator.ValidateAsync(config, cancellationToken);
            result.Merge(syntaxResult);

            // Stop if syntax validation failed
            if (!syntaxResult.IsValid)
            {
                result.AddError("Syntax validation failed. Fix syntax errors before proceeding to semantic validation.");
                return result;
            }
        }

        // Phase 2: Semantic Validation
        if (_options.ValidateSemantics)
        {
            var semanticValidator = new SemanticValidator();
            var semanticResult = await semanticValidator.ValidateAsync(config, cancellationToken);
            result.Merge(semanticResult);

            // Stop if semantic validation failed
            if (!semanticResult.IsValid)
            {
                result.AddError("Semantic validation failed. Fix semantic errors before proceeding to runtime validation.");
                return result;
            }
        }

        // Phase 3: Runtime Validation (optional, can be slow)
        if (_options.ValidateRuntime)
        {
            var runtimeValidator = new RuntimeValidator(_options.RuntimeValidationTimeoutSeconds, _connectionFactory);
            var runtimeResult = await runtimeValidator.ValidateAsync(config, cancellationToken);
            result.Merge(runtimeResult);
        }

        return result;
    }

    /// <summary>
    /// Validate a configuration file.
    /// </summary>
    public static async Task<ValidationResult> ValidateFileAsync(
        string filePath,
        ValidationOptions? options = null,
        IDbConnectionFactory? connectionFactory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var config = await HonuaConfigLoader.LoadAsync(filePath);
            var validator = new ConfigurationValidator(options, connectionFactory);
            return await validator.ValidateAsync(config, cancellationToken);
        }
        catch (ParseException ex)
        {
            return ValidationResult.Error($"Parse error: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return ValidationResult.Error($"Configuration error: {ex.Message}");
        }
        catch (Exception ex)
        {
            return ValidationResult.Error($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validate a configuration file (synchronous).
    /// </summary>
    public static ValidationResult ValidateFile(
        string filePath,
        ValidationOptions? options = null,
        IDbConnectionFactory? connectionFactory = null)
    {
        return ValidateFileAsync(filePath, options, connectionFactory).GetAwaiter().GetResult();
    }
}
