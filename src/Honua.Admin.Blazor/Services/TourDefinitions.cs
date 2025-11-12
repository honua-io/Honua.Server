// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Admin.Blazor.Services;

/// <summary>
/// Pre-built tour definitions for Honua Admin platform
/// </summary>
public static class TourDefinitions
{
    /// <summary>
    /// Welcome tour - introduces new users to the platform
    /// </summary>
    public static TourConfiguration WelcomeTour => new()
    {
        Steps = new List<TourStep>
        {
            new()
            {
                Id = "welcome",
                Title = "Welcome to Honua!",
                Text = @"
                    <p>Welcome to Honua, your comprehensive spatial data platform!</p>
                    <p>This quick tour will show you around the main features. It should only take about 2 minutes.</p>
                    <p>Click <strong>Next</strong> to continue, or <strong>Skip Tour</strong> to explore on your own.</p>
                "
            },
            new()
            {
                Id = "navigation",
                Title = "Navigation Menu",
                Text = @"
                    <p>The navigation menu on the left provides access to all major sections:</p>
                    <ul>
                        <li><strong>Services</strong> - Manage your spatial data services</li>
                        <li><strong>Layers</strong> - Configure map layers</li>
                        <li><strong>Maps</strong> - Create interactive maps</li>
                        <li><strong>Data Sources</strong> - Connect to your data</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".mud-drawer",
                    Position = TourStepPosition.Right
                }
            },
            new()
            {
                Id = "global-search",
                Title = "Global Search",
                Text = @"
                    <p>Use the global search to quickly find:</p>
                    <ul>
                        <li>Services and layers</li>
                        <li>Maps and folders</li>
                        <li>Data sources</li>
                        <li>Configuration settings</li>
                    </ul>
                    <p><strong>Tip:</strong> Press <code>Ctrl+K</code> (or <code>Cmd+K</code> on Mac) to focus the search from anywhere!</p>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".mud-autocomplete",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "theme-toggle",
                Title = "Customize Your Experience",
                Text = @"
                    <p>Toggle between light and dark mode to suit your preference.</p>
                    <p>Your theme choice is automatically saved for future sessions.</p>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = "button[title*='Mode']",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "license-tier",
                Title = "License & Features",
                Text = @"
                    <p>Your current license tier is displayed here.</p>
                    <p>Different tiers unlock advanced features like:</p>
                    <ul>
                        <li><strong>Professional:</strong> Advanced caching, versioning</li>
                        <li><strong>Enterprise:</strong> GeoETL workflows, custom transformations</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".mud-chip",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "next-steps",
                Title = "You're All Set!",
                Text = @"
                    <p>Great! You now know the basics of navigating Honua.</p>
                    <p><strong>Next steps:</strong></p>
                    <ul>
                        <li>Create your first service</li>
                        <li>Upload spatial data</li>
                        <li>Build an interactive map</li>
                    </ul>
                    <p>Check the <strong>Getting Started</strong> checklist in your dashboard for guided tasks!</p>
                "
            }
        },
        UseModalOverlay = true
    };

    /// <summary>
    /// Map creation tour - guides users through creating their first map
    /// </summary>
    public static TourConfiguration MapCreationTour => new()
    {
        Steps = new List<TourStep>
        {
            new()
            {
                Id = "map-intro",
                Title = "Create Your First Map",
                Text = @"
                    <p>Let's create an interactive map visualization!</p>
                    <p>Maps in Honua are powerful, customizable visualizations that can display multiple layers of spatial data.</p>
                "
            },
            new()
            {
                Id = "map-list",
                Title = "Maps Overview",
                Text = @"
                    <p>This page shows all your maps. You can:</p>
                    <ul>
                        <li>View and edit existing maps</li>
                        <li>Clone maps for quick variations</li>
                        <li>Delete maps you no longer need</li>
                        <li>Share maps with your team</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".mud-table",
                    Position = TourStepPosition.Top
                }
            },
            new()
            {
                Id = "create-map-button",
                Title = "Create a New Map",
                Text = @"
                    <p>Click this button to create a new map.</p>
                    <p>You'll be able to configure:</p>
                    <ul>
                        <li>Map name and description</li>
                        <li>Initial view (center and zoom)</li>
                        <li>Base map style</li>
                        <li>Layers to display</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = "button:has(.mud-icon-root)[aria-label*='Create']",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "map-layers",
                Title = "Add Layers to Your Map",
                Text = @"
                    <p>Maps can contain multiple layers of data:</p>
                    <ul>
                        <li><strong>Vector layers</strong> - Points, lines, polygons</li>
                        <li><strong>Raster layers</strong> - Satellite imagery, elevation</li>
                        <li><strong>Tile layers</strong> - Pre-rendered map tiles</li>
                    </ul>
                    <p>Each layer can be styled independently with custom colors, symbols, and labels.</p>
                "
            },
            new()
            {
                Id = "map-sharing",
                Title = "Share Your Map",
                Text = @"
                    <p>Once created, you can share your map in several ways:</p>
                    <ul>
                        <li><strong>Embed code</strong> - Add to your website</li>
                        <li><strong>Public link</strong> - Share with anyone</li>
                        <li><strong>API access</strong> - Integrate programmatically</li>
                    </ul>
                "
            },
            new()
            {
                Id = "map-complete",
                Title = "Ready to Create!",
                Text = @"
                    <p>You're ready to create amazing maps!</p>
                    <p><strong>Tips:</strong></p>
                    <ul>
                        <li>Start simple with one or two layers</li>
                        <li>Use layer groups to organize complex maps</li>
                        <li>Save your favorite styles as presets</li>
                    </ul>
                "
            }
        },
        UseModalOverlay = true
    };

    /// <summary>
    /// Data upload tour - shows users how to import spatial data
    /// </summary>
    public static TourConfiguration DataUploadTour => new()
    {
        Steps = new List<TourStep>
        {
            new()
            {
                Id = "upload-intro",
                Title = "Upload Spatial Data",
                Text = @"
                    <p>Honua supports a wide variety of spatial data formats.</p>
                    <p>This tour will show you how to import your data and make it available as a service.</p>
                "
            },
            new()
            {
                Id = "upload-formats",
                Title = "Supported Formats",
                Text = @"
                    <p>You can upload these popular formats:</p>
                    <ul>
                        <li><strong>GeoJSON</strong> - Web-friendly format</li>
                        <li><strong>Shapefile</strong> - Industry standard (.shp + supporting files)</li>
                        <li><strong>GeoPackage</strong> - Modern SQLite-based format</li>
                        <li><strong>CSV</strong> - With latitude/longitude columns</li>
                        <li><strong>KML/KMZ</strong> - Google Earth format</li>
                    </ul>
                "
            },
            new()
            {
                Id = "drag-drop",
                Title = "Drag and Drop",
                Text = @"
                    <p>Simply drag and drop your files here!</p>
                    <p>You can upload:</p>
                    <ul>
                        <li>Single files (GeoJSON, GeoPackage)</li>
                        <li>Multiple files (Shapefile components)</li>
                        <li>ZIP archives (containing spatial data)</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".mud-drop-zone",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "import-options",
                Title = "Configure Import",
                Text = @"
                    <p>After selecting a file, you can configure:</p>
                    <ul>
                        <li><strong>Service name</strong> - How it appears in Honua</li>
                        <li><strong>Coordinate system</strong> - Automatically detected or manual</li>
                        <li><strong>Field mapping</strong> - Map columns to attributes</li>
                        <li><strong>Styling</strong> - Default visualization style</li>
                    </ul>
                "
            },
            new()
            {
                Id = "import-progress",
                Title = "Track Import Progress",
                Text = @"
                    <p>For large files, you can monitor the import progress.</p>
                    <p>Imports run in the background, so you can continue working while data loads.</p>
                    <p>You'll receive a notification when the import completes.</p>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".mud-progress-linear",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "upload-complete",
                Title = "Data is Ready!",
                Text = @"
                    <p>Once imported, your data is immediately available as:</p>
                    <ul>
                        <li><strong>OGC Services</strong> - WFS, WMS, WMTS</li>
                        <li><strong>Vector Tiles</strong> - For fast rendering</li>
                        <li><strong>REST API</strong> - For custom integrations</li>
                    </ul>
                    <p>You can now add it to maps, dashboards, and share with your team!</p>
                "
            }
        },
        UseModalOverlay = true
    };

    /// <summary>
    /// Dashboard creation tour - introduces dashboard widgets and customization
    /// </summary>
    public static TourConfiguration DashboardTour => new()
    {
        Steps = new List<TourStep>
        {
            new()
            {
                Id = "dashboard-intro",
                Title = "Build Custom Dashboards",
                Text = @"
                    <p>Dashboards help you visualize and monitor your spatial data at a glance.</p>
                    <p>Let's explore how to create powerful, interactive dashboards!</p>
                "
            },
            new()
            {
                Id = "dashboard-widgets",
                Title = "Dashboard Widgets",
                Text = @"
                    <p>Honua offers various widget types:</p>
                    <ul>
                        <li><strong>Map Widget</strong> - Interactive map view</li>
                        <li><strong>Chart Widget</strong> - Bar, line, pie charts</li>
                        <li><strong>Stats Widget</strong> - Key metrics and KPIs</li>
                        <li><strong>Table Widget</strong> - Tabular data display</li>
                        <li><strong>Filter Widget</strong> - Interactive data filtering</li>
                    </ul>
                "
            },
            new()
            {
                Id = "add-widget",
                Title = "Add a Widget",
                Text = @"
                    <p>Click here to add a new widget to your dashboard.</p>
                    <p>You can drag and resize widgets to create the perfect layout!</p>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = "button:has(.mud-icon-root)[title*='Add Widget']",
                    Position = TourStepPosition.Bottom
                }
            },
            new()
            {
                Id = "widget-config",
                Title = "Configure Widgets",
                Text = @"
                    <p>Each widget can be customized:</p>
                    <ul>
                        <li>Connect to different data sources</li>
                        <li>Apply filters and queries</li>
                        <li>Customize colors and styling</li>
                        <li>Set refresh intervals</li>
                    </ul>
                "
            },
            new()
            {
                Id = "dashboard-layout",
                Title = "Arrange Your Layout",
                Text = @"
                    <p>Drag widgets to rearrange them.</p>
                    <p>Resize using the handles in the bottom-right corner.</p>
                    <p><strong>Tip:</strong> Hold Shift while dragging to snap to grid!</p>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = ".dashboard-grid",
                    Position = TourStepPosition.Top
                }
            },
            new()
            {
                Id = "dashboard-sharing",
                Title = "Share Dashboards",
                Text = @"
                    <p>Share your dashboard with teammates or make it public.</p>
                    <p>You can also schedule email reports to be sent automatically!</p>
                "
            },
            new()
            {
                Id = "dashboard-complete",
                Title = "Create Amazing Dashboards!",
                Text = @"
                    <p>You're ready to build insightful dashboards!</p>
                    <p><strong>Pro tips:</strong></p>
                    <ul>
                        <li>Use linked filters to create interactive experiences</li>
                        <li>Set up auto-refresh for real-time monitoring</li>
                        <li>Export dashboards as PDF for reports</li>
                    </ul>
                "
            }
        },
        UseModalOverlay = true
    };

    /// <summary>
    /// Sharing and collaboration tour
    /// </summary>
    public static TourConfiguration SharingTour => new()
    {
        Steps = new List<TourStep>
        {
            new()
            {
                Id = "sharing-intro",
                Title = "Sharing & Collaboration",
                Text = @"
                    <p>Honua makes it easy to collaborate with your team and share data with the world.</p>
                    <p>Let's explore the sharing and permission features!</p>
                "
            },
            new()
            {
                Id = "team-members",
                Title = "Invite Team Members",
                Text = @"
                    <p>Navigate to User Management to invite your team.</p>
                    <p>You can assign different roles:</p>
                    <ul>
                        <li><strong>Admin</strong> - Full access</li>
                        <li><strong>Editor</strong> - Create and modify content</li>
                        <li><strong>Viewer</strong> - Read-only access</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = "a[href='/users']",
                    Position = TourStepPosition.Right
                }
            },
            new()
            {
                Id = "rbac",
                Title = "Role-Based Access Control",
                Text = @"
                    <p>Create custom roles with specific permissions:</p>
                    <ul>
                        <li>Control who can view/edit services</li>
                        <li>Restrict access to sensitive data</li>
                        <li>Set up folder-level permissions</li>
                    </ul>
                ",
                AttachTo = new TourStepAttachment
                {
                    Element = "a[href='/roles']",
                    Position = TourStepPosition.Right
                }
            },
            new()
            {
                Id = "public-sharing",
                Title = "Public Sharing",
                Text = @"
                    <p>Make your maps and services publicly accessible:</p>
                    <ul>
                        <li>Generate shareable links</li>
                        <li>Embed maps in websites</li>
                        <li>Publish OGC services</li>
                        <li>Create public API endpoints</li>
                    </ul>
                    <p><strong>Note:</strong> Configure CORS settings for cross-origin access.</p>
                "
            },
            new()
            {
                Id = "api-access",
                Title = "API Access",
                Text = @"
                    <p>Every service automatically gets REST and OGC API endpoints:</p>
                    <ul>
                        <li><strong>REST API</strong> - GeoJSON, JSON, CSV</li>
                        <li><strong>WFS</strong> - OGC Web Feature Service</li>
                        <li><strong>WMS</strong> - OGC Web Map Service</li>
                        <li><strong>Vector Tiles</strong> - Mapbox Vector Tiles</li>
                    </ul>
                "
            },
            new()
            {
                Id = "sharing-complete",
                Title = "Collaborate Effectively!",
                Text = @"
                    <p>You now know how to share and collaborate in Honua!</p>
                    <p><strong>Remember:</strong></p>
                    <ul>
                        <li>Always review permissions before sharing</li>
                        <li>Use folders to organize shared content</li>
                        <li>Monitor API usage in the analytics dashboard</li>
                    </ul>
                "
            }
        },
        UseModalOverlay = true
    };

    /// <summary>
    /// Get all available tours
    /// </summary>
    public static Dictionary<string, TourConfiguration> GetAllTours() => new()
    {
        { "welcome-tour", WelcomeTour },
        { "map-creation-tour", MapCreationTour },
        { "data-upload-tour", DataUploadTour },
        { "dashboard-tour", DashboardTour },
        { "sharing-tour", SharingTour }
    };

    /// <summary>
    /// Get tour by ID
    /// </summary>
    public static TourConfiguration? GetTourById(string tourId)
    {
        var tours = GetAllTours();
        return tours.TryGetValue(tourId, out var tour) ? tour : null;
    }
}
