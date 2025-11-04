// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Services.ControlPlane;

public sealed record ControlPlaneConnection(Uri BaseUri, string? BearerToken)
{
    public static ControlPlaneConnection Create(string baseAddress, string? bearerToken)
    {
        if (baseAddress.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Base address must be provided.", nameof(baseAddress));
        }

        if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException($"'{baseAddress}' is not a valid absolute URI.", nameof(baseAddress));
        }

        return new ControlPlaneConnection(uri, bearerToken.IsNullOrWhiteSpace() ? null : bearerToken);
    }
}
