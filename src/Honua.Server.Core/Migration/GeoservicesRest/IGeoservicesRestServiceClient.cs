// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public interface IGeoservicesRestServiceClient
{
    Task<GeoservicesRestFeatureServiceInfo> GetServiceAsync(Uri serviceUri, string? token = null, CancellationToken cancellationToken = default);

    Task<GeoservicesRestLayerInfo> GetLayerAsync(Uri layerUri, string? token = null, CancellationToken cancellationToken = default);

    Task<GeoservicesRestQueryResult> QueryAsync(Uri layerUri, GeoservicesRestQueryParameters parameters, string? token = null, CancellationToken cancellationToken = default);

    Task<GeoservicesRestIdQueryResult> QueryObjectIdsAsync(Uri layerUri, GeoservicesRestQueryParameters parameters, string? token = null, CancellationToken cancellationToken = default);
}
