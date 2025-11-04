# API Versioning

Honua Server now supports API versioning using URL path segments. This allows for graceful API evolution and deprecation workflows.

## Configuration

API versioning is configured in `appsettings.json`:

```json
{
  "honua": {
    "apiVersioning": {
      "defaultVersion": "1.0",
      "assumeDefaultVersionWhenUnspecified": true,
      "reportApiVersions": true,
      "versioningMethod": "UrlSegment"
    }
  }
}
```

### Configuration Options

- **defaultVersion**: The API version to use when not specified (default: `1.0`)
- **assumeDefaultVersionWhenUnspecified**: If true, requests without a version will use the default version (default: `true`)
- **reportApiVersions**: If true, include supported API versions in response headers (default: `true`)
- **versioningMethod**: How to specify the version - currently supports `UrlSegment` (default: `UrlSegment`)

## How to Use

### For Controllers (Developers)

To add versioning support to a controller, add the `[ApiVersion]` attribute and update the route:

```csharp
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[ApiVersion("1.0")]  // Declare supported version
[Route("stac/collections")]  // Default route (no version)
[Route("v{version:apiVersion}/stac/collections")]  // Versioned route
public class StacCollectionsController : ControllerBase
{
    // ... controller methods
}
```

### For API Consumers

Consumers can access endpoints in two ways:

1. **Without version** (uses default v1.0):
   ```
   GET /stac/collections
   ```

2. **With explicit version**:
   ```
   GET /v1/stac/collections
   ```

### Response Headers

When `reportApiVersions` is enabled, responses include version information:

```
api-supported-versions: 1.0
api-deprecated-versions: (empty if none deprecated)
```

## Example Controller

See `src/Honua.Server.Host/Stac/StacCollectionsController.cs` for a working example.

## Deprecation Workflow

To deprecate an API version:

1. Add a new version to the controller:
   ```csharp
   [ApiVersion("1.0", Deprecated = true)]
   [ApiVersion("2.0")]
   ```

2. Deprecated versions will be reported in response headers:
   ```
   api-deprecated-versions: 1.0
   ```

3. Eventually remove the deprecated version after a transition period

## Integration with Swagger

The Swagger UI at `/swagger` automatically includes version information in the API documentation. The API version is displayed in the description and operation details.

## Verification

To verify API versioning is working:

1. **Start the server**:
   ```bash
   dotnet run --project src/Honua.Server.Host
   ```

2. **Check response headers** on any endpoint:
   ```bash
   curl -I https://localhost:5001/stac/collections
   ```

   Look for `api-supported-versions: 1.0` in the response headers.

3. **Test versioned endpoint**:
   ```bash
   curl https://localhost:5001/v1/stac/collections
   ```

   Should return the same result as the unversioned endpoint.

4. **View Swagger documentation**:
   ```
   https://localhost:5001/swagger
   ```

   The API version should be displayed as "v1.0" in the title.

## Technical Details

- **Package**: `Asp.Versioning.Mvc` v8.1.0
- **Configuration**: `src/Honua.Server.Host/Middleware/ApiVersioningConfiguration.cs`
- **Registration**: `src/Honua.Server.Host/Extensions/ServiceCollectionExtensions.cs`
- **Swagger Integration**: `src/Honua.Server.Host/Extensions/ApiDocumentationExtensions.cs`

## Benefits

1. **Backward Compatibility**: Existing endpoints continue to work without modification
2. **Opt-in**: Controllers can adopt versioning incrementally via attributes
3. **Clear Deprecation**: Deprecated versions are clearly marked in headers
4. **Client Discovery**: Clients can discover supported versions via response headers
5. **Swagger Support**: API documentation automatically reflects versioning

## Future Enhancements

- Support for header-based versioning (`api-version: 1.0`)
- Support for query string versioning (`?api-version=1.0`)
- Per-endpoint versioning granularity
- Automatic version negotiation
