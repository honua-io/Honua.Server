// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Configuration;

/// <summary>
/// Validates OpenRosa configuration options.
/// </summary>
public sealed class OpenRosaOptionsValidator : IValidateOptions<OpenRosaOptions>
{
    public ValidateOptionsResult Validate(string? name, OpenRosaOptions options)
    {
        var failures = new List<string>();

        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        // Validate base URL
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            failures.Add("OpenRosa BaseUrl is required when OpenRosa is enabled. Set 'OpenRosa:BaseUrl'. Example: '/openrosa' or 'https://example.com/openrosa'");
        }
        else if (!options.BaseUrl.StartsWith("/") && !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add($"OpenRosa BaseUrl '{options.BaseUrl}' is invalid. Must be a relative path starting with '/' or an absolute URL. Example: '/openrosa' or 'https://example.com/openrosa'");
        }

        // Validate submission size
        if (options.MaxSubmissionSizeMB <= 0)
        {
            failures.Add($"OpenRosa MaxSubmissionSizeMB must be > 0. Current: {options.MaxSubmissionSizeMB}. Set 'OpenRosa:MaxSubmissionSizeMB'.");
        }

        if (options.MaxSubmissionSizeMB > 200)
        {
            failures.Add($"OpenRosa MaxSubmissionSizeMB ({options.MaxSubmissionSizeMB}) exceeds recommended limit of 200 MB. Large submissions can cause memory and storage issues. Reduce 'OpenRosa:MaxSubmissionSizeMB'.");
        }

        // Validate media types
        if (options.AllowedMediaTypes == null || !options.AllowedMediaTypes.Any())
        {
            failures.Add("OpenRosa AllowedMediaTypes must contain at least one media type. Set 'OpenRosa:AllowedMediaTypes'. Example: ['image/jpeg', 'image/png']");
        }
        else
        {
            foreach (var mediaType in options.AllowedMediaTypes)
            {
                if (string.IsNullOrWhiteSpace(mediaType) || !mediaType.Contains('/'))
                {
                    failures.Add($"OpenRosa AllowedMediaTypes contains invalid media type '{mediaType}'. Media types must be in format 'type/subtype'. Example: 'image/jpeg'");
                }
            }
        }

        // Validate retention
        if (options.StagingRetentionDays < 0)
        {
            failures.Add($"OpenRosa StagingRetentionDays cannot be negative. Current: {options.StagingRetentionDays}. Set 'OpenRosa:StagingRetentionDays' to 0 (no retention) or positive value.");
        }

        if (options.StagingRetentionDays > 365)
        {
            failures.Add($"OpenRosa StagingRetentionDays ({options.StagingRetentionDays}) exceeds recommended limit of 365 days. This can cause storage bloat. Reduce 'OpenRosa:StagingRetentionDays'.");
        }

        // Validate digest auth settings
        if (options.DigestAuth?.Enabled == true)
        {
            if (string.IsNullOrWhiteSpace(options.DigestAuth.Realm))
            {
                failures.Add("OpenRosa DigestAuth Realm is required when DigestAuth is enabled. Set 'OpenRosa:DigestAuth:Realm'. Example: 'Honua OpenRosa'");
            }

            if (options.DigestAuth.NonceLifetimeMinutes <= 0)
            {
                failures.Add($"OpenRosa DigestAuth NonceLifetimeMinutes must be > 0. Current: {options.DigestAuth.NonceLifetimeMinutes}. Set 'OpenRosa:DigestAuth:NonceLifetimeMinutes'.");
            }

            if (options.DigestAuth.NonceLifetimeMinutes > 60)
            {
                failures.Add($"OpenRosa DigestAuth NonceLifetimeMinutes ({options.DigestAuth.NonceLifetimeMinutes}) exceeds recommended limit of 60 minutes. Long nonce lifetimes reduce security. Reduce 'OpenRosa:DigestAuth:NonceLifetimeMinutes'.");
            }
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
