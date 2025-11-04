// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Net;
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
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = RateLimitPartition.GetFixedWindowLimiter("global", _ => new()
        {
            PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:GlobalPermitLimit", 1000),
            Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimiting:GlobalWindowSeconds", 60)),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 10
        });

        // Add per-IP rate limiting
        options.AddPolicy("per-ip", httpContext =>
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new()
            {
                PermitLimit = builder.Configuration.GetValue<int>("RateLimiting:PerIpPermitLimit", 100),
                Window = TimeSpan.FromSeconds(builder.Configuration.GetValue<int>("RateLimiting:PerIpWindowSeconds", 60)),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.Headers["Retry-After"] = "60";

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                message = "Rate limit exceeded. Please try again later.",
                retryAfter = 60
            }, cancellationToken);

            Log.Warning("Rate limit exceeded for {IpAddress} on {Path}",
                context.HttpContext.Connection.RemoteIpAddress,
                context.HttpContext.Request.Path);
        };
    });

    // Configure YARP Reverse Proxy
    builder.Services.AddReverseProxy()
        .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
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
                  .WithExposedHeaders("X-Request-ID", "X-Correlation-ID");
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

    // Rate limiting
    app.UseRateLimiter();

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
