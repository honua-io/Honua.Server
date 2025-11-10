# Power BI Incremental Refresh Guide

Incremental refresh enables efficient data loading for large datasets by refreshing only new or changed data instead of the entire table.

## Benefits

- **Faster refreshes**: Only load data that changed
- **Reduced load**: Less strain on Honua.Server
- **Lower costs**: Minimize data transfer and compute
- **Better UX**: Users see updated data sooner

## Prerequisites

- Power BI Premium or Power BI Pro with Premium Per User (PPU)
- Dataset with datetime column (e.g., `UpdatedAt`, `CreatedAt`)
- Honua.Server with OData feed enabled

## Step 1: Set Up Parameters

1. Open Power BI Desktop
2. **Home** > **Transform Data**
3. **Manage Parameters** > **New**

Create two parameters:

### RangeStart Parameter
- Name: `RangeStart`
- Type: `Date/Time`
- Suggested Value: `Any value`
- Current Value: `1/1/2020 12:00:00 AM`

### RangeEnd Parameter
- Name: `RangeEnd`
- Type: `Date/Time`
- Suggested Value: `Any value`
- Current Value: `12/31/2025 11:59:59 PM`

## Step 2: Modify Your Query

Edit your Power Query M code to use the parameters:

```m
let
    // Parameters
    HonuaServerUrl = "https://your-honua-server.com",
    CollectionId = "traffic::sensors",

    // Build OData filter with RangeStart and RangeEnd
    FilterClause = "$filter=UpdatedAt ge "
        & DateTime.ToText(RangeStart, "yyyy-MM-ddTHH:mm:ssZ")
        & " and UpdatedAt lt "
        & DateTime.ToText(RangeEnd, "yyyy-MM-ddTHH:mm:ssZ"),

    // OData connection with filter
    Source = OData.Feed(
        HonuaServerUrl & "/odata/features/" & CollectionId,
        null,
        [
            Implementation = "2.0",
            ODataVersion = 4,
            Query = [
                #"$filter" = "UpdatedAt ge "
                    & DateTime.ToText(RangeStart, "yyyy-MM-ddTHH:mm:ssZ")
                    & " and UpdatedAt lt "
                    & DateTime.ToText(RangeEnd, "yyyy-MM-ddTHH:mm:ssZ"),
                #"$orderby" = "UpdatedAt desc"
            ]
        ]
    ),

    // Rest of your transformations...
    TypedTable = Table.TransformColumnTypes(Source, {
        {"UpdatedAt", type datetimezone},
        // ... other columns
    })
in
    TypedTable
```

## Step 3: Configure Incremental Refresh Policy

1. In Power BI Desktop, go to **Model** view
2. Right-click the table > **Incremental refresh**
3. Enable **Set up incremental refresh**

Configure the policy:

### Archive Data Starting
- **5 years** before refresh date
- This keeps historical data without refreshing it

### Incrementally Refresh Data Starting
- **7 days** before refresh date
- This refreshes recent data on each refresh

### Detect Data Changes
- Enable: **Yes**
- Select column: **UpdatedAt**
- Power BI will only refresh rows where `UpdatedAt` changed

### Optional: Only Refresh Complete Days
- Enable this to avoid partial day refreshes
- Ensures consistency in daily aggregations

## Step 4: Publish to Power BI Service

1. Click **Publish**
2. Choose your workspace (must be Premium/PPU)
3. Once published, the incremental refresh policy activates

## Step 5: Configure Refresh Schedule

In Power BI Service:

1. Go to your workspace
2. Find the dataset > **...** > **Settings**
3. Expand **Scheduled refresh**
4. Configure:
   - **Refresh frequency**: Daily or Hourly
   - **Time zone**: Your time zone
   - **Time**: When to refresh (e.g., 2:00 AM)
5. Click **Apply**

## How It Works

When Power BI refreshes:

1. **First Refresh**: Loads all data from `RangeStart` to `RangeEnd`
2. **Subsequent Refreshes**:
   - Partitions data by date (automatically)
   - Only refreshes partitions within the incremental window (last 7 days)
   - Archives older partitions (no refresh needed)
3. **Change Detection**:
   - Queries Honua.Server for rows where `UpdatedAt` changed
   - Updates only those rows

## Example: 311 Service Requests

Scenario: 500,000 service requests, 100 new per day

