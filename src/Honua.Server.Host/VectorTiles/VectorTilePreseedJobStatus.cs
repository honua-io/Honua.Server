// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Host.VectorTiles;

/// <summary>
/// Status of a vector tile preseed job
/// </summary>
public enum VectorTilePreseedJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}
