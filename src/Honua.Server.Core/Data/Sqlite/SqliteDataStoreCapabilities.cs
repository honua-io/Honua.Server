// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Data.Sqlite;

internal sealed class SqliteDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly SqliteDataStoreCapabilities Instance = new();

    private SqliteDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true; // Via SpatiaLite extension
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true; // Via SpatiaLite extension
    public bool SupportsServerSideGeometryOperations => true; // Via SpatiaLite extension
    public bool SupportsCrsTransformations => true; // Via SpatiaLite extension
    public int MaxQueryParameters => 32766; // SQLITE_MAX_VARIABLE_NUMBER default
    public bool SupportsReturningClause => true; // Since SQLite 3.35.0
    public bool SupportsBulkOperations => true;
    public bool SupportsSoftDelete => true;
}
