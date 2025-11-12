# AI Map Generation - Quick Start Guide

This guide will help you quickly set up and test the AI-Powered Map Generation feature.

## Prerequisites

- OpenAI API key OR Azure OpenAI credentials
- Honua.Server running locally or deployed
- Access to Honua Admin portal

## Step 1: Configuration

### Option A: Using OpenAI

Add to `/home/user/Honua.Server/src/Honua.Server.Host/appsettings.Development.json`:

```json
{
  "MapAI": {
    "ApiKey": "sk-your-openai-api-key-here",
    "Model": "gpt-4",
    "IsAzure": false
  }
}
```

### Option B: Using Azure OpenAI

```json
{
  "MapAI": {
    "ApiKey": "your-azure-openai-key",
    "Model": "gpt-4-deployment-name",
    "IsAzure": true,
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiVersion": "2024-02-15-preview"
  }
}
```

## Step 2: Register Services

Add to `/home/user/Honua.Server/src/Honua.Server.Host/Program.cs`:

```csharp
using Honua.Server.Core.Maps.AI;

// Add this with other service registrations
builder.Services.AddMapGenerationAi(builder.Configuration);
```

## Step 3: Build and Run

```bash
cd /home/user/Honua.Server/src/Honua.Server.Host
dotnet build
dotnet run
```

## Step 4: Test the API

### Check Service Health

```bash
curl http://localhost:5000/api/maps/ai/health
```

Expected response:
```json
{
  "available": true,
  "message": "AI map generation service is available"
}
```

### Get Example Prompts

```bash
curl http://localhost:5000/api/maps/ai/examples
```

### Generate a Simple Map

```bash
curl -X POST http://localhost:5000/api/maps/ai/generate \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Show me all schools in San Francisco"
  }'
```

Expected response structure:
```json
{
  "mapConfiguration": {
    "name": "San Francisco Schools",
    "description": "Map showing all school locations in San Francisco",
    "settings": { ... },
    "layers": [ ... ],
    "controls": [ ... ]
  },
  "explanation": "This map displays all schools...",
  "confidence": 0.85,
  "spatialOperations": [],
  "warnings": []
}
```

## Step 5: Test the Blazor UI

1. Navigate to: `http://localhost:5000/maps/ai-create`
2. Enter a prompt like: "Show me all parks in Seattle"
3. Click "Generate Map"
4. Review the generated map configuration
5. Click "Save Map" or "Edit Map"

## Testing Scenarios

### Test 1: Simple Point Map
**Prompt:** "Show me all schools in San Francisco"
**Expected:** Single layer with school points, navigation controls

### Test 2: Proximity Analysis
**Prompt:** "Show me all schools within 2 miles of industrial zones"
**Expected:** Two layers, spatial query explanation

### Test 3: Heatmap
**Prompt:** "Create a heatmap of crime incidents in the last month"
**Expected:** Heatmap layer with color gradient, temporal filters

### Test 4: 3D Visualization
**Prompt:** "Show downtown buildings in 3D"
**Expected:** 3D layer with extrusion, tilted view

### Test 5: Error Handling
**Prompt:** "" (empty)
**Expected:** Error message about empty prompt

## Verification Checklist

- [ ] Service health endpoint returns `available: true`
- [ ] Example prompts endpoint returns list of prompts
- [ ] Map generation endpoint returns valid map configuration
- [ ] Generated map has at least one layer
- [ ] Generated map has appropriate controls
- [ ] Blazor UI loads without errors
- [ ] Blazor UI shows example prompts
- [ ] Blazor UI generates maps successfully
- [ ] Map details are displayed correctly
- [ ] Error messages are user-friendly

## Troubleshooting

### Service Not Available

**Problem:** Health endpoint returns `available: false`

**Solutions:**
1. Check API key in appsettings.json
2. Verify network connectivity
3. Check OpenAI API status at status.openai.com
4. Review application logs for errors

### Poor Quality Maps

**Problem:** Generated maps don't match expectations

**Solutions:**
1. Use more specific prompts
2. Include actual data source names from your database
3. Specify location information (city, coordinates)
4. Try example prompts first

### Slow Generation

**Problem:** Maps take too long to generate

**Solutions:**
1. Switch to gpt-3.5-turbo for faster responses
2. Reduce prompt complexity
3. Check network latency to OpenAI
4. Consider implementing caching

### Build Errors

**Problem:** Compilation errors after adding files

**Solutions:**
1. Ensure all using statements are correct
2. Verify MapSDK models are accessible
3. Clean and rebuild solution
4. Check for missing dependencies

## API Examples with curl

### Generate with Spatial Query

```bash
curl -X POST http://localhost:5000/api/maps/ai/generate \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Show me all parks within 1 mile of downtown"
  }'
```

### Explain a Map

```bash
curl -X POST http://localhost:5000/api/maps/ai/explain \
  -H "Content-Type: application/json" \
  -d '{
    "mapConfiguration": {
      "name": "Test Map",
      "settings": { "style": "maplibre://honua/streets", "center": [-122.4, 37.7], "zoom": 12 },
      "layers": [...]
    }
  }'
```

### Get Suggestions

```bash
curl -X POST http://localhost:5000/api/maps/ai/suggest \
  -H "Content-Type: application/json" \
  -d '{
    "mapConfiguration": {...},
    "userFeedback": "Map loads too slowly"
  }'
```

## Next Steps

1. **Integrate with Existing Data**
   - Update prompts to use your actual table names
   - Configure PostGIS connections
   - Test with real data sources

2. **Customize Styling**
   - Review generated layer styles
   - Adjust colors and symbols
   - Add custom basemaps

3. **Add Authentication**
   - Implement user authentication
   - Add authorization policies
   - Track user usage

4. **Monitor Usage**
   - Set up logging for map generation
   - Track success rates and errors
   - Monitor API costs

5. **Gather Feedback**
   - Share with beta users
   - Collect prompt examples
   - Identify common patterns

## Additional Resources

- **Full Documentation**: `/src/Honua.Server.Core/Maps/AI/README.md`
- **Implementation Summary**: `/AI_MAP_GENERATION_IMPLEMENTATION.md`
- **Unit Tests**: `/tests/Honua.Server.Core.Tests/Maps/AI/`
- **Swagger UI**: `http://localhost:5000/swagger`

## Support

For issues or questions:
- Review logs in the application output
- Check the README for detailed documentation
- Review unit tests for usage examples
- Consult Swagger docs for API reference

---

**Ready to Test!** 🚀

Start with Step 1 (Configuration) and work through each step. The entire setup should take less than 10 minutes.
