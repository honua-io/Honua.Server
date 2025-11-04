# OpenAPI Filters Implementation

This directory contains enhanced OpenAPI/Swagger filters for customizing API documentation in the Honua Server.

## Overview

The OpenAPI filters system provides comprehensive customization of Swagger/OpenAPI documentation through:
- **Operation Filters**: Applied to individual API operations/endpoints
- **Document Filters**: Applied to the entire OpenAPI document

## Implemented Filters

### Operation Filters (4)

#### 1. DefaultValuesOperationFilter
Automatically populates default values for operation parameters from:
- `DefaultValueAttribute` decorations
- Optional parameter defaults
- API metadata default values

**Features:**
- Extracts default values from multiple sources
- Populates OpenAPI schema default fields
- Preserves existing defaults (non-overwriting)

#### 2. ExampleValuesOperationFilter
Adds example values to parameters and request bodies for better API documentation.

**Features:**
- Custom `SwaggerExampleAttribute` for parameter examples
- Request body example generation
- Property-level example support

**Usage:**
```csharp
public IActionResult GetCollection([SwaggerExample("buildings")] string collectionId)
{
    // ...
}
```

#### 3. SecurityRequirementsOperationFilter
Automatically applies security requirements based on authorization attributes.

**Features:**
- Detects `[Authorize]` attributes at method and controller levels
- Handles `[AllowAnonymous]` overrides
- Extracts roles and policies as OAuth scopes
- Adds security descriptions to operation documentation

#### 4. SchemaExtensionsOperationFilter
Adds custom OpenAPI extensions and validation metadata to parameter schemas.

**Features:**
- Custom `OpenApiExtensionAttribute` for vendor extensions
- Automatic extraction of validation attributes:
  - `[Range]` → min/max values
  - `[StringLength]` → minLength/maxLength
  - `[MinLength]` / `[MaxLength]`
  - `[Required]` → required flag
- Appends validation info to parameter descriptions

**Usage:**
```csharp
public IActionResult Search(
    [OpenApiExtension("x-query-type", "spatial")]
    [Range(0, 100)]
    int limit = 10)
{
    // ...
}
```

### Document Filters (3)

#### 1. VersionInfoDocumentFilter
Enhances the OpenAPI document with comprehensive version information.

**Features:**
- Assembly version details
- Build version and date (from environment variables)
- Runtime environment information (.NET version)
- Custom extensions: `x-api-version`, `x-environment`, `x-build-date`, `x-assembly-version`

#### 2. ContactInfoDocumentFilter
Adds comprehensive contact and support information to the API documentation.

**Features:**
- Contact details (name, email, URL)
- License information
- Terms of service
- External documentation links
- Support resources (email, portal, documentation, issue tracker)
- Custom extensions: `x-support-email`, `x-support-url`, `x-api-status`

**Configuration:**
```csharp
services.AddHonuaApiDocumentation(
    configureContactInfo: options =>
    {
        options.ContactName = "API Support Team";
        options.ContactEmail = "support@example.com";
        options.SupportUrl = new Uri("https://support.example.com");
        options.DocumentationUrl = new Uri("https://docs.example.com");
        options.ApiStatus = "stable";
    });
```

#### 3. DeprecationInfoDocumentFilter
Manages API deprecation notices and lifecycle information.

**Features:**
- Deprecation warnings with sunset dates
- Replacement version information
- Migration guide links
- Deprecation reason explanations
- API stability indicators (stable, beta, alpha, experimental)
- Changelog links
- Custom extensions: `x-deprecated`, `x-sunset-date`, `x-replacement-version`, `x-stability`, `x-changelog`

**Configuration:**
```csharp
services.AddHonuaApiDocumentation(
    configureDeprecationInfo: options =>
    {
        options.IsDeprecated = true;
        options.SunsetDate = DateTimeOffset.Parse("2026-12-31");
        options.ReplacementVersion = "v2.0";
        options.MigrationGuideUrl = "https://docs.example.com/migration";
        options.DeprecationReason = "Security vulnerabilities";
    });
```

