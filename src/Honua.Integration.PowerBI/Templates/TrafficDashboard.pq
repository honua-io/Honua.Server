// Power Query M code for Traffic Monitoring Dashboard
// This query connects to Honua.Server OData feed and loads traffic sensor data
//
// Instructions:
// 1. Open Power BI Desktop
// 2. Get Data > Blank Query
// 3. Advanced Editor > Paste this code
// 4. Update the parameters section with your Honua.Server URL

let
    // ===== PARAMETERS - UPDATE THESE =====
    HonuaServerUrl = "https://your-honua-server.com",
    TenantId = "your-tenant-id",
    CollectionId = "traffic::sensors",

    // ===== ODATA CONNECTION =====
    Source = OData.Feed(
        HonuaServerUrl & "/odata/features/" & CollectionId,
        null,
        [
            Implementation = "2.0",
            ODataVersion = 4,
            MoreColumns = true
        ]
    ),

    // ===== DATA TRANSFORMATION =====
    // Expand properties column if it's a JSON string
    ExpandedProperties =
        if Value.Is(Source[Properties]{0}, type text) then
            let
                ParsedJson = Table.TransformColumns(Source, {{"Properties", Json.Document}}),
                ExpandedTable = Table.ExpandRecordColumn(
                    ParsedJson,
                    "Properties",
                    {"VehicleCount", "AverageSpeed", "CongestionLevel", "Direction", "LocationName"},
                    {"VehicleCount", "AverageSpeed", "CongestionLevel", "Direction", "LocationName"}
                )
            in
                ExpandedTable
        else
            Source,

    // Type conversion
    TypedTable = Table.TransformColumnTypes(
        ExpandedProperties,
        {
            {"Id", type text},
            {"DisplayName", type text},
            {"Latitude", type number},
            {"Longitude", type number},
            {"VehicleCount", Int64.Type},
            {"AverageSpeed", type number},
            {"CongestionLevel", type text},
            {"Direction", type text},
            {"LocationName", type text},
            {"CreatedAt", type datetimezone},
            {"UpdatedAt", type datetimezone}
        }
    ),

    // Add calculated columns
    WithCalculatedColumns = Table.AddColumn(
        TypedTable,
        "CongestionColor",
        each
            if [CongestionLevel] = "High" then "#FF0000"
            else if [CongestionLevel] = "Medium" then "#FFA500"
            else "#00FF00",
        type text
    ),

    // Add refresh timestamp
    WithRefreshTime = Table.AddColumn(
        WithCalculatedColumns,
        "LastRefresh",
        each DateTime.LocalNow(),
        type datetime
    ),

    // Filter out rows with null coordinates
    FilteredRows = Table.SelectRows(
        WithRefreshTime,
        each [Latitude] <> null and [Longitude] <> null
    )
in
    FilteredRows
