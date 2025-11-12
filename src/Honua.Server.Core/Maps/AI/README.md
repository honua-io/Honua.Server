# AI-Powered Map Generation

This module provides AI-powered map generation capabilities that allow end-users to create interactive maps using natural language descriptions.

## Overview

The AI Map Generation system extends Honua's existing capabilities to provide an end-user interface for creating maps through natural language. Unlike the GeoETL AI system (which is admin/developer focused), this system is designed for end-users who want to quickly create and visualize spatial data on maps.

## Features

- **Natural Language Map Creation**: Describe your map in plain English, and AI will generate a complete map configuration
- **Multi-Layer Support**: Automatically generates appropriate layers based on your description
- **Spatial Analysis**: Handles proximity queries, buffers, intersections, and other spatial operations
- **Interactive Controls**: Adds appropriate map controls (navigation, search, legend, etc.)
- **Map Explanations**: Get natural language explanations of existing map configurations
- **Improvement Suggestions**: AI-powered suggestions for improving map usability and performance

## Architecture

```
┌─────────────────────┐
│  Blazor UI          │  AiMapCreation.razor
│  (End-User)         │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  API Controller     │  MapsAiController
│  (REST Endpoints)   │  POST /api/maps/ai/generate
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  Service Layer      │  OpenAiMapGenerationService
│  (Business Logic)   │  IMapGenerationAiService
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐
│  LLM Integration    │  OpenAI / Azure OpenAI
│  (AI Provider)      │  GPT-4, GPT-3.5-turbo
└─────────────────────┘
```

## Components

### 1. Service Layer

- **IMapGenerationAiService**: Interface defining AI map generation operations
- **OpenAiMapGenerationService**: Implementation using OpenAI/Azure OpenAI
- **MapGenerationPromptTemplates**: Prompt engineering templates and examples

### 2. API Layer

- **MapsAiController**: REST API endpoints for map generation
  - `POST /api/maps/ai/generate` - Generate map from natural language
  - `POST /api/maps/ai/explain` - Explain existing map configuration
  - `POST /api/maps/ai/suggest` - Get improvement suggestions
  - `GET /api/maps/ai/examples` - Get example prompts
  - `GET /api/maps/ai/health` - Check service availability

### 3. UI Layer

- **AiMapCreation.razor**: Blazor component for end-user map creation
  - Natural language input field
  - Example prompts
  - Map preview and details
  - Edit and save generated maps

## Configuration

Add to your `appsettings.json`:

```json
{
  "MapAI": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4",
    "IsAzure": false,
    "Endpoint": "",
    "ApiVersion": "2024-02-15-preview"
  }
}
```

Or use the existing OpenAI configuration:

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4",
    "IsAzure": false
  }
}
```

### Azure OpenAI Configuration

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

## Registration

In your `Program.cs` or `Startup.cs`:

```csharp
using Honua.Server.Core.Maps.AI;

// Register AI Map Generation services
services.AddMapGenerationAi(configuration);
```

## Usage Examples

### Example 1: Simple Point Map

**User Prompt:**
```
Show me all schools in San Francisco
```

**Generated Map:**
- Single layer with school points
- Circle markers styled in blue
- Popup templates showing school information
- Search and navigation controls
- Filters for school type and enrollment

### Example 2: Proximity Analysis

**User Prompt:**
```
Show me all schools within 2 miles of industrial zones
```

**Generated Map:**
- Two layers: industrial zones (polygons) and schools (points)
- Schools filtered by proximity to industrial zones
- Different colors to distinguish layers
- Measure tool for verifying distances
- Explanation of spatial query

### Example 3: Heatmap Visualization

**User Prompt:**
```
Create a heatmap of crime incidents in the last month
```

**Generated Map:**
- Heatmap layer showing crime density
- Point layer for individual incidents (visible at high zoom)
- Temporal filters for date ranges
- Color ramp from blue (low) to red (high)
- Legend showing density scale

### Example 4: 3D Buildings

**User Prompt:**
```
Show downtown buildings in 3D
```

**Generated Map:**
- 3D extruded building layer
- Tilted view (pitch: 60 degrees)
- Height-based extrusion
- Building attribute popups
- Fullscreen control

## API Examples

### Generate Map

```bash
curl -X POST https://api.honua.io/api/maps/ai/generate \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Show me all schools within 2 miles of industrial zones"
  }'
