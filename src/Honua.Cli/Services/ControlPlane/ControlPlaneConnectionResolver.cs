// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Services.Configuration;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.ControlPlane;

public interface IControlPlaneConnectionResolver
{
    Task<ControlPlaneConnection> ResolveAsync(string? hostOverride, string? tokenOverride, CancellationToken cancellationToken);
}

public sealed class ControlPlaneConnectionResolver : IControlPlaneConnectionResolver
{
    private readonly IHonuaCliConfigStore _configStore;

    public ControlPlaneConnectionResolver(IHonuaCliConfigStore configStore)
    {
        _configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
    }

    public async Task<ControlPlaneConnection> ResolveAsync(string? hostOverride, string? tokenOverride, CancellationToken cancellationToken)
    {
        var config = await _configStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var host = hostOverride;
        if (host.IsNullOrWhiteSpace())
        {
            host = config.Host;
        }

        host ??= "http://localhost:5000";

        var token = tokenOverride;
        if (token.IsNullOrWhiteSpace())
        {
            token = config.Token;
        }

        return ControlPlaneConnection.Create(host, token);
    }
}
