// Power Query M code for Real-Time Sensor Observations (Streaming Dataset)
// This query connects to a Power BI Push Dataset for real-time updates

let
    // ===== IMPORTANT =====
    // This query is for reference only. Streaming datasets are created via the Power BI REST API.
    // Use the IPowerBIDatasetService.CreateStreamingDatasetAsync() method to create the dataset.
    // Once created, data flows automatically via the IPowerBIStreamingService.

    // ===== TO CONSUME STREAMING DATA IN POWER BI =====
    // 1. Use IPowerBIDatasetService to create a streaming dataset
    // 2. In Power BI Desktop, create a new dashboard
    // 3. Add a tile > REAL-TIME DATA > Custom Streaming Data
    // 4. Select your streaming dataset
    // 5. Choose visualization type (line chart, gauge, card, etc.)

    // ===== EXAMPLE: QUERY HISTORICAL DATA FROM STREAMING DATASET =====
    WorkspaceId = "your-workspace-id",
    DatasetId = "your-streaming-dataset-id",

    Source = PowerBI.Dataflows(null){[workspaceId=WorkspaceId]}[Data],
    Navigation = Source{[dataflowId=DatasetId]}[Data],
    ObservationsTable = Navigation{[entity="Observations"]}[Data]
in
    ObservationsTable

/*
STREAMING DATASET SCHEMA (configured in C#):

new Table
{
    Name = "Observations",
    Columns = new List<Column>
    {
        new Column { Name = "ObservationId", DataType = "string" },
        new Column { Name = "DatastreamId", DataType = "string" },
        new Column { Name = "DatastreamName", DataType = "string" },
        new Column { Name = "Result", DataType = "double" },
        new Column { Name = "ResultTime", DataType = "datetime" },
        new Column { Name = "PhenomenonTime", DataType = "datetime" },
        new Column { Name = "UnitOfMeasurement", DataType = "string" },
        new Column { Name = "ObservedPropertyName", DataType = "string" }
    }
}

VISUALIZATION EXAMPLES:
- Line Chart: ResultTime (X-axis), Result (Y-axis), filter by DatastreamName
- Gauge: Latest Result value, with min/max thresholds
- Card: COUNT(ObservationId) for total observations
- Map: Combine with location data from Features API
*/
