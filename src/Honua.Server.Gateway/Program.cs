// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Net;
using System.Threading.RateLimiting;
using Honua.Server.Core.BlueGreen;
using Honua.Server.Gateway.Configuration;
using Honua.Server.Gateway.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddEnvironmentVariables()
        .Build())
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .CreateLogger();

try
{
    Log.Information("Starting Honua YARP API Gateway");

    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog();

    // Add configuration sources
    builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables()
        .AddCommandLine(args);

    // Add OpenTelemetry
    var serviceName = builder.Configuration.GetValue<string>("ServiceName") ?? "honua-gateway";
    var serviceVersion = builder.Configuration.GetValue<string>("ServiceVersion") ?? "1.0.0";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter())
        .WithTracing(tracing => tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddSource(serviceName));

    // Configure OTLP exporter if endpoint is provided
    var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint");
    if (!string.IsNullOrEmpty(otlpEndpoint))
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(otlpEndpoint);
            }));
    }

    // Add Health Checks
    var healthChecks = builder.Services.AddHealthChecks();

    // Add Redis health check if configured
    var redisConnection = builder.Configuration.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrEmpty(redisConnection))
    {
        healthChecks.AddRedis(redisConnection, name: "redis", tags: new[] { "cache", "ready" });

        // Add Redis caching for distributed rate limiting
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "Honua.Gateway:";
        });
    }

    // Add health checks for backend services
    var backendServices = builder.Configuration.GetSection("BackendServices").Get<Dictionary<string, string>>() ?? new();
    foreach (var (name, url) in backendServices)
    {
        healthChecks.AddUrlGroup(new Uri($"{url}/health"), name: $"backend-{name}", tags: new[] { "backend", "ready" });
    }

    // Add Rate Limiting
    // Store rate limit configuration for use in middleware
    var globalPermitLimit = builder.Configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 1000);
    var globalWindowSeconds = builder.Configuration.GetValue<int>("RateLimiting:GlobalWindowSeconds", 60);
    var perIpPermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerIpPermitLimit", 100);
    var perIpWindowSeconds = builder.Configuration.GetValue<int>("RateLimiting:PerIpWindowSeconds", 60);
    var exposeHeaders = builder.Configuration.GetValue<bool>("RateLimiting:ExposeHeaders", true);

    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
            RateLimitPartition.GetFixedWindowLimiter("global", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = globalPermitLimit,
                Window = TimeSpan.FromSeconds(globalWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

        options.AddPolicy<string>("per-ip", httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = perIpPermitLimit,
                Window = TimeSpan.FromSeconds(perIpWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

            // Add rate limit headers on rejection
            if (exposeHeaders)
            {
                // Use the more restrictive limit (per-IP)
                context.HttpContext.Response.Headers["X-RateLimit-Limit"] = perIpPermitLimit.ToString();
                context.HttpContext.Response.Headers["X-RateLimit-Remaining"] = "0";
                context.HttpContext.Response.Headers["X-RateLimit-Reset"] =
                    DateTimeOffset.UtcNow.AddSeconds(perIpWindowSeconds).ToUnixTimeSeconds().ToString();
            }

            context.HttpContext.Response.Headers["Retry-After"] = perIpWindowSeconds.ToString();

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = perIpWindowSeconds
            }, cancellationToken);

            Log.Warning("Rate limit exceeded for {IpAddress} on {Path}",
                context.HttpContext.Connection.RemoteIpAddress,
                context.HttpContext.Request.Path);
        };
    });

    // Configure YARP Reverse Proxy with InMemoryConfigProvider
    // This loads the initial configuration from appsettings.json but allows
    // dynamic updates at runtime for blue-green deployments and traffic management
    var inMemoryConfigProvider = YarpConfigurationExtensions.LoadFromConfiguration(
        builder.Configuration.GetSection("ReverseProxy"));

    builder.Services.AddSingleton<IProxyConfigProvider>(inMemoryConfigProvider);

    builder.Services.AddReverseProxy()
        .LoadFromMemory(inMemoryConfigProvider.GetConfig().Routes, inMemoryConfigProvider.GetConfig().Clusters)
        .AddTransforms(builderContext =>
        {
            // Add security headers
            builderContext.AddResponseHeader("X-Content-Type-Options", "nosniff");
            builderContext.AddResponseHeader("X-Frame-Options", "SAMEORIGIN");
            builderContext.AddResponseHeader("X-XSS-Protection", "1; mode=block");
            builderContext.AddResponseHeader("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            builderContext.AddResponseHeader("Referrer-Policy", "strict-origin-when-cross-origin");

            // Add X-Forwarded-* headers
            builderContext.AddXForwarded();

            // Add custom header to identify gateway
            builderContext.AddResponseHeader("X-Gateway", "Honua-YARP");

            // Remove sensitive server headers
            builderContext.AddResponseHeader("Server", "", append: false);

            // Add request ID for tracing
            builderContext.AddRequestTransform(async context =>
            {
                if (!context.HttpContext.Request.Headers.ContainsKey("X-Request-ID"))
                {
                    context.HttpContext.Request.Headers["X-Request-ID"] = Guid.NewGuid().ToString();
                }
                await ValueTask.CompletedTask;
            });

            // Log requests
            builderContext.AddRequestTransform(async context =>
            {
                Log.Information("Proxying request {Method} {Path} to {Cluster}",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    builderContext.Cluster?.ClusterId ?? "unknown");
                await ValueTask.CompletedTask;
            });
        });

    // Register BlueGreenTrafficManager with the InMemoryConfigProvider
    // This enables dynamic traffic switching without restarting the gateway
    builder.Services.AddSingleton(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<BlueGreenTrafficManager>>();
        var configProvider = sp.GetRequiredService<IProxyConfigProvider>();
        return new BlueGreenTrafficManager(logger, configProvider);
    });

    // Add HttpClient factory for health checks
    builder.Services.AddHttpClient();

    // Add Authentication and Authorization for traffic management endpoints
    // This uses JWT bearer tokens to protect admin endpoints
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var jwtSettings = builder.Configuration.GetSection("Authentication:Jwt");
            options.Authority = jwtSettings["Authority"];
            options.Audience = jwtSettings["Audience"];
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

            // For development/testing with API keys
            if (builder.Environment.IsDevelopment())
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Allow API key authentication for testing
                        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey))
                        {
                            var configuredApiKey = builder.Configuration["Authentication:ApiKey"];
                            if (!string.IsNullOrEmpty(configuredApiKey) && apiKey == configuredApiKey)
                            {
                                // API key is valid, skip token validation
                                context.Success();
                            }
                        }
                        return Task.CompletedTask;
                    }
                };
            }
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminPolicy", policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole("Admin", "TrafficManager");
        });
    });

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins);
            }
            else
            {
                policy.AllowAnyOrigin();
            }

            policy.AllowAnyMethod()
                  .AllowAnyHeader()
                  .WithExposedHeaders(
                      "X-Request-ID",
                      "X-Correlation-ID",
                      "X-RateLimit-Limit",
                      "X-RateLimit-Remaining",
                      "X-RateLimit-Reset",
                      "Retry-After");
        });
    });

    var app = builder.Build();

    // Configure request pipeline

    // Use HTTPS redirection in production
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Add custom middleware for additional security
    app.Use(async (context, next) =>
    {
        // Remove sensitive headers from requests before proxying
        context.Request.Headers.Remove("X-Powered-By");
        context.Request.Headers.Remove("Server");

        await next();
    });

    // CORS
    app.UseCors();

    // Authentication and Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // Rate limiting
    app.UseRateLimiter();

    // Rate limit headers middleware
    // This middleware runs AFTER rate limiting and adds X-RateLimit-* headers to ALL responses
    // to provide clients with quota information for implementing intelligent retry logic
    if (exposeHeaders)
    {
        app.Use(async (context, next) =>
        {
            // Store the original response body stream
            var originalBodyStream = context.Response.Body;

            try
            {
                // Execute the rest of the pipeline
                await next();

                // After rate limiting has executed, add headers
                // Note: We can't access the actual lease statistics from FixedWindowRateLimiter
                // through the public API, so we provide the configured limits
                // The actual "remaining" count would require custom rate limiter implementation

                // Determine which policy is active for this request
                var endpoint = context.GetEndpoint();
                var rateLimitPolicy = endpoint?.Metadata.GetMetadata<IRateLimiterPolicy<string>>();

                // Default to per-IP limits (more restrictive)
                var limit = perIpPermitLimit;
                var windowSeconds = perIpWindowSeconds;

                // Calculate reset time (start of next window)
                // For fixed window, reset occurs at fixed intervals
                var now = DateTimeOffset.UtcNow;
                var windowStart = new DateTimeOffset(
                    now.Year, now.Month, now.Day, now.Hour, now.Minute,
                    (now.Second / windowSeconds) * windowSeconds, 0, TimeSpan.Zero);
                var resetTime = windowStart.AddSeconds(windowSeconds);

                // Add headers if not already present (OnRejected handler may have set them)
                if (!context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
                {
                    context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
                }

                if (!context.Response.Headers.ContainsKey("X-RateLimit-Reset"))
                {
                    context.Response.Headers["X-RateLimit-Reset"] = resetTime.ToUnixTimeSeconds().ToString();
                }

                // Note: We set a placeholder for "Remaining" since we cannot access the actual
                // lease statistics from the rate limiter without a custom implementation
                // In a production system, you would:
                // 1. Use a custom rate limiter that exposes statistics
                // 2. Store counters in Redis/distributed cache
                // 3. Use response headers from the rate limiter's lease metadata
                if (!context.Response.Headers.ContainsKey("X-RateLimit-Remaining"))
                {
                    // For now, indicate available unless explicitly rejected
                    context.Response.Headers["X-RateLimit-Remaining"] =
                        context.Response.StatusCode == 429 ? "0" : limit.ToString();
                }
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        });
    }

    // Health checks
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => true
    });

    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Prometheus metrics endpoint
    app.MapPrometheusScrapingEndpoint("/metrics");

    // Traffic management endpoints for blue-green deployments
    // These endpoints allow dynamic traffic switching without restarting the gateway
    app.MapTrafficManagementEndpoints();

    // Map reverse proxy
    app.MapReverseProxy();

    // Log startup configuration
    Log.Information("Honua YARP API Gateway started successfully");
    Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
    Log.Information("Rate Limiting - Global: {GlobalLimit} per {GlobalWindow}s, Per IP: {IpLimit} per {IpWindow}s",
        builder.Configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 1000),
        builder.Configuration.GetValue<int>("RateLimiting:GlobalWindowSeconds", 60),
        builder.Configuration.GetValue<int>("RateLimiting:PerIpPermitLimit", 100),
        builder.Configuration.GetValue<int>("RateLimiting:PerIpWindowSeconds", 60));

    // Log traffic management configuration
    var trafficMgmtEnabled = builder.Configuration.GetValue<bool>("TrafficManagement:Enabled", true);
    Log.Information("Traffic Management - Enabled: {Enabled}", trafficMgmtEnabled);
    if (trafficMgmtEnabled)
    {
        Log.Information("Dynamic configuration provider: InMemoryConfigProvider (blue-green deployments enabled)");
        Log.Information("Loaded {RouteCount} routes and {ClusterCount} clusters from configuration",
            inMemoryConfigProvider.GetConfig().Routes.Count,
            inMemoryConfigProvider.GetConfig().Clusters.Count);
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Honua YARP API Gateway terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
