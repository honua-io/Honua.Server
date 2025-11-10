// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.MapSDK.Models;

/// <summary>
/// Template configuration for popup display
/// </summary>
public class PopupTemplate
{
    /// <summary>
    /// Template title (can include placeholders like {attribute_name})
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// HTML content template with placeholders
    /// </summary>
    public string? ContentTemplate { get; set; }

    /// <summary>
    /// Fields to display in the popup
    /// </summary>
    public List<PopupField> Fields { get; set; } = new();

    /// <summary>
    /// Actions/buttons to show in the popup
    /// </summary>
    public List<PopupAction> Actions { get; set; } = new();

    /// <summary>
    /// Maximum width of the popup in pixels
    /// </summary>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Maximum height of the popup in pixels
    /// </summary>
    public int? MaxHeight { get; set; }

    /// <summary>
    /// Show close button
    /// </summary>
    public bool ShowCloseButton { get; set; } = true;

    /// <summary>
    /// CSS class to apply to the popup
    /// </summary>
    public string? CssClass { get; set; }
}

/// <summary>
/// Field definition for popup display
/// </summary>
public class PopupField
{
    /// <summary>
    /// Field name/attribute name
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Display label for the field
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Field type (text, number, date, url, image, etc.)
    /// </summary>
    public PopupFieldType Type { get; set; } = PopupFieldType.Text;

    /// <summary>
    /// Format string for the field value
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Whether this field is visible
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Display order (lower numbers first)
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Condition for displaying this field (JavaScript expression)
    /// </summary>
    public string? VisibilityCondition { get; set; }
}

/// <summary>
/// Field types for popup display
/// </summary>
public enum PopupFieldType
{
    Text,
    Number,
    Date,
    DateTime,
    Boolean,
    Url,
    Email,
    Phone,
    Image,
    Html,
    Currency,
    Percentage
}

/// <summary>
/// Action button configuration for popup
/// </summary>
public class PopupAction
{
    /// <summary>
    /// Unique identifier for the action
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Display label for the action button
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// Icon name (MudBlazor icon)
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Action type
    /// </summary>
    public PopupActionType Type { get; set; } = PopupActionType.Custom;

    /// <summary>
    /// Button color
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Whether this action is visible
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Display order (lower numbers first)
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Tooltip text
    /// </summary>
    public string? Tooltip { get; set; }
}

/// <summary>
/// Predefined action types
/// </summary>
public enum PopupActionType
{
    Custom,
    ZoomTo,
    Edit,
    Delete,
    CopyCoordinates,
    Export,
    ViewDetails,
    OpenUrl
}

/// <summary>
/// Trigger mode for popup display
/// </summary>
public enum PopupTrigger
{
    /// <summary>
    /// Show popup on click
    /// </summary>
    Click,

    /// <summary>
    /// Show popup on hover
    /// </summary>
    Hover,

    /// <summary>
    /// Manual control only
    /// </summary>
    Manual
}
