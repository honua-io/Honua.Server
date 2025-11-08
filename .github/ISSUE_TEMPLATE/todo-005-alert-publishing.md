---
name: 'TODO-005: Implement Alert Publishing Logic'
about: Implement actual alert publishing using IAlertPublisher infrastructure
title: '[P1] Implement Alert Publishing Logic'
labels: ['priority: high', 'feature', 'alerts', 'todo-cleanup']
assignees: []
---

## Summary

The alert testing endpoint currently returns mock responses instead of actually publishing alerts through notification channels. Need to integrate with the existing `IAlertPublisher` infrastructure.

**Priority:** P1 - High (Core Feature)
**Effort:** Medium (3-5 days)
**Sprint Target:** Sprint 2

## Context

### Files Affected

- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/AlertAdministrationEndpoints.cs:361`

### Current Implementation

```csharp
// Line 361
public static async Task<IResult> TestAlertRule(
    string id,
    IAlertRuleStore alertRuleStore,
    ILogger<AlertAdministrationEndpoints> logger)
{
    var rule = await alertRuleStore.GetAlertRuleByIdAsync(id);

    if (rule == null)
    {
        return Results.Problem(
            title: "Alert rule not found",
            statusCode: StatusCodes.Status404NotFound,
            detail: $"Alert rule with ID '{id}' does not exist");
    }

    // TODO: Implement actual alert publishing logic
    // This would use the existing IAlertPublisher infrastructure
    logger.LogInformation("Testing alert rule {RuleId}: {RuleName}", id, rule.Name);

    var response = new TestAlertRuleResponse
    {
        Success = true,
        Message = $"Test alert would be sent to {rule.NotificationChannelIds.Count} channel(s)",
        PublishedChannels = rule.NotificationChannelIds.Select(cid => $"Channel {cid}").ToList(),
        FailedChannels = new List<string>()
    };

    return Results.Ok(response);
}
```

### Problem

- Test alerts are not actually sent - just mock responses
- Cannot verify notification channel configurations are working
- Users cannot test their alert setup end-to-end
- Missing integration with existing alert infrastructure

## Expected Behavior

### 1. Define IAlertPublisher Interface (if not exists)

```csharp
public interface IAlertPublisher
{
    /// <summary>
    /// Publishes an alert to configured notification channels.
    /// </summary>
    /// <param name="alert">The alert to publish.</param>
    /// <param name="channels">The notification channels to publish to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Publishing results for each channel.</returns>
    Task<AlertPublishResult> PublishAlertAsync(
        Alert alert,
        IEnumerable<NotificationChannel> channels,
        CancellationToken cancellationToken = default);
}

public class AlertPublishResult
{
    public bool Success { get; set; }
    public List<ChannelPublishResult> ChannelResults { get; set; } = new();
}

public class ChannelPublishResult
{
    public string ChannelId { get; set; }
    public string ChannelName { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
}
```

### 2. Implement Alert Publisher

```csharp
public class AlertPublisher : IAlertPublisher
{
    private readonly INotificationChannelStore _channelStore;
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly ILogger<AlertPublisher> _logger;

    public AlertPublisher(
        INotificationChannelStore channelStore,
        IEnumerable<INotificationProvider> providers,
        ILogger<AlertPublisher> logger)
    {
        _channelStore = channelStore;
        _providers = providers;
        _logger = logger;
    }

    public async Task<AlertPublishResult> PublishAlertAsync(
        Alert alert,
        IEnumerable<NotificationChannel> channels,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ChannelPublishResult>();

        foreach (var channel in channels)
        {
            var provider = _providers.FirstOrDefault(p => p.Type == channel.Type);

            if (provider == null)
            {
                _logger.LogWarning(
                    "No provider found for channel type {ChannelType}",
                    channel.Type);

                results.Add(new ChannelPublishResult
                {
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    Success = false,
                    ErrorMessage = $"No provider available for {channel.Type}",
                    PublishedAt = DateTimeOffset.UtcNow
                });
                continue;
            }

            try
            {
                await provider.SendNotificationAsync(alert, channel, cancellationToken);

                results.Add(new ChannelPublishResult
                {
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    Success = true,
                    PublishedAt = DateTimeOffset.UtcNow
                });

                _logger.LogInformation(
                    "Alert {AlertId} published to channel {ChannelName} ({ChannelType})",
                    alert.Id,
                    channel.Name,
                    channel.Type);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish alert {AlertId} to channel {ChannelName}",
                    alert.Id,
                    channel.Name);

                results.Add(new ChannelPublishResult
                {
                    ChannelId = channel.Id,
                    ChannelName = channel.Name,
                    Success = false,
                    ErrorMessage = ex.Message,
                    PublishedAt = DateTimeOffset.UtcNow
                });
            }
        }

