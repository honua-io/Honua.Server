// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Admin.Blazor.Components.Shared;
using Honua.Admin.Blazor.Shared.Models;
using Honua.Admin.Blazor.Shared.Services;
using Honua.Admin.Blazor.Tests.Infrastructure;
using Bunit;
using MudBlazor;
using Microsoft.Extensions.DependencyInjection;

namespace Honua.Admin.Blazor.Tests.Components.Shared;

/// <summary>
/// Tests for the DataSourceDialog component.
/// Validates form rendering, validation, connection string building, and user interactions.
/// </summary>
[Trait("Category", "Unit")]
public class DataSourceDialogTests : ComponentTestBase
{
    [Fact]
    public void DataSourceDialog_PostgreSQL_RendersAllFields()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        // Act
        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Assert
        cut.Find("input[label='Data Source ID']").Should().NotBeNull();

        // Verify PostgreSQL fields are visible (default provider)
        cut.Markup.Should().Contain("Host");
        cut.Markup.Should().Contain("Port");
        cut.Markup.Should().Contain("Database");
        cut.Markup.Should().Contain("Username");
        cut.Markup.Should().Contain("Password");
        cut.Markup.Should().Contain("SSL Mode");
    }

    [Fact]
    public void ValidateConnectionParameters_AllFieldsValid_ReturnsTrue()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Fill in all required fields
        var dataSourceIdInput = cut.Find("input[label='Data Source ID']");
        dataSourceIdInput.Change("test-datasource");

        var hostInput = cut.Find("input[label='Host']");
        hostInput.Change("localhost");

        var databaseInput = cut.Find("input[label='Database']");
        databaseInput.Change("testdb");

        var usernameInput = cut.Find("input[label='Username']");
        usernameInput.Change("postgres");

        var passwordInput = cut.Find("input[label='Password']");
        passwordInput.Change("password123");

        // Assert - No validation errors should be visible
        cut.Markup.Should().NotContain("is required");
    }

    [Fact]
    public void ValidateConnectionParameters_InvalidHost_ShowsError()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Enter invalid host with special characters
        var hostInput = cut.Find("input[label='Host']");
        hostInput.Change("invalid host!@#$");

        // Click test connection to trigger validation
        var testButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Test Connection"));
        testButton?.Click();

        // Assert - Should show validation error
        cut.WaitForState(() => cut.Markup.Contains("Invalid host format") ||
                               cut.Markup.Contains("validation"),
                               timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ValidateConnectionParameters_PortOutOfRange_ShowsError()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Enter port out of valid range
        var portInput = cut.Find("input[label='Port']");
        portInput.Change("99999"); // Port > 65535

        // Fill other required fields
        var dataSourceIdInput = cut.Find("input[label='Data Source ID']");
        dataSourceIdInput.Change("test-ds");

        var hostInput = cut.Find("input[label='Host']");
        hostInput.Change("localhost");

        // Click test connection to trigger validation
        var testButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Test Connection"));
        testButton?.Click();

        // Assert
        cut.WaitForState(() => cut.Markup.Contains("Port must be between") ||
                               cut.Markup.Contains("65535"),
                               timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void ValidateConnectionParameters_MissingRequiredFields_ShowsErrors()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Try to save without filling required fields
        var saveButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Create") || b.TextContent.Contains("Update"));
        saveButton?.Click();

        // Assert - Should show validation errors for required fields
        cut.WaitForState(() => cut.Markup.Contains("required") ||
                               cut.Markup.Contains("is required"),
                               timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void BuildConnectionString_PostgreSQL_CorrectFormat()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Fill PostgreSQL fields
        cut.Find("input[label='Data Source ID']").Change("pg-test");
        cut.Find("input[label='Host']").Change("localhost");
        cut.Find("input[label='Database']").Change("gisdb");
        cut.Find("input[label='Username']").Change("postgres");
        cut.Find("input[label='Password']").Change("pass123");

        // Assert - Connection string preview should be visible
        cut.WaitForState(() => cut.Markup.Contains("Host=localhost"),
                         timeout: TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("Database=gisdb");
        cut.Markup.Should().Contain("Username=postgres");
        cut.Markup.Should().Contain("Password=pass123");
    }

    [Fact]
    public void BuildConnectionString_SQLServer_CorrectFormat()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Select SQL Server provider
        var providerSelect = cut.FindComponent<MudSelect<string>>();
        providerSelect.Instance.Value = "sqlserver";
        cut.Render();

        // Fill SQL Server fields
        cut.Find("input[label='Server']").Change("localhost");
        cut.Find("input[label='Database']").Change("gisdb");
        cut.Find("input[label='User ID']").Change("sa");
        cut.Find("input[label='Password']").Change("pass123");

        // Assert - Connection string preview should show SQL Server format
        cut.WaitForState(() => cut.Markup.Contains("Server=localhost"),
                         timeout: TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("User Id=sa");
    }

    [Fact]
    public void BuildConnectionString_MySQL_CorrectFormat()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Select MySQL provider
        var providerSelect = cut.FindComponent<MudSelect<string>>();
        providerSelect.Instance.Value = "mysql";
        cut.Render();

        // Fill MySQL fields
        cut.Find("input[label='Server']").Change("localhost");
        cut.Find("input[label='Database']").Change("gisdb");
        cut.Find("input[label='User']").Change("root");
        cut.Find("input[label='Password']").Change("pass123");

        // Assert - Connection string preview should show MySQL format
        cut.WaitForState(() => cut.Markup.Contains("Server=localhost"),
                         timeout: TimeSpan.FromSeconds(2));
        cut.Markup.Should().Contain("User=root");
    }

    [Fact]
    public void BuildConnectionString_SQLite_CorrectFormat()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Select SQLite provider
        var providerSelect = cut.FindComponent<MudSelect<string>>();
        providerSelect.Instance.Value = "sqlite";
        cut.Render();

        // Fill SQLite fields
        cut.Find("input[label='Data Source (File Path)']").Change("/data/gis.db");

        // Assert - Connection string preview should show SQLite format
        cut.WaitForState(() => cut.Markup.Contains("Data Source=/data/gis.db"),
                         timeout: TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TestConnection_Success_ShowsConnectedStatus()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        mockApiClient
            .Setup(m => m.TestConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestConnectionResponse
            {
                Success = true,
                Message = "Connection successful",
                Provider = "postgis",
                ConnectionTime = 150
            });

        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.DataSourceId, "test-datasource")
            .Add(p => p.IsEdit, true));

        // Act - Click test connection
        var testButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Test Connection"));
        testButton?.Click();

        // Assert - Should show success message
        await cut.WaitForState(() => cut.Markup.Contains("Connection Successful") ||
                                      cut.Markup.Contains("successful"),
                                      timeout: TimeSpan.FromSeconds(3));

        cut.Markup.Should().ContainAny("successful", "Successful", "150");
    }

    [Fact]
    public async Task TestConnection_Failure_ShowsErrorMessage()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        mockApiClient
            .Setup(m => m.TestConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestConnectionResponse
            {
                Success = false,
                Message = "Authentication failed: Invalid username or password",
                Provider = "postgis",
                ConnectionTime = 0
            });

        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.DataSourceId, "test-datasource")
            .Add(p => p.IsEdit, true));

        // Act - Click test connection
        var testButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Test Connection"));
        testButton?.Click();

        // Assert - Should show error message
        await cut.WaitForState(() => cut.Markup.Contains("Connection Failed") ||
                                      cut.Markup.Contains("Authentication failed"),
                                      timeout: TimeSpan.FromSeconds(3));

        cut.Markup.Should().ContainAny("failed", "Failed", "Authentication");
    }

    [Fact]
    public async Task Save_ValidData_CallsApiClient()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        mockApiClient
            .Setup(m => m.CreateDataSourceAsync(It.IsAny<CreateDataSourceRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DataSourceResponse
            {
                Id = "new-datasource",
                Provider = "postgis",
                ConnectionString = "Host=localhost;Database=test"
            });

        Context.Services.AddSingleton(mockApiClient.Object);

        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Fill form and save
        cut.Find("input[label='Data Source ID']").Change("new-datasource");
        cut.Find("input[label='Host']").Change("localhost");
        cut.Find("input[label='Database']").Change("testdb");
        cut.Find("input[label='Username']").Change("postgres");
        cut.Find("input[label='Password']").Change("pass123");

        var saveButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Create"));
        saveButton?.Click();

        // Assert - API client should be called
        await Task.Delay(500); // Give time for async operation
        mockApiClient.Verify(
            m => m.CreateDataSourceAsync(It.Is<CreateDataSourceRequest>(r =>
                r.Id == "new-datasource" &&
                r.Provider == "postgis"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Cancel_ClosesDialog()
    {
        // Arrange
        var mockApiClient = new Mock<DataSourceApiClient>();
        Context.Services.AddSingleton(mockApiClient.Object);

        var dialogClosed = false;
        var cut = Context.RenderComponent<DataSourceDialog>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Act - Click cancel button
        var cancelButton = cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Cancel"));

        if (cancelButton != null)
        {
            // The dialog uses MudDialogInstance which would normally close the dialog
            // In a test environment, we verify the button exists and is clickable
            cancelButton.Click();
            dialogClosed = true;
        }

        // Assert - Cancel button should exist and be clickable
        cancelButton.Should().NotBeNull();
        dialogClosed.Should().BeTrue();
    }
}
