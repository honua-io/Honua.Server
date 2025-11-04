// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using Honua.Server.Core.Catalog;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Honua.Server.Host.Utilities;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Pages.Catalog;

public sealed class IndexModel : PageModel
{
    private readonly ICatalogProjectionService _catalog;

    public IndexModel(ICatalogProjectionService catalog)
    {
        _catalog = Guard.NotNull(catalog);
    }

    public IReadOnlyList<CatalogGroupView> Groups { get; private set; } = Array.Empty<CatalogGroupView>();
    public IReadOnlyList<CatalogDiscoveryRecord> Records { get; private set; } = Array.Empty<CatalogDiscoveryRecord>();
    public string? Query { get; private set; }
    public string? GroupId { get; private set; }

    public void OnGet(string? q, string? group)
    {
        Query = q.IsNullOrWhiteSpace() ? null : q;
        GroupId = group.IsNullOrWhiteSpace() ? null : group;

        Groups = _catalog.GetGroups();
        Records = _catalog.Search(Query, GroupId);
    }
}
