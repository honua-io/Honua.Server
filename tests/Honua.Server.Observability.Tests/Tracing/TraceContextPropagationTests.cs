using System.Diagnostics;
using FluentAssertions;
using Honua.Server.Observability.Tracing;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Honua.Server.Observability.Tests.Tracing;

/// <summary>
/// Tests for trace context propagation across service boundaries.
/// </summary>
public class TraceContextPropagationTests
{
    private static readonly ActivitySource TestActivitySource = new("Test.Source", "1.0.0");

    [Fact]
    public void ExtractTraceContext_WithValidTraceparent_ReturnsContext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["traceparent"] = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01";

        // Act
        var extracted = TraceContextPropagation.ExtractTraceContext(context.Request);

        // Assert
        extracted.Should().NotBe(default(ActivityContext));
        extracted.TraceId.ToString().Should().Be("0af7651916cd43dd8448eb211c80319c");
    }

    [Fact]
    public void ExtractTraceContext_WithoutTraceparent_ReturnsDefault()
    {
        // Arrange
        var context = new DefaultHttpContext();

        // Act
        var extracted = TraceContextPropagation.ExtractTraceContext(context.Request);

        // Assert
        extracted.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void ExtractTraceContext_WithInvalidTraceparent_ReturnsDefault()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["traceparent"] = "invalid-format";

        // Act
        var extracted = TraceContextPropagation.ExtractTraceContext(context.Request);

        // Assert
        extracted.Should().Be(default(ActivityContext));
    }

    [Fact]
    public void InjectTraceContext_WithActivity_AddsHeaders()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");
        var request = new HttpRequestMessage();

        // Act
        TraceContextPropagation.InjectTraceContext(request, activity);

        // Assert
        request.Headers.Should().ContainKey("traceparent");
        var traceparent = request.Headers.GetValues("traceparent").First();
        traceparent.Should().NotBeNullOrEmpty();
        traceparent.Should().StartWith("00-"); // W3C version
    }

    [Fact]
    public void InjectTraceContext_WithNullActivity_DoesNotThrow()
    {
        // Arrange
        var request = new HttpRequestMessage();

        // Act
        Action act = () => TraceContextPropagation.InjectTraceContext(request, null);

        // Assert
        act.Should().NotThrow();
        request.Headers.Should().NotContainKey("traceparent");
    }

    [Fact]
    public void InjectTraceContext_WithBaggage_PropagatesBaggage()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");
        activity?.SetBaggage("user.id", "12345");
        activity?.SetBaggage("tenant.id", "acme");

        var request = new HttpRequestMessage();

        // Act
        TraceContextPropagation.InjectTraceContext(request, activity);

        // Assert
        request.Headers.Should().ContainKey("baggage-user.id");
        request.Headers.Should().ContainKey("baggage-tenant.id");
        request.Headers.GetValues("baggage-user.id").First().Should().Be("12345");
        request.Headers.GetValues("baggage-tenant.id").First().Should().Be("acme");
    }

    [Fact]
    public void CorrelateActivity_WithCorrelationId_AddsTagAndBaggage()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");
        var correlationId = "test-correlation-123";

        // Act
        TraceContextPropagation.CorrelateActivity(correlationId, activity);

        // Assert
        activity?.GetTagItem("correlation.id").Should().Be(correlationId);
        activity?.GetBaggageItem("correlation.id").Should().Be(correlationId);
    }

    [Fact]
    public void GetCorrelationId_FromBaggage_ReturnsId()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");
        var correlationId = "test-correlation-456";
        activity?.SetBaggage("correlation.id", correlationId);

        // Act
        var retrieved = TraceContextPropagation.GetCorrelationId(activity);

        // Assert
        retrieved.Should().Be(correlationId);
    }

    [Fact]
    public void GetCorrelationId_FromTag_ReturnsId()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");
        var correlationId = "test-correlation-789";
        activity?.SetTag("correlation.id", correlationId);

        // Act
        var retrieved = TraceContextPropagation.GetCorrelationId(activity);

        // Assert
        retrieved.Should().Be(correlationId);
    }

    [Fact]
    public void GetCorrelationId_WithNullActivity_ReturnsNull()
    {
        // Act
        var retrieved = TraceContextPropagation.GetCorrelationId(null);

        // Assert
        retrieved.Should().BeNull();
    }

    [Fact]
    public void AddBaggage_AddsKeyValue()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");

        // Act
        TraceContextPropagation.AddBaggage("custom.key", "custom-value", activity);

        // Assert
        activity?.GetBaggageItem("custom.key").Should().Be("custom-value");
    }

    [Fact]
    public void GetBaggage_RetrievesValue()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");
        activity?.SetBaggage("test.key", "test-value");

        // Act
        var value = TraceContextPropagation.GetBaggage("test.key", activity);

        // Assert
        value.Should().Be("test-value");
    }

    [Fact]
    public void RecordEvent_AddsEventToActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");

        // Act
        TraceContextPropagation.RecordEvent("CacheHit", activity: activity);

        // Assert
        activity?.Events.Should().ContainSingle();
        activity?.Events.First().Name.Should().Be("CacheHit");
    }

    [Fact]
    public void RecordEvent_WithAttributes_AddsEventWithAttributes()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = TestActivitySource.StartActivity("TestActivity");

        var attributes = new[]
        {
            new KeyValuePair<string, object?>("cache.key", "user:123"),
            new KeyValuePair<string, object?>("cache.hit", true)
        };

        // Act
        TraceContextPropagation.RecordEvent("CacheOperation", attributes, activity);

        // Assert
        activity?.Events.Should().ContainSingle();
        var evt = activity?.Events.First();
        evt?.Name.Should().Be("CacheOperation");
        evt?.Tags.Should().Contain(new KeyValuePair<string, object?>("cache.key", "user:123"));
    }

    [Fact]
    public void PropagateBaggage_CopiesBaggageToTarget()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var source = TestActivitySource.StartActivity("SourceActivity");
        source?.SetBaggage("key1", "value1");
        source?.SetBaggage("key2", "value2");

        using var target = TestActivitySource.StartActivity("TargetActivity");

        // Act
        TraceContextPropagation.PropagateBaggage(source, target);

        // Assert
        target?.GetBaggageItem("key1").Should().Be("value1");
        target?.GetBaggageItem("key2").Should().Be("value2");
    }

    [Fact]
    public void StartActivityFromContext_CreatesChildActivity()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = TestActivitySource.StartActivity("ParentActivity");
        var parentContext = parent!.Context;

        // Act
        using var child = TraceContextPropagation.StartActivityFromContext(
            TestActivitySource,
            "ChildActivity",
            parentContext,
            ActivityKind.Internal);

        // Assert
        child.Should().NotBeNull();
        child!.ParentId.Should().Be(parent.Id);
    }

    [Fact]
    public void StartActivityFromContext_WithTags_AddsTags()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var parent = TestActivitySource.StartActivity("ParentActivity");
        var parentContext = parent!.Context;

        var tags = new[]
        {
            new KeyValuePair<string, object?>("operation", "test"),
            new KeyValuePair<string, object?>("user.id", "123")
        };

        // Act
        using var child = TraceContextPropagation.StartActivityFromContext(
            TestActivitySource,
            "ChildActivity",
            parentContext,
            tags: tags);

        // Assert
        child!.GetTagItem("operation").Should().Be("test");
        child.GetTagItem("user.id").Should().Be("123");
    }
}
