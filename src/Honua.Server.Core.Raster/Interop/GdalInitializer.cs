// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace Honua.Server.Core.Raster.Interop;

/// <summary>
/// Centralises GDAL/OGR initialisation so native dependencies are configured exactly once per process.
/// </summary>
internal static class GdalInitializer
{
    private static readonly Lazy<bool> Initialised = new(() =>
    {
        GdalBase.ConfigureAll();
        Gdal.AllRegister();
        Ogr.RegisterAll();
        return true;
    });

    public static void EnsureInitialized()
    {
        _ = Initialised.Value;
    }
}
