# Automatic Geocoding on Upload

This directory contains services for automatic address detection and geocoding when users upload CSV/Excel files.

## Services

### AddressDetectionService
**File:** `AddressDetectionService.cs`

Intelligent detection of address columns in uploaded data files.

**Features:**
- Column name analysis (detects "address", "street", "city", etc.)
- Content pattern matching (US addresses, postal codes, lat/lon pairs)
- Confidence scoring (0-1) for each candidate column
- Multi-format support (single column, multi-column, international)
- Automatic configuration suggestion

**Usage:**
```csharp
var service = new AddressDetectionService(logger);
var candidates = service.DetectAddressColumns(parsedData);
var config = service.SuggestAddressConfiguration(parsedData);
```

### AutoGeocodingService
**File:** `AutoGeocodingService.cs`

Orchestrates the complete automatic geocoding workflow.

**Features:**
- Session management for geocoding operations
- Address extraction from parsed data
- Batch geocoding coordination
- Result application to features
- Progress tracking and statistics
- Retry logic for failed geocodes

**Usage:**
```csharp
var service = new AutoGeocodingService(
    addressDetection,
    batchGeocoding,
    csvGeocoding,
    logger);

var request = new AutoGeocodingRequest
{
    DatasetId = "dataset-123",
    ParsedData = parsedData,
    AddressConfiguration = config,
    Provider = "nominatim"
};

var result = await service.StartGeocodingAsync(request);
```

### CsvParser
**File:** `CsvParser.cs`

Parses CSV/TSV files and detects geographic data.

**Features:**
- CSV/TSV parsing with configurable delimiters
- Field type detection (string, number, date, etc.)
- Lat/lon column detection (IsLikelyLatitude, IsLikelyLongitude)
- Address column detection (IsLikelyAddress)
- Automatic geometry creation from lat/lon fields

## Models

### AutoGeocodingModels
**File:** `../../Models/Import/AutoGeocodingModels.cs`

Data models for automatic geocoding:
- `AutoGeocodingRequest` - Request to start geocoding
- `AutoGeocodingResult` - Result of geocoding operation
- `AutoGeocodingProgress` - Real-time progress updates
- `AddressDetectionResponse` - Detected address columns
- `GeocodedFeature` - Individual geocoded feature
- `AutoGeocodingStatistics` - Success rates, timing, etc.

### Address Detection Models
**File:** `AddressDetectionService.cs`

Models for address detection:
- `AddressColumnCandidate` - Detected address column with confidence
- `AddressConfiguration` - Configuration for extracting addresses
- `AddressColumnType` - Type of address column (FullAddress, Street, City, etc.)

## API Endpoints

**Controller:** `src/Honua.Server.Host/API/AutoGeocodingController.cs`

- `POST /api/v1/geocoding/auto/detect` - Detect address columns
- `POST /api/v1/geocoding/auto/start` - Start geocoding
- `GET /api/v1/geocoding/auto/session/{id}` - Get session status
- `POST /api/v1/geocoding/auto/retry` - Retry failed geocodes
- `GET /api/v1/geocoding/auto/providers` - List providers
- `GET /api/v1/geocoding/auto/examples` - Get examples

## Configuration

**File:** `src/Honua.Server.Host/appsettings.Geocoding.json`

```json
{
  "Geocoding": {
    "Providers": {
      "Nominatim": { "Enabled": true, ... },
      "Mapbox": { "Enabled": false, "ApiKey": "" },
      "Google": { "Enabled": false, "ApiKey": "" }
    },
    "AutoGeocoding": {
      "Enabled": true,
      "DefaultProvider": "nominatim",
      "MinConfidenceThreshold": 0.5,
      "MaxRowsAutoGeocode": 1000
    }
  }
}
```

## Workflow

```
1. User uploads CSV/Excel file
   ↓
2. CsvParser parses file → ParsedData
   ↓
3. AddressDetectionService analyzes columns → AddressColumnCandidates
   ↓
4. System suggests AddressConfiguration (or user customizes)
   ↓
5. User confirms configuration
   ↓
6. AutoGeocodingService starts geocoding
   ↓
7. BatchGeocodingService geocodes addresses (with rate limiting)
   ↓
8. Results applied to features (geometry added)
   ↓
9. Updated data returned to user
```

## Address Detection Algorithm

The address detection uses a scoring system combining multiple factors:

