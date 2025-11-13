// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Linq;
using Honua.Server.Core.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Pages.Catalog;

public sealed class DetailModel : PageModel
{
    private readonly ICatalogProjectionService catalog;

    public DetailModel(ICatalogProjectionService catalog)
    {
        this.catalog = Guard.NotNull(catalog);
    }

    public CatalogDiscoveryRecord Record { get; private set; } = null!;
    public CatalogServiceView? Service { get; private set; }
    public CatalogLayerView? Layer { get; private set; }

    public IActionResult OnGet(string serviceId, string layerId)
    {
        if (serviceId.IsNullOrWhiteSpace() || layerId.IsNullOrWhiteSpace())
        {
            return this.NotFound();
        }

        var record = this.catalog.GetRecord($"{serviceId}:{layerId}");
        if (record is null)
        {
            return this.NotFound();
        }

        Record = record;
        Service = this.catalog.GetService(serviceId);
        Layer = Service?.Layers.FirstOrDefault(l => string.Equals(l.Layer.Id, layerId, StringComparison.OrdinalIgnoreCase));

        return Page();
    }
}