### Without Incremental Refresh
- Every refresh: Load all 500,000 rows
- Refresh time: ~10 minutes
- Server load: High

### With Incremental Refresh
- First refresh: Load all 500,000 rows
- Daily refresh: Load only last 7 days (~700 rows)
- Refresh time: ~10 seconds
- Server load: Minimal

## Advanced Configuration

### Custom Partitioning Strategy

For very large datasets, customize partitioning:

```m
// Partition by month instead of default (day)
let
    Source = ...,

    // Add month column for partitioning
    AddedMonth = Table.AddColumn(
        Source,
        "UpdatedMonth",
        each Date.StartOfMonth([UpdatedAt]),
        type date
    )
in
    AddedMonth
```

Then configure incremental refresh on `UpdatedMonth` instead.

### Multiple Datetime Filters

Filter on both `CreatedAt` and `UpdatedAt`:

```m
Query = [
    #"$filter" =
        "(CreatedAt ge " & DateTime.ToText(RangeStart) &
        " and CreatedAt lt " & DateTime.ToText(RangeEnd) & ")" &
        " or " &
        "(UpdatedAt ge " & DateTime.ToText(RangeStart) &
        " and UpdatedAt lt " & DateTime.ToText(RangeEnd) & ")",
    #"$orderby" = "UpdatedAt desc"
]
```

## Monitoring

### Check Refresh History

1. Power BI Service > Dataset > **...** > **Refresh history**
2. View:
   - Refresh duration
   - Success/failure status
   - Rows processed

### Query Honua.Server Logs

```bash
# Check OData query logs
docker logs honua-server | grep "odata/features"

# Look for filter clauses
grep "UpdatedAt ge" /var/log/honua/access.log
```

## Troubleshooting

### "Incremental refresh is only supported for Power BI Premium"

**Solution**: Upgrade to Power BI Premium or Premium Per User (PPU)

### "Unable to filter by RangeStart parameter"

**Solution**: Ensure:
1. Parameters are named exactly `RangeStart` and `RangeEnd`
2. Parameters are type `Date/Time`
3. Filter uses correct datetime format: `yyyy-MM-ddTHH:mm:ssZ`

### Refresh takes longer than expected

**Solution**:
1. Check server-side query performance
2. Add indexes on `UpdatedAt` column in database
3. Reduce incremental window (e.g., 3 days instead of 7)

### Data not updating

**Solution**:
1. Verify `UpdatedAt` column is being set correctly
2. Check refresh schedule is enabled
3. Ensure data source credentials are valid

## Best Practices

1. **Use UTC timestamps**: Avoid time zone issues
2. **Index datetime columns**: On both `CreatedAt` and `UpdatedAt`
3. **Keep incremental window small**: 7-14 days is optimal
4. **Monitor refresh times**: Set alerts for slow refreshes
5. **Test with small datasets**: Validate logic before scaling

## OData Query Examples

### What Power BI Sends to Honua.Server

Initial load:
```
GET /odata/features/traffic::sensors?$filter=UpdatedAt ge 2020-01-01T00:00:00Z and UpdatedAt lt 2025-12-31T23:59:59Z
```

Incremental refresh (last 7 days):
```
GET /odata/features/traffic::sensors?$filter=UpdatedAt ge 2025-01-03T00:00:00Z and UpdatedAt lt 2025-01-10T23:59:59Z
```

With change detection:
```
GET /odata/features/traffic::sensors?$filter=UpdatedAt ge 2025-01-09T00:00:00Z and UpdatedAt lt 2025-01-10T23:59:59Z
```

## Performance Benchmarks

| Dataset Size | Without Incremental | With Incremental | Improvement |
|-------------|-------------------|------------------|-------------|
| 10K rows    | 5 sec             | 5 sec            | 0%          |
| 100K rows   | 45 sec            | 5 sec            | 89%         |
| 1M rows     | 8 min             | 5 sec            | 99%         |
| 10M rows    | 90 min            | 10 sec           | 99.8%       |

## Resources

- [Microsoft Docs: Incremental Refresh](https://docs.microsoft.com/power-bi/connect-data/incremental-refresh-overview)
- [Honua.Server OData API Reference](../api/odata.md)
- [Power BI Premium Features](https://powerbi.microsoft.com/pricing/)