        return new AlertPublishResult
        {
            Success = results.All(r => r.Success),
            ChannelResults = results
        };
    }
}
```

### 3. Update Test Alert Endpoint

```csharp
public static async Task<IResult> TestAlertRule(
    string id,
    IAlertRuleStore alertRuleStore,
    INotificationChannelStore channelStore,
    IAlertPublisher alertPublisher, // ✅ Inject alert publisher
    IUserIdentityService userIdentity,
    ILogger<AlertAdministrationEndpoints> logger)
{
    try
    {
        var rule = await alertRuleStore.GetAlertRuleByIdAsync(id);

        if (rule == null)
        {
            return Results.Problem(
                title: "Alert rule not found",
                statusCode: StatusCodes.Status404NotFound,
                detail: $"Alert rule with ID '{id}' does not exist");
        }

        // Get notification channels
        var channels = new List<NotificationChannel>();
        foreach (var channelId in rule.NotificationChannelIds)
        {
            var channel = await channelStore.GetNotificationChannelByIdAsync(channelId);
            if (channel != null)
            {
                channels.Add(channel);
            }
            else
            {
                logger.LogWarning(
                    "Notification channel {ChannelId} not found for alert rule {RuleId}",
                    channelId,
                    id);
            }
        }

        if (channels.Count == 0)
        {
            return Results.BadRequest(new
            {
                error = "no_channels",
                message = "No valid notification channels configured for this alert rule"
            });
        }

        // Create test alert
        var testAlert = new Alert
        {
            Id = Guid.NewGuid().ToString(),
            RuleId = rule.Id,
            RuleName = rule.Name,
            Severity = rule.Severity,
            Message = $"[TEST] This is a test alert from rule '{rule.Name}'",
            Timestamp = DateTime.UtcNow,
            Labels = new Dictionary<string, string>
            {
                ["test"] = "true",
                ["rule_id"] = rule.Id,
                ["triggered_by"] = userIdentity.GetCurrentUserName()
            }
        };

        // ✅ Publish alert to channels
        var publishResult = await alertPublisher.PublishAlertAsync(
            testAlert,
            channels,
            CancellationToken.None);

        logger.LogInformation(
            "Test alert published for rule {RuleId}: {SuccessCount}/{TotalCount} channels succeeded",
            id,
            publishResult.ChannelResults.Count(r => r.Success),
            publishResult.ChannelResults.Count);

        var response = new TestAlertRuleResponse
        {
            Success = publishResult.Success,
            Message = publishResult.Success
                ? $"Test alert sent to {channels.Count} channel(s)"
                : $"Test alert partially failed: {publishResult.ChannelResults.Count(r => !r.Success)} channel(s) failed",
            PublishedChannels = publishResult.ChannelResults
                .Where(r => r.Success)
                .Select(r => r.ChannelName)
                .ToList(),
            FailedChannels = publishResult.ChannelResults
                .Where(r => !r.Success)
                .Select(r => $"{r.ChannelName}: {r.ErrorMessage}")
                .ToList()
        };

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to test alert rule {RuleId}", id);
        return Results.Ok(new TestAlertRuleResponse
        {
            Success = false,
            Message = $"Test failed: {ex.Message}",
            PublishedChannels = new List<string>(),
            FailedChannels = new List<string> { ex.Message }
        });
    }
}
```

### 4. Register Services

```csharp
// ServiceCollectionExtensions.cs
services.AddScoped<IAlertPublisher, AlertPublisher>();

