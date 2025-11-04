// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.OData.Services;
using Microsoft.Extensions.DependencyInjection;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData;

public static class ODataServiceCollectionExtensions
{
    public static IServiceCollection AddHonuaOData(this IServiceCollection services)
    {
        Guard.NotNull(services);

        services.AddSingleton<IODataFieldTypeMapper, ODataFieldTypeMapper>();
        services.AddSingleton<DynamicEdmModelBuilder>();
        services.AddSingleton<ODataModelCache>();

        // Register OData service layer
        services.AddSingleton<ODataMetadataResolver>();
        services.AddSingleton<ODataQueryService>();
        services.AddSingleton<ODataGeometryService>();
        services.AddSingleton<ODataConverterService>();
        services.AddSingleton<ODataEntityService>();

        return services;
    }
}

