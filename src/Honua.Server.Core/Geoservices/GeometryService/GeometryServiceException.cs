// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

public sealed class GeometryServiceException : Exception
{
    public GeometryServiceException(string message)
        : base(message)
    {
    }

    public GeometryServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
