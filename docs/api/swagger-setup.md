# Swagger/OpenAPI Setup Guide

This guide explains how to set up and configure Swagger/OpenAPI documentation for the Honua Build Orchestrator API.

## Prerequisites

The following NuGet packages are required (already included in the project):

```xml
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
<PackageReference Include="Swashbuckle.AspNetCore.Annotations" Version="7.2.0" />
<PackageReference Include="Swashbuckle.AspNetCore.ReDoc" Version="7.2.0" />
```

## Configuration

### 1. Add Swagger Services

In your `Program.cs` or startup configuration:

```csharp
using Honua.Server.Intake.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers();

// Add Swagger with Honua configuration
builder.Services.AddHonuaSwagger();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
{
    app.UseHonuaSwagger();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 2. Enable XML Documentation

Ensure your `.csproj` file includes:

```xml
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

### 3. Add XML Comments to Controllers

```csharp
/// <summary>
/// AI-guided intake conversation for build configuration
/// </summary>
[ApiController]
[Route("api/intake")]
[Produces("application/json")]
public class IntakeController : ControllerBase
{
    /// <summary>
    /// Start a new AI conversation
    /// </summary>
    /// <remarks>
    /// Starts a new conversation with the AI agent to configure your build.
    ///
    /// Returns a conversation ID to use for subsequent messages.
    /// </remarks>
    /// <param name="request">Optional request with customer ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <response code="200">Conversation started successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("start")]
    [ProducesResponseType(typeof(ConversationResponse), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> StartConversation(
        [FromBody] StartConversationRequest? request,
        CancellationToken cancellationToken)
    {
        // Implementation
    }
}
```

## Accessing Documentation

Once configured, access the documentation at:

### Swagger UI (Interactive)
```
https://api.honua.io/api-docs
```

Features:
- Interactive API testing
- Try It Out functionality
- Request/response examples
- Schema documentation

### ReDoc (Reference)
```
https://api.honua.io/docs
```

Features:
- Clean, readable layout
- Three-panel design
- Download OpenAPI spec
- Responsive design

### OpenAPI Spec (JSON)
```
https://api.honua.io/api-docs/v1/openapi.json
```

Use this for:
- SDK generation
- Import into Postman/Insomnia
- API testing tools
- Documentation generation

## Customization

### Custom CSS

Add custom branding in `wwwroot/swagger-ui/custom.css`:

```css
.swagger-ui .topbar {
    background-color: #1e3a8a;
}

.swagger-ui .info .title {
    color: #1e3a8a;
}
```

### Custom JavaScript

Add custom behavior in `wwwroot/swagger-ui/custom.js`:

```javascript
window.onload = function() {
    // Custom initialization
    console.log('Honua API Documentation Loaded');
};
```

## Security

### Production Configuration

In production, consider:

1. **Disable in Production** (optional):
```csharp
if (!app.Environment.IsProduction())
{
    app.UseHonuaSwagger();
}
```

2. **Require Authentication**:
```csharp
app.UseSwaggerUI(options =>
{
    options.ConfigObject.AdditionalItems["onComplete"] = "() => { ui.preauthorizeApiKey('Bearer', 'my-token'); }";
});
```

3. **IP Whitelisting**:
```csharp
app.UseWhen(context => context.Request.Path.StartsWithSegments("/api-docs"), appBuilder =>
{
    appBuilder.UseMiddleware<IpWhitelistMiddleware>();
});
```

## SDK Generation

### Generate C# Client

```bash
# Install NSwag CLI
dotnet tool install -g NSwag.ConsoleCore

# Generate C# client
nswag openapi2csclient /input:https://api.honua.io/api-docs/v1/openapi.json /output:HonuaApiClient.cs /namespace:Honua.Client
```

### Generate TypeScript Client

```bash
# Install OpenAPI Generator
npm install @openapitools/openapi-generator-cli -g

# Generate TypeScript client
openapi-generator-cli generate \
  -i https://api.honua.io/api-docs/v1/openapi.json \
  -g typescript-fetch \
  -o ./src/generated/honua-client
```

### Generate Python Client

```bash
# Generate Python client
openapi-generator-cli generate \
  -i https://api.honua.io/api-docs/v1/openapi.json \
  -g python \
  -o ./honua-client-python
```

### Generate Go Client

```bash
# Generate Go client
openapi-generator-cli generate \
  -i https://api.honua.io/api-docs/v1/openapi.json \
  -g go \
  -o ./honua-client-go
```

## Testing with Swagger UI

### 1. Authenticate

Click the **Authorize** button and enter your JWT token:

```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

### 2. Test Endpoints

1. Expand an endpoint
2. Click **Try it out**
3. Fill in parameters
4. Click **Execute**
5. View response

### 3. Copy curl Command

Swagger UI generates curl commands for each request:

```bash
curl -X 'POST' \
  'https://api.honua.io/api/intake/start' \
  -H 'accept: application/json' \
  -H 'Authorization: Bearer YOUR_TOKEN' \
  -H 'Content-Type: application/json' \
  -d '{
  "customerId": "cust_abc123"
}'
```

## Troubleshooting

### XML Comments Not Showing

1. Verify `GenerateDocumentationFile` is `true` in `.csproj`
2. Rebuild project to generate XML file
3. Verify XML file exists in `bin` directory
4. Check Swagger configuration includes XML file path

### Authentication Not Working

1. Verify JWT token is valid and not expired
2. Check `Authorization` header format: `Bearer {token}`
3. Verify API key is correct (if using API key auth)
4. Check token scopes/permissions

### Schemas Not Generating

1. Ensure models are public
2. Add XML comments to model properties
3. Use `[Required]` attribute for required properties
4. Use proper data annotations

### Filters Not Applied

1. Verify filters are registered in `AddSwaggerGen()`
2. Check filter implementation
3. Verify filter namespace is imported

## Best Practices

### 1. Document Everything

Add XML comments to:
- Controllers
- Actions
- Parameters
- Response types
- Models
- Enums

### 2. Use Data Annotations

```csharp
public class SendMessageRequest
{
    [Required]
    [StringLength(100)]
    public string ConversationId { get; set; }

    [Required]
    [StringLength(10000)]
    public string Message { get; set; }
}
```

### 3. Provide Examples

Use operation filters to add examples:

```csharp
options.OperationFilter<ExampleValuesOperationFilter>();
```

### 4. Version Your API

```csharp
options.SwaggerDoc("v1", new OpenApiInfo { Version = "v1", Title = "API v1" });
options.SwaggerDoc("v2", new OpenApiInfo { Version = "v2", Title = "API v2" });
```

### 5. Group Operations

Use tags to organize endpoints:

```csharp
[Tags("Intake")]
public class IntakeController : ControllerBase
{
    // Operations
}
```

## Additional Resources

- [Swashbuckle Documentation](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
- [OpenAPI Specification](https://swagger.io/specification/)
- [ReDoc Documentation](https://github.com/Redocly/redoc)
- [NSwag Documentation](https://github.com/RicoSuter/NSwag)
