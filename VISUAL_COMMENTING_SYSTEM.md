# Honua Visual Commenting System

## Overview

The Honua Visual Commenting System provides a comprehensive, real-time collaborative annotation platform for maps. It enables users to add context-aware comments with spatial anchoring, threaded discussions, and advanced collaboration features.

## Features

### 1. Comment Placement
- ✅ **Point Comments**: Click anywhere on the map to add a comment
- ✅ **Feature Comments**: Attach comments to specific features (by feature ID)
- ✅ **Layer Comments**: Comment on entire layers
- ✅ **Line Comments**: Draw lines for route or boundary discussions
- ✅ **Polygon Comments**: Define areas for region-based annotations

### 2. Comment UI
- ✅ **Map Markers**: Visual indicators showing comment locations
- ✅ **Collapsible Panel**: Side panel with comment list
- ✅ **Threaded Discussions**: Reply to comments with nested threads
- ✅ **Rich Content**: Markdown support for formatted text
- ✅ **@Mentions**: Tag users with @username notation
- ✅ **File Attachments**: Attach images and documents

### 3. Collaboration Features
- ✅ **Real-time Updates**: SignalR-based live synchronization
- ✅ **Presence Indicators**: See who's viewing the map
- ✅ **Typing Indicators**: Know when others are composing replies
- ✅ **Status Management**: Open, Resolved, Closed states
- ✅ **Categories & Tags**: Organize comments by category
- ✅ **Priority Levels**: Low, Medium, High, Critical

### 4. Comment Management
- ✅ **Filtering**: By status, category, priority, author, date
- ✅ **Search**: Full-text search across comment text and authors
- ✅ **Export**: CSV export for reporting
- ✅ **Analytics**: Comment counts, response times, trends
- ✅ **Moderation**: Approve/hide/delete comments

### 5. Permissions
- ✅ **Role-based Access**: Control who can comment, resolve, moderate
- ✅ **Edit Own Comments**: Users can edit their own comments
- ✅ **Resolution Permissions**: Map owners can resolve comments
- ✅ **Guest Comments**: Optional guest commenting with moderation

## Architecture

```
┌─────────────────┐
│   MapSDK UI     │  MapComments.razor Component
│   (Blazor)      │  CommentItem.razor Component
└────────┬────────┘
         │
         ↓
┌─────────────────┐
│ SignalR Hub     │  CommentHub (Real-time)
│ (WebSockets)    │  - Broadcasts comment events
└────────┬────────┘  - Presence tracking
         │
         ↓
┌─────────────────┐
│  API Layer      │  CommentsController
│  (REST API)     │  - CRUD operations
└────────┬────────┘  - Search & filters
         │
         ↓
┌─────────────────┐
│ Service Layer   │  CommentService
│                 │  - Business logic
└────────┬────────┘  - @mention extraction
         │
         ↓
┌─────────────────┐
│ Repository      │  SqliteCommentRepository
│ (Data Access)   │  - Database operations
└────────┬────────┘  - Transactions
         │
         ↓
┌─────────────────┐
│   Database      │  SQLite / PostgreSQL
│                 │  - map_comments
│                 │  - map_comment_reactions
│                 │  - map_comment_notifications
└─────────────────┘
```

## API Endpoints

### Comment CRUD

#### 1. Create Comment
```http
POST /api/v1/maps/{mapId}/comments
Authorization: Bearer {token}
Content-Type: application/json

{
  "commentText": "This area needs attention",
  "geometryType": "point",
  "longitude": -122.4194,
  "latitude": 37.7749,
  "category": "Data Quality",
  "priority": "high",
  "color": "#FF5733"
}
```

**Response**: `201 Created`
```json
{
  "id": "comment-uuid",
  "mapId": "map-001",
  "author": "John Doe",
  "authorUserId": "user-123",
  "commentText": "This area needs attention",
  "geometryType": "point",
  "longitude": -122.4194,
  "latitude": 37.7749,
  "createdAt": "2025-01-15T10:30:00Z",
  "status": "open",
  "priority": "high",
  "category": "Data Quality",
  "color": "#FF5733",
  "replyCount": 0,
  "likeCount": 0
}
```

