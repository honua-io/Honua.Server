# GeoETL Workflow Template Library

A comprehensive library of pre-built workflow templates for common geospatial operations. Templates enable users to quickly deploy production-ready workflows with one-click instantiation.

## Overview

The template library system provides:
- **30+ Pre-built Templates** organized by category
- **REST API** for template management and instantiation
- **Blazor UI** with template gallery and filtering
- **JSON-based Storage** for easy template creation and maintenance
- **One-Click Deployment** to create workflows from templates

## Architecture

### Core Components

1. **WorkflowTemplate.cs** - Template model with metadata and workflow definition
2. **IWorkflowTemplateRepository** - Repository interface for template operations
3. **JsonWorkflowTemplateRepository** - File-based repository implementation
4. **GeoEtlTemplateEndpoints.cs** - REST API endpoints
5. **TemplateGallery.razor** - Blazor UI for browsing and using templates

### Template Structure

```csharp
public class WorkflowTemplate
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; }
    public TemplateDifficulty Difficulty { get; set; }
    public int EstimatedMinutes { get; set; }
    public List<string> Tags { get; set; }
    public List<WorkflowNode> Nodes { get; set; }
    public List<WorkflowEdge> Edges { get; set; }
    public Dictionary<string, WorkflowParameter> Parameters { get; set; }
    // ... additional properties
}
```

## Template Categories

### 1. Data Import/Export (7 templates)
- Import Shapefile to PostGIS
- Export PostGIS to GeoPackage
- Convert Shapefile to GeoJSON
- Batch GeoPackage to PostGIS
- PostGIS to Multiple Formats
- Import and Validate Geometry
- Reproject and Export

### 2. Buffer Operations (5 templates)
- Simple Buffer with Fixed Distance
- Variable Buffer by Attribute
- Multi-Ring Buffer
- Negative Buffer (Erosion)
- Buffer and Export to GeoPackage

### 3. Overlay Operations (7 templates)
- Intersection Analysis
- Union and Dissolve
- Difference Analysis
- Symmetric Difference
- Clip Features by Boundary
- Spatial Join and Export
- Point-in-Polygon Spatial Join

### 4. Geometry Processing (6 templates)
- Simplify Complex Geometries
- Convex Hull Generation
- Centroid Calculation
- Boundary Extraction
- Geometry Validation and Repair
- Area Calculation and Export

### 5. Workflow Chains (5 templates)
- Import, Transform, and Export Pipeline
- Multi-Source Data Merge
- Data Quality Assurance Pipeline
- Distance Analysis and Classification
- Attribute Filter and Export

## REST API Endpoints

### Get All Templates
```http
GET /admin/api/geoetl/templates
```

Query Parameters:
- `category` - Filter by category
- `tag` - Filter by tag
- `search` - Search by keyword
- `featured` - Filter featured templates

### Get Template by ID
```http
GET /admin/api/geoetl/templates/{templateId}
```

### Get Categories
```http
GET /admin/api/geoetl/templates/categories
```

### Get Tags
```http
GET /admin/api/geoetl/templates/tags
```

### Instantiate Template
```http
POST /admin/api/geoetl/templates/{templateId}/instantiate
```

Request Body:
```json
{
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "createdBy": "00000000-0000-0000-0000-000000000001",
  "workflowName": "My Custom Workflow Name",
  "parameterOverrides": {
    "node_1": {
      "parameter1": "value1"
    }
  }
}
```

### Get Template Statistics
```http
GET /admin/api/geoetl/templates/statistics
```

Returns:
- Total templates count
- Category breakdown with usage statistics
- Difficulty breakdown
- Most used templates

## Creating New Templates

Templates are stored as JSON files in `Templates/Library/`. To create a new template:

1. Create a JSON file with a unique ID
2. Follow the template schema:

```json
{
  "id": "my-template-id",
  "name": "Template Name",
  "description": "Template description",
  "category": "Data Import/Export",
  "icon": "upload",
  "difficulty": 0,
  "estimatedMinutes": 5,
  "tags": ["tag1", "tag2"],
  "author": "Your Name",
  "version": "1.0",
  "isFeatured": false,
  "inputRequirements": [
    "List of input requirements"
  ],
  "expectedOutputs": [
    "List of expected outputs"
  ],
  "useCases": [
    "Use case 1",
    "Use case 2"
  ],
  "nodes": [
    {
      "id": "node_1",
      "type": "data_source.shapefile",
      "name": "Node Name",
      "description": "Node description",
      "parameters": {
        "filePath": "/path/to/file.shp"
      },
      "position": {
        "x": 100,
        "y": 100
      }
    }
  ],
  "edges": [
    {
      "from": "node_1",
      "to": "node_2"
    }
  ],
  "parameters": {
    "param1": {
      "name": "param1",
      "type": "string",
      "description": "Parameter description",
      "defaultValue": "default",
      "required": true
    }
  }
}
```

