# Zero-Click Sharing with Embeddable Maps

## Overview

Honua's Zero-Click Sharing feature enables users to instantly share maps with a single click, generating shareable URLs with configurable permissions, expiration dates, and embed codes. This feature is designed for non-technical users while providing powerful capabilities for advanced use cases.

## Features

### 1. **Share Link Generation**
- **One-Click Sharing**: Generate instant shareable URLs with a single API call
- **Configurable Permissions**:
  - `view`: View-only access (default)
  - `comment`: View and comment on the map
  - `edit`: View, comment, and edit map features
- **Guest Access**: Allow viewing without requiring login
- **Expiration Options**:
  - Never expires
  - 7 days
  - 30 days
  - 90 days
  - Custom date
- **Password Protection**: Optional password for sensitive shares
- **Access Analytics**: Track view count and last access time

### 2. **Embed Code Generator**
- **iframe Embed**: Standard HTML iframe embed code
- **JavaScript SDK Embed**: Advanced embedding with Honua MapSDK
- **Customizable Options**:
  - Width and height
  - Control visibility (zoom, layers, search, scale bar)
  - Custom CSS styling

### 3. **Shared Map Viewer**
- Lightweight, fast-loading viewer page
- Respects permission settings
- Mobile-friendly responsive design
- Direct URL access: `https://your-server/share/{token}`

### 4. **Guest Commenting**
- Allow comments without login
- Moderation workflow for guest comments
- Optional location-based comments (map annotations)
- Threaded discussions support

## Architecture

### Database Schema

#### share_tokens Table
```sql
- token (PK): Unique GUID-based share token
- map_id: Reference to map configuration
- created_by: User who created the share
- permission: view | comment | edit
- allow_guest_access: Boolean
- expires_at: Expiration timestamp (nullable)
- created_at: Creation timestamp
- access_count: Number of times accessed
- last_accessed_at: Last access timestamp
- is_active: Boolean (for deactivation)
- password_hash: Optional password protection
- embed_settings: JSON configuration for embeds
```

#### share_comments Table
```sql
- id (PK): Unique comment ID
- share_token: Reference to share token
- map_id: Direct map reference
- author: Comment author name
- is_guest: Boolean flag
- guest_email: Optional email for notifications
- comment_text: Comment content
- created_at: Creation timestamp
- is_approved: Moderation flag
- is_deleted: Soft delete flag
- parent_id: For threaded discussions
- location_x, location_y: Optional map coordinates
- ip_address, user_agent: For spam prevention
```

### Components

1. **ShareToken Entity** (`/src/Honua.Server.Core/Models/ShareToken.cs`)
   - Domain model for share tokens
   - Validation and expiration logic

2. **ShareComment Entity** (`/src/Honua.Server.Core/Models/ShareComment.cs`)
   - Domain model for comments
   - Support for threaded discussions

3. **IShareRepository** (`/src/Honua.Server.Core/Data/Sharing/IShareRepository.cs`)
   - Repository interface for data access
   - Database-agnostic design

4. **SqliteShareRepository** (`/src/Honua.Server.Core/Data/Sharing/SqliteShareRepository.cs`)
   - SQLite implementation
   - Additional implementations needed for PostgreSQL, MySQL, SQL Server

5. **ShareService** (`/src/Honua.Server.Core/Services/Sharing/ShareService.cs`)
   - Business logic layer
   - Token validation and management
   - Embed code generation

6. **ShareController** (`/src/Honua.Server.Host/API/ShareController.cs`)
   - REST API endpoints
   - Request/response models

7. **Share Viewer Page** (`/src/Honua.Server.Host/Pages/Share.cshtml`)
   - Razor page for viewing shared maps
   - Integrated commenting UI

8. **ShareMapDialog Component** (`/src/Honua.Admin.Blazor/Components/Shared/ShareMapDialog.razor`)
   - Blazor UI for creating shares
   - Settings configuration

