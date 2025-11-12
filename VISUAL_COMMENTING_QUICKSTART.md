# Visual Commenting System - Quick Start Guide

## 5-Minute Setup

### Step 1: Database Setup (1 minute)

```bash
# Run the schema creation script
sqlite3 honua.db < src/Honua.Server.Core/Data/Comments/CommentsDatabaseSetup.sql
```

### Step 2: Backend Configuration (2 minutes)

**Program.cs or Startup.cs:**

```csharp
using Honua.Server.Core.Data.Comments;
using Honua.Server.Core.Services.Comments;
using Honua.Server.Host.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add comment services
builder.Services.AddSingleton<ICommentRepository>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connectionString = config.GetConnectionString("DefaultConnection");
    var logger = sp.GetRequiredService<ILogger<SqliteCommentRepository>>();
    var repo = new SqliteCommentRepository(connectionString, logger);
    repo.EnsureInitializedAsync().GetAwaiter().GetResult();
    return repo;
});

builder.Services.AddScoped<CommentService>();

// Add SignalR
builder.Services.AddSignalR();

// Add CORS for SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Use CORS
app.UseCors("AllowAll");

// Map SignalR hub
app.MapHub<CommentHub>("/hubs/comments");

// Map controllers
app.MapControllers();

app.Run();
```

### Step 3: Frontend Integration (2 minutes)

**Add to your Blazor page:**

```razor
@page "/map/{MapId}"
@using Honua.MapSDK.Components.Comments

<div class="map-view">
    <HonuaMap MapId="@MapId" @ref="mapComponent">
        <!-- Your existing map content -->
    </HonuaMap>

    <MapComments MapId="@MapId"
                ApiBaseUrl="/api/v1"
                ShowPanel="true"
                ShowMapMarkers="true"
                EnableRealTime="true"
                Categories="@categories"
                OnCommentCreated="OnCommentCreated" />
</div>

@code {
    [Parameter]
    public string MapId { get; set; } = string.Empty;

    private HonuaMap? mapComponent;

    private string[] categories = new[]
    {
        "Data Quality",
        "Feature Request",
        "Bug Report",
        "General"
    };

    private void OnCommentCreated(MapComment comment)
    {
        // Optional: Handle new comments
        Console.WriteLine($"New comment: {comment.CommentText}");
    }
}
```

**Add JavaScript reference to _Host.cshtml or index.html:**

```html
<script src="_content/Honua.MapSDK/js/honua-comments.js"></script>
```

### Step 4: Test It! (30 seconds)

1. Run your application
2. Navigate to a map page
3. Click on the map to add a comment
4. Open the same map in another browser window
5. Watch real-time updates in action!

## Common Use Cases

### 1. Enable Click-to-Comment

```razor
<HonuaMap @ref="mapRef" OnMapClick="HandleMapClick">
    <MapComments @ref="commentsRef" MapId="@MapId" />
</HonuaMap>

@code {
    private HonuaMap? mapRef;
    private MapComments? commentsRef;

    private async Task HandleMapClick(MapClickEvent evt)
    {
        await commentsRef!.StartAddingComment(evt.Longitude, evt.Latitude);
    }
}
```

### 2. Load Comments for a Specific Feature

```csharp
// In your controller or service
var comments = await _commentService.GetFeatureCommentsAsync(mapId, featureId);
```

### 3. Export Comments to CSV

```csharp
[HttpGet("maps/{mapId}/comments/export")]
public async Task<IActionResult> ExportComments(string mapId)
{
    var comments = await _commentService.GetMapCommentsAsync(mapId);
    var csv = _commentService.ExportToCSV(comments);
    return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"comments-{mapId}.csv");
}
```

### 4. Filter Comments by Status

```http
GET /api/v1/maps/map-001/comments?status=open&priority=high
```

### 5. Create Polygon Comment (Area Selection)

```javascript
// Using Leaflet Draw or similar
map.on('draw:created', async (e) => {
    const geometry = e.layer.toGeoJSON().geometry;

    await fetch('/api/v1/maps/map-001/comments', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Authorization': 'Bearer YOUR_TOKEN'
        },
        body: JSON.stringify({
            commentText: 'This area needs review',
            geometryType: 'polygon',
            geometry: JSON.stringify(geometry),
            priority: 'high'
        })
    });
});
```

## Configuration Options

### Comment Panel Customization

```razor
<MapComments MapId="@MapId"
            ShowPanel="true"              // Show/hide side panel
            ShowMapMarkers="true"         // Show markers on map
            EnableRealTime="true"         // Real-time updates via SignalR
            Categories="@categories"      // Comment categories
            ApiBaseUrl="/api/v1"          // API endpoint base URL
            OnCommentCreated="Handler"    // Event callback
            OnCommentSelected="Handler"   // Event callback
            />
```

### Permissions

```csharp
// In your authorization setup
services.AddAuthorization(options =>
{
    options.AddPolicy("RequireUser", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("RequireEditor", policy =>
        policy.RequireRole("Editor", "Admin"));

    options.AddPolicy("RequireModerator", policy =>
        policy.RequireRole("Moderator", "Admin"));
});
```

### Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("comments", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10; // 10 comments per minute
    });
});

// Apply to controller
[EnableRateLimiting("comments")]
public class CommentsController : ControllerBase { }
```

## API Quick Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/v1/maps/{mapId}/comments` | POST | Create comment |
| `/api/v1/maps/{mapId}/comments` | GET | Get all comments |
| `/api/v1/maps/{mapId}/comments/{id}` | GET | Get specific comment |
| `/api/v1/maps/{mapId}/comments/{id}` | PUT | Update comment |
| `/api/v1/maps/{mapId}/comments/{id}` | DELETE | Delete comment |
| `/api/v1/maps/{mapId}/comments/{id}/resolve` | POST | Resolve comment |
| `/api/v1/maps/{mapId}/comments/{id}/reopen` | POST | Reopen comment |
| `/api/v1/maps/{mapId}/comments/{id}/reactions` | POST | Add reaction |
| `/api/v1/maps/{mapId}/comments/search?q={term}` | GET | Search comments |
| `/api/v1/maps/{mapId}/comments/analytics` | GET | Get analytics |
| `/api/v1/maps/{mapId}/comments/export` | GET | Export to CSV |

## SignalR Events

| Event | Description |
|-------|-------------|
| `CommentCreated` | New comment added |
| `CommentUpdated` | Comment modified |
| `CommentDeleted` | Comment removed |
| `CommentStatusChanged` | Status updated |
| `CommentReactionAdded` | Reaction added |
| `UserJoinedMap` | User started viewing |
| `UserLeftMap` | User stopped viewing |
| `TypingIndicatorChanged` | User typing status |

## Troubleshooting

### Comments not saving?
- Check database connection string
- Verify repository is initialized
- Check browser console for errors

### Real-time not working?
- Ensure SignalR hub is mapped: `app.MapHub<CommentHub>("/hubs/comments")`
- Check CORS settings
- Verify WebSocket support

### Permission errors?
- Check authentication token
- Verify authorization policies
- Check user roles

## Next Steps

1. Read the [full documentation](./VISUAL_COMMENTING_SYSTEM.md)
2. Customize comment categories for your use case
3. Set up moderation workflows
4. Configure notifications for @mentions
5. Export analytics and reports

## Support

- Documentation: [VISUAL_COMMENTING_SYSTEM.md](./VISUAL_COMMENTING_SYSTEM.md)
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Email: support@honua.io

Happy commenting! 🗣️📍
