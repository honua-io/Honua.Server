# Automatic Geocoding - Quick Start Guide

## 5-Minute Setup

### 1. Enable Geocoding (Already enabled by default)

The automatic geocoding feature is enabled by default in Honua. To verify or customize settings, check `appsettings.Geocoding.json`.

### 2. Configure a Geocoding Provider

**Option A: Use Nominatim (Free, No API Key)**

Already configured! Nominatim works out of the box with 1 request/second rate limit.

**Option B: Use Mapbox (Requires API Key)**

1. Get a free API key from https://www.mapbox.com/
2. Add to your configuration:

```json
{
  "Geocoding": {
    "Providers": {
      "Mapbox": {
        "Enabled": true,
        "ApiKey": "your-mapbox-api-key-here"
      }
    },
    "AutoGeocoding": {
      "DefaultProvider": "mapbox"
    }
  }
}
```

Or use environment variable:
```bash
export MAPBOX_API_KEY=your-mapbox-api-key-here
```

### 3. Test with Sample Data

Create a file `sample-addresses.csv`:
```csv
Name,Address,City,State,Zip
Google HQ,1600 Amphitheatre Parkway,Mountain View,CA,94043
Microsoft,1 Microsoft Way,Redmond,WA,98052
Apple Park,1 Apple Park Way,Cupertino,CA,95014
```

### 4. Use the API

#### Step 1: Upload and Parse File

```bash
# Upload CSV file (replace with actual endpoint)
curl -X POST http://localhost:5000/api/v1/data/upload \
  -F "file=@sample-addresses.csv" \
  -H "Content-Type: multipart/form-data"
```

Response includes `datasetId` and `parsedData`.

#### Step 2: Detect Address Columns

```bash
curl -X POST http://localhost:5000/api/v1/geocoding/auto/detect \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "your-dataset-id",
    "parsedData": { ... }
  }'
```

Response:
```json
{
  "suggestedConfiguration": {
    "type": "SingleColumn",
    "singleColumnName": "Address",
    "confidence": 0.85
  },
  "sampleAddresses": [
    "1600 Amphitheatre Parkway, Mountain View, CA, 94043",
    "1 Microsoft Way, Redmond, WA, 98052"
  ]
}
```

#### Step 3: Start Geocoding

```bash
curl -X POST http://localhost:5000/api/v1/geocoding/auto/start \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "your-dataset-id",
    "parsedData": { ... },
    "addressConfiguration": {
      "type": "SingleColumn",
      "singleColumnName": "Address"
    },
    "provider": "nominatim"
  }'
```

Response:
```json
{
  "sessionId": "geocoding-session-123",
  "status": "Completed",
  "statistics": {
    "successCount": 3,
    "totalRows": 3,
    "successRate": 100.0
  }
}
```

### 5. View Results

The response includes geocoded features with coordinates:
```json
{
  "geocodedFeatures": [
    {
      "originalAddress": "1600 Amphitheatre Parkway, Mountain View, CA, 94043",
      "latitude": 37.4220,
      "longitude": -122.0841,
      "confidence": 0.95,
      "status": "Success"
    }
  ]
}
```

## Code Examples

### C# Client

```csharp
using Honua.MapSDK.Services.Import;
using Honua.MapSDK.Models.Import;

// 1. Parse uploaded file
var csvParser = new CsvParser();
var parsedData = await csvParser.ParseAsync(fileContent, fileName);

// 2. Detect addresses
var addressDetection = new AddressDetectionService(logger);
var candidates = addressDetection.DetectAddressColumns(parsedData);
var config = addressDetection.SuggestAddressConfiguration(parsedData);

// 3. Start geocoding
var autoGeocoding = new AutoGeocodingService(
    addressDetection,
    batchGeocoding,
    csvGeocoding,
    logger);

var request = new AutoGeocodingRequest
{
    DatasetId = "my-dataset",
    ParsedData = parsedData,
    AddressConfiguration = config!,
    Provider = "nominatim"
};

var result = await autoGeocoding.StartGeocodingAsync(request);

Console.WriteLine($"Geocoded {result.Statistics.SuccessCount} of {result.Statistics.TotalRows} addresses");
```

### JavaScript/TypeScript Client

