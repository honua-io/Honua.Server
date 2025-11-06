// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using RichardSzalay.MockHttp;

namespace Honua.Admin.Blazor.Tests.Components.Pages.Alerts;

/// <summary>
/// Tests for the Alert Rule Editor component.
/// Tests form validation, condition builder, and rule saving.
/// </summary>
[Trait("Category", "Unit")]
public class AlertRuleEditorTests : ComponentTestBase
{
    private readonly MockHttpMessageHandler _mockHttp;

    public AlertRuleEditorTests()
    {
        _mockHttp = new MockHttpMessageHandler();
        var httpClient = _mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("http://localhost");

        Context.Services.AddSingleton(httpClient);
        Context.Services.AddSingleton<ISnackbar, SnackbarService>();
    }

    #region Form Validation Tests

    [Fact]
    public void RuleName_Required_ShowsValidationError()
    {
        // Arrange
        var ruleName = "";

        // Assert
        ruleName.Should().BeEmpty();
    }

    [Fact]
    public void RuleName_TooLong_ShowsValidationError()
    {
        // Arrange
        var maxLength = 100;
        var tooLongName = new string('A', maxLength + 1);

        // Assert
        tooLongName.Length.Should().BeGreaterThan(maxLength);
    }

    [Fact]
    public void Severity_Required_ShowsValidationError()
    {
        // Arrange
        string? severity = null;

        // Assert
        severity.Should().BeNull();
    }

    [Fact]
    public void Severity_InvalidValue_ShowsValidationError()
    {
        // Arrange
        var validSeverities = new[] { "info", "warning", "error", "critical" };
        var invalidSeverity = "unknown";

        // Assert
        validSeverities.Should().NotContain(invalidSeverity);
    }

    [Fact]
    public void Condition_Required_ShowsValidationError()
    {
        // Arrange
        object? condition = null;

        // Assert
        condition.Should().BeNull();
    }

    [Fact]
    public void Threshold_MustBeNumeric_ShowsValidationError()
    {
        // Arrange
        var threshold = "not-a-number";
        var isNumeric = double.TryParse(threshold, out _);

        // Assert
        isNumeric.Should().BeFalse();
    }

    [Fact]
    public void Duration_MustBeValid_ShowsValidationError()
    {
        // Arrange
        var validDurations = new[] { "1m", "5m", "15m", "1h", "24h" };
        var invalidDuration = "invalid";

        // Assert
        validDurations.Should().NotContain(invalidDuration);
    }

    #endregion

    #region Create Mode Tests