## API Reference

### Create Share Link

**POST** `/api/v1/maps/{mapId}/share`

Creates a new share link for a map.

**Request Body:**
```json
{
  "permission": "view",
  "allowGuestAccess": true,
  "expiresAt": "2025-12-31T23:59:59Z",
  "password": "optional-password",
  "embedSettings": {
    "width": "100%",
    "height": "600px",
    "showZoomControls": true,
    "showLayerSwitcher": true,
    "showSearch": false,
    "showScaleBar": true,
    "showAttribution": true,
    "allowFullscreen": true
  }
}
```

**Response:**
```json
{
  "token": "abc123xyz789",
  "shareUrl": "https://your-server/share/abc123xyz789",
  "permission": "view",
  "allowGuestAccess": true,
  "expiresAt": "2025-12-31T23:59:59Z",
  "createdAt": "2025-11-12T10:00:00Z",
  "embedCode": "<iframe src=\"...\"></iframe>",
  "jsEmbedCode": "<div id=\"...\">...</div><script>...</script>",
  "hasPassword": true
}
```

### Get Share Token

**GET** `/api/v1/maps/share/{token}?password={password}`

Retrieves share token details (validates the token).

**Response:**
```json
{
  "token": "abc123xyz789",
  "shareUrl": "https://your-server/share/abc123xyz789",
  "mapId": "map-123",
  "permission": "view",
  "allowGuestAccess": true,
  "expiresAt": "2025-12-31T23:59:59Z",
  "createdAt": "2025-11-12T10:00:00Z",
  "accessCount": 42,
  "lastAccessedAt": "2025-11-12T15:30:00Z",
  "hasPassword": true
}
```

### List Shares for Map

**GET** `/api/v1/maps/{mapId}/shares`

Gets all share links for a specific map.

**Response:**
```json
[
  {
    "token": "abc123xyz789",
    "shareUrl": "https://your-server/share/abc123xyz789",
    "permission": "view",
    "allowGuestAccess": true,
    "expiresAt": "2025-12-31T23:59:59Z",
    "createdAt": "2025-11-12T10:00:00Z",
    "accessCount": 42,
    "isActive": true
  }
]
```

### Update Share

**PUT** `/api/v1/maps/share/{token}`

Updates share settings.

**Request Body:**
```json
{
  "permission": "comment",
  "allowGuestAccess": false,
  "expiresAt": "2026-01-01T00:00:00Z",
  "isActive": true
}
```

### Deactivate Share

**DELETE** `/api/v1/maps/share/{token}`

