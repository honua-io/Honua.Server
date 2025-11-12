# Automatic Geocoding on Upload

## Overview

Honua's Automatic Geocoding on Upload feature automatically detects address columns in CSV/Excel files and geocodes them to add geographic coordinates (latitude/longitude) to your data. This enables immediate visualization of address-based data on maps without manual geocoding steps.

## Features

### 1. Intelligent Address Detection
- **Automatic Column Detection**: Uses heuristics to identify address columns based on column names and content patterns
- **Confidence Scoring**: Provides confidence scores (0-1) for each detected address column
- **Multi-Format Support**: Handles various address formats:
  - US addresses (street number + street name)
  - International addresses
  - Structured addresses (separate columns for street, city, state, etc.)
  - Lat/lon coordinate pairs
- **Content Analysis**: Analyzes sample values to detect address patterns using regex

### 2. Flexible Address Configuration
- **Single Column**: Use one column containing complete addresses
- **Multi-Column**: Combine multiple columns (e.g., Street, City, State, Zip)
- **Custom Separator**: Configure how multiple columns are joined
- **User Confirmation**: Preview detected addresses before geocoding

### 3. Batch Geocoding
- **Multiple Providers**: Support for Nominatim (OSM), Mapbox, Google Maps, and Azure Maps
- **Rate Limiting**: Automatic rate limiting per provider to avoid throttling
- **Retry Logic**: Automatic retry with exponential backoff for failed requests
- **Progress Tracking**: Real-time progress updates (X of Y geocoded)
- **Parallel Processing**: Configurable concurrent requests for performance

### 4. Result Handling
- **Automatic Geometry Addition**: Adds GeoJSON Point geometry to features
- **Quality Metrics**: Success rate, confidence scores, ambiguous results
- **Error Handling**: Detailed error messages for failed geocodes
- **Result Export**: Export geocoded data with original columns plus lat/lon

## Architecture

### Services

#### AddressDetectionService
Located: `src/Honua.MapSDK/Services/Import/AddressDetectionService.cs`

Responsible for detecting address columns in uploaded data:
- Analyzes column names for address-related keywords
- Examines sample values for address patterns
- Calculates confidence scores
- Suggests optimal address configuration

**Key Methods:**
```csharp
List<AddressColumnCandidate> DetectAddressColumns(ParsedData parsedData)
AddressConfiguration? SuggestAddressConfiguration(ParsedData parsedData)
```

#### AutoGeocodingService
Located: `src/Honua.MapSDK/Services/Import/AutoGeocodingService.cs`

Orchestrates the entire geocoding workflow:
- Manages geocoding sessions
- Extracts addresses from features
- Coordinates with batch geocoding service
- Applies results back to features
- Tracks progress and statistics

**Key Methods:**
```csharp
Task<AddressDetectionResponse> DetectAddressColumnsAsync(string datasetId, ParsedData parsedData)
Task<AutoGeocodingResult> StartGeocodingAsync(AutoGeocodingRequest request, IProgress<AutoGeocodingProgress>? progress)
Task<AutoGeocodingResult> RetryFailedGeocodingAsync(RetryGeocodingRequest request)
```

#### BatchGeocodingService
Located: `src/Honua.MapSDK/Services/BatchGeocoding/BatchGeocodingService.cs`

Handles actual geocoding API calls:
- Rate limiting per provider
- Retry logic with exponential backoff
- Concurrent request management
- Result parsing and quality determination

### API Endpoints

Located: `src/Honua.Server.Host/API/AutoGeocodingController.cs`

#### POST /api/v1/geocoding/auto/detect
Detects address columns in uploaded data.

**Request:**
```json
{
  "datasetId": "upload-session-123",
  "parsedData": {
    "features": [...],
    "fields": [...]
  }
}
```

**Response:**
```json
{
  "datasetId": "upload-session-123",
  "candidates": [
    {
      "fieldName": "Address",
      "type": "FullAddress",
      "confidence": 0.85,
      "sampleValues": ["123 Main St, San Francisco, CA", ...]
    }
  ],
  "suggestedConfiguration": {
    "type": "SingleColumn",
    "singleColumnName": "Address",
    "confidence": 0.85
  },
  "totalRows": 500,
  "rowsWithGeometry": 0,
  "rowsToGeocode": 500,
  "sampleAddresses": ["123 Main St, San Francisco, CA", ...]
}
```

