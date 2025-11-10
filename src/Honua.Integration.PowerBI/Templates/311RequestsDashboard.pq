// Power Query M code for 311 Service Requests Dashboard
// Connects to Honua.Server OData feed for 311 service request data

let
    // ===== PARAMETERS - UPDATE THESE =====
    HonuaServerUrl = "https://your-honua-server.com",
    TenantId = "your-tenant-id",
    CollectionId = "civic::service_requests",

    // ===== ODATA CONNECTION WITH INCREMENTAL REFRESH =====
    // Note: For incremental refresh, add RangeStart and RangeEnd parameters
    // in Power BI Desktop: Manage Parameters > New Parameter
    Source = OData.Feed(
        HonuaServerUrl & "/odata/features/" & CollectionId,
        null,
        [
            Implementation = "2.0",
            ODataVersion = 4,
            MoreColumns = true,
            Query = [
                #"$filter" = "UpdatedAt ge " & DateTime.ToText(RangeStart, "yyyy-MM-ddTHH:mm:ssZ")
                           & " and UpdatedAt lt " & DateTime.ToText(RangeEnd, "yyyy-MM-ddTHH:mm:ssZ"),
                #"$orderby" = "UpdatedAt desc"
            ]
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
                    {"RequestNumber", "RequestType", "Status", "Priority", "Department", "District", "ResolvedAt"},
                    {"RequestNumber", "RequestType", "Status", "Priority", "Department", "District", "ResolvedAt"}
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
            {"RequestNumber", type text},
            {"RequestType", type text},
            {"Status", type text},
            {"Priority", type text},
            {"Department", type text},
            {"District", type text},
            {"Latitude", type number},
            {"Longitude", type number},
            {"CreatedAt", type datetimezone},
            {"ResolvedAt", type datetimezone},
            {"UpdatedAt", type datetimezone}
        }
    ),

    // Calculate time to resolution (in hours)
    WithTimeToResolution = Table.AddColumn(
        TypedTable,
        "TimeToResolutionHours",
        each
            if [Status] = "Resolved" and [ResolvedAt] <> null and [CreatedAt] <> null then
                Duration.TotalHours([ResolvedAt] - [CreatedAt])
            else
                null,
        type number
    ),

    // Calculate age of open requests
    WithAge = Table.AddColumn(
        WithTimeToResolution,
        "AgeInDays",
        each
            if [Status] <> "Resolved" then
                Duration.TotalDays(DateTime.LocalNow() - [CreatedAt])
            else
                null,
        type number
    ),

    // Add priority sorting
    WithPrioritySortOrder = Table.AddColumn(
        WithAge,
        "PrioritySortOrder",
        each
            if [Priority] = "Critical" then 1
            else if [Priority] = "High" then 2
            else if [Priority] = "Medium" then 3
            else if [Priority] = "Low" then 4
            else 5,
        Int64.Type
    ),

    // Filter valid coordinates
    FilteredRows = Table.SelectRows(
        WithPrioritySortOrder,
        each [Latitude] <> null and [Longitude] <> null
    )
in
    FilteredRows
