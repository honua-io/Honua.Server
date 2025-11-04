// Honua Power BI Custom Connector
// This connector enables Power BI to connect to Honua Geospatial Server
// Supports OGC Features API and STAC Catalogs

[Version = "1.0.0"]
section Honua;

// Data Source Kind definition
[DataSource.Kind="Honua", Publish="Honua.Publish"]
shared Honua.Contents = Value.ReplaceType(HonuaImpl, HonuaType);

// Type definition for the function
HonuaType = type function (
    serverUrl as (type text meta [
        Documentation.FieldCaption = "Server URL",
        Documentation.FieldDescription = "Base URL of your Honua server",
        Documentation.SampleValues = {"https://api.honua.io"}
    ]),
    dataSource as (type text meta [
        Documentation.FieldCaption = "Data Source",
        Documentation.FieldDescription = "Choose OGC Features or STAC",
        Documentation.AllowedValues = {"OGC Features", "STAC"}
    ]),
    optional collectionId as (type text meta [
        Documentation.FieldCaption = "Collection/Layer ID",
        Documentation.FieldDescription = "Specific collection or layer to query",
        Documentation.SampleValues = {"world-cities", "sentinel-2"}
    ]))
    as table meta [
        Documentation.Name = "Honua Geospatial Data",
        Documentation.LongDescription = "Connect to Honua geospatial server via OGC Features API and STAC catalogs. Retrieve vector features and spatiotemporal assets for analysis in Power BI."
    ];

// Implementation
HonuaImpl = (serverUrl as text, dataSource as text, optional collectionId as text) as table =>
    let
        // Normalize server URL
        normalizedUrl = Text.TrimEnd(serverUrl, "/"),

        // Build API endpoint based on data source
        apiEndpoint = if dataSource = "OGC Features" then
            normalizedUrl & "/ogc/features/v1/collections"
        else
            normalizedUrl & "/stac/collections",

        // If collectionId is provided, fetch items directly
        result = if collectionId <> null and collectionId <> "" then
            FetchCollectionItems(apiEndpoint, collectionId, dataSource)
        else
            FetchCollections(apiEndpoint, dataSource)
    in
        result;

// Fetch collections list
FetchCollections = (apiEndpoint as text, dataSource as text) as table =>
    let
        // Fetch collections
        response = Web.Contents(apiEndpoint),
        json = Json.Document(response),
        collections = json[collections],

        // Convert to table
        collectionsTable = Table.FromList(collections, Splitter.SplitByNothing(), null, null, ExtraValues.Error),
        expandedTable = Table.ExpandRecordColumn(collectionsTable, "Column1",
            {"id", "title", "description", "extent", "itemType", "crs"},
            {"CollectionId", "Title", "Description", "Extent", "ItemType", "CRS"}),

        // Add function to fetch items for each collection
        withItemsFunction = Table.AddColumn(expandedTable, "Items",
            each FetchCollectionItems(apiEndpoint, [CollectionId], dataSource), type table)
    in
        withItemsFunction;

// Fetch items from a specific collection
FetchCollectionItems = (apiEndpoint as text, collectionId as text, dataSource as text) as table =>
    let
        // Build items URL
        itemsUrl = apiEndpoint & "/" & collectionId & "/items?limit=1000",

        // Fetch all pages
        allPages = FetchAllPages(itemsUrl),

        // Combine all items
        allItems = List.Combine(List.Transform(allPages, each _[items])),

        // Convert to table based on data source type
        itemsTable = if dataSource = "OGC Features" then
            ProcessOGCFeatures(allItems)
        else
            ProcessSTACItems(allItems)
    in
        itemsTable;

// Fetch all pages with pagination
FetchAllPages = (url as text) as list =>
    let
        // Function to fetch a single page
        FetchPage = (pageUrl as text) as record =>
            let
                response = Web.Contents(pageUrl),
                json = Json.Document(response),
                items = if Record.HasFields(json, "features") then json[features] else json[items],
                links = json[links],
                nextLink = List.First(List.Select(links, each [rel] = "next"), null),
                nextUrl = if nextLink <> null then nextLink[href] else null
            in
                [items = items, nextUrl = nextUrl],

        // Recursively fetch all pages
        FetchAllPagesImpl = (currentUrl as text, accumulator as list) as list =>
            let
                page = FetchPage(currentUrl),
                newAccumulator = accumulator & {page},
                result = if page[nextUrl] <> null then
                    @FetchAllPagesImpl(page[nextUrl], newAccumulator)
                else
                    newAccumulator
            in
                result,

        pages = FetchAllPagesImpl(url, {})
    in
        pages;

