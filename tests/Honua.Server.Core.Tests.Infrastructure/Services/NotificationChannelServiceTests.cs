// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Services;

/// <summary>
/// Tests for the Notification Channel Service.
/// Tests channel configuration, validation, and notification delivery.
/// </summary>
[Trait("Category", "Unit")]
public class NotificationChannelServiceTests
{
    private readonly Mock<INotificationChannelRepository> _mockChannelRepository;
    private readonly Mock<IEmailNotificationProvider> _mockEmailProvider;
    private readonly Mock<ISlackNotificationProvider> _mockSlackProvider;
    private readonly Mock<IPagerDutyNotificationProvider> _mockPagerDutyProvider;
    private readonly Mock<ISnsNotificationProvider> _mockSnsProvider;
    private readonly Mock<ILogger<NotificationChannelService>> _mockLogger;
    private readonly NotificationChannelService _service;

    public NotificationChannelServiceTests()
    {
        _mockChannelRepository = new Mock<INotificationChannelRepository>();
        _mockEmailProvider = new Mock<IEmailNotificationProvider>();
        _mockSlackProvider = new Mock<ISlackNotificationProvider>();
        _mockPagerDutyProvider = new Mock<IPagerDutyNotificationProvider>();
        _mockSnsProvider = new Mock<ISnsNotificationProvider>();
        _mockLogger = new Mock<ILogger<NotificationChannelService>>();

        var providers = new Dictionary<string, INotificationProvider>
        {
            { "email", _mockEmailProvider.Object },
            { "slack", _mockSlackProvider.Object },
            { "pagerduty", _mockPagerDutyProvider.Object },
            { "sns", _mockSnsProvider.Object }
        };

        _service = new NotificationChannelService(
            _mockChannelRepository.Object,
            providers,
            _mockLogger.Object);
    }

    #region Create Channel Tests

