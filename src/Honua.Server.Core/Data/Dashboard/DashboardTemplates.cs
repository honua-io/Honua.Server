// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models.Dashboard;

namespace Honua.Server.Core.Data.Dashboard;

/// <summary>
/// Provides pre-built dashboard templates for common use cases.
/// </summary>
public static class DashboardTemplates
{
    /// <summary>
    /// Get all available dashboard templates.
    /// </summary>
    public static List<DashboardDefinition> GetAllTemplates()
    {
        return new List<DashboardDefinition>
        {
            GetSalesAnalyticsDashboard(),
            GetOperationalDashboard(),
            GetGisOverviewDashboard(),
            GetRealtimeMonitoringDashboard(),
            GetExecutiveSummaryDashboard()
        };
    }

    /// <summary>
    /// Sales Analytics Dashboard - Shows sales metrics, charts, and geographic distribution.
    /// </summary>
    public static DashboardDefinition GetSalesAnalyticsDashboard()
    {
        return new DashboardDefinition
        {
            Name = "Sales Analytics Dashboard",
            Description = "Track sales performance with KPIs, charts, and geographic distribution",
            OwnerId = "system",
            IsTemplate = true,
            IsPublic = true,
            Tags = new List<string> { "Sales", "Analytics", "Business" },
            Layout = new DashboardLayout
            {
                Columns = 12,
                RowHeight = 60,
                Gap = 16
            },
            Widgets = new List<WidgetDefinition>
            {
                // KPI Cards Row
                new WidgetDefinition
                {
                    Title = "Total Revenue",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 0, Y = 0, Width = 3, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "revenue",
                        Aggregation = "sum",
                        Format = "C0",
                        Prefix = "$",
                        Icon = Icons.Material.Filled.AttachMoney,
                        Color = "#4CAF50",
                        ShowTrend = true
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "Sales"
                    }
                },
                new WidgetDefinition
                {
                    Title = "Total Orders",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 3, Y = 0, Width = 3, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "id",
                        Aggregation = "count",
                        Format = "N0",
                        Icon = Icons.Material.Filled.ShoppingCart,
                        Color = "#2196F3",
                        ShowTrend = true
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "Sales"
                    }
                },
                new WidgetDefinition
                {
                    Title = "Average Order Value",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 6, Y = 0, Width = 3, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "revenue",
                        Aggregation = "avg",
                        Format = "C2",
                        Prefix = "$",
                        Icon = Icons.Material.Filled.TrendingUp,
                        Color = "#FF9800"
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "Sales"
                    }
                },
                new WidgetDefinition
                {
                    Title = "Active Customers",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 9, Y = 0, Width = 3, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "customer_id",
                        Aggregation = "count",
                        Format = "N0",
                        Icon = Icons.Material.Filled.People,
                        Color = "#9C27B0"
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "Sales",
                        Filter = "status eq 'active'"
                    }
                },
                // Sales by Region Map
                new WidgetDefinition
                {
                    Title = "Sales by Region",
                    Type = "map",
                    Position = new WidgetPosition { X = 0, Y = 2, Width = 6, Height = 6 },
                    Config = new MapWidgetConfig
                    {
                        Center = new[] { -98.5795, 39.8283 }, // US center
                        Zoom = 4,
                        ShowControls = true,
                        Layers = new List<MapLayerConfig>()
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "SalesLocations"
                    }
                },
                // Monthly Sales Trend
                new WidgetDefinition
                {
                    Title = "Monthly Sales Trend",
                    Type = "chart",
                    Position = new WidgetPosition { X = 6, Y = 2, Width = 6, Height = 6 },
                    Config = new ChartWidgetConfig
                    {
                        ChartType = "line",
                        XAxis = "month",
                        YAxis = new List<string> { "revenue" },
                        ShowLegend = true,
                        ShowDataLabels = false,
                        Colors = new List<string> { "#4CAF50" }
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "MonthlySales"
                    }
                },
                // Top Products Table
                new WidgetDefinition
                {
                    Title = "Top Products",
                    Type = "table",
                    Position = new WidgetPosition { X = 0, Y = 8, Width = 12, Height = 4 },
                    Config = new TableWidgetConfig
                    {
                        Columns = new List<TableColumnConfig>
                        {
                            new TableColumnConfig { Field = "product_name", Header = "Product" },
                            new TableColumnConfig { Field = "quantity", Header = "Quantity Sold", Format = "N0" },
                            new TableColumnConfig { Field = "revenue", Header = "Revenue", Format = "C2" },
                            new TableColumnConfig { Field = "growth", Header = "Growth %", Format = "P1" }
                        },
                        Sortable = true,
                        Paginated = true,
                        PageSize = 10
                    },
                    DataSource = new WidgetDataSource
                    {
                        Type = "layer",
                        Source = "ProductSales"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Operational Dashboard - Real-time operations monitoring.
    /// </summary>
    public static DashboardDefinition GetOperationalDashboard()
    {
        return new DashboardDefinition
        {
            Name = "Operational Dashboard",
            Description = "Monitor operational metrics and performance in real-time",
            OwnerId = "system",
            IsTemplate = true,
            IsPublic = true,
            Tags = new List<string> { "Operations", "Monitoring", "Real-time" },
            Layout = new DashboardLayout
            {
                Columns = 12,
                RowHeight = 60,
                Gap = 16
            },
            RefreshInterval = 30, // Auto-refresh every 30 seconds
            Widgets = new List<WidgetDefinition>
            {
                new WidgetDefinition
                {
                    Title = "System Status",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 0, Y = 0, Width = 4, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "uptime",
                        Aggregation = "avg",
                        Format = "P1",
                        Suffix = " uptime",
                        Icon = Icons.Material.Filled.CheckCircle,
                        Color = "#4CAF50"
                    }
                },
                new WidgetDefinition
                {
                    Title = "Active Tasks",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 4, Y = 0, Width = 4, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "id",
                        Aggregation = "count",
                        Icon = Icons.Material.Filled.Assignment,
                        Color = "#2196F3"
                    }
                },
                new WidgetDefinition
                {
                    Title = "Response Time",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 8, Y = 0, Width = 4, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "response_time",
                        Aggregation = "avg",
                        Format = "N0",
                        Suffix = " ms",
                        Icon = Icons.Material.Filled.Speed,
                        Color = "#FF9800"
                    }
                }
            }
        };
    }

    /// <summary>
    /// GIS Overview Dashboard - Geographic data visualization.
    /// </summary>
    public static DashboardDefinition GetGisOverviewDashboard()
    {
        return new DashboardDefinition
        {
            Name = "GIS Overview Dashboard",
            Description = "Comprehensive geographic data visualization and analysis",
            OwnerId = "system",
            IsTemplate = true,
            IsPublic = true,
            Tags = new List<string> { "GIS", "Maps", "Spatial" },
            Layout = new DashboardLayout
            {
                Columns = 12,
                RowHeight = 60,
                Gap = 16
            },
            Widgets = new List<WidgetDefinition>
            {
                new WidgetDefinition
                {
                    Title = "Main Map",
                    Type = "map",
                    Position = new WidgetPosition { X = 0, Y = 0, Width = 9, Height = 10 },
                    Config = new MapWidgetConfig
                    {
                        Zoom = 10,
                        ShowControls = true,
                        Interactive = true
                    }
                },
                new WidgetDefinition
                {
                    Title = "Layer Filters",
                    Type = "filter",
                    Position = new WidgetPosition { X = 9, Y = 0, Width = 3, Height = 5 },
                    Config = new FilterWidgetConfig
                    {
                        Filters = new List<FilterControl>
                        {
                            new FilterControl { Id = "layer", Label = "Layer", Field = "layer_type", Type = "select", Options = new List<string> { "Points", "Lines", "Polygons" } },
                            new FilterControl { Id = "date", Label = "Date Range", Field = "created_date", Type = "daterange" }
                        },
                        AutoApply = true
                    }
                },
                new WidgetDefinition
                {
                    Title = "Feature Count",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 9, Y = 5, Width = 3, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "id",
                        Aggregation = "count",
                        Icon = Icons.Material.Filled.Layers
                    }
                }
            }
        };
    }

    /// <summary>
    /// Real-time Monitoring Dashboard - Live data streams and alerts.
    /// </summary>
    public static DashboardDefinition GetRealtimeMonitoringDashboard()
    {
        return new DashboardDefinition
        {
            Name = "Real-time Monitoring Dashboard",
            Description = "Monitor live data streams, alerts, and system health",
            OwnerId = "system",
            IsTemplate = true,
            IsPublic = true,
            Tags = new List<string> { "Real-time", "Monitoring", "IoT" },
            Layout = new DashboardLayout
            {
                Columns = 12,
                RowHeight = 60,
                Gap = 16
            },
            RefreshInterval = 10, // Refresh every 10 seconds
            Widgets = new List<WidgetDefinition>
            {
                new WidgetDefinition
                {
                    Title = "Live Sensor Locations",
                    Type = "map",
                    Position = new WidgetPosition { X = 0, Y = 0, Width = 8, Height = 8 },
                    Config = new MapWidgetConfig
                    {
                        Zoom = 12,
                        ShowControls = true
                    }
                },
                new WidgetDefinition
                {
                    Title = "Sensor Readings",
                    Type = "chart",
                    Position = new WidgetPosition { X = 8, Y = 0, Width = 4, Height = 4 },
                    Config = new ChartWidgetConfig
                    {
                        ChartType = "line",
                        XAxis = "timestamp",
                        YAxis = new List<string> { "value" },
                        ShowLegend = false
                    }
                },
                new WidgetDefinition
                {
                    Title = "Active Alerts",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 8, Y = 4, Width = 4, Height = 2 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "id",
                        Aggregation = "count",
                        Icon = Icons.Material.Filled.Warning,
                        Color = "#F44336"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Executive Summary Dashboard - High-level KPIs and trends.
    /// </summary>
    public static DashboardDefinition GetExecutiveSummaryDashboard()
    {
        return new DashboardDefinition
        {
            Name = "Executive Summary Dashboard",
            Description = "High-level overview of key business metrics and trends",
            OwnerId = "system",
            IsTemplate = true,
            IsPublic = true,
            Tags = new List<string> { "Executive", "Summary", "KPIs" },
            Layout = new DashboardLayout
            {
                Columns = 12,
                RowHeight = 60,
                Gap = 16
            },
            Theme = new DashboardTheme
            {
                Primary = "#1976D2",
                Background = "#FAFAFA"
            },
            Widgets = new List<WidgetDefinition>
            {
                new WidgetDefinition
                {
                    Title = "Revenue",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 0, Y = 0, Width = 3, Height = 3 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "revenue",
                        Aggregation = "sum",
                        Format = "C0",
                        Icon = Icons.Material.Filled.TrendingUp,
                        ShowTrend = true
                    }
                },
                new WidgetDefinition
                {
                    Title = "Customers",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 3, Y = 0, Width = 3, Height = 3 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "customer_id",
                        Aggregation = "count",
                        Icon = Icons.Material.Filled.People,
                        ShowTrend = true
                    }
                },
                new WidgetDefinition
                {
                    Title = "Growth Rate",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 6, Y = 0, Width = 3, Height = 3 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "growth_rate",
                        Aggregation = "avg",
                        Format = "P1",
                        Icon = Icons.Material.Filled.ShowChart
                    }
                },
                new WidgetDefinition
                {
                    Title = "Market Share",
                    Type = "kpi",
                    Position = new WidgetPosition { X = 9, Y = 0, Width = 3, Height = 3 },
                    Config = new KpiWidgetConfig
                    {
                        ValueField = "market_share",
                        Aggregation = "avg",
                        Format = "P1",
                        Icon = Icons.Material.Filled.PieChart
                    }
                },
                new WidgetDefinition
                {
                    Title = "Quarterly Performance",
                    Type = "chart",
                    Position = new WidgetPosition { X = 0, Y = 3, Width = 6, Height = 5 },
                    Config = new ChartWidgetConfig
                    {
                        ChartType = "bar",
                        XAxis = "quarter",
                        YAxis = new List<string> { "revenue", "profit" },
                        ShowLegend = true
                    }
                },
                new WidgetDefinition
                {
                    Title = "Department Performance",
                    Type = "chart",
                    Position = new WidgetPosition { X = 6, Y = 3, Width = 6, Height = 5 },
                    Config = new ChartWidgetConfig
                    {
                        ChartType = "pie",
                        XAxis = "department",
                        YAxis = new List<string> { "revenue" },
                        ShowLegend = true
                    }
                }
            }
        };
    }

    // Helper class for Material Icons (would be imported from MudBlazor in actual implementation)
    private static class Icons
    {
        public static class Material
        {
            public static class Filled
            {
                public const string AttachMoney = "@Icons.Material.Filled.AttachMoney";
                public const string ShoppingCart = "@Icons.Material.Filled.ShoppingCart";
                public const string TrendingUp = "@Icons.Material.Filled.TrendingUp";
                public const string People = "@Icons.Material.Filled.People";
                public const string CheckCircle = "@Icons.Material.Filled.CheckCircle";
                public const string Assignment = "@Icons.Material.Filled.Assignment";
                public const string Speed = "@Icons.Material.Filled.Speed";
                public const string Layers = "@Icons.Material.Filled.Layers";
                public const string Warning = "@Icons.Material.Filled.Warning";
                public const string ShowChart = "@Icons.Material.Filled.ShowChart";
                public const string PieChart = "@Icons.Material.Filled.PieChart";
            }
        }
    }
}
