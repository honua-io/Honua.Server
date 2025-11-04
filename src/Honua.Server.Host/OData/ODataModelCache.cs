// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.Extensions.Primitives;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData;

public sealed class ODataModelCache : IDisposable
{
    private readonly DynamicEdmModelBuilder _builder;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private ODataModelDescriptor? _descriptor;
    private readonly IDisposable _metadataSubscription;
    private readonly IDisposable _configurationSubscription;

    public ODataModelCache(
        DynamicEdmModelBuilder builder,
        IMetadataRegistry metadataRegistry,
        IHonuaConfigurationService configuration)
    {
        _builder = Guard.NotNull(builder);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(configuration);

        _metadataSubscription = ChangeToken.OnChange(metadataRegistry.GetChangeToken, Reset);
        _configurationSubscription = ChangeToken.OnChange(configuration.GetChangeToken, Reset);
    }

    public ValueTask<ODataModelDescriptor> GetOrCreateAsync(CancellationToken cancellationToken = default)
    {
        var cached = Volatile.Read(ref _descriptor);
        if (cached is not null)
        {
            return new ValueTask<ODataModelDescriptor>(cached);
        }

        return new ValueTask<ODataModelDescriptor>(CreateAsync(cancellationToken));
    }

    private async Task<ODataModelDescriptor> CreateAsync(CancellationToken cancellationToken)
    {
        await _initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var cached = _descriptor;
            if (cached is null)
            {
                cached = await _builder.BuildAsync(cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _descriptor, cached);
            }

            return cached;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public void Reset()
    {
        Volatile.Write(ref _descriptor, null);
    }

    public void Dispose()
    {
        _metadataSubscription.Dispose();
        _configurationSubscription.Dispose();
        _initializationLock.Dispose();
    }
}
