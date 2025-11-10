# Embedding Power BI Reports in Web Applications

This guide shows how to embed Power BI reports in your municipal website or application using Honua.Server's Power BI integration.

## Prerequisites

- Power BI Pro or Premium workspace
- Azure AD Service Principal with Power BI permissions
- Honua.Server with Power BI integration configured
- Published Power BI report

## Embedding Architecture

```
┌──────────────┐
│   Web App    │
│  (Your Site) │
└──────┬───────┘
       │ 1. Request embed token
       ↓
┌──────────────────┐
│  Honua.Server    │
│   (Backend)      │
└──────┬───────────┘
       │ 2. Request token from Power BI
       ↓
┌──────────────────┐
│  Power BI API    │
│  (Azure AD)      │
└──────┬───────────┘
       │ 3. Return embed token
       ↓
┌──────────────────┐
│  Web App         │
│  (Embed report)  │
└──────────────────┘
```

## Step 1: Configure Backend API

### 1.1 Create Embed Token Controller

Create `/Controllers/PowerBIEmbedController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Honua.Integration.PowerBI.Services;

namespace YourApp.Controllers;

[ApiController]
[Route("api/powerbi")]
[Authorize] // Require authentication
public class PowerBIEmbedController : ControllerBase
{
    private readonly IPowerBIDatasetService _datasetService;
    private readonly ILogger<PowerBIEmbedController> _logger;

    public PowerBIEmbedController(
        IPowerBIDatasetService datasetService,
        ILogger<PowerBIEmbedController> logger)
    {
        _datasetService = datasetService;
        _logger = logger;
    }

    /// <summary>
    /// Generates an embed token for a Power BI report
    /// </summary>
    [HttpGet("embed-token")]
    public async Task<IActionResult> GetEmbedToken(
        [FromQuery] string reportId,
        [FromQuery] string datasetId)
    {
        try
        {
            // Validate user has permission to view this report
            // TODO: Add your authorization logic here

            var token = await _datasetService.GenerateEmbedTokenAsync(
                reportId,
                datasetId);

            return Ok(new
            {
                token,
                expiration = DateTimeOffset.UtcNow.AddMinutes(60),
                reportId,
                datasetId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating embed token for report {ReportId}", reportId);
            return StatusCode(500, new { error = "Failed to generate embed token" });
        }
    }

    /// <summary>
    /// Gets report metadata (embed URL, etc.)
    /// </summary>
    [HttpGet("reports/{reportId}")]
    public IActionResult GetReportMetadata(string reportId)
    {
        // In production, fetch this from Power BI API
        var workspaceId = Configuration["PowerBI:WorkspaceId"];

        return Ok(new
        {
            reportId,
            embedUrl = $"https://app.powerbi.com/reportEmbed?reportId={reportId}&groupId={workspaceId}",
            name = "Smart City Dashboard"
        });
    }
}
```

### 1.2 Add CORS (if needed)