#### POST /api/v1/geocoding/auto/start
Starts automatic geocoding operation.

**Request:**
```json
{
  "datasetId": "upload-session-123",
  "parsedData": {...},
  "addressConfiguration": {
    "type": "SingleColumn",
    "singleColumnName": "Address"
  },
  "provider": "nominatim",
  "autoApply": true,
  "maxConcurrentRequests": 10,
  "skipExistingGeometry": true,
  "minConfidenceThreshold": 0.5
}
```

**Response:**
```json
{
  "datasetId": "upload-session-123",
  "sessionId": "geocoding-456",
  "status": "Completed",
  "statistics": {
    "totalRows": 500,
    "processedRows": 500,
    "successCount": 485,
    "failedCount": 15,
    "skippedCount": 0,
    "ambiguousCount": 12,
    "averageConfidence": 0.78,
    "averageTimeMs": 250,
    "successRate": 97.0,
    "progressPercentage": 100.0
  },
  "geocodedFeatures": [...],
  "provider": "nominatim",
  "duration": "00:02:05"
}
```

#### GET /api/v1/geocoding/auto/session/{sessionId}
Gets the status of a geocoding session.

#### POST /api/v1/geocoding/auto/retry
Retries failed geocoding operations.

#### GET /api/v1/geocoding/auto/providers
Lists available geocoding providers and their configurations.

#### GET /api/v1/geocoding/auto/examples
Returns example address configurations.

## Configuration

### Geocoding Providers

Configure providers in `appsettings.Geocoding.json`:

```json
{
  "Geocoding": {
    "Providers": {
      "Nominatim": {
        "Enabled": true,
        "BaseUrl": "https://nominatim.openstreetmap.org",
        "UserAgent": "Honua.Server/1.0",
        "RateLimit": {
          "MaxRequestsPerSecond": 1
        }
      },
      "Mapbox": {
        "Enabled": true,
        "ApiKey": "your-mapbox-api-key",
        "BaseUrl": "https://api.mapbox.com/geocoding/v5",
        "RateLimit": {
          "MaxRequestsPerSecond": 50
        }
      }
    }
  }
}
```

### Auto-Geocoding Settings

```json
{
  "Geocoding": {
    "AutoGeocoding": {
      "Enabled": true,
      "RequireUserConfirmation": true,
      "DefaultProvider": "nominatim",
      "MinConfidenceThreshold": 0.5,
      "MaxRowsAutoGeocode": 1000,
      "SkipExistingGeometry": true,
      "MaxConcurrentRequests": 10,
      "TimeoutMs": 10000,
      "MaxRetries": 3
    }
  }
}
```

### Environment Variables

Alternative to JSON configuration:
```bash
MAPBOX_API_KEY=your-mapbox-api-key
GOOGLE_MAPS_API_KEY=your-google-api-key
AZURE_MAPS_KEY=your-azure-maps-key
```

## Usage Examples

### Example 1: Single Address Column

**CSV Input:**
```csv
Name,Address,Phone
Acme Corp,"123 Main St, San Francisco, CA 94102",555-1234
Tech Inc,"456 Oak Ave, Seattle, WA 98101",555-5678
```

**Steps:**
1. Upload CSV file
2. API detects "Address" column with high confidence
3. User confirms or adjusts configuration
4. System geocodes all addresses
5. Result includes lat/lon for each row

**Output:**
```json
{
  "features": [
    {
      "properties": {
        "Name": "Acme Corp",
        "Address": "123 Main St, San Francisco, CA 94102",
        "Phone": "555-1234"
      },
      "geometry": {
        "type": "Point",
        "coordinates": [-122.4194, 37.7749]
      }
    }
  ]
}
```

### Example 2: Multi-Column Address

**CSV Input:**
```csv
Name,Street,City,State,Zip
Acme Corp,123 Main St,San Francisco,CA,94102
Tech Inc,456 Oak Ave,Seattle,WA,98101
```

**Configuration:**
```json
{
  "type": "MultiColumn",
  "multiColumnNames": ["Street", "City", "State", "Zip"],
  "separator": ", "
}
```

