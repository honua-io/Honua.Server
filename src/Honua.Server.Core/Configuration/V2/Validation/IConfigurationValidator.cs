// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;

namespace Honua.Server.Core.Configuration.V2.Validation;

/// <summary>
/// Interface for configuration validators.
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validate a configuration.
    /// </summary>
    Task<ValidationResult> ValidateAsync(HonuaConfig config, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validation options.
/// </summary>
public sealed class ValidationOptions
{
    /// <summary>
    /// Whether to perform syntax validation (default: true).
    /// </summary>
    public bool ValidateSyntax { get; init; } = true;

    /// <summary>
    /// Whether to perform semantic validation (default: true).
    /// References, type checking, etc.
    /// </summary>
    public bool ValidateSemantics { get; init; } = true;

    /// <summary>
    /// Whether to perform runtime validation (default: false).
    /// Database connectivity, table existence, etc.
    /// Can be slow and requires infrastructure.
    /// </summary>
    public bool ValidateRuntime { get; init; } = false;

    /// <summary>
    /// Timeout for runtime validation checks (default: 10 seconds).
    /// </summary>
    public int RuntimeValidationTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Default validation options (syntax + semantics only).
    /// </summary>
    public static ValidationOptions Default => new();

    /// <summary>
    /// Full validation (syntax + semantics + runtime).
    /// </summary>
    public static ValidationOptions Full => new()
    {
        ValidateSyntax = true,
        ValidateSemantics = true,
        ValidateRuntime = true
    };

    /// <summary>
    /// Syntax-only validation.
    /// </summary>
    public static ValidationOptions SyntaxOnly => new()
    {
        ValidateSyntax = true,
        ValidateSemantics = false,
        ValidateRuntime = false
    };
}
