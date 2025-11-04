// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Honua.Server.Enterprise.Versioning;

/// <summary>
/// Interface for merge engine that performs three-way merges
/// </summary>
public interface IMergeEngine<T> where T : IVersionedEntity
{
    /// <summary>
    /// Detect conflicts between three versions
    /// </summary>
    List<MergeConflict> DetectConflicts(T baseVersion, T currentVersion, T incomingVersion);

    /// <summary>
    /// Perform three-way merge
    /// </summary>
    MergeResult<T> Merge(
        T baseVersion,
        T currentVersion,
        T incomingVersion,
        MergeStrategy strategy,
        Dictionary<string, ResolutionStrategy>? fieldResolutions = null);
}

/// <summary>
/// Default three-way merge engine with conflict detection
/// </summary>
public class DefaultMergeEngine<T> : IMergeEngine<T> where T : IVersionedEntity
{
    public List<MergeConflict> DetectConflicts(T baseVersion, T currentVersion, T incomingVersion)
    {
        var conflicts = new List<MergeConflict>();
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !IsVersionMetadataProperty(p.Name))
            .ToList();

        foreach (var property in properties)
        {
            var baseValue = property.GetValue(baseVersion);
            var currentValue = property.GetValue(currentVersion);
            var incomingValue = property.GetValue(incomingVersion);

            // Check for conflicts
            var baseChanged = !AreEqual(baseValue, currentValue);
            var incomingChanged = !AreEqual(baseValue, incomingValue);

            if (baseChanged && incomingChanged)
            {
                // Both sides changed - potential conflict
                if (!AreEqual(currentValue, incomingValue))
                {
                    conflicts.Add(new MergeConflict
                    {
                        FieldName = property.Name,
                        FieldPath = property.Name,
                        BaseValue = baseValue,
                        CurrentValue = currentValue,
                        IncomingValue = incomingValue,
                        Type = DetermineConflictType(baseValue, currentValue, incomingValue)
                    });
                }
            }
        }

