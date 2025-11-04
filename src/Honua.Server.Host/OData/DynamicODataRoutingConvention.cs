// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.OData.Edm;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Routing.Conventions;
using Microsoft.AspNetCore.OData.Routing.Template;
using Microsoft.OData.Edm;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData;

public sealed class DynamicODataRoutingConvention : IODataControllerActionConvention
{
    private static readonly Type ControllerType = typeof(DynamicODataController);

    public int Order => -1000;

    public bool AppliesToController(ODataControllerActionContext context)
    {
        Guard.NotNull(context);
        return context.Controller.ControllerType == ControllerType;
    }

    public bool AppliesToAction(ODataControllerActionContext context)
    {
        Guard.NotNull(context);

        if (!AppliesToController(context))
        {
            return false;
        }

        var model = context.Model ?? throw new InvalidOperationException("OData model is required.");
        var container = model.EntityContainer ?? throw new InvalidOperationException("OData model does not define an entity container.");
        var action = context.Action ?? throw new InvalidOperationException("Action context is missing.");
        var added = false;

        foreach (var entitySet in container.EntitySets())
        {
            var entityType = entitySet.EntityType;
            var hasKey = action.HasODataKeyParameter(entityType, context.Options?.RouteOptions?.EnablePropertyNameCaseInsensitive ?? false);
            var comparison = context.Options?.RouteOptions?.EnableActionNameCaseInsensitive == true
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            if (hasKey)
            {
                added |= TryMapKeyAction(action, context, entitySet, entityType, comparison);
            }
            else
            {
                added |= TryMapCollectionAction(action, context, entitySet, comparison);
            }
        }

        return added;
    }

    private static bool TryMapCollectionAction(ActionModel action, ODataControllerActionContext context, IEdmEntitySet entitySet, StringComparison comparison)
    {
        if (string.Equals(action.ActionName, "Get", comparison))
        {
            AddCollectionSelector(action, context, entitySet, "Get");
            AddCountSelector(action, context, entitySet);
            return true;
        }

        if (string.Equals(action.ActionName, "Post", comparison))
        {
            AddCollectionSelector(action, context, entitySet, "Post");
            return true;
        }

        return false;
    }

    private static bool TryMapKeyAction(ActionModel action, ODataControllerActionContext context, IEdmEntitySet entitySet, IEdmEntityType entityType, StringComparison comparison)
    {
        if (string.Equals(action.ActionName, "Get", comparison))
        {
            AddKeySelector(action, context, entitySet, entityType, "Get");
            return true;
        }

        if (string.Equals(action.ActionName, "Put", comparison))
        {
            AddKeySelector(action, context, entitySet, entityType, "Put");
            return true;
        }

        if (string.Equals(action.ActionName, "Patch", comparison))
        {
            AddKeySelector(action, context, entitySet, entityType, "Patch");
            return true;
        }

        if (string.Equals(action.ActionName, "Delete", comparison))
        {
            AddKeySelector(action, context, entitySet, entityType, "Delete");
            return true;
        }

        return false;
    }

    private static void AddCollectionSelector(ActionModel action, ODataControllerActionContext context, IEdmEntitySet entitySet, string httpMethod)
    {
        var segments = new List<ODataSegmentTemplate>
        {
            new EntitySetSegmentTemplate(entitySet)
        };

        var template = new ODataPathTemplate(segments);
        action.AddSelector(httpMethod, context.Prefix, context.Model, template, context.Options?.RouteOptions);
    }

    private static void AddCountSelector(ActionModel action, ODataControllerActionContext context, IEdmEntitySet entitySet)
    {
        var segments = new List<ODataSegmentTemplate>
        {
            new EntitySetSegmentTemplate(entitySet),
            CountSegmentTemplate.Instance
        };

        var template = new ODataPathTemplate(segments);
        action.AddSelector("Get", context.Prefix, context.Model, template, context.Options?.RouteOptions);
    }

    private static void AddKeySelector(ActionModel action, ODataControllerActionContext context, IEdmEntitySet entitySet, IEdmEntityType entityType, string httpMethod)
    {
        var keyMappings = entityType.Key().ToDictionary(k => k.Name, k => $"{{{k.Name}}}");

        var segments = new List<ODataSegmentTemplate>
        {
            new EntitySetSegmentTemplate(entitySet),
            new KeySegmentTemplate(keyMappings, entityType, entitySet)
        };

        var template = new ODataPathTemplate(segments);
        action.AddSelector(httpMethod, context.Prefix, context.Model, template, context.Options?.RouteOptions);
    }
}

