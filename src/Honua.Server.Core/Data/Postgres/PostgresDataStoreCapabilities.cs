// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Data.Postgres;

internal sealed class PostgresDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly PostgresDataStoreCapabilities Instance = new();

    private PostgresDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true;
    public bool SupportsNativeMvt => true;
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true;
    public bool SupportsServerSideGeometryOperations => true;
    public bool SupportsCrsTransformations => true;
    public int MaxQueryParameters => 32767; // PostgreSQL limit
    public bool SupportsReturningClause => true;
    public bool SupportsBulkOperations => true;
    public bool SupportsSoftDelete => true;
}