In `Program.cs`:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("PowerBIEmbed", policy =>
    {
        policy.WithOrigins("https://your-website.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors("PowerBIEmbed");
```

## Step 2: Frontend Integration

### 2.1 Install Power BI JavaScript SDK

#### Via CDN (easiest)
```html
<script src="https://cdn.jsdelivr.net/npm/powerbi-client@2.22.0/dist/powerbi.min.js"></script>
```

#### Via npm
```bash
npm install powerbi-client
```

### 2.2 Create Embed Container

```html
<!DOCTYPE html>
<html>
<head>
    <title>Smart City Dashboard</title>
    <style>
        #reportContainer {
            width: 100%;
            height: 600px;
            border: none;
        }
        .loading {
            text-align: center;
            padding: 50px;
        }
    </style>
</head>
<body>
    <h1>Traffic Monitoring Dashboard</h1>
    <div id="reportContainer" class="loading">
        Loading dashboard...
    </div>

    <script src="https://cdn.jsdelivr.net/npm/powerbi-client@2.22.0/dist/powerbi.min.js"></script>
    <script src="embed.js"></script>
</body>
</html>
```

### 2.3 Embed Report (JavaScript)

Create `embed.js`:

```javascript
const reportId = 'your-report-id';
const datasetId = 'your-dataset-id';
const embedUrl = `https://app.powerbi.com/reportEmbed?reportId=${reportId}`;

// Fetch embed token from backend
async function getEmbedToken() {
    const response = await fetch(
        `/api/powerbi/embed-token?reportId=${reportId}&datasetId=${datasetId}`,
        {
            headers: {
                'Authorization': `Bearer ${yourAuthToken}` // If using auth
            }
        }
    );

    if (!response.ok) {
        throw new Error('Failed to get embed token');
    }

    return await response.json();
}

// Embed the report
async function embedReport() {
    try {
        const { token, expiration } = await getEmbedToken();

        const models = window['powerbi-client'].models;
        const reportContainer = document.getElementById('reportContainer');

        const config = {
            type: 'report',
            tokenType: models.TokenType.Embed,
            accessToken: token,
            embedUrl: embedUrl,
            id: reportId,
            permissions: models.Permissions.Read,
            settings: {
                filterPaneEnabled: true,
                navContentPaneEnabled: true,
                background: models.BackgroundType.Transparent
            }
        };

        // Embed the report
        const powerbi = new window['powerbi-client'].service.Service(
            window['powerbi-client'].factories.hpmFactory,
            window['powerbi-client'].factories.wpmpFactory,
            window['powerbi-client'].factories.routerFactory
        );

        const report = powerbi.embed(reportContainer, config);

        // Handle events
        report.on('loaded', function() {
            console.log('Report loaded successfully');
        });

        report.on('rendered', function() {
            console.log('Report rendered');
            reportContainer.classList.remove('loading');
        });

        report.on('error', function(event) {
            console.error('Report error:', event.detail);
            reportContainer.innerHTML = '<p style="color: red;">Error loading report</p>';
        });

        // Refresh token before expiration
        const expirationTime = new Date(expiration).getTime();
        const now = Date.now();
        const timeUntilExpiration = expirationTime - now;

        if (timeUntilExpiration > 60000) { // Refresh 1 minute before expiration
            setTimeout(async () => {
                const { token: newToken } = await getEmbedToken();
                await report.setAccessToken(newToken);
                console.log('Token refreshed');
            }, timeUntilExpiration - 60000);
        }

    } catch (error) {
        console.error('Error embedding report:', error);
        document.getElementById('reportContainer').innerHTML =
            '<p style="color: red;">Failed to load dashboard. Please try again later.</p>';
    }
}

// Initialize on page load
embedReport();
```

## Step 3: React Integration

### 3.1 Install Dependencies

```bash
npm install powerbi-client powerbi-client-react
```

### 3.2 Create React Component

```tsx
import React, { useState, useEffect } from 'react';
import { PowerBIEmbed } from 'powerbi-client-react';
import { models } from 'powerbi-client';

interface EmbedConfig {
    token: string;
    expiration: string;
    reportId: string;
    datasetId: string;
}

export const TrafficDashboard: React.FC = () => {
    const [embedConfig, setEmbedConfig] = useState<EmbedConfig | null>(null);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        fetchEmbedToken();
    }, []);

    const fetchEmbedToken = async () => {
        try {
            const response = await fetch(
                '/api/powerbi/embed-token?reportId=your-report-id&datasetId=your-dataset-id',
                {
                    headers: {
                        'Authorization': `Bearer ${localStorage.getItem('authToken')}`
                    }
                }
            );

            if (!response.ok) {
                throw new Error('Failed to fetch embed token');
            }

            const data = await response.json();
            setEmbedConfig(data);
        } catch (err) {
            setError(err.message);
            console.error('Error fetching embed token:', err);
        }
    };

    if (error) {
        return <div className="alert alert-danger">Error: {error}</div>;
    }

    if (!embedConfig) {
        return <div className="text-center p-5">Loading dashboard...</div>;
    }

    return (
        <div className="dashboard-container">
            <h1>Traffic Monitoring Dashboard</h1>
            <PowerBIEmbed
                embedConfig={{
                    type: 'report',
                    id: embedConfig.reportId,
                    embedUrl: `https://app.powerbi.com/reportEmbed?reportId=${embedConfig.reportId}`,
                    accessToken: embedConfig.token,
                    tokenType: models.TokenType.Embed,
                    settings: {
                        filterPaneEnabled: true,
                        navContentPaneEnabled: true,
                        background: models.BackgroundType.Transparent
                    }
                }}
                cssClassName="report-container"
                getEmbeddedComponent={(embeddedReport) => {
                    // Handle component
                    embeddedReport.on('loaded', () => {
                        console.log('Report loaded');
                    });
                }}
            />
        </div>
    );
};
```

## Step 4: Row-Level Security (RLS)

Implement RLS to show users only their data:

### 4.1 Configure RLS in Power BI Desktop

1. Open your report in Power BI Desktop
2. **Modeling** > **Manage Roles**
3. Create a role (e.g., "CityUser"):
   ```dax
   [District] = USERNAME()
   ```
4. Save and publish

### 4.2 Generate Token with RLS

```csharp
public async Task<string> GenerateEmbedTokenWithRLSAsync(
    string reportId,
    string datasetId,
    string username,
    string[] roles)
{
    using var client = await CreateClientAsync();

    var generateTokenRequest = new GenerateTokenRequest(
        accessLevel: "View",
        datasetId: datasetId,
        identities: new List<EffectiveIdentity>
        {
            new EffectiveIdentity(
                username: username,
                roles: roles.ToList(),
                datasets: new List<string> { datasetId })
        });

    var embedToken = await client.Reports.GenerateTokenInGroupAsync(
        _options.WorkspaceId,
        reportId,
        generateTokenRequest);

    return embedToken.Token;
}
```

Usage:
```csharp
var token = await _datasetService.GenerateEmbedTokenWithRLSAsync(
    reportId,
    datasetId,
    username: "North District",
    roles: new[] { "CityUser" });
