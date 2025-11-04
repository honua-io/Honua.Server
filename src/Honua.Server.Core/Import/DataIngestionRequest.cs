// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel.DataAnnotations;
using Honua.Server.Core.Import.Validation;

namespace Honua.Server.Core.Import;

public sealed record DataIngestionRequest(
    [Required(ErrorMessage = "ServiceId is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "ServiceId must be between 1 and 255 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "ServiceId must contain only alphanumeric characters, underscores, and hyphens.")]
    string ServiceId,

    [Required(ErrorMessage = "LayerId is required.")]
    [StringLength(255, MinimumLength = 1, ErrorMessage = "LayerId must be between 1 and 255 characters.")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "LayerId must contain only alphanumeric characters, underscores, and hyphens.")]
    string LayerId,

    [Required(ErrorMessage = "SourcePath is required.")]
    [StringLength(4096, MinimumLength = 1, ErrorMessage = "SourcePath must be between 1 and 4096 characters.")]
    string SourcePath,

    [Required(ErrorMessage = "WorkingDirectory is required.")]
    [StringLength(4096, MinimumLength = 1, ErrorMessage = "WorkingDirectory must be between 1 and 4096 characters.")]
    string WorkingDirectory,

    [StringLength(500, ErrorMessage = "SourceFileName cannot exceed 500 characters.")]
    string? SourceFileName,

    [StringLength(100, ErrorMessage = "ContentType cannot exceed 100 characters.")]
    string? ContentType,

    bool Overwrite,

    SchemaValidationOptions? ValidationOptions = null
)
{
    /// <summary>
    /// Gets the effective validation options to use, defaulting to strict validation if not specified.
    /// </summary>
    public SchemaValidationOptions GetEffectiveValidationOptions() => ValidationOptions ?? SchemaValidationOptions.Default;

    public void EnsureValid()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ServiceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(LayerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(WorkingDirectory);
    }
}
