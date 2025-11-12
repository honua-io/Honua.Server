# Honua No-Code Dashboard Builder

## Overview

The Honua Dashboard Builder is a powerful no-code tool that allows business users to create interactive spatial dashboards without writing any code. It provides a drag-and-drop interface for building custom dashboards with maps, charts, tables, filters, and KPI cards.

## Features

### Widget Types

1. **Map Widget**
   - Interactive spatial visualization using MapSDK
   - Support for multiple layers and basemaps
   - Click interactions and feature selection
   - Navigation controls

2. **Chart Widget**
   - Multiple chart types: bar, line, pie, area, scatter
   - Customizable colors and styles
   - Legend and data label support
   - Responsive design

3. **Table Widget**
   - Sortable and filterable columns
   - Pagination support
   - Row selection
   - Custom column formatting

4. **Filter Panel Widget**
   - Multiple filter types: text, select, date, range
   - Auto-apply or manual apply modes
   - Connected to other widgets

5. **KPI Card Widget**
   - Aggregation functions: sum, avg, count, min, max
   - Trend indicators
   - Custom icons and colors
   - Number formatting

### Dashboard Features

- **Drag-and-Drop Designer**: Visual interface for arranging widgets
- **Grid Layout**: Responsive 12-column grid system
- **Real-time Updates**: Auto-refresh capabilities
- **Cross-Widget Filtering**: Connect widgets for interactive filtering
- **Templates**: Pre-built dashboard templates for common use cases
- **Sharing**: Public/private dashboard sharing
- **Export/Import**: JSON-based dashboard definitions
- **Themes**: Customizable colors and styling

## Getting Started

### 1. Access the Dashboard Builder

Navigate to `/dashboards` in the Honua Admin Portal to see your dashboard list.

### 2. Create a New Dashboard

Click the "New Dashboard" button to open the Dashboard Designer.

### 3. Add Widgets

From the Widget Library on the left:
- Click on a widget type to add it to the canvas
- Available widgets:
  - Map Widget
  - Chart Widget
  - Table Widget
  - Filter Panel
  - KPI Card

### 4. Configure Widgets

Select a widget on the canvas to configure it:
- **Properties Panel (Right)**: Edit widget title, position, and size
- **Data Source**: Select where the widget gets its data
  - Layer: Query data from a Honua layer
  - Query: Use a custom OData query
  - API: Fetch from an external API endpoint
  - Static: Use hardcoded data

### 5. Arrange Layout

- Position widgets by setting X, Y coordinates
- Resize widgets by adjusting Width and Height (in grid units)
- The grid uses 12 columns and configurable row height

### 6. Save Dashboard

Click the "Save" button in the toolbar to persist your dashboard.

### 7. Preview Dashboard

Click "Preview" to see your dashboard in runtime mode.

## Widget Configuration Guide

### Map Widget Configuration

```json
{
  "type": "map",
  "config": {
    "center": [-122.4194, 37.7749],
    "zoom": 10,
    "baseMapStyle": "mapbox://styles/mapbox/streets-v12",
    "showControls": true,
    "interactive": true,
    "layers": [
      {
        "id": "my-layer",
        "type": "fill",
        "paint": {
          "fill-color": "#4CAF50",
          "fill-opacity": 0.6
        }
      }
    ]
  },
  "dataSource": {
    "type": "layer",
    "source": "Properties"
  }
}
```

### Chart Widget Configuration

```json
{
  "type": "chart",
  "config": {
    "chartType": "bar",
    "xAxis": "category",
    "yAxis": ["revenue", "profit"],
    "colors": ["#4CAF50", "#2196F3"],
    "showLegend": true,
    "showDataLabels": false,
    "aggregation": "sum"
  },
  "dataSource": {
    "type": "layer",
    "source": "Sales",
    "filter": "year eq 2025"
  }
}
```

### Table Widget Configuration

```json
{
  "type": "table",
  "config": {
    "columns": [
      {
        "field": "name",
        "header": "Name",
        "sortable": true
      },
      {
        "field": "revenue",
        "header": "Revenue",
        "format": "C2",
        "sortable": true
      }
    ],
    "sortable": true,
    "filterable": true,
    "paginated": true,
    "pageSize": 25,
    "selectable": true
  }
}
```

### Filter Widget Configuration

```json
{
  "type": "filter",
  "config": {
    "filters": [
      {
        "id": "status",
        "label": "Status",
        "field": "status",
        "type": "select",
        "options": ["Active", "Inactive", "Pending"]
      },
      {
        "id": "date",
        "label": "Date Range",
        "field": "created_date",
        "type": "daterange"
      }
    ],
    "showActions": true,
    "autoApply": false
  }
}
```

### KPI Widget Configuration

```json
{
  "type": "kpi",
  "config": {
    "valueField": "revenue",
    "aggregation": "sum",
    "format": "C0",
    "prefix": "$",
    "suffix": "",
    "icon": "@Icons.Material.Filled.AttachMoney",
    "color": "#4CAF50",
    "showTrend": true,
    "trendComparison": "previous"
  }
}
```

## Data Sources

### Layer Data Source

Query data from a Honua layer using OData:

```json
{
  "type": "layer",
  "source": "Properties",
  "filter": "status eq 'active' and price gt 100000",
  "fields": ["id", "name", "price", "status"]
}
```

### API Data Source

