// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Import.Validation;

/// <summary>
/// Validates feature properties against layer schema definitions.
/// </summary>
public interface IFeatureSchemaValidator
{
    /// <summary>
    /// Validates a single feature's properties against the layer schema.
    /// </summary>
    /// <param name="properties">Feature properties to validate.</param>
    /// <param name="layer">Layer definition containing field schemas.</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Validation result with any errors found.</returns>
    FeatureValidationResult ValidateFeature(
        IDictionary<string, object?> properties,
        LayerDefinition layer,
        SchemaValidationOptions? options = null);

    /// <summary>
    /// Validates multiple features' properties against the layer schema.
    /// </summary>
    /// <param name="features">Collection of feature properties to validate.</param>
    /// <param name="layer">Layer definition containing field schemas.</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Collection of validation results, one per feature.</returns>
    IReadOnlyList<FeatureValidationResult> ValidateFeatures(
        IEnumerable<IDictionary<string, object?>> features,
        LayerDefinition layer,
        SchemaValidationOptions? options = null);
}
