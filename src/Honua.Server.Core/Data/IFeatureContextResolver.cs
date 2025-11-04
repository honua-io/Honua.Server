// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

public interface IFeatureContextResolver
{
    Task<FeatureContext> ResolveAsync(string serviceId, string layerId, CancellationToken cancellationToken = default);
}