**System Action:**
1. Combines columns: "123 Main St, San Francisco, CA, 94102"
2. Geocodes combined address
3. Adds geometry to feature

### Example 3: Custom Provider

```csharp
var request = new AutoGeocodingRequest
{
    DatasetId = "my-dataset",
    ParsedData = parsedData,
    AddressConfiguration = new AddressConfiguration
    {
        Type = AddressConfigurationType.SingleColumn,
        SingleColumnName = "Location"
    },
    Provider = "mapbox", // Use Mapbox instead of Nominatim
    MaxConcurrentRequests = 50, // Higher rate limit
    MinConfidenceThreshold = 0.7 // Higher quality threshold
};

var result = await autoGeocodingService.StartGeocodingAsync(request);
```

## Best Practices

### 1. Provider Selection
- **Nominatim (Free)**: Good for development and small datasets (<1000 addresses)
  - Rate limit: 1 request/second
  - No API key required
  - Must include User-Agent header
- **Mapbox**: Good for production with moderate volumes
  - 100,000 free requests/month
  - 50 requests/second
- **Google Maps**: Best accuracy but highest cost
  - $5 per 1,000 requests (after free tier)
  - Good international coverage
- **Azure Maps**: Good for Azure-integrated applications
  - 1,000 free requests/month

### 2. Performance Optimization
- Set appropriate `MaxConcurrentRequests` based on provider limits
- Use caching to avoid re-geocoding same addresses
- Consider processing large datasets in batches
- Monitor rate limits to avoid throttling

### 3. Quality Control
- Set `MinConfidenceThreshold` to filter low-quality results
- Review ambiguous results (multiple matches)
- Manually verify failed geocodes
- Use retry functionality with different providers for failures

### 4. Data Privacy
- Avoid sending sensitive data to external geocoding services
- Review provider terms of service
- Consider self-hosted Nominatim for sensitive data

## Integration with Upload Workflow

### Typical Flow

1. **File Upload**
   ```
   User uploads CSV/Excel → CsvParser → ParsedData
   ```

2. **Address Detection**
   ```
   ParsedData → AddressDetectionService → AddressDetectionResponse
   ```

3. **User Confirmation**
   ```
   UI displays detected addresses → User confirms/adjusts → AddressConfiguration
   ```

4. **Geocoding**
   ```
   AutoGeocodingRequest → AutoGeocodingService → BatchGeocodingService → API calls
   ```

5. **Result Application**
   ```
   GeocodingResults → Add geometry to features → Updated ParsedData
   ```

6. **Data Storage**
   ```
   Updated ParsedData → Feature layer → Database
   ```

## Troubleshooting

### Common Issues

**Issue: Low success rate**
- Check address format in source data
- Try different geocoding provider
- Review failed addresses for patterns
- Increase timeout or retry settings

**Issue: Rate limiting errors**
- Reduce `MaxConcurrentRequests`
- Add delays between batches
- Upgrade provider plan or use different provider

**Issue: Poor address detection**
- Manually specify address configuration
- Check column names and sample data
- Ensure data is properly formatted (not merged cells, consistent formatting)

**Issue: Ambiguous results**
- Add more specific address components (city, state, country)
- Use structured multi-column format
- Manually verify ambiguous matches

## Monitoring and Metrics

The system tracks:
- Success rate by provider
- Average geocoding time
- Confidence score distribution
- Error rates and types
- Provider API usage

Access metrics via:
- API: `/api/v1/geocoding/auto/session/{sessionId}`
- Logs: Check Serilog output for detailed operation logs
- Metrics endpoint: `/metrics` (if enabled)

## Future Enhancements

Planned improvements:
- [ ] SignalR support for real-time progress updates
- [ ] Persistent session storage (Redis/Database)
- [ ] Bulk retry operations
- [ ] Custom geocoding provider plugins
- [ ] Address validation before geocoding
- [ ] Geocoding result caching
- [ ] Usage analytics dashboard
- [ ] Excel file support (currently CSV only)
- [ ] Geofencing/bounds filtering

## API Reference

See the full API documentation at:
- Swagger UI: `/swagger`
- OpenAPI spec: `/swagger/v1/swagger.json`

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/honua-server/issues
- Documentation: https://docs.honua.io
- Community: https://community.honua.io
