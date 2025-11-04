// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.AspNetCore.Authorization;

namespace Honua.Server.Core.Authorization;

/// <summary>
/// Base class for resource-based authorization requirements.
/// </summary>
public abstract class ResourceAuthorizationRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the operation required for this authorization requirement.
    /// </summary>
    public string Operation { get; }

    protected ResourceAuthorizationRequirement(string operation)
    {
        Operation = operation ?? throw new ArgumentNullException(nameof(operation));
    }
}

/// <summary>
/// Authorization requirement for reading a layer.
/// </summary>
public sealed class ReadLayerRequirement : ResourceAuthorizationRequirement
{
    public ReadLayerRequirement() : base("read") { }
}

/// <summary>
/// Authorization requirement for writing to a layer.
/// </summary>
public sealed class WriteLayerRequirement : ResourceAuthorizationRequirement
{
    public WriteLayerRequirement() : base("write") { }
}

/// <summary>
/// Authorization requirement for deleting a layer.
/// </summary>
public sealed class DeleteLayerRequirement : ResourceAuthorizationRequirement
{
    public DeleteLayerRequirement() : base("delete") { }
}

/// <summary>
/// Authorization requirement for reading a collection.
/// </summary>
public sealed class ReadCollectionRequirement : ResourceAuthorizationRequirement
{
    public ReadCollectionRequirement() : base("read") { }
}

/// <summary>
/// Authorization requirement for writing to a collection.
/// </summary>
public sealed class WriteCollectionRequirement : ResourceAuthorizationRequirement
{
    public WriteCollectionRequirement() : base("write") { }
}

/// <summary>
/// Authorization requirement for deleting a collection.
/// </summary>
public sealed class DeleteCollectionRequirement : ResourceAuthorizationRequirement
{
    public DeleteCollectionRequirement() : base("delete") { }
}

/// <summary>
/// Authorization requirement for managing styles on a layer.
/// </summary>
public sealed class ManageStylesRequirement : ResourceAuthorizationRequirement
{
    public ManageStylesRequirement() : base("manage-styles") { }
}

/// <summary>
/// Authorization requirement for managing metadata.
/// </summary>
public sealed class ManageMetadataRequirement : ResourceAuthorizationRequirement
{
    public ManageMetadataRequirement() : base("manage-metadata") { }
}
