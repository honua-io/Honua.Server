// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Styling;

/// <summary>
/// Repository for managing style definitions with versioning support
/// </summary>
public interface IStyleRepository
{
    /// <summary>
    /// Gets a style by ID
    /// </summary>
    Task<StyleDefinition?> GetAsync(string styleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all styles
    /// </summary>
    Task<IReadOnlyList<StyleDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new style
    /// </summary>
    Task<StyleDefinition> CreateAsync(StyleDefinition style, string? createdBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing style (creates a new version)
    /// </summary>
    Task<StyleDefinition> UpdateAsync(string styleId, StyleDefinition style, string? updatedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a style (soft delete, keeps history)
    /// </summary>
    Task<bool> DeleteAsync(string styleId, string? deletedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the version history for a style
    /// </summary>
    Task<IReadOnlyList<StyleVersion>> GetVersionHistoryAsync(string styleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version of a style
    /// </summary>
    Task<StyleDefinition?> GetVersionAsync(string styleId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a style exists
    /// </summary>
    Task<bool> ExistsAsync(string styleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a version of a style in the history
/// </summary>
public sealed record StyleVersion
{
    public required string StyleId { get; init; }
    public required int Version { get; init; }
    public required StyleDefinition Definition { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public string? ChangeDescription { get; init; }
}