### Column Name Scoring (0-0.4)
- Full match on "address", "location" → +0.4
- Partial match on street keywords → +0.3
- Component match (city, state, zip) → +0.3

### Content Analysis Scoring (0-0.5)
- US address pattern (number + street) → Match
- Postal code pattern (ZIP, postal) → Match
- Lat/lon pair pattern → Match
- Comma-separated components → Partial match

### Confidence Calculation
```
confidence = (nameScore + contentScore) * (1 - nullRatio * 0.5)
```

### Quality Thresholds
- **High Confidence (>0.7)**: Auto-suggest for geocoding
- **Medium Confidence (0.5-0.7)**: Show as option
- **Low Confidence (<0.5)**: Available but not recommended

## Supported Address Formats

### 1. Single Column Full Address
```
"123 Main St, San Francisco, CA 94102"
"456 Oak Ave, Seattle, WA 98101"
```

### 2. Multi-Column Structured
```
Street: "123 Main St"
City: "San Francisco"
State: "CA"
Zip: "94102"
```

### 3. International
```
"1-1-1 Marunouchi, Chiyoda-ku, Tokyo, Japan"
"10 Downing Street, London, UK"
```

### 4. Lat/Lon Pairs
```
"37.7749, -122.4194"
"47.6062, -122.3321"
```

## Geocoding Providers

### Nominatim (OSM)
- **Free**: Yes
- **API Key**: Not required
- **Rate Limit**: 1 request/second
- **Best For**: Development, small datasets

### Mapbox
- **Free Tier**: 100,000 requests/month
- **API Key**: Required
- **Rate Limit**: 50 requests/second
- **Best For**: Production, moderate volumes

### Google Maps
- **Free Tier**: $200 credit/month (~40,000 requests)
- **API Key**: Required
- **Rate Limit**: 50 requests/second
- **Best For**: Highest accuracy, international

### Azure Maps
- **Free Tier**: 1,000 requests/month
- **API Key**: Required
- **Rate Limit**: 50 requests/second
- **Best For**: Azure-integrated applications

## Testing

### Unit Tests
```bash
dotnet test --filter Category=AddressDetection
dotnet test --filter Category=AutoGeocoding
```

### Integration Tests
```bash
dotnet test --filter Category=GeocodingIntegration
```

### Sample Data
Generate sample CSV with addresses:
```csharp
var csvService = new CsvGeocodingService(logger);
var sampleCsv = await csvService.GenerateSampleCsvAsync();
File.WriteAllBytes("sample-addresses.csv", sampleCsv);
```

## Performance Considerations

### Rate Limiting
- Nominatim: Max 1 req/sec (86,400/day)
- Mapbox: Max 50 req/sec (4,320,000/day)
- Configure `MaxConcurrentRequests` appropriately

### Batch Size
- Small (<100 rows): Process immediately
- Medium (100-1,000 rows): Process with progress updates
- Large (>1,000 rows): Consider background job queue

### Caching
- Cache geocoding results by address string
- Use Redis for distributed caching
- Expiration: 30 days (configurable)

## Error Handling

### Common Errors
1. **No Results Found**: Address not in geocoder database
2. **Ambiguous Results**: Multiple matches for address
3. **Rate Limited**: Too many requests
4. **Timeout**: Geocoding service slow/unavailable
5. **Invalid Address**: Malformed or incomplete address

### Retry Strategy
```
Attempt 1: Immediate
Attempt 2: 2 second delay
Attempt 3: 4 second delay
Attempt 4: 8 second delay (max)
```

## Documentation

- **Full Documentation**: `/docs/features/automatic-geocoding-on-upload.md`
- **Quick Start Guide**: `/docs/features/automatic-geocoding-quickstart.md`
- **API Reference**: Available at `/swagger` endpoint

## Dependencies

- `CsvHelper` - CSV parsing
- `System.Text.RegularExpressions` - Pattern matching
- Existing `BatchGeocodingService` - Geocoding API calls
- Existing `IGeocoder` implementations - Provider-specific logic

## Future Enhancements

- [ ] Excel (.xlsx) file support
- [ ] Address validation before geocoding
- [ ] Custom geocoding provider plugins
- [ ] Persistent session storage (Redis/Database)
- [ ] Real-time progress via SignalR
- [ ] Bulk retry operations UI
- [ ] Geocoding result caching
- [ ] Usage analytics and reporting
- [ ] Multi-language address support
- [ ] Fuzzy matching for typos
