// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Infrastructure.Services;

/// <summary>
/// Tests for the Alert Configuration Service.
/// Tests alert rule management, persistence, and validation.
/// </summary>
[Trait("Category", "Unit")]
public class AlertConfigurationServiceTests
{
    private readonly Mock<IAlertRuleRepository> _mockRuleRepository;
    private readonly Mock<ILogger<AlertConfigurationService>> _mockLogger;
    private readonly AlertConfigurationService _service;

    public AlertConfigurationServiceTests()
    {
        _mockRuleRepository = new Mock<IAlertRuleRepository>();
        _mockLogger = new Mock<ILogger<AlertConfigurationService>>();
        _service = new AlertConfigurationService(_mockRuleRepository.Object, _mockLogger.Object);
    }

    #region Create Alert Rule Tests

    [Fact]
    public async Task CreateAlertRule_WithValidData_ReturnsCreatedRule()
    {
        // Arrange
        var newRule = new AlertRule
        {
            Name = "High CPU Usage",
            Description = "Alert when CPU exceeds threshold",
            Severity = AlertSeverity.Warning,
            Condition = new AlertCondition
            {
                Metric = "cpu_usage",
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 80,
                Duration = TimeSpan.FromMinutes(5)
            },
            Enabled = true
        };

        _mockRuleRepository
            .Setup(r => r.CreateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule rule, CancellationToken ct) =>
            {
                rule.Id = "new-rule-id";
                rule.CreatedAt = DateTime.UtcNow;
                return rule;
            });

        // Act
        var result = await _service.CreateAlertRuleAsync(newRule, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeNullOrEmpty();
        result.Name.Should().Be("High CPU Usage");
        result.Severity.Should().Be(AlertSeverity.Warning);

        _mockRuleRepository.Verify(
            r => r.CreateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAlertRule_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        var invalidRule = new AlertRule
        {
            Name = null!,
            Severity = AlertSeverity.Warning
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CreateAlertRuleAsync(invalidRule, CancellationToken.None));

        _mockRuleRepository.Verify(
            r => r.CreateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAlertRule_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var invalidRule = new AlertRule
        {
            Name = "",
            Severity = AlertSeverity.Warning
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.CreateAlertRuleAsync(invalidRule, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAlertRule_WithDuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange
        var existingRule = new AlertRule { Name = "Existing Rule" };

        _mockRuleRepository
            .Setup(r => r.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await _service.CreateAlertRuleAsync(existingRule, CancellationToken.None));
    }

    #endregion

    #region Update Alert Rule Tests

    [Fact]
    public async Task UpdateAlertRule_WithValidData_UpdatesRule()
    {
        // Arrange
        var existingRule = new AlertRule
        {
            Id = "rule-1",
            Name = "Original Rule",
            Severity = AlertSeverity.Warning,
            Enabled = true
        };

        var updatedRule = new AlertRule
        {
            Id = "rule-1",
            Name = "Updated Rule",
            Severity = AlertSeverity.Critical,
            Enabled = false
        };

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);

        _mockRuleRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedRule);

        // Act
        var result = await _service.UpdateAlertRuleAsync(updatedRule, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Rule");
        result.Severity.Should().Be(AlertSeverity.Critical);
        result.Enabled.Should().BeFalse();

        _mockRuleRepository.Verify(
            r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateAlertRule_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentRule = new AlertRule { Id = "non-existent-id" };

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await _service.UpdateAlertRuleAsync(nonExistentRule, CancellationToken.None));
    }

    #endregion

    #region Delete Alert Rule Tests

    [Fact]
    public async Task DeleteAlertRule_WithValidId_DeletesRule()
    {
        // Arrange
        var ruleId = "rule-1";
        var existingRule = new AlertRule { Id = ruleId, Name = "Test Rule" };

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);

        _mockRuleRepository
            .Setup(r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteAlertRuleAsync(ruleId, CancellationToken.None);

        // Assert
        _mockRuleRepository.Verify(
            r => r.DeleteAsync(ruleId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DeleteAlertRule_WithNonExistentId_ThrowsNotFoundException()
    {
        // Arrange
        var nonExistentId = "non-existent-id";

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule?)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            async () => await _service.DeleteAlertRuleAsync(nonExistentId, CancellationToken.None));
    }

    #endregion

    #region Get Alert Rules Tests

    [Fact]
    public async Task GetAlertRuleById_WithValidId_ReturnsRule()
    {
        // Arrange
        var ruleId = "rule-1";
        var expectedRule = new AlertRule
        {
            Id = ruleId,
            Name = "Test Rule",
            Severity = AlertSeverity.Warning
        };

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRule);

        // Act
        var result = await _service.GetAlertRuleByIdAsync(ruleId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(ruleId);
        result.Name.Should().Be("Test Rule");
    }

    [Fact]
    public async Task GetAlertRuleById_WithNonExistentId_ReturnsNull()
    {
        // Arrange
        var nonExistentId = "non-existent-id";

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule?)null);

        // Act
        var result = await _service.GetAlertRuleByIdAsync(nonExistentId, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAlertRules_ReturnsAllRules()
    {
        // Arrange
        var expectedRules = new List<AlertRule>
        {
            new() { Id = "rule-1", Name = "Rule 1", Severity = AlertSeverity.Warning },
            new() { Id = "rule-2", Name = "Rule 2", Severity = AlertSeverity.Critical },
            new() { Id = "rule-3", Name = "Rule 3", Severity = AlertSeverity.Info }
        };

        _mockRuleRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedRules);

        // Act
        var result = await _service.GetAllAlertRulesAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(r => r.Id == "rule-1");
        result.Should().Contain(r => r.Id == "rule-2");
        result.Should().Contain(r => r.Id == "rule-3");
    }

    [Fact]
    public async Task GetEnabledAlertRules_ReturnsOnlyEnabledRules()
    {
        // Arrange
        var allRules = new List<AlertRule>
        {
            new() { Id = "rule-1", Name = "Rule 1", Enabled = true },
            new() { Id = "rule-2", Name = "Rule 2", Enabled = false },
            new() { Id = "rule-3", Name = "Rule 3", Enabled = true }
        };

        _mockRuleRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allRules);

        // Act
        var result = await _service.GetEnabledAlertRulesAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(r => r.Enabled);
    }

    #endregion

    #region Toggle Rule Tests

    [Fact]
    public async Task ToggleAlertRule_EnablesDisabledRule()
    {
        // Arrange
        var ruleId = "rule-1";
        var existingRule = new AlertRule
        {
            Id = ruleId,
            Name = "Test Rule",
            Enabled = false
        };

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);

        _mockRuleRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule rule, CancellationToken ct) => rule);