## Registration

All filters are automatically registered in `ApiDocumentationExtensions.cs`:

```csharp
services.AddHonuaApiDocumentation(
    environment: hostEnvironment,
    configureContactInfo: options =>
    {
        options.SupportEmail = "support@honua.io";
        // ... other options
    },
    configureDeprecationInfo: options =>
    {
        options.Stability = "stable";
        // ... other options
    });
```

## Custom Attributes

### SwaggerExampleAttribute
Specifies example values for parameters in OpenAPI documentation.

```csharp
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class SwaggerExampleAttribute : Attribute
{
    public SwaggerExampleAttribute(object example);
}
```

### OpenApiExtensionAttribute
Adds custom OpenAPI extensions (must start with "x-").

```csharp
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
public sealed class OpenApiExtensionAttribute : Attribute
{
    public OpenApiExtensionAttribute(string key, string value);
}
```

## Testing

Comprehensive unit tests are provided in `/tests/Honua.Server.Host.Tests/OpenApi/`:

- `DefaultValuesOperationFilterTests.cs` (8 tests)
- `ExampleValuesOperationFilterTests.cs` (8 tests)
- `SecurityRequirementsOperationFilterTests.cs` (10 tests)
- `SchemaExtensionsOperationFilterTests.cs` (10 tests)
- `VersionInfoDocumentFilterTests.cs` (12 tests)
- `ContactInfoDocumentFilterTests.cs` (15 tests)
- `DeprecationInfoDocumentFilterTests.cs` (12 tests)

**Total: 75 unit tests**

## Architecture

### Filter Execution Order

1. **Operation Filters** (executed for each operation):
   - SwaggerDefaultValues (legacy, kept for compatibility)
   - DefaultValuesOperationFilter
   - ExampleValuesOperationFilter
   - SecurityRequirementsOperationFilter
   - SchemaExtensionsOperationFilter

2. **Document Filters** (executed once for entire document):
   - VersionInfoDocumentFilter
   - ContactInfoDocumentFilter
   - DeprecationInfoDocumentFilter

### Design Principles

- **Non-invasive**: Filters preserve existing documentation and only enhance it
- **Composable**: Multiple filters work together without conflicts
- **Extensible**: Easy to add new filters or customize existing ones
- **Well-documented**: Comprehensive XML documentation on all public APIs
- **Thoroughly tested**: High test coverage with realistic scenarios

## Environment Variables

The following environment variables are used by filters:

- `BUILD_VERSION`: Build version number (used by VersionInfoDocumentFilter)
- `BUILD_DATE`: Build timestamp (used by VersionInfoDocumentFilter)

## Example Output

With all filters applied, your OpenAPI document will include:

```json
{
  "openapi": "3.0.1",
  "info": {
    "title": "Honua Server API",
    "version": "1.2.3",
    "description": "...\n\n**Version Information:**\n- Build: 1.2.3\n- Environment: Production\n- Runtime: .NET 9.0\n\n**Support & Resources:**\n- Support Email: support@example.com\n- Documentation: https://docs.example.com"
  },
  "x-api-version": "1.2.3",
  "x-environment": "Production",
  "x-support-email": "support@example.com",
  "x-stability": "stable",
  "paths": {
    "/collections/{collectionId}": {
      "get": {
        "parameters": [
          {
            "name": "collectionId",
            "schema": {
              "type": "string",
              "default": "buildings",
              "example": "buildings"
            },
            "description": "Collection identifier\n\n**Validation:** Required",
            "required": true,
            "x-custom-property": "value"
          }
        ],
        "security": [
          {
            "Bearer": ["Admin", "User"]
          }
        ]
      }
    }
  }
}
```

## Future Enhancements

Potential additions:
- Response example filters
- Rate limiting documentation
- Pagination metadata
- Custom schema validators
- API gateway integration metadata