#### 2. Get Comments
```http
GET /api/v1/maps/{mapId}/comments?status=open&category=DataQuality&limit=50
```

**Response**: `200 OK`
```json
[
  {
    "id": "comment-uuid",
    "author": "John Doe",
    "commentText": "This area needs attention",
    ...
  }
]
```

#### 3. Get Specific Comment
```http
GET /api/v1/maps/{mapId}/comments/{commentId}
```

#### 4. Update Comment
```http
PUT /api/v1/maps/{mapId}/comments/{commentId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "commentText": "Updated comment text",
  "priority": "critical"
}
```

#### 5. Delete Comment
```http
DELETE /api/v1/maps/{mapId}/comments/{commentId}
Authorization: Bearer {token}
```

**Response**: `204 No Content`

### Thread Operations

#### 6. Get Replies
```http
GET /api/v1/maps/{mapId}/comments/{commentId}/replies
```

#### 7. Create Reply
```http
POST /api/v1/maps/{mapId}/comments
Content-Type: application/json

{
  "commentText": "@JohnDoe I can help with this",
  "parentId": "parent-comment-uuid"
}
```

### Status Management

#### 8. Resolve Comment
```http
POST /api/v1/maps/{mapId}/comments/{commentId}/resolve
Authorization: Bearer {token}
```

#### 9. Reopen Comment
```http
POST /api/v1/maps/{mapId}/comments/{commentId}/reopen
Authorization: Bearer {token}
```

### Reactions

#### 10. Add Reaction
```http
POST /api/v1/maps/{mapId}/comments/{commentId}/reactions
Authorization: Bearer {token}
Content-Type: application/json

{
  "reactionType": "like"
}
```

#### 11. Get Reactions
```http
GET /api/v1/maps/{mapId}/comments/{commentId}/reactions
```

### Search & Analytics

#### 12. Search Comments
```http
GET /api/v1/maps/{mapId}/comments/search?q=data+quality
```

#### 13. Get Analytics
```http
GET /api/v1/maps/{mapId}/comments/analytics
Authorization: Bearer {token}
```

**Response**: `200 OK`
```json
{
  "totalComments": 150,
  "openComments": 45,
  "resolvedComments": 95,
  "closedComments": 10,
  "commentsByCategory": {
    "Data Quality": 30,
    "Feature Request": 20
  },
  "commentsByPriority": {
    "critical": 5,
    "high": 20,
    "medium": 70,
    "low": 55
  },
  "averageResponseTime": 2.5
}
```

#### 14. Export Comments
```http
GET /api/v1/maps/{mapId}/comments/export
Authorization: Bearer {token}
Accept: text/csv
```

### Moderation

#### 15. Approve Comment
```http
POST /api/v1/maps/{mapId}/comments/{commentId}/approve
Authorization: Bearer {token}
Requires: Editor role
```

#### 16. Get Pending Comments
```http
GET /api/v1/maps/{mapId}/comments/pending
Authorization: Bearer {token}
Requires: Editor role
```

## Integration Examples

### 1. Basic Blazor Integration

```razor
@page "/map/{MapId}"
@using Honua.MapSDK.Components.Comments

<div class="map-container">
    <HonuaMap MapId="@MapId" @ref="mapRef">
        <MapComments MapId="@MapId"
                    ApiBaseUrl="/api/v1"
                    ShowPanel="true"
                    ShowMapMarkers="true"
                    EnableRealTime="true"
                    Categories="@commentCategories"
                    OnCommentCreated="HandleCommentCreated"
                    OnCommentSelected="HandleCommentSelected" />
    </HonuaMap>
</div>

@code {
    [Parameter]
    public string MapId { get; set; } = string.Empty;

    private HonuaMap? mapRef;
    private string[] commentCategories = new[]
    {
        "Data Quality",
        "Feature Request",
        "Bug Report",
        "General"
    };

    private async Task HandleCommentCreated(MapComment comment)
    {
        // Handle new comment
        await JSRuntime.InvokeVoidAsync("showNotification",
            $"New comment by {comment.Author}");
    }

    private async Task HandleCommentSelected(MapComment comment)
    {
        // Zoom to comment location
        if (comment.Longitude.HasValue && comment.Latitude.HasValue)
        {
            await mapRef!.FlyTo(comment.Longitude.Value, comment.Latitude.Value, 15);
        }
    }
}
```