3. Place the file in `Templates/Library/`
4. The repository will automatically load it on startup

## Template Difficulty Levels

- **Easy (0)** - Simple, single-operation workflows (2-3 min)
- **Medium (1)** - Multi-step workflows with transformations (4-6 min)
- **Hard (2)** - Complex workflows with multiple data sources (7+ min)

## Blazor UI Usage

### Accessing the Template Gallery

Navigate to `/geoetl/templates` in the Admin UI.

### Features

1. **Search** - Full-text search across names, descriptions, and tags
2. **Filter by Category** - View templates by specific category
3. **Filter by Difficulty** - Filter by Easy/Medium/Hard
4. **Featured Templates** - Toggle to show only featured templates
5. **Template Cards** - Visual cards showing:
   - Name and description
   - Category and difficulty chips
   - Estimated execution time
   - Node count
   - Tags
   - Usage count
6. **Template Details Dialog** - View comprehensive details:
   - Full description
   - Input requirements
   - Expected outputs
   - Use cases
   - Usage statistics
7. **One-Click Instantiation** - Create workflow from template

## Configuration

### Service Registration

Templates are automatically registered in `ServiceCollectionExtensions.cs`:

```csharp
services.AddSingleton<IWorkflowTemplateRepository, JsonWorkflowTemplateRepository>();
```

### Endpoint Mapping

Template endpoints are mapped in `EndpointExtensions.cs`:

```csharp
app.MapGeoEtlTemplateEndpoints();
```

### Template Location

Templates are loaded from:
```
{AssemblyLocation}/ETL/Templates/Library/*.json
```

## Best Practices

### Template Design

1. **Keep it Simple** - Templates should solve one clear problem
2. **Use Descriptive Names** - Make purpose obvious from the name
3. **Provide Context** - Include detailed descriptions and use cases
4. **Set Realistic Estimates** - Provide accurate execution time estimates
5. **Tag Appropriately** - Use relevant tags for discoverability
6. **Document Requirements** - Clearly list inputs and outputs

### Parameter Design

1. **Sensible Defaults** - Provide working default values
2. **Clear Descriptions** - Explain what each parameter does
3. **Validation Rules** - Set min/max values and required flags
4. **Use Standard Paths** - Use conventional paths like `/data/input/`, `/data/output/`

### Node Positioning

Use consistent positioning for better visual flow:
- Sources: x=100
- Transformations: x=300, x=500
- Sinks: x=700
- Vertical spacing: 100px for parallel nodes

## Usage Statistics

The system tracks template usage to help identify:
- Most popular templates
- Category preferences
- Template effectiveness

Statistics are available via:
- REST API: `/admin/api/geoetl/templates/statistics`
- Template cards show usage count
- Template details dialog shows popularity

## Future Enhancements

Potential improvements:
1. **User-Created Templates** - Allow users to save workflows as templates
2. **Template Versioning** - Track template versions
3. **Template Import/Export** - Share templates between instances
4. **Template Testing** - Automated validation of templates
5. **Template Ratings** - User ratings and reviews
6. **Custom Icons** - Support custom icons per template
7. **Preview Images** - Visual workflow diagrams
8. **Parameter Wizards** - Guided parameter customization

## Examples

### Example 1: Using a Template via API

```bash
# Get all templates
curl http://localhost:5000/admin/api/geoetl/templates

# Get specific template
curl http://localhost:5000/admin/api/geoetl/templates/import-shapefile-to-postgis

# Instantiate template
curl -X POST http://localhost:5000/admin/api/geoetl/templates/import-shapefile-to-postgis/instantiate \
  -H "Content-Type: application/json" \
  -d '{
    "tenantId": "00000000-0000-0000-0000-000000000001",
    "createdBy": "00000000-0000-0000-0000-000000000001",
    "workflowName": "Import Boundaries"
  }'
```

### Example 2: Using the Blazor UI

1. Navigate to GeoETL > Template Gallery
2. Browse or search for a template
3. Click "View Details" to see full information
4. Click "Use Template" to instantiate
5. The workflow designer opens with the new workflow
6. Customize parameters as needed
7. Save and execute the workflow

## Troubleshooting

### Templates Not Loading

Check:
1. Template files are valid JSON
2. Files are in correct directory
3. Template IDs are unique
4. Required fields are present

View logs for detailed error messages.

### Template Instantiation Fails

Verify:
1. Tenant ID is valid
2. User has permissions
3. Node types are registered
4. Connection strings are configured

### UI Not Showing Templates

Ensure:
1. GeoETL feature flag is enabled
2. User is authenticated
3. API endpoint is accessible
4. No CORS issues

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