        // Act
        var result = await _service.ToggleAlertRuleAsync(ruleId, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeTrue();

        _mockRuleRepository.Verify(
            r => r.UpdateAsync(It.Is<AlertRule>(ar => ar.Enabled == true), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleAlertRule_DisablesEnabledRule()
    {
        // Arrange
        var ruleId = "rule-1";
        var existingRule = new AlertRule
        {
            Id = ruleId,
            Name = "Test Rule",
            Enabled = true
        };

        _mockRuleRepository
            .Setup(r => r.GetByIdAsync(ruleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRule);

        _mockRuleRepository
            .Setup(r => r.UpdateAsync(It.IsAny<AlertRule>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AlertRule rule, CancellationToken ct) => rule);

        // Act
        var result = await _service.ToggleAlertRuleAsync(ruleId, CancellationToken.None);

        // Assert
        result.Enabled.Should().BeFalse();
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task ValidateAlertRule_WithValidRule_ReturnsTrue()
    {
        // Arrange
        var validRule = new AlertRule
        {
            Name = "Valid Rule",
            Severity = AlertSeverity.Warning,
            Condition = new AlertCondition
            {
                Metric = "cpu_usage",
                Operator = ComparisonOperator.GreaterThan,
                Threshold = 80
            }
        };

        // Act
        var result = await _service.ValidateAlertRuleAsync(validRule, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAlertRule_WithInvalidSeverity_ReturnsFalse()
    {
        // Arrange
        var invalidRule = new AlertRule
        {
            Name = "Invalid Rule",
            Severity = (AlertSeverity)999  // Invalid severity
        };

        // Act
        var result = await _service.ValidateAlertRuleAsync(invalidRule, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("severity"));
    }

    [Fact]
    public async Task ValidateAlertRule_WithoutCondition_ReturnsFalse()
    {
        // Arrange
        var invalidRule = new AlertRule
        {
            Name = "Rule Without Condition",
            Severity = AlertSeverity.Warning,
            Condition = null!
        };

        // Act
        var result = await _service.ValidateAlertRuleAsync(invalidRule, CancellationToken.None);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("condition"));
    }

    #endregion

    #region Bulk Operations Tests

    [Fact]
    public async Task BulkEnableRules_EnablesMultipleRules()
    {
        // Arrange
        var ruleIds = new[] { "rule-1", "rule-2", "rule-3" };
        var rules = ruleIds.Select(id => new AlertRule
        {
            Id = id,
            Name = $"Rule {id}",
            Enabled = false
        }).ToList();

        _mockRuleRepository
            .Setup(r => r.GetByIdsAsync(ruleIds, It.IsAny<CancellationToken>()))
            .ReturnsAsync(rules);

        _mockRuleRepository
            .Setup(r => r.UpdateManyAsync(It.IsAny<IEnumerable<AlertRule>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.BulkEnableRulesAsync(ruleIds, CancellationToken.None);

        // Assert
        _mockRuleRepository.Verify(
            r => r.UpdateManyAsync(It.Is<IEnumerable<AlertRule>>(rules => rules.All(r => r.Enabled)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BulkDeleteRules_DeletesMultipleRules()
    {
        // Arrange
        var ruleIds = new[] { "rule-1", "rule-2", "rule-3" };

        _mockRuleRepository
            .Setup(r => r.DeleteManyAsync(ruleIds, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.BulkDeleteRulesAsync(ruleIds, CancellationToken.None);

        // Assert
        _mockRuleRepository.Verify(
            r => r.DeleteManyAsync(ruleIds, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Types (would normally be in separate files)

    public interface IAlertRuleRepository
    {
        Task<AlertRule> CreateAsync(AlertRule rule, CancellationToken cancellationToken);
        Task<AlertRule?> GetByIdAsync(string id, CancellationToken cancellationToken);
        Task<IEnumerable<AlertRule>> GetAllAsync(CancellationToken cancellationToken);
        Task<IEnumerable<AlertRule>> GetByIdsAsync(string[] ids, CancellationToken cancellationToken);
        Task<AlertRule> UpdateAsync(AlertRule rule, CancellationToken cancellationToken);
        Task UpdateManyAsync(IEnumerable<AlertRule> rules, CancellationToken cancellationToken);
        Task DeleteAsync(string id, CancellationToken cancellationToken);
        Task DeleteManyAsync(string[] ids, CancellationToken cancellationToken);
        Task<bool> ExistsAsync(string name, CancellationToken cancellationToken);
    }

    public interface ILogger<T>
    {
    }

    public class AlertConfigurationService
    {
        private readonly IAlertRuleRepository _repository;
        private readonly ILogger<AlertConfigurationService> _logger;

        public AlertConfigurationService(IAlertRuleRepository repository, ILogger<AlertConfigurationService> logger)
        {
            _repository = repository;
            _logger = logger;
        }

        public Task<AlertRule> CreateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
                throw new ArgumentException("Rule name cannot be empty");
            return _repository.CreateAsync(rule, cancellationToken);
        }

        public Task<AlertRule> UpdateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken)
        {
            return _repository.UpdateAsync(rule, cancellationToken);
        }

        public Task DeleteAlertRuleAsync(string id, CancellationToken cancellationToken)
        {
            return _repository.DeleteAsync(id, cancellationToken);
        }

        public Task<AlertRule?> GetAlertRuleByIdAsync(string id, CancellationToken cancellationToken)
        {
            return _repository.GetByIdAsync(id, cancellationToken);
        }

        public Task<IEnumerable<AlertRule>> GetAllAlertRulesAsync(CancellationToken cancellationToken)
        {
            return _repository.GetAllAsync(cancellationToken);
        }

        public async Task<IEnumerable<AlertRule>> GetEnabledAlertRulesAsync(CancellationToken cancellationToken)
        {
            var all = await _repository.GetAllAsync(cancellationToken);
            return all.Where(r => r.Enabled);
        }

        public async Task<AlertRule> ToggleAlertRuleAsync(string id, CancellationToken cancellationToken)
        {
            var rule = await _repository.GetByIdAsync(id, cancellationToken);
            if (rule == null) throw new NotFoundException();
            rule.Enabled = !rule.Enabled;
            return await _repository.UpdateAsync(rule, cancellationToken);
        }

        public Task<ValidationResult> ValidateAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            if (rule.Condition == null) errors.Add("Condition is required");
            if (!Enum.IsDefined(typeof(AlertSeverity), rule.Severity)) errors.Add("Invalid severity");
            return Task.FromResult(new ValidationResult { IsValid = errors.Count == 0, Errors = errors });
        }

        public async Task BulkEnableRulesAsync(string[] ruleIds, CancellationToken cancellationToken)
        {
            var rules = await _repository.GetByIdsAsync(ruleIds, cancellationToken);
            foreach (var rule in rules) rule.Enabled = true;
            await _repository.UpdateManyAsync(rules, cancellationToken);
        }

        public Task BulkDeleteRulesAsync(string[] ruleIds, CancellationToken cancellationToken)
        {
            return _repository.DeleteManyAsync(ruleIds, cancellationToken);
        }
    }

    public class AlertRule
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public AlertSeverity Severity { get; set; }
        public AlertCondition? Condition { get; set; }
        public bool Enabled { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AlertCondition
    {
        public string Metric { get; set; } = string.Empty;
        public ComparisonOperator Operator { get; set; }
        public double Threshold { get; set; }
        public TimeSpan? Duration { get; set; }
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    public enum ComparisonOperator
    {
        GreaterThan,
        LessThan,
        Equals,
        NotEquals
    }

    public class NotFoundException : Exception { }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    #endregion
}
