// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Editing;

public interface IFeatureEditAuthorizationService
{
    FeatureEditAuthorizationResult Authorize(FeatureEditCommand command, LayerDefinition layerDefinition, IEnumerable<string> userRoles, bool isAuthenticated);
}

public sealed record FeatureEditAuthorizationResult(bool IsAuthorized, FeatureEditError? Error = null)
{
    public static FeatureEditAuthorizationResult Success { get; } = new(true, null);

    public static FeatureEditAuthorizationResult Denied(string code, string message)
        => new(false, new FeatureEditError(code, message));
}

public sealed class FeatureEditAuthorizationService : IFeatureEditAuthorizationService
{
    public FeatureEditAuthorizationResult Authorize(FeatureEditCommand command, LayerDefinition layerDefinition, IEnumerable<string> userRoles, bool isAuthenticated)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (layerDefinition is null)
        {
            throw new ArgumentNullException(nameof(layerDefinition));
        }

        var capabilities = layerDefinition.Editing.Capabilities;

        if (!isAuthenticated && capabilities.RequireAuthentication)
        {
            return FeatureEditAuthorizationResult.Denied("unauthenticated", "Editing operations require authentication.");
        }

        if (capabilities.AllowedRoles.Count > 0)
        {
            var roleSet = new HashSet<string>(userRoles ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (!roleSet.Overlaps(capabilities.AllowedRoles))
            {
                return FeatureEditAuthorizationResult.Denied("insufficient_permissions", "User lacks required edit roles.");
            }
        }

        return command.Operation switch
        {
            FeatureEditOperation.Add when !capabilities.AllowAdd
                => FeatureEditAuthorizationResult.Denied("add_not_allowed", "Layer does not permit adding features."),
            FeatureEditOperation.Update when !capabilities.AllowUpdate
                => FeatureEditAuthorizationResult.Denied("update_not_allowed", "Layer does not permit updating features."),
            FeatureEditOperation.Delete when !capabilities.AllowDelete
                => FeatureEditAuthorizationResult.Denied("delete_not_allowed", "Layer does not permit deleting features."),
            _ => FeatureEditAuthorizationResult.Success
        };
    }
}
