// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Microsoft.OData.Edm;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.OData;

public sealed class ODataModelDescriptor
{
    private readonly IReadOnlyDictionary<string, ODataEntityMetadata> _entitySetLookup;

    public ODataModelDescriptor(IEdmModel model, IEnumerable<ODataEntityMetadata> entitySets)
    {
        Model = Guard.NotNull(model);
        Guard.NotNull(entitySets);

        var metadata = entitySets.ToArray();

        // Allow empty entity sets for scenarios like testing or before data is loaded
        if (metadata.Length > 0)
        {
            _entitySetLookup = new ReadOnlyDictionary<string, ODataEntityMetadata>(
                metadata.ToDictionary(m => m.EntitySetName, StringComparer.OrdinalIgnoreCase));
        }
        else
        {
            _entitySetLookup = new ReadOnlyDictionary<string, ODataEntityMetadata>(
                new Dictionary<string, ODataEntityMetadata>(StringComparer.OrdinalIgnoreCase));
        }

        EntitySets = Array.AsReadOnly(metadata);
    }

    public IEdmModel Model { get; }

    public IReadOnlyList<ODataEntityMetadata> EntitySets { get; }

    public bool TryGetByEntitySet(string entitySetName, out ODataEntityMetadata metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entitySetName);
        return _entitySetLookup.TryGetValue(entitySetName, out metadata!);
    }

    public ODataEntityMetadata GetByEntitySet(string entitySetName)
    {
        if (!TryGetByEntitySet(entitySetName, out var metadata))
        {
            throw new KeyNotFoundException($"Unknown OData entity set '{entitySetName}'.");
        }

        return metadata;
    }
}

public sealed record ODataEntityMetadata(
    string EntitySetName,
    string EntityTypeName,
    ServiceDefinition Service,
    LayerDefinition Layer,
    IEdmEntitySet EntitySet,
    IEdmEntityType EntityType,
    string? GeometryShadowProperty);
