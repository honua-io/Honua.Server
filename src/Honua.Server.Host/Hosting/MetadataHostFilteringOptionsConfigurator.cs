// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Hosting;

internal sealed class MetadataHostFilteringOptionsConfigurator :
    IConfigureOptions<HostFilteringOptions>,
    IOptionsChangeTokenSource<HostFilteringOptions>
{
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ILogger<MetadataHostFilteringOptionsConfigurator> _logger;

    public MetadataHostFilteringOptionsConfigurator(
        IMetadataRegistry metadataRegistry,
        ILogger<MetadataHostFilteringOptionsConfigurator> logger)
    {
        _metadataRegistry = Guard.NotNull(metadataRegistry);
        _logger = Guard.NotNull(logger);
    }

    public void Configure(HostFilteringOptions options)
    {
        Guard.NotNull(options);

        if (!TryGetCurrentSnapshot(out var snapshot))
        {
            _logger.LogWarning(
                "Metadata registry is not ready yet; host filtering temporarily allows all hosts until metadata initialization completes.");
            options.AllowedHosts = new[] { "*" };
            return;
        }

        try
        {
            var allowedHosts = snapshot.Server.AllowedHosts;
            if (allowedHosts.Count == 0)
            {
                throw new InvalidOperationException("Metadata configuration does not declare any allowed hosts.");
            }

            var normalizedHosts = new List<string>(allowedHosts.Count);
            var containsWildcardPattern = false;

            foreach (var host in allowedHosts)
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                if (string.Equals(host, "*", StringComparison.Ordinal))
                {
                    normalizedHosts.Clear();
                    normalizedHosts.Add("*");
                    containsWildcardPattern = false;
                    break;
                }

                if (host.Contains('*', StringComparison.Ordinal))
                {
                    containsWildcardPattern = true;
                    continue;
                }

                normalizedHosts.Add(host);
            }

            if (containsWildcardPattern)
            {
                _logger.LogWarning(
                    "Wildcard host entries (e.g. '*.example.com') are not supported by ASP.NET Core host filtering. Allowing all hosts instead. Configure explicit hosts to enable filtering.");
                normalizedHosts.Clear();
                normalizedHosts.Add("*");
            }

            if (normalizedHosts.Count == 0)
            {
                throw new InvalidOperationException("No valid host entries found after normalization.");
            }

            options.AllowedHosts = normalizedHosts.ToArray();
        }
        catch (Exception ex) when (ex is InvalidOperationException or TaskCanceledException)
        {
            _logger.LogCritical(ex, "Failed to configure host filtering options from metadata.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to load metadata for host filtering configuration.");
            throw new InvalidOperationException("Unable to configure host filtering options from metadata.", ex);
        }
    }

    public string Name => Options.DefaultName;

    public IChangeToken GetChangeToken()
    {
        return _metadataRegistry.GetChangeToken();
    }

    private bool TryGetCurrentSnapshot(out MetadataSnapshot snapshot)
    {
        if (_metadataRegistry.TryGetSnapshot(out snapshot))
        {
            return true;
        }

        snapshot = null!;
        return false;
    }
}
