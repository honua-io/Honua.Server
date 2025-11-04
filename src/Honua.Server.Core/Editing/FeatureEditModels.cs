// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Honua.Server.Core.Editing;

public enum FeatureEditOperation
{
    Add,
    Update,
    Delete,
    Replace
}

public abstract record FeatureEditCommand(string ServiceId, string LayerId)
{
    public abstract FeatureEditOperation Operation { get; }
}

public sealed record AddFeatureCommand(
    string ServiceId,
    string LayerId,
    IReadOnlyDictionary<string, object?> Attributes,
    string? ClientReference = null) : FeatureEditCommand(ServiceId, LayerId)
{
    public override FeatureEditOperation Operation => FeatureEditOperation.Add;
}

public sealed record UpdateFeatureCommand(
    string ServiceId,
    string LayerId,
    string FeatureId,
    IReadOnlyDictionary<string, object?> Attributes,
    object? Version = null,
    string? ETag = null,
    string? ClientReference = null) : FeatureEditCommand(ServiceId, LayerId)
{
    public override FeatureEditOperation Operation => FeatureEditOperation.Update;
}

public sealed record DeleteFeatureCommand(
    string ServiceId,
    string LayerId,
    string FeatureId,
    string? ETag = null,
    string? ClientReference = null) : FeatureEditCommand(ServiceId, LayerId)
{
    public override FeatureEditOperation Operation => FeatureEditOperation.Delete;
}

public sealed record ReplaceFeatureCommand(
    string ServiceId,
    string LayerId,
    string FeatureId,
    IReadOnlyDictionary<string, object?> Attributes,
    object? Version = null,
    string? ETag = null,
    string? ClientReference = null) : FeatureEditCommand(ServiceId, LayerId)
{
    public override FeatureEditOperation Operation => FeatureEditOperation.Replace;
}

public sealed record FeatureEditBatch
{
    public FeatureEditBatch(
        IReadOnlyList<FeatureEditCommand> commands,
        bool rollbackOnFailure = true,
        string? clientReference = null,
        bool isAuthenticated = false,
        IReadOnlyList<string>? userRoles = null)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        RollbackOnFailure = rollbackOnFailure;
        ClientReference = clientReference;
        IsAuthenticated = isAuthenticated;
        UserRoles = userRoles ?? Array.Empty<string>();
    }

    public IReadOnlyList<FeatureEditCommand> Commands { get; }
    public bool RollbackOnFailure { get; }
    public string? ClientReference { get; }
    public bool IsAuthenticated { get; }
    public IReadOnlyList<string> UserRoles { get; }
}

public sealed record FeatureEditError(string Code, string Message, IReadOnlyDictionary<string, string?>? Details = null)
{
    public static FeatureEditError NotImplemented { get; } = new("not_implemented", "Feature editing is not yet implemented.");
}

public sealed record FeatureEditCommandResult(
    FeatureEditCommand Command,
    bool Success,
    string? FeatureId,
    FeatureEditError? Error,
    object? Version = null)
{
    public static FeatureEditCommandResult CreateSuccess(FeatureEditCommand command, string? featureId, object? version = null) =>
        new(command ?? throw new ArgumentNullException(nameof(command)), true, featureId, null, version);

    public static FeatureEditCommandResult CreateFailure(FeatureEditCommand command, FeatureEditError error) =>
        new(command ?? throw new ArgumentNullException(nameof(command)), false, null, error ?? throw new ArgumentNullException(nameof(error)), null);
}

public sealed record FeatureEditBatchResult
{
    public FeatureEditBatchResult(IReadOnlyList<FeatureEditCommandResult> results)
    {
        Results = results ?? throw new ArgumentNullException(nameof(results));
    }

    public IReadOnlyList<FeatureEditCommandResult> Results { get; }

    public bool Succeeded => Results.Count > 0 && Results.All(result => result.Success);

    public static FeatureEditBatchResult FromSingleFailure(FeatureEditCommand command, FeatureEditError error)
    {
        return new FeatureEditBatchResult(new ReadOnlyCollection<FeatureEditCommandResult>(new[]
        {
            FeatureEditCommandResult.CreateFailure(command, error)
        }));
    }
}