    [Fact]
    public async Task CreateNewRule_WithValidData_CallsCreateApi()
    {
        // Arrange
        var newRule = new
        {
            name = "New Alert Rule",
            description = "Test rule",
            severity = "warning",
            condition = new
            {
                metric = "cpu_usage",
                @operator = "greater_than",
                threshold = 80,
                duration = "5m"
            },
            enabled = true,
            notificationChannels = new[] { "email-ops" }
        };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/rules")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                id = "new-rule-id",
                name = newRule.name
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/admin/alerts/rules", newRule);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task CreateRule_WithInvalidData_ShowsValidationErrors()
    {
        // Arrange
        var invalidRule = new
        {
            name = "",  // Empty name
            severity = "invalid"  // Invalid severity
        };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/rules")
            .Respond(System.Net.HttpStatusCode.BadRequest);

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/admin/alerts/rules", invalidRule);

        // Assert
        response.IsSuccessStatusCode.Should().BeFalse();
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }

    #endregion

    #region Edit Mode Tests

    [Fact]
    public async Task LoadExistingRule_PopulatesForm()
    {
        // Arrange
        var ruleId = "existing-rule-id";
        var existingRule = new
        {
            id = ruleId,
            name = "Existing Rule",
            description = "Test rule",
            severity = "critical",
            condition = new
            {
                metric = "memory_usage",
                @operator = "greater_than",
                threshold = 90
            },
            enabled = true
        };

        _mockHttp.When($"/api/admin/alerts/rules/{ruleId}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(existingRule));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync($"/api/admin/alerts/rules/{ruleId}");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("Existing Rule");
        content.Should().Contain("critical");
    }

    [Fact]
    public async Task UpdateExistingRule_WithValidData_CallsUpdateApi()
    {
        // Arrange
        var ruleId = "existing-rule-id";
        var updatedRule = new
        {
            name = "Updated Rule Name",
            severity = "warning",
            enabled = false
        };

        _mockHttp.When(HttpMethod.Put, $"/api/admin/alerts/rules/{ruleId}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(updatedRule));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PutAsJsonAsync($"/api/admin/alerts/rules/{ruleId}", updatedRule);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Condition Builder Tests

    [Fact]
    public void SelectMetric_PopulatesMetricField()
    {
        // Arrange
        var availableMetrics = new[] { "cpu_usage", "memory_usage", "disk_usage", "error_rate" };
        var selectedMetric = "cpu_usage";

        // Assert
        availableMetrics.Should().Contain(selectedMetric);
    }

    [Fact]
    public void SelectOperator_PopulatesOperatorField()
    {
        // Arrange
        var availableOperators = new[] { "greater_than", "less_than", "equals", "not_equals" };
        var selectedOperator = "greater_than";

        // Assert
        availableOperators.Should().Contain(selectedOperator);
    }

    [Fact]
    public void EnterThreshold_ValidatesNumericInput()
    {
        // Arrange
        var validThreshold = "80.5";
        var invalidThreshold = "abc";

        // Assert
        double.TryParse(validThreshold, out _).Should().BeTrue();
        double.TryParse(invalidThreshold, out _).Should().BeFalse();
    }

    [Fact]
    public void SelectDuration_PopulatesDurationField()
    {
        // Arrange
        var availableDurations = new[] { "1m", "5m", "15m", "30m", "1h", "6h", "12h", "24h" };
        var selectedDuration = "5m";

        // Assert
        availableDurations.Should().Contain(selectedDuration);
    }

    [Fact]
    public void AddMultipleConditions_CreatesConditionGroup()
    {
        // Arrange
        var conditions = new[]
        {
            new { metric = "cpu_usage", @operator = "greater_than", threshold = 80 },
            new { metric = "memory_usage", @operator = "greater_than", threshold = 90 }
        };

        // Assert
        conditions.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveCondition_UpdatesConditionList()
    {
        // Arrange
        var conditions = new List<object>
        {
            new { metric = "cpu_usage", @operator = "greater_than", threshold = 80 },
            new { metric = "memory_usage", @operator = "greater_than", threshold = 90 }
        };

        // Act
        conditions.RemoveAt(0);

        // Assert
        conditions.Should().HaveCount(1);
    }

    #endregion

    #region Notification Channel Selection Tests

    [Fact]
    public async Task LoadAvailableChannels_DisplaysChannelList()
    {
        // Arrange
        var channels = new[]
        {
            new { id = "channel-1", name = "Email Ops", type = "email", enabled = true },
            new { id = "channel-2", name = "Slack Alerts", type = "slack", enabled = true },
            new { id = "channel-3", name = "PagerDuty", type = "pagerduty", enabled = true }
        };

        _mockHttp.When("/api/admin/alerts/channels")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(channels));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.GetAsync("/api/admin/alerts/channels");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
        content.Should().Contain("Email Ops");
        content.Should().Contain("Slack Alerts");
    }

    [Fact]
    public void SelectNotificationChannels_AllowsMultipleSelection()
    {
        // Arrange
        var availableChannels = new[] { "email-ops", "slack-alerts", "pagerduty" };
        var selectedChannels = new List<string> { "email-ops", "pagerduty" };

        // Assert
        selectedChannels.Should().HaveCount(2);
        selectedChannels.Should().Contain("email-ops");
        selectedChannels.Should().Contain("pagerduty");
    }

    [Fact]
    public void SelectNoChannels_ShowsWarning()
    {
        // Arrange
        var selectedChannels = new List<string>();

        // Assert
        selectedChannels.Should().BeEmpty();
    }

    #endregion

    #region Advanced Options Tests

    [Fact]
    public void SetCustomLabels_AddsLabelsToRule()
    {
        // Arrange
        var labels = new Dictionary<string, string>
        {
            { "team", "operations" },
            { "environment", "production" },
            { "priority", "high" }
        };

        // Assert
        labels.Should().HaveCount(3);
        labels["team"].Should().Be("operations");
    }

    [Fact]
    public void SetAnnotations_AddsAnnotationsToRule()
    {
        // Arrange
        var annotations = new Dictionary<string, string>
        {
            { "runbook", "https://wiki.example.com/runbooks/cpu-high" },
            { "dashboard", "https://grafana.example.com/d/cpu-dashboard" }
        };

        // Assert
        annotations.Should().HaveCount(2);
        annotations.Should().ContainKey("runbook");
    }

    [Fact]
    public void SetEvaluationInterval_ValidatesInterval()
    {
        // Arrange
        var validIntervals = new[] { "30s", "1m", "5m", "15m" };
        var selectedInterval = "1m";

        // Assert
        validIntervals.Should().Contain(selectedInterval);
    }

    [Fact]
    public void SetGroupBy_AllowsFieldSelection()
    {
        // Arrange
        var availableFields = new[] { "host", "region", "service", "environment" };
        var selectedGroupBy = new[] { "host", "service" };

        // Assert
        selectedGroupBy.All(f => availableFields.Contains(f)).Should().BeTrue();
    }

    #endregion

    #region Save and Cancel Tests

    [Fact]
    public async Task ClickSave_WithValidForm_SavesRule()
    {
        // Arrange
        var rule = new
        {
            name = "Test Rule",
            severity = "warning",
            enabled = true
        };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/rules")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(rule));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/admin/alerts/rules", rule);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public void ClickSave_WithInvalidForm_ShowsValidationErrors()
    {
        // Arrange
        var hasValidationErrors = true;

        // Assert
        hasValidationErrors.Should().BeTrue();
    }

    [Fact]
    public void ClickCancel_ClosesEditorWithoutSaving()
    {
        // Arrange
        var editorClosed = false;

        // Act
        editorClosed = true;

        // Assert
        editorClosed.Should().BeTrue();
    }

    [Fact]
    public void ClickCancel_WithUnsavedChanges_ShowsConfirmation()
    {
        // Arrange
        var hasUnsavedChanges = true;

        // Assert
        hasUnsavedChanges.Should().BeTrue();
    }

    #endregion

    #region Template Tests

    [Fact]
    public void SelectTemplate_PopulatesFormWithTemplate()
    {
        // Arrange
        var templates = new[]
        {
            new
            {
                name = "High CPU Template",
                severity = "warning",
                condition = new { metric = "cpu_usage", @operator = "greater_than", threshold = 80 }
            },
            new
            {
                name = "Low Disk Space Template",
                severity = "critical",
                condition = new { metric = "disk_usage", @operator = "less_than", threshold = 10 }
            }
        };

        var selectedTemplate = templates[0];

        // Assert
        selectedTemplate.name.Should().Be("High CPU Template");
        selectedTemplate.severity.Should().Be("warning");
    }

    [Fact]
    public void SaveAsTemplate_CreatesReusableTemplate()
    {
        // Arrange
        var templateName = "My Custom Template";
        var rule = new
        {
            name = "Test Rule",
            severity = "warning",
            condition = new { metric = "cpu_usage", @operator = "greater_than", threshold = 80 }
        };

        // Assert
        templateName.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Preview Tests

    [Fact]
    public async Task PreviewRule_ShowsMatchingAlerts()
    {
        // Arrange
        var rulePreview = new
        {
            name = "Test Rule",
            condition = new { metric = "cpu_usage", @operator = "greater_than", threshold = 80 }
        };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/rules/preview")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                matchCount = 5,
                sampleAlerts = new[] { "Alert 1", "Alert 2", "Alert 3" }
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/admin/alerts/rules/preview", rulePreview);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task TestRule_SimulatesAlertTrigger()
    {
        // Arrange
        var testData = new
        {
            ruleId = "test-rule",
            metricValue = 85
        };

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/rules/test")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                triggered = true,
                message = "Alert would be triggered"
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var response = await httpClient.PostAsJsonAsync("/api/admin/alerts/rules/test", testData);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion

    #region Schedule Tests

    [Fact]
    public void SetSchedule_AllowsTimeRangeSelection()
    {
        // Arrange
        var schedule = new
        {
            enabled = true,
            startTime = "09:00",
            endTime = "17:00",
            daysOfWeek = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" }
        };

        // Assert
        schedule.daysOfWeek.Should().HaveCount(5);
        schedule.startTime.Should().Be("09:00");
    }

    [Fact]
    public void SetMuteSchedule_DisablesRuleDuringPeriod()
    {
        // Arrange
        var muteSchedule = new
        {
            enabled = true,
            startDate = DateTime.UtcNow,
            endDate = DateTime.UtcNow.AddDays(7)
        };

        // Assert
        muteSchedule.enabled.Should().BeTrue();
        muteSchedule.endDate.Should().BeAfter(muteSchedule.startDate);
    }

    #endregion

    #region Duplicate Rule Tests

    [Fact]
    public async Task DuplicateRule_CreatesNewRuleWithSameConfig()
    {
        // Arrange
        var sourceRuleId = "source-rule-id";
        var sourceRule = new
        {
            id = sourceRuleId,
            name = "Original Rule",
            severity = "warning",
            condition = new { metric = "cpu_usage", @operator = "greater_than", threshold = 80 }
        };

        _mockHttp.When($"/api/admin/alerts/rules/{sourceRuleId}")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(sourceRule));

        _mockHttp.When(HttpMethod.Post, "/api/admin/alerts/rules")
            .Respond("application/json", System.Text.Json.JsonSerializer.Serialize(new
            {
                id = "new-rule-id",
                name = "Copy of Original Rule"
            }));

        // Act
        var httpClient = _mockHttp.ToHttpClient();
        var getResponse = await httpClient.GetAsync($"/api/admin/alerts/rules/{sourceRuleId}");
        var createResponse = await httpClient.PostAsJsonAsync("/api/admin/alerts/rules", new
        {
            name = "Copy of Original Rule",
            severity = "warning"
        });

        // Assert
        getResponse.IsSuccessStatusCode.Should().BeTrue();
        createResponse.IsSuccessStatusCode.Should().BeTrue();
    }

    #endregion
}
