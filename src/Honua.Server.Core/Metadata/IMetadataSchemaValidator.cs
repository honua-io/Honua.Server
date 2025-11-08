// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Metadata;

/// <summary>
/// Validates metadata against a defined schema.
/// Ensures metadata conforms to structural and semantic requirements.
/// </summary>
public interface IMetadataSchemaValidator
{
    /// <summary>
    /// Validates a JSON metadata document against the schema.
    /// </summary>
    /// <param name="json">The JSON metadata document to validate</param>
    /// <returns>A validation result containing success status and any error messages</returns>
    MetadataSchemaValidationResult Validate(string json);
}
