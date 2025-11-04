// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.Ogc;

/// <summary>
/// Metadata to mark an endpoint as deprecated.
/// </summary>
/// <param name="Message">The deprecation message to display to clients.</param>
internal sealed record DeprecatedEndpointMetadata(string Message);
