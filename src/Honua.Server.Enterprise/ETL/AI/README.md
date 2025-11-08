# GeoETL AI-Powered Workflow Generation

This module provides AI-powered natural language to workflow conversion for the Honua GeoETL system.

## Overview

The AI module allows users to describe workflows in plain language and automatically generates executable workflow definitions. It uses OpenAI's GPT models with structured prompts that include:
- Complete catalog of available node types (17 nodes)
- Few-shot learning examples
- JSON schema enforcement
- Automatic validation

## Components

### IGeoEtlAiService
Core service interface with three main methods:
- `GenerateWorkflowAsync()` - Convert natural language to workflow
- `ExplainWorkflowAsync()` - Convert workflow to natural language
- `IsAvailableAsync()` - Check service health

### OpenAiGeoEtlService
Production implementation supporting:
- OpenAI API (api.openai.com)
- Azure OpenAI Service
- Configurable models (GPT-4, GPT-3.5-turbo, etc.)
- Structured JSON responses
- Error handling and retry logic

### GeoEtlPromptTemplates
Prompt engineering system providing:
- System prompt with node catalog
- Few-shot examples (3 workflows)
- Response format specification
- User prompt formatting

## Configuration

### OpenAI

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4"
  }
}
```

### Azure OpenAI

```json
{
  "OpenAI": {
    "ApiKey": "your-azure-key",
    "Model": "gpt-4",
    "IsAzure": true,
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiVersion": "2024-02-15-preview"
  }
}
```

## Usage

### Register Services

```csharp
// In Startup/Program.cs
services.AddGeoEtlAi(configuration);
```

### Generate Workflow

```csharp
var aiService = serviceProvider.GetService<IGeoEtlAiService>();

if (aiService != null)
{
    var result = await aiService.GenerateWorkflowAsync(
        prompt: "Buffer buildings by 50 meters and export to geopackage",
        tenantId: tenantId,
        userId: userId
    );

    if (result.Success)
    {
        // Use result.Workflow
        Console.WriteLine($"Generated: {result.Workflow.Metadata.Name}");
        Console.WriteLine($"Nodes: {result.Workflow.Nodes.Count}");
        Console.WriteLine($"Explanation: {result.Explanation}");
    }
}
```

### REST API

```bash
POST /admin/api/geoetl/ai/generate
Content-Type: application/json

{
  "prompt": "Buffer buildings by 50 meters and export to geopackage",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "userId": "00000000-0000-0000-0000-000000000001",
  "validateWorkflow": true
}
```

### Blazor UI

Navigate to `/geoetl/designer` and click the "AI Generate Workflow" button.

## Example Prompts

### Simple Buffer
> "Buffer buildings by 50 meters and export to geopackage"

### Intersection Analysis
> "Read parcels from PostGIS, intersect with flood zones, export to shapefile"

### Multi-Step Process
> "Load roads from GeoPackage, create 100m buffer, find union with existing buffers, export to PostGIS"

### Complex Workflow
> "Read building footprints from shapefile, simplify to 1m tolerance, buffer by 10 meters, intersect with zoning districts from PostGIS, export results to GeoPackage"

## Prompt Engineering

The system uses carefully crafted prompts to guide the AI:

1. **System Prompt** - Establishes context and rules
   - Role: "expert GeoETL workflow designer"
   - Node catalog with full parameter descriptions
   - Workflow structure rules
   - JSON response format

2. **Few-Shot Examples** - Provides 3 example workflows
   - Simple buffer and export
   - Two-source intersection
   - Complex multi-step pipeline

3. **User Prompt** - Wraps user input with instructions
   - Emphasizes JSON-only response
   - Reminds about available nodes

## Error Handling

The service handles several error scenarios:

- **Empty/null API key** - Service returns null, graceful degradation
- **API errors** - Returns failure result with error message
- **Invalid JSON** - Attempts cleanup, returns parse error
- **Network errors** - Returns failure with network error details
- **Malformed workflows** - Validation catches issues

## Testing

Unit tests cover:
- Prompt template generation
- Configuration handling
- API response parsing
- Error scenarios
- Availability checks

See:
- `GeoEtlPromptTemplatesTests.cs`
- `OpenAiGeoEtlServiceTests.cs`

## Limitations

Current limitations:
- Requires external AI service (OpenAI/Azure OpenAI)
- API rate limits apply
- May generate invalid workflows (validation recommended)
- Limited to 17 available node types
- Parameter values may need manual adjustment

## Future Enhancements

Potential improvements:
- Support for local LLMs (Ollama, LLaMA)
- Workflow optimization suggestions
- Parameter inference from context
- Multi-turn conversation for refinement
- Template-based generation
- Learning from user corrections

## Security Considerations

- API keys stored in configuration
- No user data sent to AI (only prompts)
- Generated workflows validated before execution
- Tenant isolation enforced
- Rate limiting recommended

## Cost Estimation

Approximate OpenAI API costs per generation:
- Prompt tokens: ~3,000-4,000 tokens
- Response tokens: ~500-1,000 tokens
- Total: ~4,000-5,000 tokens
- Cost (GPT-4): ~$0.20-$0.25 per generation
- Cost (GPT-3.5-turbo): ~$0.01 per generation

Consider:
- Caching frequently used workflows
- Using GPT-3.5-turbo for simple workflows
- Implementing request quotas per tenant

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