// Register notification providers
services.AddScoped<INotificationProvider, SlackNotificationProvider>();
services.AddScoped<INotificationProvider, EmailNotificationProvider>();
services.AddScoped<INotificationProvider, PagerDutyNotificationProvider>();
services.AddScoped<INotificationProvider, WebhookNotificationProvider>();
services.AddScoped<INotificationProvider, TeamsNotificationProvider>();
```

## Acceptance Criteria

- [ ] `IAlertPublisher` interface defined (or verify existing)
- [ ] `AlertPublisher` implementation created
- [ ] Test alert endpoint actually publishes alerts to notification channels
- [ ] Response includes success/failure status for each channel
- [ ] Failed channels include error messages
- [ ] Test alerts are marked with `test=true` label
- [ ] Alert publishing is logged with appropriate log levels
- [ ] Unit tests verify alert publisher logic
- [ ] Integration tests verify end-to-end alert publishing

## Testing Checklist

### Unit Tests

```csharp
[Fact]
public async Task PublishAlertAsync_WithValidChannels_PublishesToAllChannels()
{
    // Arrange
    var alert = CreateTestAlert();
    var channels = new[]
    {
        new NotificationChannel { Id = "1", Type = "slack", Name = "Slack" },
        new NotificationChannel { Id = "2", Type = "email", Name = "Email" }
    };

    var mockProvider = new Mock<INotificationProvider>();
    mockProvider.Setup(p => p.Type).Returns("slack");

    var publisher = new AlertPublisher(
        Mock.Of<INotificationChannelStore>(),
        new[] { mockProvider.Object },
        Mock.Of<ILogger<AlertPublisher>>());

    // Act
    var result = await publisher.PublishAlertAsync(alert, channels, CancellationToken.None);

    // Assert
    Assert.Contains(result.ChannelResults, r => r.ChannelId == "1" && r.Success);
    mockProvider.Verify(
        p => p.SendNotificationAsync(alert, It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()),
        Times.Once);
}

[Fact]
public async Task PublishAlertAsync_WithFailingChannel_RecordsFailure()
{
    // Arrange
    var alert = CreateTestAlert();
    var channel = new NotificationChannel { Id = "1", Type = "slack", Name = "Slack" };

    var mockProvider = new Mock<INotificationProvider>();
    mockProvider.Setup(p => p.Type).Returns("slack");
    mockProvider
        .Setup(p => p.SendNotificationAsync(It.IsAny<Alert>(), It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
        .ThrowsAsync(new Exception("Connection failed"));

    var publisher = new AlertPublisher(
        Mock.Of<INotificationChannelStore>(),
        new[] { mockProvider.Object },
        Mock.Of<ILogger<AlertPublisher>>());

    // Act
    var result = await publisher.PublishAlertAsync(alert, new[] { channel }, CancellationToken.None);

    // Assert
    Assert.False(result.Success);
    Assert.Contains(result.ChannelResults, r => r.ChannelId == "1" && !r.Success);
    Assert.Contains("Connection failed", result.ChannelResults.First().ErrorMessage);
}
```

### Integration Tests

```csharp
[Fact]
public async Task TestAlertRule_SendsActualAlert()
{
    // Arrange
    var client = _factory.CreateClientWithAdmin();

    // Create alert rule and notification channel
    var channel = await CreateSlackChannel("test-channel", webhookUrl: _testSlackWebhook);
    var rule = await CreateAlertRule("Test Rule", channelIds: new[] { channel.Id });

    // Act
    var response = await client.PostAsync($"/admin/alerts/rules/{rule.Id}/test", null);

    // Assert
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<TestAlertRuleResponse>();
    Assert.True(result.Success);
    Assert.Contains(channel.Name, result.PublishedChannels);

    // Verify alert was actually sent (check webhook was called)
    // This depends on your test infrastructure
}
```

## Related Files

- `/home/user/Honua.Server/src/Honua.Server.Host/Admin/AlertAdministrationEndpoints.cs:361`
- Alert infrastructure (verify existing `IAlertPublisher` or create new)
- Notification providers (Slack, Email, PagerDuty, etc.)

## Related Issues

- #TBD-006 - Implement Notification Channel Testing
- #TBD-007 - Enhance AlertHistoryStore with Full Filtering

## References

- [Alert Manager Documentation](https://prometheus.io/docs/alerting/latest/alertmanager/)
- [Notification Patterns](https://learn.microsoft.com/en-us/azure/architecture/patterns/publisher-subscriber)
