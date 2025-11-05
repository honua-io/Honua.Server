// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to perform a bulk operation on multiple items.
/// </summary>
public sealed class BulkOperationRequest
{
    [JsonPropertyName("itemIds")]
    public List<string> ItemIds { get; set; } = new();

    [JsonPropertyName("operation")]
    public string Operation { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Request to bulk delete items.
/// </summary>
public sealed class BulkDeleteRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = new();

    [JsonPropertyName("force")]
    public bool Force { get; set; } = false;
}

/// <summary>
/// Request to bulk move items to a folder.
/// </summary>
public sealed class BulkMoveToFolderRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = new();

    [JsonPropertyName("targetFolderId")]
    public string? TargetFolderId { get; set; }
}

/// <summary>
/// Request to bulk update metadata fields.
/// </summary>
public sealed class BulkUpdateMetadataRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    [JsonPropertyName("keywords")]
    public List<string>? Keywords { get; set; }

    [JsonPropertyName("updateMode")]
    public string UpdateMode { get; set; } = "merge"; // merge, replace, append
}

/// <summary>
/// Request to bulk apply a style to layers.
/// </summary>
public sealed class BulkApplyStyleRequest
{
    [JsonPropertyName("layerIds")]
    public List<string> LayerIds { get; set; } = new();

    [JsonPropertyName("styleId")]
    public string StyleId { get; set; } = string.Empty;

    [JsonPropertyName("setAsDefault")]
    public bool SetAsDefault { get; set; } = false;
}

/// <summary>
/// Request to bulk enable/disable services.
/// </summary>
public sealed class BulkEnableServicesRequest
{
    [JsonPropertyName("serviceIds")]
    public List<string> ServiceIds { get; set; } = new();

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

/// <summary>
/// Response from a bulk operation.
/// </summary>
public sealed class BulkOperationResponse
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("successCount")]
    public int SuccessCount { get; set; }

    [JsonPropertyName("failureCount")]
    public int FailureCount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "completed"; // pending, in_progress, completed, failed

    [JsonPropertyName("results")]
    public List<BulkOperationItemResult> Results { get; set; } = new();

    [JsonPropertyName("startedAt")]
    public DateTimeOffset StartedAt { get; set; }

    [JsonPropertyName("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result for a single item in a bulk operation.
/// </summary>
public sealed class BulkOperationItemResult
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("itemName")]
    public string ItemName { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status of a long-running bulk operation.
/// </summary>
public sealed class BulkOperationStatus
{
    [JsonPropertyName("operationId")]
    public string OperationId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending"; // pending, in_progress, completed, failed

    [JsonPropertyName("progress")]
    public int Progress { get; set; } = 0; // 0-100

    [JsonPropertyName("processedItems")]
    public int ProcessedItems { get; set; }

    [JsonPropertyName("totalItems")]
    public int TotalItems { get; set; }

    [JsonPropertyName("currentItem")]
    public string? CurrentItem { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("canCancel")]
    public bool CanCancel { get; set; } = true;
}

/// <summary>
/// Bulk operation type enumeration.
/// </summary>
public enum BulkOperationType
{
    Delete,
    MoveToFolder,
    UpdateMetadata,
    ApplyStyle,
    EnableServices,
    DisableServices,
    ExportData,
    ClearCache
}

/// <summary>
/// Bulk selection state helper.
/// </summary>
public sealed class BulkSelectionState<T> where T : class
{
    public HashSet<string> SelectedIds { get; set; } = new();
    public bool IsAllSelected { get; set; } = false;
    public List<T> Items { get; set; } = new();

    public int SelectedCount => SelectedIds.Count;
    public bool HasSelection => SelectedIds.Count > 0;

    public void ToggleSelection(string id)
    {
        if (SelectedIds.Contains(id))
            SelectedIds.Remove(id);
        else
            SelectedIds.Add(id);

        UpdateAllSelectedState();
    }

    public void SelectAll(List<T> items, Func<T, string> idSelector)
    {
        Items = items;
        IsAllSelected = true;
        SelectedIds.Clear();
        foreach (var item in items)
        {
            SelectedIds.Add(idSelector(item));
        }
    }

    public void DeselectAll()
    {
        IsAllSelected = false;
        SelectedIds.Clear();
    }

    public bool IsSelected(string id) => SelectedIds.Contains(id);

    private void UpdateAllSelectedState()
    {
        if (Items.Count > 0)
            IsAllSelected = SelectedIds.Count == Items.Count;
        else
            IsAllSelected = false;
    }
}
