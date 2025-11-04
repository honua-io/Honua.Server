// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

public sealed class AttachmentStoreSelector : DisposableBase, IAttachmentStoreSelector
{
    private readonly IHonuaConfigurationService _configurationService;
    private readonly IDictionary<string, IAttachmentStoreProvider> _providers;
    private readonly ILogger<AttachmentStoreSelector> _logger;
    private readonly ConcurrentDictionary<string, IAttachmentStore> _cache = new(StringComparer.OrdinalIgnoreCase);
    private IDisposable? _changeRegistration;

    public AttachmentStoreSelector(
        IHonuaConfigurationService configurationService,
        IEnumerable<IAttachmentStoreProvider> providers,
        ILogger<AttachmentStoreSelector> logger)
    {
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var providerMap = new Dictionary<string, IAttachmentStoreProvider>(StringComparer.OrdinalIgnoreCase);
        if (providers is null)
        {
            throw new ArgumentNullException(nameof(providers));
        }

        foreach (var provider in providers)
        {
            if (provider is null || string.IsNullOrWhiteSpace(provider.ProviderKey))
            {
                continue;
            }

            providerMap[provider.ProviderKey.Trim()] = provider;
        }

        _providers = providerMap;
        RegisterForConfigurationChanges();
    }

    public IAttachmentStore Resolve(string storageProfileId)
    {
        if (string.IsNullOrWhiteSpace(storageProfileId))
        {
            throw new AttachmentStoreNotFoundException(storageProfileId ?? string.Empty);
        }

        ThrowIfDisposed();

        return _cache.GetOrAdd(storageProfileId, CreateStore);
    }

    private IAttachmentStore CreateStore(string profileId)
    {
        var config = _configurationService.Current.Attachments;
        if (!config.Profiles.TryGetValue(profileId, out var profile))
        {
            throw new AttachmentStoreNotFoundException(profileId);
        }

        var providerKey = string.IsNullOrWhiteSpace(profile.Provider)
            ? string.Empty
            : profile.Provider.Trim();

        if (!_providers.TryGetValue(providerKey, out var provider))
        {
            _logger.LogError("No attachment store provider registered for key '{ProviderKey}'", providerKey);
            throw new AttachmentStoreNotFoundException(profileId);
        }

        return provider.Create(profileId, profile);
    }

    private void RegisterForConfigurationChanges()
    {
        _changeRegistration = ChangeToken.OnChange(
            () => _configurationService.GetChangeToken(),
            OnConfigurationChanged);
    }

    private void OnConfigurationChanged()
    {
        _logger.LogInformation("Attachment configuration changed; clearing store cache");
        _cache.Clear();
    }

    protected override void DisposeCore()
    {
        _changeRegistration?.Dispose();
        _cache.Clear();
    }
}
