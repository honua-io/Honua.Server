// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Data.MySql;

internal sealed class MySqlDataStoreCapabilities : IDataStoreCapabilities
{
    public static readonly MySqlDataStoreCapabilities Instance = new();

    private MySqlDataStoreCapabilities()
    {
    }

    public bool SupportsNativeGeometry => true;
    public bool SupportsNativeMvt => false;
    public bool SupportsTransactions => true;
    public bool SupportsSpatialIndexes => true;
    public bool SupportsServerSideGeometryOperations => true;
    public bool SupportsCrsTransformations => true; // MySQL 8.0+
    public int MaxQueryParameters => 65535; // MySQL limit
    public bool SupportsReturningClause => false; // MySQL doesn't support RETURNING
    public bool SupportsBulkOperations => true;
    public bool SupportsSoftDelete => true;
}
