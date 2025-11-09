using System;
using System.Linq;
using Honua.MapSDK.Services.VR;
using Xunit;

namespace Honua.MapSDK.Tests.Services.VR
{
    public class VRSessionServiceTests
    {
        [Fact]
        public void CreateSession_ShouldCreateNewSession()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var config = new VRSessionConfig
            {
                SessionMode = "immersive-vr",
                ReferenceSpace = "local-floor",
                EnableHandTracking = true,
                TargetFrameRate = 72
            };

            // Act
            var session = service.CreateSession(sessionId, config);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(sessionId, session.SessionId);
            Assert.Equal(config.SessionMode, session.Config.SessionMode);
            Assert.True(session.IsActive);
            Assert.NotEmpty(session.Features);
        }

        [Fact]
        public void CreateSession_WithFloorTracking_ShouldIncludeFloorFeature()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var config = new VRSessionConfig
            {
                ReferenceSpace = "local-floor"
            };

            // Act
            var session = service.CreateSession(sessionId, config);

            // Assert
            Assert.Contains("floor-tracking", session.Features);
        }

        [Fact]
        public void CreateSession_WithHandTracking_ShouldIncludeHandTrackingFeature()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var config = new VRSessionConfig
            {
                EnableHandTracking = true
            };

            // Act
            var session = service.CreateSession(sessionId, config);

            // Assert
            Assert.Contains("hand-tracking", session.Features);
        }

        [Fact]
        public void GetSession_ShouldReturnExistingSession()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var config = new VRSessionConfig();
            service.CreateSession(sessionId, config);

            // Act
            var session = service.GetSession(sessionId);

            // Assert
            Assert.NotNull(session);
            Assert.Equal(sessionId, session.SessionId);
        }

        [Fact]
        public void GetSession_NonExistentSession_ShouldReturnNull()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var session = service.GetSession(sessionId);

            // Assert
            Assert.Null(session);
        }

        [Fact]
        public void UpdateSessionPreferences_ShouldUpdatePreferences()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var config = new VRSessionConfig();
            service.CreateSession(sessionId, config);

            var preferences = new VRPreferences
            {
                LocomotionMode = "smooth",
                Scale = 200.0f,
                QualityLevel = "high",
                EnableSnapTurning = false
            };

            // Act
            var result = service.UpdateSessionPreferences(sessionId, preferences);

            // Assert
            Assert.True(result);
            var session = service.GetSession(sessionId);
            Assert.Equal("smooth", session?.Preferences.LocomotionMode);
            Assert.Equal(200.0f, session?.Preferences.Scale);
        }

        [Fact]
        public void UpdateSessionPreferences_NonExistentSession_ShouldReturnFalse()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var preferences = new VRPreferences();

            // Act
            var result = service.UpdateSessionPreferences(sessionId, preferences);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void EndSession_ShouldMarkSessionInactive()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();
            var config = new VRSessionConfig();
            service.CreateSession(sessionId, config);

            // Act
            var result = service.EndSession(sessionId);

            // Assert
            Assert.True(result);
            var session = service.GetSession(sessionId);
            Assert.False(session?.IsActive);
            Assert.NotNull(session?.EndTime);
        }

        [Fact]
        public void EndSession_NonExistentSession_ShouldReturnFalse()
        {
            // Arrange
            var service = new VRSessionService();
            var sessionId = Guid.NewGuid().ToString();

            // Act
            var result = service.EndSession(sessionId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetActiveSessions_ShouldReturnOnlyActiveSessions()
        {
            // Arrange
            var service = new VRSessionService();
            var config = new VRSessionConfig();

            var session1 = service.CreateSession("session1", config);
            var session2 = service.CreateSession("session2", config);
            var session3 = service.CreateSession("session3", config);

            service.EndSession("session2");

            // Act
            var activeSessions = service.GetActiveSessions().ToList();

            // Assert
            Assert.Equal(2, activeSessions.Count);
            Assert.Contains(activeSessions, s => s.SessionId == "session1");
            Assert.Contains(activeSessions, s => s.SessionId == "session3");
            Assert.DoesNotContain(activeSessions, s => s.SessionId == "session2");
        }

        [Fact]
        public void CleanupExpiredSessions_ShouldRemoveOldSessions()
        {
            // Arrange
            var service = new VRSessionService();
            var config = new VRSessionConfig();

            var session1 = service.CreateSession("session1", config);
            service.EndSession("session1");

            // Force end time to be old
            session1.EndTime = DateTime.UtcNow.AddHours(-25);

            // Act
            var removedCount = service.CleanupExpiredSessions();

            // Assert
            Assert.Equal(1, removedCount);
            Assert.Null(service.GetSession("session1"));
        }

        [Fact]
        public void VRPreferences_DefaultValues_ShouldBeSet()
        {
            // Arrange & Act
            var preferences = new VRPreferences();

            // Assert
            Assert.False(preferences.EnableVignette);
            Assert.True(preferences.EnableSnapTurning);
            Assert.Equal(45f, preferences.TurnAngle);
            Assert.Equal("teleport", preferences.LocomotionMode);
            Assert.Equal(2.0f, preferences.MovementSpeed);
            Assert.Equal(100.0f, preferences.Scale);
            Assert.True(preferences.ShowMinimap);
            Assert.Equal("medium", preferences.QualityLevel);
            Assert.True(preferences.EnableHaptics);
            Assert.Equal(0.5f, preferences.HapticIntensity);
        }

        [Fact]
        public void VRSessionConfig_DefaultValues_ShouldBeSet()
        {
            // Arrange & Act
            var config = new VRSessionConfig();

            // Assert
            Assert.Equal("immersive-vr", config.SessionMode);
            Assert.Equal("local-floor", config.ReferenceSpace);
            Assert.True(config.EnableHandTracking);
            Assert.False(config.EnableHitTest);
            Assert.Equal(72, config.TargetFrameRate);
            Assert.Equal("quest", config.DeviceType);
        }
    }
}
