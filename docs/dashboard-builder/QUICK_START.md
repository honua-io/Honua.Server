# Dashboard Builder - Quick Start Guide

## Create Your First Dashboard in 5 Minutes

### Step 1: Navigate to Dashboards

1. Open Honua Admin Portal
2. Click on **Dashboards** in the navigation menu
3. Click the **New Dashboard** button

### Step 2: Add Widgets

**Add a KPI Card:**
1. Click "KPI Card" in the Widget Library
2. Set title to "Total Revenue"
3. In the Properties panel:
   - Value Field: `revenue`
   - Aggregation: `sum`
   - Format: `C0` (currency)
4. Select Data Source: Layer → "Sales"

**Add a Chart:**
1. Click "Chart Widget" in the Widget Library
2. Set title to "Monthly Sales"
3. In the Properties panel:
   - Chart Type: `line`
   - X Axis: `month`
   - Y Axis: `revenue`
4. Select Data Source: Layer → "MonthlySales"

**Add a Map:**
1. Click "Map Widget" in the Widget Library
2. Set title to "Sales Locations"
3. Resize to fill left half of canvas
4. Select Data Source: Layer → "SalesLocations"

### Step 3: Arrange Layout

Drag widgets to arrange them:
- KPI cards at the top (row 0)
- Map on the left (rows 2-8)
- Chart on the right (rows 2-8)

Or use the Properties panel to set exact positions:
- **KPI**: X=0, Y=0, Width=3, Height=2
- **Map**: X=0, Y=2, Width=6, Height=6
- **Chart**: X=6, Y=2, Width=6, Height=6

### Step 4: Configure Dashboard Settings

In the left sidebar:
1. Set Dashboard Name: "Sales Dashboard"
2. Add Description: "Overview of sales performance"
3. Add Tags: "Sales", "Analytics"
4. Set Refresh Interval: 60 seconds (optional)

### Step 5: Save and Preview

1. Click **Save** button in the toolbar
2. Click **Preview** to view your dashboard in action

## Using Dashboard Templates

**Option 1: Start from Template**

1. Go to Dashboards → Templates tab
2. Browse available templates:
   - Sales Analytics Dashboard
   - Operational Dashboard
   - GIS Overview Dashboard
   - Real-time Monitoring Dashboard
   - Executive Summary Dashboard
3. Click **Use Template** on any template
4. Customize the widgets and data sources
5. Save as your own dashboard

**Option 2: Import Dashboard**

1. Get a dashboard JSON file
2. Click **Import** in Dashboards list
3. Paste or upload the JSON
4. Dashboard is created in your account

## Common Widget Configurations

### Revenue KPI

```
Title: Total Revenue
Type: KPI
Value Field: revenue
Aggregation: sum
Format: C0
Prefix: $
Icon: AttachMoney
Color: #4CAF50
Show Trend: Yes
```

### Sales Chart

```
Title: Sales by Category
Type: Chart
Chart Type: bar
X Axis: category
Y Axis: revenue, profit
Show Legend: Yes
Colors: #4CAF50, #2196F3
```

### Customer Table

```
Title: Customer List
Type: Table
Columns:
  - name (Customer Name)
  - email (Email)
  - total_purchases (Total Purchases, format: C2)
  - last_order_date (Last Order, format: yyyy-MM-dd)
Sortable: Yes
Paginated: Yes
Page Size: 25
```

### Date Filter

```
Title: Date Filter
Type: Filter
Filters:
  - Date Range (daterange, created_date field)
  - Status (select, status field, options: Active, Inactive)
Auto Apply: Yes
```

## Connecting Widgets

To create interactive dashboards where widgets communicate:

### Example: Map-to-Table Connection

1. Add a Map widget showing locations
2. Add a Table widget showing details
3. In Dashboard Designer, configure connection:
   - Source: Map widget
   - Target: Table widget
   - Type: filter
   - Field Mapping: `id` → `location_id`

When users click a map feature, the table automatically filters to show related records.

## Tips for Better Dashboards

1. **Start Simple**: Begin with 3-5 widgets, add more later
2. **Use Templates**: Modify existing templates rather than starting from scratch
3. **Test Data Sources**: Verify your layers/APIs work before building
4. **Mobile Friendly**: Keep widgets reasonably sized for responsive design
5. **Performance**: Use filters to limit data volume
6. **Naming**: Use clear, descriptive names for widgets and dashboards

## Next Steps

- Explore [Full Documentation](./README.md)
- Learn about [Widget Interactions](./README.md#widget-interactions)
- Review [API Reference](./README.md#api-reference)
- Check [Best Practices](./README.md#best-practices)

## Example Dashboard JSON

Here's a minimal dashboard definition you can import:

```json
{
  "name": "Simple Sales Dashboard",
  "description": "Basic sales overview",
  "layout": {
    "type": "grid",
    "columns": 12,
    "rowHeight": 60,
    "gap": 16
  },
  "widgets": [
    {
      "id": "kpi-1",
      "title": "Total Revenue",
      "type": "kpi",
      "position": { "x": 0, "y": 0, "width": 4, "height": 2 },
      "config": {
        "$type": "kpi",
        "valueField": "revenue",
        "aggregation": "sum",
        "format": "C0",
        "icon": "@Icons.Material.Filled.AttachMoney",
        "color": "#4CAF50"
      },
      "dataSource": {
        "type": "layer",
        "source": "Sales"
      }
    },
    {
      "id": "chart-1",
      "title": "Monthly Trend",
      "type": "chart",
      "position": { "x": 0, "y": 2, "width": 12, "height": 6 },
      "config": {
        "$type": "chart",
        "chartType": "line",
        "xAxis": "month",
        "yAxis": ["revenue"],
        "showLegend": true
      },
      "dataSource": {
        "type": "layer",
        "source": "MonthlySales"
      }
    }
  ],
  "tags": ["Sales"],
  "isPublic": false,
  "schemaVersion": "1.0"
}
```

Save this as `dashboard.json` and import it to get started quickly!
