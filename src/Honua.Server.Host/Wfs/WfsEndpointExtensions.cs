// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Wfs;

internal static class WfsEndpointExtensions
{
    public static IEndpointRouteBuilder MapWfs(this IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);

        var group = endpoints.MapGroup("/wfs");
        group.RequireAuthorization("RequireViewer");
        group.MapGet(string.Empty, WfsHandlers.HandleAsync);
        group.MapPost(string.Empty, WfsHandlers.HandleAsync);

        return endpoints;
    }
}
