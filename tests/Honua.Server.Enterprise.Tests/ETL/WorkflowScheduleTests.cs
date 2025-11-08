// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Honua.Server.Enterprise.ETL.Scheduling;
using Xunit;

namespace Honua.Server.Enterprise.Tests.ETL;

/// <summary>
/// Unit tests for WorkflowSchedule model and scheduling logic
/// </summary>
public class WorkflowScheduleTests
{
    [Fact]
    public void WorkflowSchedule_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var schedule = new WorkflowSchedule();

        // Assert
        Assert.NotEqual(Guid.Empty, schedule.Id);
        Assert.Equal("UTC", schedule.Timezone);
        Assert.True(schedule.Enabled);
        Assert.Equal(ScheduleStatus.Active, schedule.Status);
        Assert.Equal(1, schedule.MaxConcurrentExecutions);
        Assert.Equal(0, schedule.RetryAttempts);
        Assert.Equal(5, schedule.RetryDelayMinutes);
        Assert.NotNull(schedule.Tags);
    }

    [Theory]
    [InlineData("0 0 * * *", true)]  // Daily at midnight
    [InlineData("0 * * * *", true)]  // Every hour
    [InlineData("*/15 * * * *", true)] // Every 15 minutes
    [InlineData("0 9 * * MON-FRI", true)] // Weekdays at 9am
    [InlineData("0 0 1 * *", true)]  // Monthly on 1st
    [InlineData("invalid", false)]   // Invalid expression
    [InlineData("", false)]          // Empty expression
    public void IsValidCronExpression_ValidatesCorrectly(string cronExpression, bool expectedValid)
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = cronExpression
        };

        // Act
        var isValid = schedule.IsValidCronExpression();

        // Assert
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void CalculateNextRun_DailyCron_ReturnsCorrectNextRun()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = "0 0 * * *", // Daily at midnight
            Timezone = "UTC"
        };

        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

        // Act
        var nextRun = schedule.CalculateNextRun(baseTime);

        // Assert
        Assert.NotNull(nextRun);
        Assert.Equal(16, nextRun.Value.Day); // Next day
        Assert.Equal(0, nextRun.Value.Hour);  // At midnight
        Assert.Equal(0, nextRun.Value.Minute);
    }

    [Fact]
    public void CalculateNextRun_HourlyCron_ReturnsCorrectNextRun()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = "0 * * * *", // Every hour
            Timezone = "UTC"
        };

        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 30, 0, TimeSpan.Zero);

        // Act
        var nextRun = schedule.CalculateNextRun(baseTime);

        // Assert
        Assert.NotNull(nextRun);
        Assert.Equal(11, nextRun.Value.Hour);  // Next hour
        Assert.Equal(0, nextRun.Value.Minute);
    }

    [Fact]
    public void CalculateNextRun_InvalidCron_ReturnsNull()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = "invalid",
            Timezone = "UTC"
        };

        // Act
        var nextRun = schedule.CalculateNextRun();

        // Assert
        Assert.Null(nextRun);
    }

    [Fact]
    public void GetNextExecutions_ReturnsCorrectCount()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = "0 * * * *", // Every hour
            Timezone = "UTC"
        };

        // Act
        var nextExecutions = schedule.GetNextExecutions(5);

        // Assert
        Assert.Equal(5, nextExecutions.Count);

        // Verify times are in order and spaced correctly
        for (int i = 1; i < nextExecutions.Count; i++)
        {
            var diff = nextExecutions[i] - nextExecutions[i - 1];
            Assert.True(diff.TotalMinutes >= 55 && diff.TotalMinutes <= 65); // Approximately 1 hour
        }
    }

    [Fact]
    public void GetNextExecutions_InvalidCron_ReturnsEmptyList()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = "invalid",
            Timezone = "UTC"
        };

        // Act
        var nextExecutions = schedule.GetNextExecutions(5);

        // Assert
        Assert.Empty(nextExecutions);
    }

    [Fact]
    public void IsActive_EnabledAndActive_ReturnsTrue()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            Enabled = true,
            Status = ScheduleStatus.Active
        };

        // Act & Assert
        Assert.True(schedule.IsActive());
    }

    [Fact]
    public void IsActive_Disabled_ReturnsFalse()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            Enabled = false,
            Status = ScheduleStatus.Active
        };

        // Act & Assert
        Assert.False(schedule.IsActive());
    }

    [Fact]
    public void IsActive_Paused_ReturnsFalse()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            Enabled = true,
            Status = ScheduleStatus.Paused
        };

        // Act & Assert
        Assert.False(schedule.IsActive());
    }

    [Fact]
    public void IsActive_Expired_ReturnsFalse()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            Enabled = true,
            Status = ScheduleStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1) // Expired yesterday
        };

        // Act & Assert
        Assert.False(schedule.IsActive());
    }

    [Fact]
    public void IsActive_NotYetExpired_ReturnsTrue()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            Enabled = true,
            Status = ScheduleStatus.Active,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) // Expires tomorrow
        };

        // Act & Assert
        Assert.True(schedule.IsActive());
    }

    [Fact]
    public void ScheduleNotificationConfig_Constructor_SetsDefaults()
    {
        // Arrange & Act
        var config = new ScheduleNotificationConfig();

        // Assert
        Assert.False(config.NotifyOnSuccess);
        Assert.True(config.NotifyOnFailure);
        Assert.NotNull(config.EmailAddresses);
        Assert.NotNull(config.WebhookUrls);
    }

    [Fact]
    public void ScheduleExecution_Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var execution = new ScheduleExecution();

        // Assert
        Assert.NotEqual(Guid.Empty, execution.Id);
        Assert.Equal(ScheduleExecutionStatus.Pending, execution.Status);
        Assert.Equal(0, execution.RetryCount);
        Assert.False(execution.Skipped);
    }

    [Theory]
    [InlineData("0 0 * * *", "America/New_York")]
    [InlineData("0 12 * * *", "Europe/London")]
    [InlineData("0 9 * * MON-FRI", "Asia/Tokyo")]
    public void CalculateNextRun_DifferentTimezones_WorksCorrectly(string cronExpression, string timezone)
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = cronExpression,
            Timezone = timezone
        };

        // Act
        var nextRun = schedule.CalculateNextRun();

        // Assert
        Assert.NotNull(nextRun);
        // Verify that a next run was calculated (actual time depends on current time and timezone)
    }

    [Fact]
    public void CalculateNextRun_Every15Minutes_ReturnsMultipleRuns()
    {
        // Arrange
        var schedule = new WorkflowSchedule
        {
            CronExpression = "*/15 * * * *", // Every 15 minutes
            Timezone = "UTC"
        };

        var baseTime = new DateTimeOffset(2025, 1, 15, 10, 0, 0, TimeSpan.Zero);

        // Act
        var runs = new List<DateTimeOffset?>();
        var current = baseTime;
        for (int i = 0; i < 4; i++)
        {
            var next = schedule.CalculateNextRun(current);
            runs.Add(next);
            if (next.HasValue)
            {
                current = next.Value;
            }
        }

        // Assert
        Assert.All(runs, run => Assert.NotNull(run));
        Assert.Equal(15, (runs[1]!.Value - runs[0]!.Value).TotalMinutes);
        Assert.Equal(15, (runs[2]!.Value - runs[1]!.Value).TotalMinutes);
        Assert.Equal(15, (runs[3]!.Value - runs[2]!.Value).TotalMinutes);
    }

    [Fact]
    public void WorkflowSchedule_TagsProperty_CanBeModified()
    {
        // Arrange
        var schedule = new WorkflowSchedule();

        // Act
        schedule.Tags.Add("production");
        schedule.Tags.Add("critical");

        // Assert
        Assert.Equal(2, schedule.Tags.Count);
        Assert.Contains("production", schedule.Tags);
        Assert.Contains("critical", schedule.Tags);
    }

    [Fact]
    public void WorkflowSchedule_ParameterValues_CanStoreComplexObjects()
    {
        // Arrange
        var schedule = new WorkflowSchedule();
        var parameters = new Dictionary<string, object>
        {
            ["inputPath"] = "/data/input.geojson",
            ["bufferDistance"] = 100,
            ["outputFormat"] = "GeoPackage"
        };

        // Act
        schedule.ParameterValues = parameters;

        // Assert
        Assert.NotNull(schedule.ParameterValues);
        Assert.Equal(3, schedule.ParameterValues.Count);
        Assert.Equal("/data/input.geojson", schedule.ParameterValues["inputPath"]);
        Assert.Equal(100, schedule.ParameterValues["bufferDistance"]);
    }
}
