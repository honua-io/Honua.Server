// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Attachments;

namespace Honua.Server.Host.Ogc.ParameterObjects;

/// <summary>
/// Encapsulates attachment-related services for features.
/// </summary>
/// <remarks>
/// Feature attachments allow associating files (images, PDFs, documents) with individual
/// geographic features. This parameter object groups the services responsible for:
/// - Orchestrating bulk attachment operations across feature collections
/// - Handling individual attachment HTTP requests and responses
///
/// These services work together to provide a complete attachment management capability
/// for OGC API Features endpoints.
/// </remarks>
public sealed record OgcFeatureAttachmentServices
{
    /// <summary>
    /// Orchestrates bulk attachment operations for feature collections.
    /// Handles efficient retrieval and management of attachments across multiple features.
    /// </summary>
    public required IFeatureAttachmentOrchestrator Orchestrator { get; init; }

    /// <summary>
    /// Handles attachment-related HTTP requests and responses.
    /// Manages individual attachment operations, content negotiation, and streaming.
    /// </summary>
    public required Services.IOgcFeaturesAttachmentHandler Handler { get; init; }
}
