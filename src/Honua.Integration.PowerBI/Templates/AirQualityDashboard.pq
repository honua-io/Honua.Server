// Power Query M code for Air Quality Dashboard
// Connects to Honua.Server OData feed for air quality sensor data

let
    // ===== PARAMETERS - UPDATE THESE =====
    HonuaServerUrl = "https://your-honua-server.com",
    TenantId = "your-tenant-id",
    CollectionId = "environment::air_quality",

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
    ExpandedProperties =
        if Value.Is(Source[Properties]{0}, type text) then
            let
                ParsedJson = Table.TransformColumns(Source, {{"Properties", Json.Document}}),
                ExpandedTable = Table.ExpandRecordColumn(
                    ParsedJson,
                    "Properties",
                    {"PM25", "PM10", "NO2", "O3", "AQI", "AQICategory", "LocationName"},
                    {"PM25", "PM10", "NO2", "O3", "AQI", "AQICategory", "LocationName"}
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
            {"PM25", type number},
            {"PM10", type number},
            {"NO2", type number},
            {"O3", type number},
            {"AQI", Int64.Type},
            {"AQICategory", type text},
            {"LocationName", type text},
            {"UpdatedAt", type datetimezone}
        }
    ),

    // Calculate AQI Category if not provided
    WithAQICategory = Table.AddColumn(
        TypedTable,
        "CalculatedAQICategory",
        each
            if [AQI] <= 50 then "Good"
            else if [AQI] <= 100 then "Moderate"
            else if [AQI] <= 150 then "Unhealthy for Sensitive Groups"
            else if [AQI] <= 200 then "Unhealthy"
            else if [AQI] <= 300 then "Very Unhealthy"
            else "Hazardous",
        type text
    ),

    // Add color coding for AQI
    WithAQIColor = Table.AddColumn(
        WithAQICategory,
        "AQIColor",
        each
            if [AQI] <= 50 then "#00E400"
            else if [AQI] <= 100 then "#FFFF00"
            else if [AQI] <= 150 then "#FF7E00"
            else if [AQI] <= 200 then "#FF0000"
            else if [AQI] <= 300 then "#8F3F97"
            else "#7E0023",
        type text
    ),

    // Filter valid coordinates
    FilteredRows = Table.SelectRows(
        WithAQIColor,
        each [Latitude] <> null and [Longitude] <> null
    )
in
    FilteredRows