        return conflicts;
    }

    public MergeResult<T> Merge(
        T baseVersion,
        T currentVersion,
        T incomingVersion,
        MergeStrategy strategy,
        Dictionary<string, ResolutionStrategy>? fieldResolutions = null)
    {
        var result = new MergeResult<T>
        {
            Strategy = strategy,
            Success = true
        };

        // Detect conflicts
        var conflicts = DetectConflicts(baseVersion, currentVersion, incomingVersion);
        result.Conflicts = conflicts;

        // Create merged entity (start with current version)
        var merged = CloneEntity(currentVersion);

        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.CanWrite && !IsVersionMetadataProperty(p.Name))
            .ToList();

        foreach (var property in properties)
        {
            var baseValue = property.GetValue(baseVersion);
            var currentValue = property.GetValue(currentVersion);
            var incomingValue = property.GetValue(incomingVersion);

            var conflict = conflicts.FirstOrDefault(c => c.FieldName == property.Name);

            if (conflict != null)
            {
                // Handle conflict
                var resolution = fieldResolutions?.GetValueOrDefault(property.Name);
                var resolvedValue = ResolveConflict(conflict, currentValue, incomingValue, baseValue, strategy, resolution);

                if (resolvedValue.HasValue)
                {
                    property.SetValue(merged, resolvedValue.Value.value);
                    conflict.IsResolved = true;
                    conflict.ResolvedValue = resolvedValue.Value.value;
                    conflict.ResolutionStrategy = resolvedValue.Value.strategy;
                }
            }
            else
            {
                // No conflict - auto-merge
                var baseChanged = !AreEqual(baseValue, currentValue);
                var incomingChanged = !AreEqual(baseValue, incomingValue);

                if (incomingChanged && !baseChanged)
                {
                    // Only incoming changed - use incoming
                    property.SetValue(merged, incomingValue);
                    result.AutoMergedChanges.Add(new FieldChange
                    {
                        FieldName = property.Name,
                        FieldPath = property.Name,
                        OldValue = currentValue,
                        NewValue = incomingValue,
                        ChangeType = ChangeType.Modified
                    });
                }
                // If only current changed or both unchanged, keep current (already in merged)
            }
        }

        result.MergedEntity = merged;
        return result;
    }

    private (object? value, ResolutionStrategy strategy)? ResolveConflict(
        MergeConflict conflict,
        object? currentValue,
        object? incomingValue,
        object? baseValue,
        MergeStrategy mergeStrategy,
        ResolutionStrategy? fieldResolution)
    {
        // Use field-specific resolution if provided
        var strategy = fieldResolution ?? mergeStrategy switch
        {
            MergeStrategy.Ours => ResolutionStrategy.UseOurs,
            MergeStrategy.Theirs => ResolutionStrategy.UseTheirs,
            MergeStrategy.Manual => ResolutionStrategy.Manual,
            _ => ResolutionStrategy.Manual
        };

        return strategy switch
        {
            ResolutionStrategy.UseOurs => (currentValue, ResolutionStrategy.UseOurs),
            ResolutionStrategy.UseTheirs => (incomingValue, ResolutionStrategy.UseTheirs),
            ResolutionStrategy.UseBase => (baseValue, ResolutionStrategy.UseBase),
            ResolutionStrategy.Manual => null, // Requires manual resolution
            _ => null
        };
    }

    private ConflictType DetermineConflictType(object? baseValue, object? currentValue, object? incomingValue)
    {
        if (baseValue == null && currentValue != null && incomingValue != null)
            return ConflictType.BothAdded;

        if (baseValue != null)
        {
            if (currentValue == null && incomingValue != null)
                return ConflictType.ModifiedAndDeleted;

            if (currentValue != null && incomingValue == null)
                return ConflictType.DeletedAndModified;
        }

        return ConflictType.BothModified;
    }

    private bool AreEqual(object? a, object? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;

        // For complex types, use JSON comparison
        if (a.GetType().IsClass && a.GetType() != typeof(string))
        {
            var jsonA = JsonSerializer.Serialize(a);
            var jsonB = JsonSerializer.Serialize(b);
            return jsonA == jsonB;
        }

        return a.Equals(b);
    }

    private bool IsVersionMetadataProperty(string propertyName)
    {
        return propertyName is "Version" or "ContentHash" or "VersionCreatedAt" or
               "VersionCreatedBy" or "ParentVersion" or "Branch" or "CommitMessage" or
               "IsDeleted";
    }

    private T CloneEntity(T source)
    {
        var json = JsonSerializer.Serialize(source);
        return JsonSerializer.Deserialize<T>(json)!;
    }
}

/// <summary>
/// Advanced merge engine with semantic merge capabilities
/// </summary>
public class SemanticMergeEngine<T> : DefaultMergeEngine<T> where T : IVersionedEntity
{
    private readonly Dictionary<string, Func<object?, object?, object?, object?>> _customMergeFunctions = new();

    /// <summary>
    /// Register a custom merge function for a specific field
    /// </summary>
    public void RegisterMergeFunction(string fieldName, Func<object?, object?, object?, object?> mergeFunction)
    {
        _customMergeFunctions[fieldName] = mergeFunction;
    }

    /// <summary>
    /// Example: Merge arrays by combining unique elements
    /// </summary>
    public void RegisterArrayMerge<TElement>(string fieldName)
    {
        _customMergeFunctions[fieldName] = (baseVal, currentVal, incomingVal) =>
        {
            var currentList = (currentVal as IEnumerable<TElement>)?.ToList() ?? new List<TElement>();
            var incomingList = (incomingVal as IEnumerable<TElement>)?.ToList() ?? new List<TElement>();

            return currentList.Union(incomingList).Distinct().ToList();
        };
    }

    /// <summary>
    /// Example: Merge dictionaries by combining keys
    /// </summary>
    public void RegisterDictionaryMerge<TKey, TValue>(string fieldName) where TKey : notnull
    {
        _customMergeFunctions[fieldName] = (baseVal, currentVal, incomingVal) =>
        {
            var current = (currentVal as IDictionary<TKey, TValue>) ?? new Dictionary<TKey, TValue>();
            var incoming = (incomingVal as IDictionary<TKey, TValue>) ?? new Dictionary<TKey, TValue>();

            var merged = new Dictionary<TKey, TValue>(current);
            foreach (var kvp in incoming)
            {
                merged[kvp.Key] = kvp.Value; // Later value wins
            }

            return merged;
        };
    }
}
