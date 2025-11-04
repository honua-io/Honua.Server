// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.ObjectPool;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data.Postgres;

/// <summary>
/// Pooling policy for PostgresFeatureQueryBuilder instances.
/// Implements IPooledObjectPolicy to control object lifecycle in the pool.
/// </summary>
internal sealed class QueryBuilderPoolPolicy : IPooledObjectPolicy<PostgresFeatureQueryBuilder>
{
    private readonly ServiceDefinition _service;
    private readonly LayerDefinition _layer;
    private readonly int _storageSrid;
    private readonly int _targetSrid;

    public QueryBuilderPoolPolicy(
        ServiceDefinition service,
        LayerDefinition layer,
        int storageSrid,
        int targetSrid)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
        _storageSrid = storageSrid;
        _targetSrid = targetSrid;
    }

    public PostgresFeatureQueryBuilder Create()
    {
        return new PostgresFeatureQueryBuilder(_service, _layer, _storageSrid, _targetSrid);
    }

    public bool Return(PostgresFeatureQueryBuilder obj)
    {
        // QueryBuilder is stateless after construction, so it can always be returned
        // No reset needed as all state is passed via method parameters
        return obj != null;
    }
}
