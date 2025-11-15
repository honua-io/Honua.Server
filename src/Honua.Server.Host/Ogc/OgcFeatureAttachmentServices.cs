// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Attachments;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Encapsulates attachment-related services for features.
/// Groups attachment orchestration and HTTP handling concerns.
/// </summary>
public sealed record OgcFeatureAttachmentServices
{
    /// <summary>
    /// Orchestrates bulk attachment operations for feature collections.
    /// Handles batch loading to prevent N+1 query problems.
    /// </summary>
    public required IFeatureAttachmentOrchestrator Orchestrator { get; init; }

    /// <summary>
    /// Handles attachment-related HTTP requests and responses.
    /// Generates attachment links and determines visibility.
    /// </summary>
    public required Services.IOgcFeaturesAttachmentHandler Handler { get; init; }
}
