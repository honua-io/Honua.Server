// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Metadata;

namespace Honua.Server.Core.Editing;

public interface IFeatureEditConstraintValidator
{
    FeatureEditError? Validate(AddFeatureCommand command, LayerDefinition layerDefinition);
    FeatureEditError? Validate(UpdateFeatureCommand command, LayerDefinition layerDefinition);
}

public sealed class FeatureEditConstraintValidator : IFeatureEditConstraintValidator
{
    public FeatureEditError? Validate(AddFeatureCommand command, LayerDefinition layerDefinition)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (layerDefinition is null)
        {
            throw new ArgumentNullException(nameof(layerDefinition));
        }

        var constraints = layerDefinition.Editing.Constraints;
        var attributes = command.Attributes ?? new Dictionary<string, object?>();
        var attributeKeys = new HashSet<string>(attributes.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var required in constraints.RequiredFields)
        {
            if (!attributeKeys.Contains(required))
            {
                return new FeatureEditError("missing_required_field", $"Field '{required}' is required.");
            }
        }

        return null;
    }

    public FeatureEditError? Validate(UpdateFeatureCommand command, LayerDefinition layerDefinition)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (layerDefinition is null)
        {
            throw new ArgumentNullException(nameof(layerDefinition));
        }

        var constraints = layerDefinition.Editing.Constraints;
        var attributes = command.Attributes ?? new Dictionary<string, object?>();
        var attributeKeys = new HashSet<string>(attributes.Keys, StringComparer.OrdinalIgnoreCase);

        var immutable = new HashSet<string>(constraints.ImmutableFields, StringComparer.OrdinalIgnoreCase)
        {
            layerDefinition.IdField
        };

        foreach (var field in immutable)
        {
            if (attributeKeys.Contains(field))
            {
                return new FeatureEditError("immutable_field", $"Field '{field}' cannot be modified.");
            }
        }

        return null;
    }
}