Deactivates a share link (doesn't delete, just marks inactive).

### Get Embed Code

**GET** `/api/v1/maps/share/{token}/embed?type=iframe`

Generates embed code for a share.

**Query Parameters:**
- `type`: `iframe` or `javascript`

**Response:**
```json
{
  "type": "iframe",
  "code": "<iframe src=\"https://your-server/share/abc123xyz789\" width=\"100%\" height=\"600px\" style=\"border:none;\"></iframe>"
}
```

### Create Comment

**POST** `/api/v1/maps/share/{token}/comments`

Creates a comment on a shared map (guest-friendly).

**Request Body:**
```json
{
  "mapId": "map-123",
  "author": "John Doe",
  "guestEmail": "john@example.com",
  "commentText": "Great map! Thanks for sharing.",
  "parentId": null,
  "locationX": -122.4194,
  "locationY": 37.7749
}
```

**Response:**
```json
{
  "id": "comment-456",
  "author": "John Doe",
  "commentText": "Great map! Thanks for sharing.",
  "createdAt": "2025-11-12T16:00:00Z",
  "isApproved": false,
  "locationX": -122.4194,
  "locationY": 37.7749
}
```

### Get Comments

**GET** `/api/v1/maps/share/{token}/comments`

Retrieves comments for a shared map.

**Response:**
```json
[
  {
    "id": "comment-456",
    "author": "John Doe",
    "commentText": "Great map! Thanks for sharing.",
    "createdAt": "2025-11-12T16:00:00Z",
    "isApproved": true,
    "locationX": -122.4194,
    "locationY": 37.7749
  }
]
```

### Approve Comment

**POST** `/api/v1/maps/comments/{commentId}/approve`

Approves a guest comment (requires editor role).

### Delete Comment

**DELETE** `/api/v1/maps/comments/{commentId}`

Deletes a comment (requires editor role).

### Get Pending Comments

**GET** `/api/v1/maps/comments/pending`

Gets comments awaiting moderation (requires editor role).

## Usage Examples

### Example 1: Basic Share (C# Client)

```csharp
using System.Net.Http.Json;

var httpClient = new HttpClient { BaseAddress = new Uri("https://your-honua-server") };

// Create a share
var request = new
{
    permission = "view",
    allowGuestAccess = true,
    expiresAt = DateTime.UtcNow.AddDays(30)
};

var response = await httpClient.PostAsJsonAsync($"/api/v1/maps/my-map-id/share", request);
var share = await response.Content.ReadFromJsonAsync<ShareTokenResponse>();

Console.WriteLine($"Share URL: {share.ShareUrl}");
Console.WriteLine($"Embed Code: {share.EmbedCode}");
```

### Example 2: Protected Share with Comments

```csharp
var request = new
{
    permission = "comment",
    allowGuestAccess = true,
    password = "secret123",
    expiresAt = DateTime.UtcNow.AddDays(7)
};

var response = await httpClient.PostAsJsonAsync($"/api/v1/maps/my-map-id/share", request);
var share = await response.Content.ReadFromJsonAsync<ShareTokenResponse>();
```

### Example 3: Embed in HTML

```html
<!-- Simple iframe embed -->
<iframe
  src="https://your-server/share/abc123xyz789"
  width="100%"
  height="600px"
  style="border:none;"
  allowfullscreen>
</iframe>

<!-- JavaScript SDK embed -->
<div id="honua-map"></div>
<script src="https://your-server/js/honua-embed.js"></script>
<script>
  HonuaEmbed.init({
    container: 'honua-map',
    shareToken: 'abc123xyz789',
    width: '100%',
    height: '600px',
    showZoomControls: true,
    showLayerSwitcher: true
  });
</script>
```

### Example 4: Guest Commenting (JavaScript)

```javascript
// Post a comment
async function postComment(token, author, text) {
  const response = await fetch(`/api/v1/maps/share/${token}/comments`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      mapId: 'map-123',
      author: author,
      commentText: text
    })
  });

  const comment = await response.json();
  console.log('Comment posted:', comment);
}

// Load comments
async function loadComments(token) {
  const response = await fetch(`/api/v1/maps/share/${token}/comments`);
  const comments = await response.json();

  comments.forEach(comment => {
    console.log(`${comment.author}: ${comment.commentText}`);
  });
}
```

### Example 5: Using Blazor Component

```razor
@inject IDialogService DialogService

<MudButton OnClick="ShowShareDialog" Color="Color.Primary">
  Share Map
</MudButton>

@code {
    [Parameter] public string MapId { get; set; } = string.Empty;

    private async Task ShowShareDialog()
    {
        var parameters = new DialogParameters
        {
            ["MapId"] = MapId
        };

        var dialog = await DialogService.ShowAsync<ShareMapDialog>(
            "Share Map",
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Medium });

        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // Share created successfully
        }
    }
}
```

## Security Considerations

1. **Token Generation**: Uses cryptographically secure random tokens (32 bytes, base64-encoded, URL-safe)

2. **Password Protection**: Passwords are hashed using SHA256 (consider upgrading to Argon2 for production)

3. **Rate Limiting**: Implement rate limiting on share creation and comment posting endpoints

4. **Comment Moderation**: Guest comments require approval before being visible

5. **Spam Prevention**: IP address and user agent tracking for identifying spam

6. **CORS Configuration**: Ensure proper CORS settings for embedded maps

7. **Content Security Policy**: Configure CSP headers for iframe embeds

## Performance Optimization

1. **Database Indexes**: Indexes on `map_id`, `created_by`, `expires_at`, and `is_active` columns

2. **Caching**: Cache frequently accessed share tokens using Redis or memory cache

3. **Cleanup Job**: Schedule periodic cleanup of expired tokens:
   ```csharp
   // Example background service
   public class ShareCleanupService : BackgroundService
   {
       protected override async Task ExecuteAsync(CancellationToken stoppingToken)
       {
           while (!stoppingToken.IsCancellationRequested)
           {
               await _shareService.CleanupExpiredTokensAsync(stoppingToken);
               await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
           }
       }
   }
   ```

4. **Pagination**: Paginate comment lists for maps with many comments

## Integration Steps

### 1. Database Migration

Run the SQL migration script for your database:

```bash
# PostgreSQL
psql -h localhost -U honua -d honua_db -f src/Honua.Server.Core/Data/Migrations/001_CreateSharingTables.sql

# SQLite (automatically runs on initialization)
# MySQL
mysql -h localhost -u honua -p honua_db < migration.sql

# SQL Server
sqlcmd -S localhost -U honua -P password -d honua_db -i migration.sql
```

### 2. Register Services

Add sharing services to dependency injection in `Program.cs`:

```csharp
// Register repository
builder.Services.AddScoped<IShareRepository>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var logger = sp.GetRequiredService<ILogger<SqliteShareRepository>>();
    return new SqliteShareRepository(connectionString, logger);
});

// Register service
builder.Services.AddScoped<ShareService>();

// Initialize schema on startup
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IShareRepository>();
    await repository.EnsureInitializedAsync();
}
```

### 3. Configure Razor Pages

Ensure Razor Pages are enabled in `Program.cs`:

```csharp
builder.Services.AddRazorPages();

var app = builder.Build();
app.MapRazorPages();
```

### 4. Add Blazor Component

Add the ShareMapDialog component to your Blazor pages:

```razor
<MudButton OnClick="@(() => ShowShareDialog(mapId))">
  Share
</MudButton>
```

### 5. Configure Authorization Policies

Ensure proper policies are configured:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireUser", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("RequireEditor", policy =>
        policy.RequireRole("editor", "admin"));
});
```

## Future Enhancements

1. **Social Sharing**: Add quick share buttons for Twitter, LinkedIn, Facebook
2. **QR Codes**: Generate QR codes for easy mobile access
3. **Analytics Dashboard**: Detailed analytics on share performance
4. **Webhooks**: Notify external services when shares are accessed
5. **Collaborative Editing**: Real-time collaborative map editing for "edit" permission
6. **Comment Notifications**: Email notifications for new comments
7. **Export Comments**: Export comments as CSV or JSON
8. **Custom Branding**: White-label the shared map viewer
9. **Access Logs**: Detailed logging of who accessed shares and when
10. **Bulk Operations**: Bulk create/delete shares for multiple maps

## Troubleshooting

### Share link returns 404
- Verify the token exists in the database
- Check that the share token hasn't expired
- Ensure the share is active (`is_active = true`)

### Comments not appearing
- Check if comments require moderation (`is_approved = false`)
- Verify the share permission allows commenting
- Check for soft-deleted comments (`is_deleted = true`)

### Embed not loading
- Verify CORS configuration allows your domain
- Check Content Security Policy headers
- Ensure the embed URL is correct
- Verify JavaScript is enabled in the browser

## Support

For questions or issues, please:
- Check the [API documentation](https://your-honua-server/swagger)
- Review the [GitHub repository](https://github.com/honua-io/Honua.Server)
- Contact support at support@honua.io

## License

Licensed under the Elastic License 2.0. See LICENSE file for details.
