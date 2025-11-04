// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Honua.Server.Core.Exceptions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Query;

public sealed class QueryEntityDefinition
{
    public QueryEntityDefinition(string id, string name, IDictionary<string, QueryFieldDefinition> fields)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(name);
        Guard.NotNull(fields);

        Id = id;
        Name = name;
        Fields = new ReadOnlyDictionary<string, QueryFieldDefinition>(fields);
    }

    public string Id { get; }
    public string Name { get; }
    public IReadOnlyDictionary<string, QueryFieldDefinition> Fields { get; }

    public QueryFieldDefinition GetField(string field)
    {
        if (!Fields.TryGetValue(field, out var definition))
        {
            throw new FieldNotFoundException(field, Name);
        }

        return definition;
    }
}
