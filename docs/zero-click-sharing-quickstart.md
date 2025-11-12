# Zero-Click Sharing - Quick Start Guide

## Installation

### 1. Run Database Migration

```bash
# PostgreSQL
psql -h localhost -U honua -d honua_db -f src/Honua.Server.Core/Data/Migrations/001_CreateSharingTables.sql
```

### 2. Register Services in Program.cs

```csharp
// Add to ConfigureServices
builder.Services.AddScoped<IShareRepository>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var logger = sp.GetRequiredService<ILogger<SqliteShareRepository>>();
    return new SqliteShareRepository(connectionString, logger);
});

builder.Services.AddScoped<ShareService>();

// Initialize schema
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IShareRepository>();
    await repository.EnsureInitializedAsync();
}
```

## Basic Usage

### Create a Share Link (C#)

```csharp
var shareService = serviceProvider.GetRequiredService<ShareService>();

var token = await shareService.CreateShareAsync(
    mapId: "my-map-id",
    permission: SharePermission.View,
    createdBy: "user-123",
    allowGuestAccess: true,
    expiresAt: DateTime.UtcNow.AddDays(30)
);

Console.WriteLine($"Share URL: https://your-server/share/{token.Token}");
```

### Create a Share via API

```bash
curl -X POST https://your-server/api/v1/maps/my-map-id/share \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "permission": "view",
    "allowGuestAccess": true,
    "expiresAt": "2025-12-31T23:59:59Z"
  }'
```

### Embed in HTML

```html
<iframe
  src="https://your-server/share/abc123xyz789"
  width="100%"
  height="600px"
  style="border:none;"
  allowfullscreen>
</iframe>
```

### Add Blazor Share Button

```razor
@inject IDialogService DialogService

<MudButton OnClick="@(() => ShowShareDialog(mapId))" Color="Color.Primary">
  <MudIcon Icon="@Icons.Material.Filled.Share" />
  Share
</MudButton>

@code {
    private async Task ShowShareDialog(string mapId)
    {
        var parameters = new DialogParameters { ["MapId"] = mapId };
        await DialogService.ShowAsync<ShareMapDialog>("Share Map", parameters);
    }
}
```

## Key Features

### Permission Levels
- **view**: View-only access (default)
- **comment**: View and comment
- **edit**: Full editing access

### Expiration Options
```csharp
// Never expires
expiresAt: null

// 7 days
expiresAt: DateTime.UtcNow.AddDays(7)

// 30 days
expiresAt: DateTime.UtcNow.AddDays(30)

// Custom
expiresAt: new DateTime(2025, 12, 31)
```

### Password Protection
```csharp
var token = await shareService.CreateShareAsync(
    mapId: "my-map-id",
    permission: SharePermission.View,
    password: "secret123"  // Optional password
);
```

### Guest Comments
```javascript
// Allow guests to comment
fetch('/api/v1/maps/share/abc123xyz789/comments', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    mapId: 'my-map-id',
    author: 'John Doe',
    guestEmail: 'john@example.com',
    commentText: 'Great map!'
  })
});
```

## Common Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/v1/maps/{mapId}/share` | Create share link |
| GET | `/api/v1/maps/share/{token}` | Get share details |
| GET | `/api/v1/maps/{mapId}/shares` | List all shares for map |
| PUT | `/api/v1/maps/share/{token}` | Update share settings |
| DELETE | `/api/v1/maps/share/{token}` | Deactivate share |
| GET | `/api/v1/maps/share/{token}/embed` | Get embed code |
| POST | `/api/v1/maps/share/{token}/comments` | Create comment |
| GET | `/api/v1/maps/share/{token}/comments` | Get comments |

## Files Created

### Core Models
- `/src/Honua.Server.Core/Models/ShareToken.cs`
- `/src/Honua.Server.Core/Models/ShareComment.cs`

### Data Layer
- `/src/Honua.Server.Core/Data/Sharing/IShareRepository.cs`
- `/src/Honua.Server.Core/Data/Sharing/SqliteShareRepository.cs`

### Services
- `/src/Honua.Server.Core/Services/Sharing/ShareService.cs`

### API
- `/src/Honua.Server.Host/API/ShareController.cs`

### UI
- `/src/Honua.Server.Host/Pages/Share.cshtml` (Viewer page)
- `/src/Honua.Server.Host/Pages/Share.cshtml.cs` (Page model)
- `/src/Honua.Admin.Blazor/Components/Shared/ShareMapDialog.razor` (Share dialog)

### Migrations
- `/src/Honua.Server.Core/Data/Migrations/001_CreateSharingTables.sql`

### Documentation
- `/docs/zero-click-sharing.md` (Full documentation)
- `/docs/zero-click-sharing-quickstart.md` (This file)

## Next Steps

1. **Test the API**: Use Swagger UI at `/swagger` to test endpoints
2. **Customize Viewer**: Modify `Share.cshtml` to match your branding
3. **Add Security**: Implement rate limiting and CAPTCHA for guest comments
4. **Monitor Usage**: Set up analytics to track share link usage
5. **Add Notifications**: Configure email notifications for new comments

## Support

For detailed documentation, see `/docs/zero-click-sharing.md`

For API reference, visit `https://your-server/swagger`