### 2. JavaScript Integration

```javascript
// Initialize comment system
const commentSystem = new HonuaCommentSystem({
    mapId: 'map-001',
    apiBaseUrl: '/api/v1',
    containerId: 'comment-panel',
    enableRealTime: true
});

// Load comments
await commentSystem.loadComments();

// Enable map click to add comments
map.on('click', async (e) => {
    const { lng, lat } = e.lngLat;

    const comment = await commentSystem.createComment({
        commentText: 'New comment',
        longitude: lng,
        latitude: lat,
        priority: 'medium'
    });
});

// Listen for real-time updates
commentSystem.on('commentCreated', (comment) => {
    console.log('New comment:', comment);
    // Add marker to map
    addCommentMarker(comment);
});
```

### 3. React Integration

```jsx
import { useComments } from '@honua/react-comments';

function MapWithComments({ mapId }) {
    const {
        comments,
        createComment,
        resolveComment,
        loading
    } = useComments(mapId);

    const handleMapClick = async (lng, lat) => {
        await createComment({
            commentText: 'New comment',
            longitude: lng,
            latitude: lat,
            priority: 'medium'
        });
    };

    return (
        <div className="map-container">
            <Map
                mapId={mapId}
                onClick={handleMapClick}
            />

            <CommentPanel
                comments={comments}
                onResolve={resolveComment}
                loading={loading}
            />
        </div>
    );
}
```

### 4. REST API Integration (cURL)

```bash
# Create a comment
curl -X POST https://api.honua.io/api/v1/maps/map-001/comments \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "commentText": "This area needs review",
    "longitude": -122.4194,
    "latitude": 37.7749,
    "priority": "high",
    "category": "Data Quality"
  }'

# Get all comments
curl https://api.honua.io/api/v1/maps/map-001/comments

# Search comments
curl "https://api.honua.io/api/v1/maps/map-001/comments/search?q=data+quality"

# Resolve a comment
curl -X POST https://api.honua.io/api/v1/maps/map-001/comments/comment-123/resolve \
  -H "Authorization: Bearer YOUR_TOKEN"
```

## SignalR Real-time Events

### Connect to Hub

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/comments")
    .withAutomaticReconnect()
    .build();

await connection.start();

// Join map group
await connection.invoke("JoinMap", "map-001");
```

### Event Listeners

```javascript
// Comment created
connection.on("CommentCreated", (comment) => {
    console.log("New comment:", comment);
    addCommentToUI(comment);
});

// Comment updated
connection.on("CommentUpdated", (comment) => {
    console.log("Comment updated:", comment);
    updateCommentInUI(comment);
});

// Comment deleted
connection.on("CommentDeleted", ({ mapId, commentId }) => {
    console.log("Comment deleted:", commentId);
    removeCommentFromUI(commentId);
});

// Status changed
connection.on("CommentStatusChanged", ({ commentId, status, resolvedBy }) => {
    console.log(`Comment ${commentId} status: ${status}`);
    updateCommentStatus(commentId, status);
});

// Reaction added
connection.on("CommentReactionAdded", ({ commentId, userId, reactionType }) => {
    console.log(`${userId} reacted with ${reactionType}`);
    updateReactionCount(commentId);
});

// User presence
connection.on("UserJoinedMap", ({ userId, mapId }) => {
    console.log(`${userId} joined the map`);
    updatePresenceIndicator(userId, true);
});

connection.on("UserLeftMap", ({ userId, mapId }) => {
    console.log(`${userId} left the map`);
    updatePresenceIndicator(userId, false);
});

