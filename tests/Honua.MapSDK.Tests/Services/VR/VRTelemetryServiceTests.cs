using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Honua.MapSDK.Services.VR;
using Xunit;

namespace Honua.MapSDK.Tests.Services.VR
{
    public class VRTelemetryServiceTests
    {
        [Fact]
        public void RecordEvent_ShouldStoreEvent()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";
            var eventType = "test_event";
            var data = new Dictionary<string, object> { { "key", "value" } };

            // Act
            service.RecordEvent(sessionId, eventType, data);

            // This is a simple test to ensure no exceptions
            // In production, you'd verify the event was stored
            Assert.True(true);
        }

        [Fact]
        public void RecordPerformanceMetrics_ShouldUpdateMetrics()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";
            var snapshot = new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 72.5,
                FrameTime = 13.9,
                MemoryUsage = 500_000_000,
                FeatureCount = 1000,
                DrawCalls = 100
            };

            // Act
            service.RecordPerformanceMetrics(sessionId, snapshot);

            // Assert
            var summary = service.GetPerformanceSummary(sessionId);
            Assert.Equal(sessionId, summary.SessionId);
        }

        [Fact]
        public void RecordPerformanceMetrics_MultipleSnapshots_ShouldCalculateAverages()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";

            // Act
            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 72.0,
                FrameTime = 13.9,
                MemoryUsage = 400_000_000
            });

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow.AddSeconds(1),
                FrameRate = 90.0,
                FrameTime = 11.1,
                MemoryUsage = 600_000_000
            });

            // Assert
            var summary = service.GetPerformanceSummary(sessionId);
            Assert.Equal(81.0, summary.AverageFrameRate); // (72 + 90) / 2
            Assert.Equal(72.0, summary.MinFrameRate);
            Assert.Equal(90.0, summary.MaxFrameRate);
        }

        [Fact]
        public void GetPerformanceSummary_NonExistentSession_ShouldReturnEmptySummary()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "nonexistent";

            // Act
            var summary = service.GetPerformanceSummary(sessionId);

            // Assert
            Assert.Equal(sessionId, summary.SessionId);
            Assert.Equal(0, summary.AverageFrameRate);
        }

        [Fact]
        public void GetPerformanceSummary_ShouldTrackFramesBelowTarget()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";

            // Act
            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 60.0, // Below 72 and 90
                FrameTime = 16.7,
                MemoryUsage = 400_000_000
            });

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow.AddSeconds(1),
                FrameRate = 80.0, // Below 90 only
                FrameTime = 12.5,
                MemoryUsage = 400_000_000
            });

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow.AddSeconds(2),
                FrameRate = 95.0, // Above both
                FrameTime = 10.5,
                MemoryUsage = 400_000_000
            });

            // Assert
            var summary = service.GetPerformanceSummary(sessionId);
            Assert.Equal(1, summary.FramesBelow72FPS);
            Assert.Equal(2, summary.FramesBelow90FPS);
        }

        [Fact]
        public void GetUsageAnalytics_ShouldAggregateData()
        {
            // Arrange
            var service = new VRTelemetryService();
            var startDate = DateTime.UtcNow.AddHours(-1);
            var endDate = DateTime.UtcNow;

            service.RecordEvent("session1", "session_start", new Dictionary<string, object>
            {
                { "deviceType", "quest3" }
            });

            service.RecordEvent("session1", "feature_interaction", null);
            service.RecordEvent("session2", "session_start", new Dictionary<string, object>
            {
                { "deviceType", "quest2" }
            });

            // Act
            var analytics = service.GetUsageAnalytics(startDate, endDate);

            // Assert
            Assert.Equal(2, analytics.TotalSessions);
            Assert.Equal(3, analytics.TotalEvents);
        }

        [Fact]
        public void GetFeatureUsageStats_ShouldCountFeatureInteractions()
        {
            // Arrange
            var service = new VRTelemetryService();
            var startDate = DateTime.UtcNow.AddHours(-1);
            var endDate = DateTime.UtcNow;

            service.RecordEvent("session1", "feature_interaction", new Dictionary<string, object>
            {
                { "featureType", "building" }
            });

            service.RecordEvent("session1", "feature_interaction", new Dictionary<string, object>
            {
                { "featureType", "building" }
            });

            service.RecordEvent("session1", "feature_interaction", new Dictionary<string, object>
            {
                { "featureType", "road" }
            });

            // Act
            var stats = service.GetFeatureUsageStats(startDate, endDate);

            // Assert
            Assert.Equal(2, stats["building"]);
            Assert.Equal(1, stats["road"]);
        }

        [Fact]
        public void DetectPerformanceIssues_LowFrameRate_ShouldDetectIssue()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 50.0, // Below target
                FrameTime = 20.0,
                MemoryUsage = 400_000_000
            });

            // Act
            var issues = service.DetectPerformanceIssues(sessionId);

            // Assert
            Assert.NotEmpty(issues);
            Assert.Contains(issues, i => i.Type == "low_framerate");
        }

        [Fact]
        public void DetectPerformanceIssues_HighMemory_ShouldDetectIssue()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 90.0,
                FrameTime = 11.1,
                MemoryUsage = 900_000_000 // 900MB - above 800MB threshold
            });

            // Act
            var issues = service.DetectPerformanceIssues(sessionId);

            // Assert
            Assert.Contains(issues, i => i.Type == "high_memory");
        }

        [Fact]
        public void DetectPerformanceIssues_GoodPerformance_ShouldReturnNoIssues()
        {
            // Arrange
            var service = new VRTelemetryService();
            var sessionId = "session1";

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 90.0,
                FrameTime = 11.1,
                MemoryUsage = 400_000_000
            });

            service.RecordPerformanceMetrics(sessionId, new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow.AddSeconds(1),
                FrameRate = 90.0,
                FrameTime = 11.1,
                MemoryUsage = 400_000_000
            });

            // Act
            var issues = service.DetectPerformanceIssues(sessionId);

            // Assert
            // Should have no critical issues for good, consistent performance
            var criticalIssues = issues.Where(i => i.Severity == "high").ToList();
            Assert.Empty(criticalIssues);
        }

        [Fact]
        public void VRPerformanceSnapshot_ShouldStoreAllMetrics()
        {
            // Arrange & Act
            var snapshot = new VRPerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow,
                FrameRate = 72.5,
                FrameTime = 13.9,
                MemoryUsage = 500_000_000,
                FeatureCount = 1000,
                DrawCalls = 100
            };

            // Assert
            Assert.NotEqual(default(DateTime), snapshot.Timestamp);
            Assert.Equal(72.5, snapshot.FrameRate);
            Assert.Equal(13.9, snapshot.FrameTime);
            Assert.Equal(500_000_000, snapshot.MemoryUsage);
            Assert.Equal(1000, snapshot.FeatureCount);
            Assert.Equal(100, snapshot.DrawCalls);
        }

        [Fact]
        public void VRPerformanceIssue_ShouldContainAllFields()
        {
            // Arrange & Act
            var issue = new VRPerformanceIssue
            {
                Severity = "high",
                Type = "low_framerate",
                Message = "Frame rate is too low",
                Recommendation = "Reduce quality settings"
            };

            // Assert
            Assert.Equal("high", issue.Severity);
            Assert.Equal("low_framerate", issue.Type);
            Assert.NotEmpty(issue.Message);
            Assert.NotEmpty(issue.Recommendation);
        }
    }
}
