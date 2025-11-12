# Zero-Click Sharing - Integration Checklist

## Pre-Integration Verification

- [x] All 12 implementation files created
- [x] Models defined (ShareToken, ShareComment)
- [x] Repository layer implemented (IShareRepository, SqliteShareRepository)
- [x] Service layer implemented (ShareService)
- [x] API endpoints created (ShareController - 11 endpoints)
- [x] UI components created (Share viewer page, Blazor dialog)
- [x] Database migrations prepared
- [x] Documentation completed

## Integration Steps

### Step 1: Database Setup ⏱️ 5 minutes

- [ ] Choose your database (PostgreSQL, SQLite, MySQL, or SQL Server)
- [ ] Run the appropriate migration from `/src/Honua.Server.Core/Data/Migrations/001_CreateSharingTables.sql`

**PostgreSQL:**
```bash
psql -h localhost -U honua -d honua_db -f src/Honua.Server.Core/Data/Migrations/001_CreateSharingTables.sql
```

**SQLite:**
Schema auto-initializes on first run via `EnsureInitializedAsync()`

**MySQL/SQL Server:**
Use the commented sections in the migration file

### Step 2: Dependency Injection ⏱️ 10 minutes

Add to `/src/Honua.Server.Host/Program.cs`:

```csharp
// 1. Register Share Repository
builder.Services.AddScoped<IShareRepository>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    var logger = sp.GetRequiredService<ILogger<SqliteShareRepository>>();
    return new SqliteShareRepository(connectionString, logger);
});

// 2. Register Share Service
builder.Services.AddScoped<ShareService>();

// 3. Initialize schema (add after var app = builder.Build();)
using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<IShareRepository>();
    await repository.EnsureInitializedAsync();
}
```

**Note:** Replace `SqliteShareRepository` with your database-specific implementation if using PostgreSQL, MySQL, or SQL Server.

### Step 3: Verify Razor Pages ⏱️ 2 minutes

Ensure Razor Pages are enabled in `Program.cs`:

```csharp
// Should already exist, but verify:
builder.Services.AddRazorPages();

var app = builder.Build();
app.MapRazorPages();
```

### Step 4: Configure Authorization Policies ⏱️ 5 minutes

Verify or add these policies in `Program.cs`:

```csharp
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireUser", policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy("RequireEditor", policy =>
        policy.RequireRole("editor", "admin"));
});
```

### Step 5: Test API Endpoints ⏱️ 15 minutes

- [ ] Start Honua Server
- [ ] Navigate to Swagger UI: `https://localhost:5001/swagger`
- [ ] Test `POST /api/v1/maps/{mapId}/share` - Create share
- [ ] Test `GET /api/v1/maps/share/{token}` - Validate share
- [ ] Test `GET /api/v1/maps/{mapId}/shares` - List shares
- [ ] Test `POST /api/v1/maps/share/{token}/comments` - Create comment
- [ ] Test `GET /api/v1/maps/share/{token}/comments` - Get comments

### Step 6: Test Viewer Page ⏱️ 10 minutes

- [ ] Create a test share token using the API
- [ ] Navigate to `https://localhost:5001/share/{token}`
- [ ] Verify map loads correctly
- [ ] Test commenting (if permission allows)
- [ ] Test password protection (if configured)
- [ ] Test on mobile device/responsive design

### Step 7: Integrate Blazor Component ⏱️ 15 minutes

Add the share button to your map management UI:

```razor
@inject IDialogService DialogService

<MudButton OnClick="@(() => ShowShareDialog(mapId))"
           Color="Color.Primary"
           StartIcon="@Icons.Material.Filled.Share">
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

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ShareMapDialog>(
            "Share Map",
            parameters,
            options
        );

        var result = await dialog.Result;

        if (!result.Canceled)
        {
            // Share created - show success message
            Snackbar.Add("Share link created successfully!", Severity.Success);
        }
    }
}
```

### Step 8: Configure CORS (for embeds) ⏱️ 5 minutes

If you plan to embed maps on external websites, configure CORS in `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowEmbeds", policy =>
    {
        policy.WithOrigins("https://your-allowed-domain.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("AllowEmbeds");
```

### Step 9: Security Hardening ⏱️ 20 minutes

#### Rate Limiting
- [ ] Install rate limiting package
- [ ] Configure rate limits for share creation (e.g., 10 per hour per user)
- [ ] Configure rate limits for comment posting (e.g., 5 per hour per IP)

#### Comment Moderation
- [ ] Configure auto-approval rules (authenticated users, trusted domains)
- [ ] Set up moderation dashboard access
- [ ] Configure email notifications for new comments

#### Password Hashing
- [ ] Consider upgrading from SHA256 to Argon2 for production:
```csharp
// Install Konscious.Security.Cryptography.Argon2
// Update ShareService password hashing
```

### Step 10: Background Jobs ⏱️ 15 minutes

Set up cleanup job for expired tokens:

```csharp
public class ShareCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ShareCleanupService> _logger;

    public ShareCleanupService(
        IServiceProvider serviceProvider,
        ILogger<ShareCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var shareService = scope.ServiceProvider
                    .GetRequiredService<ShareService>();

                var deleted = await shareService
                    .CleanupExpiredTokensAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation(
                        "Cleaned up {Count} expired share tokens",
                        deleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired tokens");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}

// Register in Program.cs
builder.Services.AddHostedService<ShareCleanupService>();
```

## Testing Checklist

### Unit Tests
- [ ] ShareService.CreateShareAsync()
- [ ] ShareService.ValidateShareAsync() - valid token
- [ ] ShareService.ValidateShareAsync() - expired token
- [ ] ShareService.ValidateShareAsync() - password validation
- [ ] ShareService.GenerateEmbedCode() - iframe
- [ ] ShareService.GenerateEmbedCode() - JavaScript
- [ ] ShareService.CreateCommentAsync()

### Integration Tests
- [ ] POST /api/v1/maps/{mapId}/share - success
- [ ] POST /api/v1/maps/{mapId}/share - unauthorized
- [ ] GET /api/v1/maps/share/{token} - valid token
- [ ] GET /api/v1/maps/share/{token} - invalid token
- [ ] GET /api/v1/maps/share/{token} - expired token
- [ ] GET /api/v1/maps/share/{token} - password required
- [ ] POST /api/v1/maps/share/{token}/comments - guest
- [ ] POST /api/v1/maps/share/{token}/comments - authenticated
- [ ] GET /api/v1/maps/share/{token}/comments

### E2E Tests
- [ ] Create share from Blazor UI
- [ ] Access share URL in browser
- [ ] View map in shared viewer
- [ ] Post comment as guest
- [ ] Approve comment as editor
- [ ] Test iframe embed
- [ ] Test JavaScript embed
- [ ] Test password-protected share
- [ ] Test expired share

## Performance Validation

- [ ] Share creation < 100ms
- [ ] Token validation < 50ms
- [ ] Viewer page load < 1s
- [ ] Comment posting < 200ms
- [ ] Database queries use indexes
- [ ] No N+1 queries in comment loading

## Documentation Review

- [ ] Read `/docs/zero-click-sharing.md`
- [ ] Read `/docs/zero-click-sharing-quickstart.md`
- [ ] Review API examples
- [ ] Review embed code examples
- [ ] Understand security considerations

## Production Deployment

### Pre-Deployment
- [ ] All tests passing
- [ ] Database migrations tested on staging
- [ ] CORS configured for embed domains
- [ ] Rate limiting configured
- [ ] Background cleanup job running
- [ ] Monitoring/logging configured

### Deployment
- [ ] Run database migrations
- [ ] Deploy updated application
- [ ] Verify health checks pass
- [ ] Smoke test share creation
- [ ] Smoke test viewer page
- [ ] Monitor error logs

### Post-Deployment
- [ ] Verify API endpoints responding
- [ ] Test share creation
- [ ] Test viewer page
- [ ] Test commenting
- [ ] Monitor performance metrics
- [ ] Monitor error rates

## Optional Enhancements

### Short Term
- [ ] Add PostgreSQL/MySQL/SQL Server repository implementations
- [ ] Implement CAPTCHA for guest comments
- [ ] Add email notifications for new comments
- [ ] Create admin dashboard for share management
- [ ] Add analytics tracking to viewer page

### Medium Term
- [ ] Social sharing buttons (Twitter, Facebook, LinkedIn)
- [ ] QR code generation
- [ ] Share link analytics dashboard
- [ ] Webhook support for share events
- [ ] Bulk share operations

### Long Term
- [ ] Real-time collaborative editing
- [ ] Advanced access controls (IP whitelisting)
- [ ] Custom branding/white-labeling
- [ ] Integration with external auth providers
- [ ] Mobile app deep linking

## Support Resources

- **Full Documentation**: `/docs/zero-click-sharing.md`
- **Quick Start**: `/docs/zero-click-sharing-quickstart.md`
- **Implementation Summary**: `/ZERO_CLICK_SHARING_IMPLEMENTATION.md`
- **API Reference**: `https://your-server/swagger`
- **GitHub Issues**: Report issues on GitHub repository

## Estimated Integration Time

| Phase | Time | Complexity |
|-------|------|------------|
| Database Setup | 5 min | Easy |
| DI Registration | 10 min | Easy |
| API Testing | 15 min | Medium |
| Viewer Testing | 10 min | Easy |
| Blazor Integration | 15 min | Medium |
| Security Hardening | 20 min | Medium |
| Background Jobs | 15 min | Medium |
| Testing | 60 min | Medium |
| **Total** | **~2.5 hours** | |

## Success Criteria

✅ Share links created successfully via API
✅ Viewer page loads and displays maps correctly
✅ Comments can be posted by guests
✅ Comments require moderation
✅ Embed codes work in external websites
✅ Expired shares are cleaned up automatically
✅ All tests passing
✅ Documentation complete and clear
✅ Performance meets targets
✅ Security measures implemented

---

**Ready to Integrate!** 🚀

Follow this checklist step-by-step, and you'll have Zero-Click Sharing fully integrated into Honua in approximately 2-3 hours.