```typescript
// 1. Upload and parse file
const formData = new FormData();
formData.append('file', file);

const uploadResponse = await fetch('/api/v1/data/upload', {
  method: 'POST',
  body: formData
});

const { datasetId, parsedData } = await uploadResponse.json();

// 2. Detect addresses
const detectionResponse = await fetch('/api/v1/geocoding/auto/detect', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ datasetId, parsedData })
});

const { suggestedConfiguration } = await detectionResponse.json();

// 3. Start geocoding
const geocodingResponse = await fetch('/api/v1/geocoding/auto/start', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    datasetId,
    parsedData,
    addressConfiguration: suggestedConfiguration,
    provider: 'nominatim'
  })
});

const result = await geocodingResponse.json();
console.log(`Success rate: ${result.statistics.successRate}%`);
```

### Python Client

```python
import requests
import json

# 1. Upload file
with open('addresses.csv', 'rb') as f:
    files = {'file': f}
    response = requests.post('http://localhost:5000/api/v1/data/upload', files=files)
    dataset_id = response.json()['datasetId']
    parsed_data = response.json()['parsedData']

# 2. Detect addresses
detection_response = requests.post(
    'http://localhost:5000/api/v1/geocoding/auto/detect',
    json={'datasetId': dataset_id, 'parsedData': parsed_data}
)
suggested_config = detection_response.json()['suggestedConfiguration']

# 3. Start geocoding
geocoding_response = requests.post(
    'http://localhost:5000/api/v1/geocoding/auto/start',
    json={
        'datasetId': dataset_id,
        'parsedData': parsed_data,
        'addressConfiguration': suggested_config,
        'provider': 'nominatim'
    }
)

result = geocoding_response.json()
print(f"Geocoded {result['statistics']['successCount']} addresses")
```

## Common Scenarios

### Scenario 1: CSV with Full Address Column

```csv
Store,Location
Store 1,"123 Main St, San Francisco, CA 94102"
Store 2,"456 Oak Ave, Seattle, WA 98101"
```

**Configuration:** Auto-detected, single column "Location"

### Scenario 2: CSV with Separate Address Components

```csv
Store,Street,City,State,Zip
Store 1,123 Main St,San Francisco,CA,94102
Store 2,456 Oak Ave,Seattle,WA,98101
```

**Configuration:** Multi-column ["Street", "City", "State", "Zip"]

### Scenario 3: International Addresses

```csv
Location,Address
Tokyo,"1-1-1 Marunouchi, Chiyoda-ku, Tokyo, Japan"
London,"10 Downing Street, London, UK"
Paris,"5 Avenue Anatole France, 75007 Paris, France"
```

**Configuration:** Single column "Address", use Google or Mapbox for better international coverage

### Scenario 4: Mixed Data with Some Addresses

```csv
Name,Description,Address,Notes
Item 1,Product A,123 Main St,In stock
Item 2,Product B,,Discontinued
Item 3,Product C,456 Oak Ave,On order
```

**Configuration:** Single column "Address", enable `SkipExistingGeometry: true`

## Testing

### Run Integration Tests

```bash
cd src/Honua.Server
dotnet test --filter Category=Geocoding
```

### Test with Sample Data

```bash
# Generate sample CSV
dotnet run --project Honua.MapSDK -- generate-sample-geocoding-csv

# Test geocoding
dotnet run --project Honua.MapSDK -- test-geocoding sample.csv
```

## Troubleshooting

### Issue: "Address column not detected"

**Solution:** Manually specify configuration:
```json
{
  "type": "SingleColumn",
  "singleColumnName": "YourColumnName"
}
```

### Issue: "Rate limit exceeded"

**Solution:** Reduce concurrent requests or use paid provider:
```json
{
  "MaxConcurrentRequests": 1,
  "Provider": "mapbox"
}
```

### Issue: "Low success rate"

**Solutions:**
1. Check address format consistency
2. Add city/state/country if missing
3. Try different provider (Google for best accuracy)
4. Use structured multi-column format

## Next Steps

- Read the full documentation: [automatic-geocoding-on-upload.md](./automatic-geocoding-on-upload.md)
- Configure production providers: [Configuration Guide](#configuration)
- Implement UI for user confirmation
- Set up monitoring and alerts
- Enable caching for better performance

## Support

- API Documentation: http://localhost:5000/swagger
- GitHub Issues: https://github.com/honua-io/honua-server/issues
- Discord Community: https://discord.gg/honua