// Typing indicators
connection.on("TypingIndicatorChanged", ({ userId, commentId, isTyping }) => {
    showTypingIndicator(userId, commentId, isTyping);
});
```

## Database Setup

### 1. Run Migration

```bash
# Apply database schema
sqlite3 honua.db < src/Honua.Server.Core/Data/Comments/CommentsDatabaseSetup.sql
```

### 2. Initialize Repository

```csharp
// Startup.cs or Program.cs
services.AddSingleton<ICommentRepository>(sp =>
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    var logger = sp.GetRequiredService<ILogger<SqliteCommentRepository>>();
    var repo = new SqliteCommentRepository(connectionString, logger);

    // Initialize schema
    repo.EnsureInitializedAsync().GetAwaiter().GetResult();

    return repo;
});

services.AddScoped<CommentService>();
```

### 3. Configure SignalR

```csharp
// Program.cs
builder.Services.AddSignalR();

// After app.Build()
app.MapHub<CommentHub>("/hubs/comments");
```

## Configuration

### appsettings.json

```json
{
  "Comments": {
    "EnableGuestComments": true,
    "RequireModeration": false,
    "MaxCommentLength": 10000,
    "MaxAttachmentSize": 5242880,
    "AllowedFileTypes": [".jpg", ".png", ".pdf", ".doc", ".docx"],
    "EnableMentions": true,
    "EnableReactions": true,
    "PriorityLevels": ["low", "medium", "high", "critical"],
    "DefaultPriority": "medium",
    "AutoResolveAfterDays": 30
  },
  "SignalR": {
    "EnableDetailedErrors": true,
    "KeepAliveInterval": "00:00:15",
    "ClientTimeoutInterval": "00:00:30"
  }
}
```

## Security Considerations

### 1. Authentication
- All write operations require authentication
- Read operations can be public or authenticated based on map permissions

### 2. Authorization
- Comment authors can edit/delete their own comments
- Map owners can resolve/close any comment
- Moderators can approve/hide/delete any comment

### 3. Rate Limiting
```csharp
// Add rate limiting middleware
services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("comments", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 10;
    });
});
```

### 4. Input Validation
- Comment text: Max 10,000 characters
- Sanitize HTML/JavaScript in comment text
- Validate geometry coordinates
- Prevent XSS attacks

### 5. Spam Prevention
- Track IP addresses
- Rate limit by IP and user
- Guest comments require moderation
- CAPTCHA for guest comments (optional)

## Performance Optimization

### 1. Caching
```csharp
services.AddMemoryCache();

// Cache frequently accessed comments
var cacheKey = $"comments_{mapId}";
if (!cache.TryGetValue(cacheKey, out List<MapComment> comments))
{
    comments = await commentService.GetMapCommentsAsync(mapId);
    cache.Set(cacheKey, comments, TimeSpan.FromMinutes(5));
}
```

### 2. Pagination
```http
GET /api/v1/maps/{mapId}/comments?limit=50&offset=0
```

### 3. Lazy Loading
- Load comments on-demand as user scrolls
- Load replies only when thread is expanded

### 4. Database Indexing
- All foreign keys indexed
- Composite indexes on frequently queried columns
- Regular VACUUM on SQLite

## Troubleshooting

### Comments Not Appearing
1. Check database connection
2. Verify comment is approved (`is_approved = 1`)
3. Check filter settings
4. Verify SignalR connection

### SignalR Connection Failed
1. Check CORS settings
2. Verify WebSocket support
3. Check firewall rules
4. Review SignalR logs

### Permission Denied
1. Verify authentication token
2. Check user roles
3. Review authorization policies

## Future Enhancements

- [ ] Video attachments
- [ ] Voice comments
- [ ] Comment templates
- [ ] Auto-translation
- [ ] Sentiment analysis
- [ ] AI-powered comment suggestions
- [ ] Mobile app integration
- [ ] Offline comment sync

## Support

For issues and questions:
- GitHub Issues: https://github.com/honua-io/Honua.Server/issues
- Documentation: https://docs.honua.io
- Email: support@honua.io

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