// Process OGC Features into table format
ProcessOGCFeatures = (features as list) as table =>
    let
        // Convert list to table
        featuresTable = Table.FromList(features, Splitter.SplitByNothing(), null, null, ExtraValues.Error),

        // Expand main fields
        expandedTable = Table.ExpandRecordColumn(featuresTable, "Column1",
            {"id", "type", "geometry", "properties", "bbox"},
            {"FeatureId", "Type", "Geometry", "Properties", "BBox"}),

        // Extract geometry details
        withGeometryType = Table.AddColumn(expandedTable, "GeometryType",
            each if [Geometry] <> null then [Geometry][type] else null, type text),

        withCoordinates = Table.AddColumn(withGeometryType, "Coordinates",
            each if [Geometry] <> null then [Geometry][coordinates] else null),

        // Extract centroid (latitude/longitude)
        withLatLon = Table.AddColumn(withCoordinates, "Latitude",
            each ExtractLatitude([GeometryType], [Coordinates]), type number),

        withLongitude = Table.AddColumn(withLatLon, "Longitude",
            each ExtractLongitude([GeometryType], [Coordinates]), type number),

        // Extract bounding box
        withBBoxMinX = Table.AddColumn(withLongitude, "BBoxMinX",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{0} else null, type number),

        withBBoxMinY = Table.AddColumn(withBBoxMinX, "BBoxMinY",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{1} else null, type number),

        withBBoxMaxX = Table.AddColumn(withBBoxMinY, "BBoxMaxX",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{2} else null, type number),

        withBBoxMaxY = Table.AddColumn(withBBoxMaxX, "BBoxMaxY",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{3} else null, type number),

        // Expand properties as separate columns (flatten common properties)
        withFlattenedProps = ExpandProperties(withBBoxMaxY),

        // Remove intermediate columns
        finalTable = Table.RemoveColumns(withFlattenedProps, {"Coordinates", "BBox"})
    in
        finalTable;

// Process STAC Items into table format
ProcessSTACItems = (items as list) as table =>
    let
        // Convert list to table
        itemsTable = Table.FromList(items, Splitter.SplitByNothing(), null, null, ExtraValues.Error),

        // Expand main fields
        expandedTable = Table.ExpandRecordColumn(itemsTable, "Column1",
            {"id", "type", "stac_version", "geometry", "bbox", "properties", "assets", "collection", "links"},
            {"ItemId", "Type", "STACVersion", "Geometry", "BBox", "Properties", "Assets", "Collection", "Links"}),

        // Extract geometry details
        withGeometryType = Table.AddColumn(expandedTable, "GeometryType",
            each if [Geometry] <> null then [Geometry][type] else null, type text),

        withCoordinates = Table.AddColumn(withGeometryType, "Coordinates",
            each if [Geometry] <> null then [Geometry][coordinates] else null),

        // Extract centroid
        withLatLon = Table.AddColumn(withCoordinates, "Latitude",
            each ExtractLatitude([GeometryType], [Coordinates]), type number),

        withLongitude = Table.AddColumn(withLatLon, "Longitude",
            each ExtractLongitude([GeometryType], [Coordinates]), type number),

        // Extract bounding box
        withBBoxMinX = Table.AddColumn(withLongitude, "BBoxMinX",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{0} else null, type number),

        withBBoxMinY = Table.AddColumn(withBBoxMinX, "BBoxMinY",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{1} else null, type number),

        withBBoxMaxX = Table.AddColumn(withBBoxMinY, "BBoxMaxX",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{2} else null, type number),

        withBBoxMaxY = Table.AddColumn(withBBoxMaxX, "BBoxMaxY",
            each if [BBox] <> null and List.Count([BBox]) >= 4 then [BBox]{3} else null, type number),

        // Extract datetime from properties
        withDateTime = Table.AddColumn(withBBoxMaxY, "DateTime",
            each if [Properties] <> null and Record.HasFields([Properties], "datetime")
                then DateTimeZone.FromText([Properties][datetime])
                else null, type datetime),

        // Flatten common STAC properties
        withFlattenedProps = ExpandSTACProperties(withDateTime),

        // Expand assets as JSON text
        withAssetsJson = Table.AddColumn(withFlattenedProps, "AssetsJson",
            each if [Assets] <> null then Json.FromValue([Assets]) else null, type text),

        // Remove intermediate columns
        finalTable = Table.RemoveColumns(withAssetsJson, {"Coordinates", "BBox", "Links"})
    in
        finalTable;

// Extract latitude from geometry
ExtractLatitude = (geometryType as nullable text, coordinates as any) as nullable number =>
    if geometryType = null or coordinates = null then
        null
    else if geometryType = "Point" then
        coordinates{1}
    else
        null; // For complex geometries, would need bbox centroid

