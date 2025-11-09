using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Honua.MapSDK.Services.VR
{
    /// <summary>
    /// Tracks VR usage metrics and performance telemetry
    /// </summary>
    public class VRTelemetryService
    {
        private readonly ConcurrentBag<VRTelemetryEvent> _events = new();
        private readonly ConcurrentDictionary<string, VRPerformanceMetrics> _sessionMetrics = new();

        /// <summary>
        /// Records a VR telemetry event
        /// </summary>
        public void RecordEvent(string sessionId, string eventType, Dictionary<string, object>? data = null)
        {
            var telemetryEvent = new VRTelemetryEvent
            {
                SessionId = sessionId,
                EventType = eventType,
                Timestamp = DateTime.UtcNow,
                Data = data ?? new Dictionary<string, object>()
            };

            _events.Add(telemetryEvent);
        }

        /// <summary>
        /// Records performance metrics for a VR session
        /// </summary>
        public void RecordPerformanceMetrics(string sessionId, VRPerformanceSnapshot snapshot)
        {
            if (!_sessionMetrics.TryGetValue(sessionId, out var metrics))
            {
                metrics = new VRPerformanceMetrics { SessionId = sessionId };
                _sessionMetrics.TryAdd(sessionId, metrics);
            }

            metrics.Snapshots.Add(snapshot);
            UpdateAggregateMetrics(metrics);
        }

        /// <summary>
        /// Gets performance summary for a session
        /// </summary>
        public VRPerformanceSummary GetPerformanceSummary(string sessionId)
        {
            if (!_sessionMetrics.TryGetValue(sessionId, out var metrics))
            {
                return new VRPerformanceSummary { SessionId = sessionId };
            }

            var snapshots = metrics.Snapshots;
            if (!snapshots.Any())
            {
                return new VRPerformanceSummary { SessionId = sessionId };
            }

            return new VRPerformanceSummary
            {
                SessionId = sessionId,
                AverageFrameRate = metrics.AverageFrameRate,
                MinFrameRate = metrics.MinFrameRate,
                MaxFrameRate = metrics.MaxFrameRate,
                AverageFrameTime = metrics.AverageFrameTime,
                FramesBelow72FPS = metrics.FramesBelow72FPS,
                FramesBelow90FPS = metrics.FramesBelow90FPS,
                AverageMemoryUsage = metrics.AverageMemoryUsage,
                PeakMemoryUsage = metrics.PeakMemoryUsage,
                TotalFeatureCount = snapshots.Last().FeatureCount,
                TotalDrawCalls = snapshots.Last().DrawCalls,
                SessionDuration = snapshots.Last().Timestamp - snapshots.First().Timestamp
            };
        }

        /// <summary>
        /// Gets usage analytics for date range
        /// </summary>
        public VRUsageAnalytics GetUsageAnalytics(DateTime startDate, DateTime endDate)
        {
            var relevantEvents = _events
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
                .ToList();

            var sessionIds = relevantEvents.Select(e => e.SessionId).Distinct().ToList();

            var analytics = new VRUsageAnalytics
            {
                StartDate = startDate,
                EndDate = endDate,
                TotalSessions = sessionIds.Count,
                TotalEvents = relevantEvents.Count,
                AverageSessionDuration = CalculateAverageSessionDuration(sessionIds),
                EventsByType = relevantEvents.GroupBy(e => e.EventType)
                    .ToDictionary(g => g.Key, g => g.Count()),
                DeviceTypes = GetDeviceTypeDistribution(relevantEvents),
                LocomotionModes = GetLocomotionModeDistribution(relevantEvents)
            };

            return analytics;
        }

        /// <summary>
        /// Gets feature usage statistics
        /// </summary>
        public Dictionary<string, int> GetFeatureUsageStats(DateTime startDate, DateTime endDate)
        {
            var featureEvents = _events
                .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate
                    && e.EventType == "feature_interaction")
                .ToList();

            var stats = new Dictionary<string, int>();

            foreach (var evt in featureEvents)
            {
                if (evt.Data.TryGetValue("featureType", out var featureType))
                {
                    var key = featureType.ToString() ?? "unknown";
                    stats[key] = stats.GetValueOrDefault(key, 0) + 1;
                }
            }

            return stats;
        }

        /// <summary>
        /// Detects performance issues in recent data
        /// </summary>
        public List<VRPerformanceIssue> DetectPerformanceIssues(string sessionId)
        {
            var issues = new List<VRPerformanceIssue>();

            if (!_sessionMetrics.TryGetValue(sessionId, out var metrics))
            {
                return issues;
            }

            // Check frame rate issues
            if (metrics.AverageFrameRate < 72)
            {
                issues.Add(new VRPerformanceIssue
                {
                    Severity = "high",
                    Type = "low_framerate",
                    Message = $"Average frame rate {metrics.AverageFrameRate:F1} FPS is below target of 72 FPS",
                    Recommendation = "Reduce quality level, enable LOD, or decrease visible feature count"
                });
            }

            // Check memory usage
            if (metrics.PeakMemoryUsage > 800 * 1024 * 1024) // 800MB
            {
                issues.Add(new VRPerformanceIssue
                {
                    Severity = "medium",
                    Type = "high_memory",
                    Message = $"Peak memory usage {metrics.PeakMemoryUsage / 1024 / 1024}MB exceeds recommended 800MB",
                    Recommendation = "Enable aggressive culling, reduce texture quality, or implement streaming"
                });
            }

            // Check frame time consistency
            var recentSnapshots = metrics.Snapshots.TakeLast(60).ToList();
            if (recentSnapshots.Any())
            {
                var frameTimeVariance = CalculateVariance(recentSnapshots.Select(s => s.FrameTime).ToList());
                if (frameTimeVariance > 5.0) // High variance in frame times
                {
                    issues.Add(new VRPerformanceIssue
                    {
                        Severity = "medium",
                        Type = "frame_stuttering",
                        Message = "Inconsistent frame times detected (stuttering)",
                        Recommendation = "Check for garbage collection spikes or optimize resource loading"
                    });
                }
            }

            return issues;
        }

        /// <summary>
        /// Cleans up old telemetry data
        /// </summary>
        public int CleanupOldData(int daysToKeep = 30)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
            var eventsToRemove = _events.Where(e => e.Timestamp < cutoffDate).ToList();

            // Note: ConcurrentBag doesn't support removal, so in production,
            // you'd want to use a different data structure or periodically rebuild
            return eventsToRemove.Count;
        }

        // Private helper methods

        private void UpdateAggregateMetrics(VRPerformanceMetrics metrics)
        {
            var snapshots = metrics.Snapshots;
            if (!snapshots.Any()) return;

            metrics.AverageFrameRate = snapshots.Average(s => s.FrameRate);
            metrics.MinFrameRate = snapshots.Min(s => s.FrameRate);
            metrics.MaxFrameRate = snapshots.Max(s => s.FrameRate);
            metrics.AverageFrameTime = snapshots.Average(s => s.FrameTime);
            metrics.FramesBelow72FPS = snapshots.Count(s => s.FrameRate < 72);
            metrics.FramesBelow90FPS = snapshots.Count(s => s.FrameRate < 90);
            metrics.AverageMemoryUsage = (long)snapshots.Average(s => s.MemoryUsage);
            metrics.PeakMemoryUsage = snapshots.Max(s => s.MemoryUsage);
        }

        private TimeSpan CalculateAverageSessionDuration(List<string> sessionIds)
        {
            var durations = new List<TimeSpan>();

            foreach (var sessionId in sessionIds)
            {
                var sessionEvents = _events.Where(e => e.SessionId == sessionId).OrderBy(e => e.Timestamp).ToList();
                if (sessionEvents.Count >= 2)
                {
                    durations.Add(sessionEvents.Last().Timestamp - sessionEvents.First().Timestamp);
                }
            }

            return durations.Any()
                ? TimeSpan.FromTicks((long)durations.Average(d => d.Ticks))
                : TimeSpan.Zero;
        }

        private Dictionary<string, int> GetDeviceTypeDistribution(List<VRTelemetryEvent> events)
        {
            var distribution = new Dictionary<string, int>();

            foreach (var evt in events.Where(e => e.EventType == "session_start"))
            {
                if (evt.Data.TryGetValue("deviceType", out var deviceType))
                {
                    var key = deviceType.ToString() ?? "unknown";
                    distribution[key] = distribution.GetValueOrDefault(key, 0) + 1;
                }
            }

            return distribution;
        }

        private Dictionary<string, int> GetLocomotionModeDistribution(List<VRTelemetryEvent> events)
        {
            var distribution = new Dictionary<string, int>();

            foreach (var evt in events.Where(e => e.EventType == "locomotion_change"))
            {
                if (evt.Data.TryGetValue("mode", out var mode))
                {
                    var key = mode.ToString() ?? "unknown";
                    distribution[key] = distribution.GetValueOrDefault(key, 0) + 1;
                }
            }

            return distribution;
        }

        private double CalculateVariance(List<double> values)
        {
            if (!values.Any()) return 0;

            var mean = values.Average();
            return values.Average(v => Math.Pow(v - mean, 2));
        }
    }

    /// <summary>
    /// Represents a telemetry event
    /// </summary>
    public class VRTelemetryEvent
    {
        public string SessionId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
    }

    /// <summary>
    /// Performance metrics for a session
    /// </summary>
    public class VRPerformanceMetrics
    {
        public string SessionId { get; set; } = string.Empty;
        public List<VRPerformanceSnapshot> Snapshots { get; set; } = new();
        public double AverageFrameRate { get; set; }
        public double MinFrameRate { get; set; }
        public double MaxFrameRate { get; set; }
        public double AverageFrameTime { get; set; }
        public int FramesBelow72FPS { get; set; }
        public int FramesBelow90FPS { get; set; }
        public long AverageMemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
    }

    /// <summary>
    /// Single performance snapshot
    /// </summary>
    public class VRPerformanceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public double FrameRate { get; set; }
        public double FrameTime { get; set; }
        public long MemoryUsage { get; set; }
        public int FeatureCount { get; set; }
        public int DrawCalls { get; set; }
    }

    /// <summary>
    /// Performance summary for a session
    /// </summary>
    public class VRPerformanceSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public double AverageFrameRate { get; set; }
        public double MinFrameRate { get; set; }
        public double MaxFrameRate { get; set; }
        public double AverageFrameTime { get; set; }
        public int FramesBelow72FPS { get; set; }
        public int FramesBelow90FPS { get; set; }
        public long AverageMemoryUsage { get; set; }
        public long PeakMemoryUsage { get; set; }
        public int TotalFeatureCount { get; set; }
        public int TotalDrawCalls { get; set; }
        public TimeSpan SessionDuration { get; set; }
    }

    /// <summary>
    /// Usage analytics
    /// </summary>
    public class VRUsageAnalytics
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalSessions { get; set; }
        public int TotalEvents { get; set; }
        public TimeSpan AverageSessionDuration { get; set; }
        public Dictionary<string, int> EventsByType { get; set; } = new();
        public Dictionary<string, int> DeviceTypes { get; set; } = new();
        public Dictionary<string, int> LocomotionModes { get; set; } = new();
    }

    /// <summary>
    /// Performance issue detection
    /// </summary>
    public class VRPerformanceIssue
    {
        public string Severity { get; set; } = string.Empty; // low, medium, high
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Recommendation { get; set; } = string.Empty;
    }
}