```

## Step 5: Mobile-Responsive Embedding

```javascript
// Detect mobile and adjust settings
const isMobile = window.innerWidth < 768;

const config = {
    type: 'report',
    tokenType: models.TokenType.Embed,
    accessToken: token,
    embedUrl: embedUrl,
    id: reportId,
    settings: {
        filterPaneEnabled: !isMobile, // Hide on mobile
        navContentPaneEnabled: !isMobile,
        layoutType: isMobile ? models.LayoutType.MobilePortrait : models.LayoutType.Master
    }
};

// Adjust container height for mobile
const reportContainer = document.getElementById('reportContainer');
reportContainer.style.height = isMobile ? '100vh' : '600px';
```

## Step 6: Security Best Practices

1. **Never expose embed tokens in client-side code**
   - Always fetch from backend API
   - Use short-lived tokens (1 hour max)

2. **Authenticate users**
   ```csharp
   [Authorize(Policy = "CanViewDashboards")]
   public async Task<IActionResult> GetEmbedToken(...)
   ```

3. **Validate permissions**
   ```csharp
   if (!User.HasClaim("dashboard", reportId))
   {
       return Forbid();
   }
   ```

4. **Use HTTPS only**
   ```csharp
   app.UseHttpsRedirection();
   app.UseHsts();
   ```

5. **Implement rate limiting**
   ```csharp
   builder.Services.AddRateLimiter(options =>
   {
       options.AddFixedWindowLimiter("embed", opt =>
       {
           opt.Window = TimeSpan.FromMinutes(1);
           opt.PermitLimit = 10;
       });
   });
   ```

## Troubleshooting

### Report fails to load

- Check browser console for errors
- Verify embed token is valid
- Ensure embed URL is correct
- Check CORS settings

### "Token has expired"

- Implement token refresh logic (example in Step 2.3)
- Generate new token before current expires

### Report shows "No data"

- Check RLS configuration
- Verify user has access to data
- Test without RLS first

## Resources

- [Power BI JavaScript SDK Docs](https://docs.microsoft.com/javascript/api/overview/powerbi/)
- [Embed Token Generation](https://docs.microsoft.com/power-bi/developer/embedded/embed-tokens)
- [Row-Level Security](https://docs.microsoft.com/power-bi/enterprise/service-admin-rls)
