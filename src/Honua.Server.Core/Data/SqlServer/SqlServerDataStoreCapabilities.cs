// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Data.SqlServer;

internal sealed class SqlServerDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly SqlServerDataStoreCapabilities Instance = new();

    private SqlServerDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true;
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true;
    public bool SupportsServerSideGeometryOperations => true;
    public bool SupportsCrsTransformations => false; // Limited CRS support
    public int MaxQueryParameters => 2100; // SQL Server limit
    public bool SupportsReturningClause => true; // OUTPUT clause
    public bool SupportsBulkOperations => true;
    public bool SupportsSoftDelete => true;
}