```

Response:
```json
{
  "mapConfiguration": {
    "name": "Schools Near Industrial Zones",
    "description": "Shows schools located within 2 miles of industrial zones",
    "settings": { ... },
    "layers": [ ... ],
    "controls": [ ... ]
  },
  "explanation": "This map displays schools within 2 miles of industrial zones...",
  "confidence": 0.85,
  "spatialOperations": [
    "Buffer operation: 2 miles around industrial zones",
    "Intersection: Schools within buffered zones"
  ],
  "warnings": []
}
```

### Explain Map

```bash
curl -X POST https://api.honua.io/api/maps/ai/explain \
  -H "Content-Type: application/json" \
  -d '{
    "mapConfiguration": { ... }
  }'
```

### Get Suggestions

```bash
curl -X POST https://api.honua.io/api/maps/ai/suggest \
  -H "Content-Type: application/json" \
  -d '{
    "mapConfiguration": { ... },
    "userFeedback": "Map loads too slowly"
  }'
```

## Prompt Engineering

The system uses carefully crafted prompts to guide the AI:

1. **System Prompt**: Explains Honua's mapping capabilities, available layer types, data sources, and styling options
2. **Few-Shot Examples**: Provides 4 complete examples showing different map types
3. **User Prompt**: Formatted user request with instructions

### Supported Map Types

- **Point Maps**: Schools, hospitals, restaurants, etc.
- **Polygon Maps**: Parcels, zones, boundaries
- **Line Maps**: Roads, trails, rivers
- **Heatmaps**: Density visualizations
- **3D Maps**: Building heights, terrain
- **Cluster Maps**: Large point datasets
- **Multi-layer Analysis**: Combining multiple datasets

### Spatial Operations

- **Buffer**: Create zones around features
- **Intersection**: Find features within areas
- **Union**: Merge geometries
- **Proximity**: Distance-based queries
- **Contains**: Features within polygons
- **Overlaps**: Overlapping geometries

## Limitations

1. **Data Availability**: AI generates map configurations, but actual data must exist in your databases
2. **Spatial Operations**: Complex spatial queries are described but executed by the backend
3. **Rate Limits**: Subject to OpenAI API rate limits and quotas
4. **Cost**: Each map generation consumes OpenAI API tokens
5. **Accuracy**: AI-generated configurations may need manual refinement

## Best Practices

1. **Clear Prompts**: Be specific about what you want to see
2. **Data Names**: Use actual table/layer names from your database
3. **Spatial Context**: Include location information (city, coordinates)
4. **Review Generated Maps**: Always review and test generated configurations
5. **Iterative Refinement**: Use the edit feature to fine-tune generated maps

## Security Considerations

1. **API Key Protection**: Never expose OpenAI API keys in client code
2. **Input Validation**: All user prompts are validated before processing
3. **Rate Limiting**: Implement rate limiting on map generation endpoints
4. **Data Access**: Ensure generated maps respect user permissions
5. **Injection Prevention**: Prompts are sanitized to prevent prompt injection attacks

## Troubleshooting

### Service Not Available

- Check API key configuration in appsettings
- Verify network connectivity to OpenAI
- Check OpenAI API status
- Review application logs for errors

### Poor Quality Maps

- Provide more specific prompts
- Include actual data source names
- Specify desired visualization type
- Use example prompts as templates

### Slow Generation

- Reduce prompt complexity
- Use faster models (gpt-3.5-turbo)
- Implement caching for common requests
- Monitor OpenAI API latency

## Future Enhancements

- [ ] Support for Anthropic Claude models
- [ ] Map template library
- [ ] User feedback learning
- [ ] Batch map generation
- [ ] Map version control
- [ ] Collaborative map editing
- [ ] Advanced styling options
- [ ] Real-time map updates
- [ ] Mobile optimization
- [ ] Offline map generation

## Related Documentation

- [MapSDK Documentation](../../MapSDK/README.md)
- [GeoETL AI Documentation](../../../Honua.Server.Enterprise/ETL/AI/README.md)
- [Honua.Cli.AI Agents](../../../Honua.Cli.AI/README.md)
- [OpenAI API Documentation](https://platform.openai.com/docs)

## Support

For issues or questions:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io
- Community: https://community.honua.io