// Extract longitude from geometry
ExtractLongitude = (geometryType as nullable text, coordinates as any) as nullable number =>
    if geometryType = null or coordinates = null then
        null
    else if geometryType = "Point" then
        coordinates{0}
    else
        null; // For complex geometries, would need bbox centroid

// Expand common properties as separate columns
ExpandProperties = (table as table) as table =>
    let
        // Get all unique property keys from first 100 rows
        sampleRows = Table.FirstN(table, 100),
        allKeys = List.Distinct(List.Combine(
            List.Transform(
                Table.Column(sampleRows, "Properties"),
                each if _ <> null then Record.FieldNames(_) else {}
            )
        )),

        // Common property names to flatten
        commonProps = List.Intersect({allKeys, {"name", "title", "description", "code", "type", "category", "value", "status"}}),

        // Add columns for each common property
        result = List.Accumulate(
            commonProps,
            table,
            (tbl, propName) =>
                Table.AddColumn(tbl, "Prop_" & propName,
                    each if [Properties] <> null and Record.HasFields([Properties], propName)
                        then Record.Field([Properties], propName)
                        else null, type text)
        )
    in
        result;

// Expand common STAC properties
ExpandSTACProperties = (table as table) as table =>
    let
        // Common STAC property names
        commonProps = {"platform", "instruments", "constellation", "mission", "gsd", "eo:cloud_cover", "view:sun_azimuth", "view:sun_elevation"},

        // Add columns for each common property
        result = List.Accumulate(
            commonProps,
            table,
            (tbl, propName) =>
                Table.AddColumn(tbl, "STAC_" & Text.Replace(propName, ":", "_"),
                    each if [Properties] <> null and Record.HasFields([Properties], propName)
                        then Record.Field([Properties], propName)
                        else null)
        )
    in
        result;

// Data Source Kind description
Honua = [
    TestConnection = (dataSourcePath) =>
        {"Honua.Contents", dataSourcePath[serverUrl], dataSourcePath[dataSource], dataSourcePath[collectionId]},
    Authentication = [
        Anonymous = [],
        UsernamePassword = [],
        Key = [
            KeyLabel = "API Key",
            Label = "API Key Authentication"
        ],
        OAuth = [
            StartLogin = StartLogin,
            FinishLogin = FinishLogin,
            Refresh = Refresh,
            Label = "OAuth 2.0"
        ]
    ],
    Label = Extension.LoadString("DataSourceLabel")
];

// OAuth implementation placeholders
StartLogin = (resourceUrl, state, display) =>
    let
        authorizeUrl = resourceUrl & "/oauth/authorize?" & Uri.BuildQueryString([
            client_id = "powerbi-connector",
            response_type = "code",
            state = state,
            redirect_uri = "https://oauth.powerbi.com/views/oauthredirect.html"
        ])
    in
        [
            LoginUri = authorizeUrl
        ];

FinishLogin = (context, callbackUri, state) =>
    let
        parts = Uri.Parts(callbackUri)[Query],
        result = if Record.HasFields(parts, {"code"}) then
            TokenMethod("authorization_code", parts[code])
        else
            error Error.Record("Authentication.Failed", parts[error], parts[error_description])
    in
        result;

Refresh = (resourceUrl, refresh_token) =>
    TokenMethod("refresh_token", refresh_token);

TokenMethod = (grantType, code) =>
    let
        response = Web.Contents("https://api.honua.io/oauth/token", [
            Content = Text.ToBinary(Uri.BuildQueryString([
                client_id = "powerbi-connector",
                grant_type = grantType,
                code = code
            ])),
            Headers = [
                #"Content-Type" = "application/x-www-form-urlencoded",
                #"Accept" = "application/json"
            ]
        ]),
        body = Json.Document(response)
    in
        body;

// Publish configuration
Honua.Publish = [
    Beta = false,
    Category = "Other",
    ButtonText = {Extension.LoadString("ButtonTitle"), Extension.LoadString("ButtonHelp")},
    LearnMoreUrl = "https://docs.honua.io/powerbi",
    SourceImage = Honua.Icons,
    SourceTypeImage = Honua.Icons
];

Honua.Icons = [
    Icon16 = {
        Extension.Contents("Honua16.png"),
        Extension.Contents("Honua20.png"),
        Extension.Contents("Honua24.png"),
        Extension.Contents("Honua32.png")
    },
    Icon32 = {
        Extension.Contents("Honua32.png"),
        Extension.Contents("Honua40.png"),
        Extension.Contents("Honua48.png"),
        Extension.Contents("Honua64.png")
    }
];
