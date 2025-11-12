// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Honua.MapSDK.Services.VR
{
    /// <summary>
    /// Manages VR session state and user preferences for WebXR sessions
    /// </summary>
    public class VRSessionService
    {
        private readonly ConcurrentDictionary<string, VRSession> _sessions = new();

        /// <summary>
        /// Creates a new VR session with the specified configuration
        /// </summary>
        public VRSession CreateSession(string sessionId, VRSessionConfig config)
        {
            var session = new VRSession
            {
                SessionId = sessionId,
                Config = config,
                StartTime = DateTime.UtcNow,
                IsActive = true,
                Features = DetermineAvailableFeatures(config)
            };

            _sessions.TryAdd(sessionId, session);
            return session;
        }

        /// <summary>
        /// Updates VR session preferences
        /// </summary>
        public bool UpdateSessionPreferences(string sessionId, VRPreferences preferences)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.Preferences = preferences;
                session.LastUpdateTime = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets session by ID
        /// </summary>
        public VRSession? GetSession(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>
        /// Ends a VR session
        /// </summary>
        public bool EndSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.IsActive = false;
                session.EndTime = DateTime.UtcNow;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets all active sessions
        /// </summary>
        public IEnumerable<VRSession> GetActiveSessions()
        {
            return _sessions.Values.Where(s => s.IsActive);
        }

        /// <summary>
        /// Determines available VR features based on device capabilities
        /// </summary>
        private List<string> DetermineAvailableFeatures(VRSessionConfig config)
        {
            var features = new List<string>();

            if (config.ReferenceSpace == "local-floor" || config.ReferenceSpace == "bounded-floor")
            {
                features.Add("floor-tracking");
            }

            if (config.EnableHandTracking)
            {
                features.Add("hand-tracking");
            }

            if (config.EnableHitTest)
            {
                features.Add("hit-testing");
            }

            features.Add("6dof-tracking");
            features.Add("stereoscopic-rendering");

            return features;
        }

        /// <summary>
        /// Cleans up expired sessions (older than 24 hours)
        /// </summary>
        public int CleanupExpiredSessions()
        {
            var expiredTime = DateTime.UtcNow.AddHours(-24);
            var expiredSessions = _sessions
                .Where(kvp => kvp.Value.EndTime.HasValue && kvp.Value.EndTime.Value < expiredTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in expiredSessions)
            {
                _sessions.TryRemove(sessionId, out _);
            }

            return expiredSessions.Count;
        }
    }

    /// <summary>
    /// Represents a VR session
    /// </summary>
    public class VRSession
    {
        public string SessionId { get; set; } = string.Empty;
        public VRSessionConfig Config { get; set; } = new();
        public VRPreferences Preferences { get; set; } = new();
        public List<string> Features { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// VR session configuration
    /// </summary>
    public class VRSessionConfig
    {
        public string SessionMode { get; set; } = "immersive-vr"; // or "immersive-ar"
        public string ReferenceSpace { get; set; } = "local-floor"; // or "bounded-floor", "unbounded"
        public bool EnableHandTracking { get; set; } = true;
        public bool EnableHitTest { get; set; } = false;
        public int TargetFrameRate { get; set; } = 72; // 72Hz for Quest 2, 90Hz for Quest 3
        public string DeviceType { get; set; } = "quest"; // quest, vive, index, etc.
    }

    /// <summary>
    /// User preferences for VR experience
    /// </summary>
    public class VRPreferences
    {
        // Comfort settings
        public bool EnableVignette { get; set; } = false;
        public bool EnableSnapTurning { get; set; } = true;
        public float TurnAngle { get; set; } = 45f; // degrees for snap turn

        // Locomotion
        public string LocomotionMode { get; set; } = "teleport"; // teleport, smooth, grab-move
        public float MovementSpeed { get; set; } = 2.0f; // m/s

        // Visual settings
        public float Scale { get; set; } = 100.0f; // 1:100 scale default
        public bool ShowMinimap { get; set; } = true;
        public bool ShowGrid { get; set; } = false;

        // Performance
        public string QualityLevel { get; set; } = "medium"; // low, medium, high
        public bool EnableShadows { get; set; } = false;
        public bool EnableLighting { get; set; } = true;

        // Interaction
        public bool EnableHaptics { get; set; } = true;
        public float HapticIntensity { get; set; } = 0.5f;
    }
}