    [Fact]
    public async Task CreateChannel_Email_WithValidConfig_ReturnsCreatedChannel()
    {
        // Arrange
        var newChannel = new NotificationChannel
        {
            Name = "Email Operations",
            Type = "email",
            Config = new Dictionary<string, object>
            {
                { "recipients", new[] { "ops@example.com" } },
                { "subject", "Alert: {alertName}" }
            },
            Enabled = true
        };

        _mockChannelRepository
            .Setup(r => r.CreateAsync(It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationChannel channel, CancellationToken ct) =>
            {
                channel.Id = "new-channel-id";
                channel.CreatedAt = DateTime.UtcNow;
                return channel;
            });

        // Act
        var result = await _service.CreateChannelAsync(newChannel, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.Name.Should().Be("Email Operations");
        result.Type.Should().Be("email");

        _mockChannelRepository.Verify(
            r => r.CreateAsync(It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateChannel_Slack_WithValidConfig_ReturnsCreatedChannel()
    {
        // Arrange
        var newChannel = new NotificationChannel
        {
            Name = "Slack Alerts",
            Type = "slack",
            Config = new Dictionary<string, object>
            {
                { "webhookUrl", "https://hooks.slack.com/services/T00/B00/XXX" },
                { "channel", "#alerts" }
            },
            Enabled = true
        };

        _mockChannelRepository
            .Setup(r => r.CreateAsync(It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationChannel channel, CancellationToken ct) =>
            {
                channel.Id = "slack-channel-id";
                return channel;
            });

        // Act
        var result = await _service.CreateChannelAsync(newChannel, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("slack");
    }

    [Fact]
    public async Task CreateChannel_WithInvalidType_ThrowsArgumentException()
    {
        // Arrange
        var invalidChannel = new NotificationChannel
        {
            Name = "Invalid Channel",
            Type = "invalid-type",
            Enabled = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CreateChannelAsync(invalidChannel, CancellationToken.None));
    }

    [Fact]
    public async Task CreateChannel_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var invalidChannel = new NotificationChannel
        {
            Name = "",
            Type = "email",
            Enabled = true
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CreateChannelAsync(invalidChannel, CancellationToken.None));
    }

    #endregion

    #region Update Channel Tests

    [Fact]
    public async Task UpdateChannel_WithValidData_UpdatesChannel()
    {
        // Arrange
        var existingChannel = new NotificationChannel
        {
            Id = "channel-1",
            Name = "Original Channel",
            Type = "email",
            Enabled = true
        };

        var updatedChannel = new NotificationChannel
        {
            Id = "channel-1",
            Name = "Updated Channel",
            Type = "email",
            Enabled = false
        };

        _mockChannelRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingChannel);

        _mockChannelRepository
            .Setup(r => r.UpdateAsync(It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedChannel);

        // Act
        var result = await _service.UpdateChannelAsync(updatedChannel, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Channel");
        result.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateChannel_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentChannel = new NotificationChannel { Id = "non-existent-id" };

        _mockChannelRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationChannel?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await _service.UpdateChannelAsync(nonExistentChannel, CancellationToken.None));
    }

    #endregion

    #region Delete Channel Tests

    [Fact]
    public async Task DeleteChannel_WithValidId_DeletesChannel()
    {
        // Arrange
        var channelId = "channel-1";
        var existingChannel = new NotificationChannel { Id = channelId, Name = "Test Channel" };

        _mockChannelRepository
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingChannel);

        _mockChannelRepository
            .Setup(r => r.DeleteAsync(channelId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteChannelAsync(channelId, CancellationToken.None);

        // Assert
        _mockChannelRepository.Verify(
            r => r.DeleteAsync(channelId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Send Notification Tests

    [Fact]
    public async Task SendNotification_Email_CallsEmailProvider()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "email-channel",
            Type = "email",
            Config = new Dictionary<string, object>
            {
                { "recipients", new[] { "test@example.com" } }
            },
            Enabled = true
        };

        var alert = new Alert
        {
            Id = "alert-1",
            RuleName = "Test Alert",
            Severity = "warning",
            Message = "Test message"
        };

        _mockEmailProvider
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendNotificationAsync(channel, alert, CancellationToken.None);

        // Assert
        _mockEmailProvider.Verify(
            p => p.SendNotificationAsync(channel, alert, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotification_Slack_CallsSlackProvider()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "slack-channel",
            Type = "slack",
            Config = new Dictionary<string, object>
            {
                { "webhookUrl", "https://hooks.slack.com/services/T00/B00/XXX" }
            },
            Enabled = true
        };

        var alert = new Alert
        {
            Id = "alert-1",
            RuleName = "Test Alert",
            Severity = "critical",
            Message = "Critical issue detected"
        };

        _mockSlackProvider
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendNotificationAsync(channel, alert, CancellationToken.None);

        // Assert
        _mockSlackProvider.Verify(
            p => p.SendNotificationAsync(channel, alert, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotification_PagerDuty_CallsPagerDutyProvider()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "pagerduty-channel",
            Type = "pagerduty",
            Config = new Dictionary<string, object>
            {
                { "integrationKey", "test-key" }
            },
            Enabled = true
        };

        var alert = new Alert
        {
            Id = "alert-1",
            RuleName = "Critical Alert",
            Severity = "critical"
        };

        _mockPagerDutyProvider
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendNotificationAsync(channel, alert, CancellationToken.None);

        // Assert
        _mockPagerDutyProvider.Verify(
            p => p.SendNotificationAsync(channel, alert, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotification_SNS_CallsSnsProvider()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "sns-channel",
            Type = "sns",
            Config = new Dictionary<string, object>
            {
                { "topicArn", "arn:aws:sns:us-east-1:123456789012:alerts" }
            },
            Enabled = true
        };

        var alert = new Alert { Id = "alert-1", RuleName = "Test Alert" };

        _mockSnsProvider
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.SendNotificationAsync(channel, alert, CancellationToken.None);

        // Assert
        _mockSnsProvider.Verify(
            p => p.SendNotificationAsync(channel, alert, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotification_ToDisabledChannel_ThrowsInvalidOperationException()
    {
        // Arrange
        var disabledChannel = new NotificationChannel
        {
            Id = "disabled-channel",
            Type = "email",
            Enabled = false
        };

        var alert = new Alert { Id = "alert-1" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.SendNotificationAsync(disabledChannel, alert, CancellationToken.None));
    }

    [Fact]
    public async Task SendNotification_WhenProviderFails_ThrowsNotificationException()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "email-channel",
            Type = "email",
            Enabled = true
        };

        var alert = new Alert { Id = "alert-1" };

        _mockEmailProvider
            .Setup(p => p.SendNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<Alert>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Email server unavailable"));

        // Act & Assert
        await Assert.ThrowsAsync<NotificationException>(
            async () => await _service.SendNotificationAsync(channel, alert, CancellationToken.None));
    }

    #endregion

    #region Test Channel Tests

    [Fact]
    public async Task TestChannel_Email_SendsTestNotification()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "email-channel",
            Type = "email",
            Config = new Dictionary<string, object>
            {
                { "recipients", new[] { "test@example.com" } }
            },
            Enabled = true
        };

        _mockEmailProvider
            .Setup(p => p.SendTestNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.TestChannelAsync(channel, CancellationToken.None);

        // Assert
        _mockEmailProvider.Verify(
            p => p.SendTestNotificationAsync(channel, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TestChannel_Slack_SendsTestNotification()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Id = "slack-channel",
            Type = "slack",
            Enabled = true
        };

        _mockSlackProvider
            .Setup(p => p.SendTestNotificationAsync(It.IsAny<NotificationChannel>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.TestChannelAsync(channel, CancellationToken.None);

        // Assert
        _mockSlackProvider.Verify(
            p => p.SendTestNotificationAsync(channel, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateChannel_Email_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Type = "email",
            Config = new Dictionary<string, object>
            {
                { "recipients", new[] { "valid@example.com" } }
            }
        };

        // Act
        var result = await _service.ValidateChannelAsync(channel, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateChannel_Email_WithInvalidEmailAddress_ReturnsFalse()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Type = "email",
            Config = new Dictionary<string, object>
            {
                { "recipients", new[] { "not-an-email" } }
            }
        };

        // Act
        var result = await _service.ValidateChannelAsync(channel, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("email"));
    }

    [Fact]
    public async Task ValidateChannel_Slack_WithInvalidWebhook_ReturnsFalse()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Type = "slack",
            Config = new Dictionary<string, object>
            {
                { "webhookUrl", "not-a-url" }
            }
        };

        // Act
        var result = await _service.ValidateChannelAsync(channel, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("webhook"));
    }

    [Fact]
    public async Task ValidateChannel_WithMissingRequiredConfig_ReturnsFalse()
    {
        // Arrange
        var channel = new NotificationChannel
        {
            Type = "email",
            Config = new Dictionary<string, object>()  // Missing recipients
        };

        // Act
        var result = await _service.ValidateChannelAsync(channel, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion

    #region Get Channels Tests

    [Fact]
    public async Task GetChannelById_WithValidId_ReturnsChannel()
    {
        // Arrange
        var channelId = "channel-1";
        var expectedChannel = new NotificationChannel
        {
            Id = channelId,
            Name = "Test Channel",
            Type = "email"
        };

        _mockChannelRepository
            .Setup(r => r.GetByIdAsync(channelId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChannel);

        // Act
        var result = await _service.GetChannelByIdAsync(channelId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(channelId);
    }

    [Fact]
    public async Task GetAllChannels_ReturnsAllChannels()
    {
        // Arrange
        var expectedChannels = new List<NotificationChannel>
        {
            new() { Id = "channel-1", Name = "Email", Type = "email" },
            new() { Id = "channel-2", Name = "Slack", Type = "slack" },
            new() { Id = "channel-3", Name = "PagerDuty", Type = "pagerduty" }
        };

        _mockChannelRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChannels);

        // Act
        var result = await _service.GetAllChannelsAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetEnabledChannels_ReturnsOnlyEnabledChannels()
    {
        // Arrange
        var allChannels = new List<NotificationChannel>
        {
            new() { Id = "channel-1", Enabled = true },
            new() { Id = "channel-2", Enabled = false },
            new() { Id = "channel-3", Enabled = true }
        };

        _mockChannelRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allChannels);

        // Act
        var result = await _service.GetEnabledChannelsAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.Enabled);
    }

    #endregion

    #region Helper Types (would normally be in separate files)

    private interface INotificationChannelRepository
    {
        Task<NotificationChannel> CreateAsync(NotificationChannel channel, CancellationToken cancellationToken);
        Task<NotificationChannel?> GetByIdAsync(string id, CancellationToken cancellationToken);
        Task<IEnumerable<NotificationChannel>> GetAllAsync(CancellationToken cancellationToken);
        Task<NotificationChannel> UpdateAsync(NotificationChannel channel, CancellationToken cancellationToken);
        Task DeleteAsync(string id, CancellationToken cancellationToken);
    }

    private interface INotificationProvider
    {
        Task SendNotificationAsync(NotificationChannel channel, Alert alert, CancellationToken cancellationToken);
        Task SendTestNotificationAsync(NotificationChannel channel, CancellationToken cancellationToken);
    }

    private interface IEmailNotificationProvider : INotificationProvider { }
    private interface ISlackNotificationProvider : INotificationProvider { }
    private interface IPagerDutyNotificationProvider : INotificationProvider { }
    private interface ISnsNotificationProvider : INotificationProvider { }

    private interface ILogger<T> { }

    private class NotificationChannelService
    {
        private readonly INotificationChannelRepository _repository;
        private readonly Dictionary<string, INotificationProvider> _providers;
        private readonly ILogger<NotificationChannelService> _logger;

        public NotificationChannelService(
            INotificationChannelRepository repository,
            Dictionary<string, INotificationProvider> providers,
            ILogger<NotificationChannelService> logger)
        {
            _repository = repository;
            _providers = providers;
            _logger = logger;
        }

        public Task<NotificationChannel> CreateChannelAsync(NotificationChannel channel, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(channel.Name))
                throw new ArgumentException("Channel name cannot be empty");
            if (!_providers.ContainsKey(channel.Type))
                throw new ArgumentException($"Invalid channel type: {channel.Type}");
            return _repository.CreateAsync(channel, cancellationToken);
        }

        public Task<NotificationChannel> UpdateChannelAsync(NotificationChannel channel, CancellationToken cancellationToken)
        {
            return _repository.UpdateAsync(channel, cancellationToken);
        }

        public Task DeleteChannelAsync(string id, CancellationToken cancellationToken)
        {
            return _repository.DeleteAsync(id, cancellationToken);
        }

        public Task<NotificationChannel?> GetChannelByIdAsync(string id, CancellationToken cancellationToken)
        {
            return _repository.GetByIdAsync(id, cancellationToken);
        }

        public Task<IEnumerable<NotificationChannel>> GetAllChannelsAsync(CancellationToken cancellationToken)
        {
            return _repository.GetAllAsync(cancellationToken);
        }

        public async Task<IEnumerable<NotificationChannel>> GetEnabledChannelsAsync(CancellationToken cancellationToken)
        {
            var all = await _repository.GetAllAsync(cancellationToken);
            return all.Where(c => c.Enabled);
        }

        public Task SendNotificationAsync(NotificationChannel channel, Alert alert, CancellationToken cancellationToken)
        {
            if (!channel.Enabled)
                throw new InvalidOperationException("Channel is disabled");

            if (!_providers.TryGetValue(channel.Type, out var provider))
                throw new NotificationException($"No provider for type: {channel.Type}");

            try
            {
                return provider.SendNotificationAsync(channel, alert, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new NotificationException($"Failed to send notification: {ex.Message}", ex);
            }
        }

        public Task TestChannelAsync(NotificationChannel channel, CancellationToken cancellationToken)
        {
            if (!_providers.TryGetValue(channel.Type, out var provider))
                throw new ArgumentException($"No provider for type: {channel.Type}");
            return provider.SendTestNotificationAsync(channel, cancellationToken);
        }

        public Task<ValidationResult> ValidateChannelAsync(NotificationChannel channel, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            if (channel.Type == "email")
            {
                if (!channel.Config.ContainsKey("recipients"))
                    errors.Add("Recipients are required");
                else if (channel.Config["recipients"] is string[] recipients)
                {
                    foreach (var email in recipients)
                    {
                        if (!email.Contains("@"))
                            errors.Add($"Invalid email address: {email}");
                    }
                }
            }
            else if (channel.Type == "slack")
            {
                if (!channel.Config.ContainsKey("webhookUrl"))
                    errors.Add("Webhook URL is required");
                else if (channel.Config["webhookUrl"] is string url && !url.StartsWith("http"))
                    errors.Add("Invalid webhook URL");
            }

            return Task.FromResult(new ValidationResult { IsValid = errors.Count == 0, Errors = errors });
        }
    }

    private class NotificationChannel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public Dictionary<string, object> Config { get; set; } = new();
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private class Alert
    {
        public string Id { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private class NotFoundException : Exception { }
    private class NotificationException : Exception
    {
        public NotificationException(string message) : base(message) { }
        public NotificationException(string message, Exception inner) : base(message, inner) { }
    }

    private class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    #endregion
}