Fetch data from an external API:

```json
{
  "type": "api",
  "source": "https://api.example.com/data",
  "refreshInterval": 60
}
```

### Static Data Source

Use hardcoded data for testing:

```json
{
  "type": "static",
  "data": [
    { "category": "A", "value": 100 },
    { "category": "B", "value": 200 },
    { "category": "C", "value": 150 }
  ]
}
```

## Dashboard Templates

Pre-built templates are available for common use cases:

1. **Sales Analytics Dashboard**
   - KPI cards for revenue, orders, and customers
   - Sales trend chart
   - Geographic sales distribution map
   - Top products table

2. **Operational Dashboard**
   - System status KPIs
   - Real-time monitoring
   - Performance metrics

3. **GIS Overview Dashboard**
   - Large interactive map
   - Layer filters
   - Feature statistics

4. **Real-time Monitoring Dashboard**
   - Live sensor locations
   - Time-series charts
   - Active alerts

5. **Executive Summary Dashboard**
   - High-level KPIs
   - Quarterly performance charts
   - Department breakdown

## Widget Interactions

### Cross-Widget Filtering

Connect widgets to enable interactive filtering:

1. Click on a map feature → Filter table to show related records
2. Select a table row → Highlight location on map
3. Apply filters → Update all connected widgets

Widget connections are defined in the dashboard definition:

```json
{
  "connections": [
    {
      "sourceWidgetId": "map-widget-id",
      "targetWidgetId": "table-widget-id",
      "type": "filter",
      "fieldMapping": {
        "id": "property_id"
      },
      "enabled": true
    }
  ]
}
```

## Dashboard Sharing

### Make Dashboard Public

1. Open dashboard in designer
2. Toggle "Public Dashboard" switch in settings panel
3. Save dashboard
4. Share the dashboard URL with others

### Clone Dashboard

Create a copy of an existing dashboard:
- From dashboard list, click "Clone" on any dashboard
- Modify the copy without affecting the original

## API Reference

### REST Endpoints

```
GET    /api/dashboards                      - List all dashboards
GET    /api/dashboards/{id}                 - Get dashboard by ID
GET    /api/dashboards/my-dashboards        - Get user's dashboards
GET    /api/dashboards/public               - Get public dashboards
GET    /api/dashboards/templates            - Get dashboard templates
GET    /api/dashboards/search?q={query}     - Search dashboards
POST   /api/dashboards                      - Create new dashboard
PUT    /api/dashboards/{id}                 - Update dashboard
DELETE /api/dashboards/{id}                 - Delete dashboard
POST   /api/dashboards/{id}/share           - Update sharing settings
POST   /api/dashboards/{id}/clone           - Clone dashboard
GET    /api/dashboards/{id}/export          - Export dashboard JSON
POST   /api/dashboards/import               - Import dashboard from JSON
```

### Example API Usage

**Create a Dashboard:**

```bash
curl -X POST https://your-honua-server/api/dashboards \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "name": "My Dashboard",
    "description": "Custom analytics dashboard",
    "widgets": [...],
    "layout": {...}
  }'
```

**Get Dashboard:**

```bash
curl https://your-honua-server/api/dashboards/{id} \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## Advanced Features

### Auto-Refresh

Set dashboard to auto-refresh data:
- Configure refresh interval in dashboard settings
- Minimum interval: 10 seconds
- Use for real-time monitoring dashboards

### Custom Themes

Customize dashboard appearance:

```json
{
  "theme": {
    "primary": "#1976D2",
    "secondary": "#DC004E",
    "background": "#FAFAFA",
    "textColor": "#333333",
    "fontFamily": "Roboto, sans-serif"
  }
}
```

### Export Dashboard

Export dashboard definition as JSON for:
- Backup
- Version control
- Sharing across environments
- Programmatic creation

## Troubleshooting

### Widget Not Loading Data

1. Check data source configuration
2. Verify layer/API endpoint exists
3. Check OData filter syntax
4. Review browser console for errors

### Dashboard Not Saving

1. Ensure you have edit permissions
2. Check dashboard name is not empty
3. Verify network connection
4. Check browser console for API errors

### Widget Positioning Issues

1. Verify X + Width ≤ 12 (grid columns)
2. Check for overlapping widgets
3. Ensure position values are positive integers

## Best Practices

1. **Performance**
   - Limit number of widgets per dashboard (< 15)
   - Use pagination for large tables
   - Set reasonable refresh intervals
   - Optimize OData queries with filters

2. **Design**
   - Group related widgets together
   - Use consistent colors across widgets
   - Provide meaningful widget titles
   - Add descriptions to dashboards

3. **Data**
   - Use appropriate aggregations
   - Filter data at the source when possible
   - Cache static reference data
   - Use auto-refresh only when needed

4. **Security**
   - Keep sensitive dashboards private
   - Use row-level security on layers
   - Validate all data sources
   - Review sharing settings regularly

## Support

For questions or issues:
- Documentation: `/docs/dashboard-builder`
- API Reference: `/api/swagger`
- Community: Honua Discussion Forum
- Email: support@honua.io

## Changelog

### Version 1.0 (2025-11-12)
- Initial release
- 5 widget types (Map, Chart, Table, Filter, KPI)
- Visual dashboard designer
- Dashboard templates
- Public/private sharing
- Export/import functionality
- Auto-refresh support
