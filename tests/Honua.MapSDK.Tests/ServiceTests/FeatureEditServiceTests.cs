using FluentAssertions;
using Honua.MapSDK.Models;
using Honua.MapSDK.Services.Editing;
using Honua.MapSDK.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Honua.MapSDK.Tests.ServiceTests;

/// <summary>
/// Comprehensive tests for FeatureEditService
/// Tests cover: session management, CRUD operations, validation, undo/redo, batch operations, conflict detection
/// </summary>
public class FeatureEditServiceTests : IDisposable
{
    private readonly MockHttpMessageHandler _mockHttp;
    private readonly HttpClient _httpClient;
    private readonly FeatureEditService _service;

    public FeatureEditServiceTests()
    {
        _mockHttp = MockHttpMessageHandler.CreateJsonHandler("{}");
        _httpClient = new HttpClient(_mockHttp);
        _service = new FeatureEditService(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #region Test Data Helpers

    private Feature CreateTestFeature(string id = "test-feature")
    {
        return new Feature
        {
            Id = id,
            LayerId = "test-layer",
            GeometryType = "Point",
            Geometry = new { type = "Point", coordinates = new[] { -122.4194, 37.7749 } },
            Attributes = new Dictionary<string, object>
            {
                ["name"] = "Test Feature",
                ["status"] = "active",
                ["population"] = 1000
            }
        };
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public void StartSession_ShouldCreateNewSession()
    {
        // Arrange
        var sessionId = "session-1";

        // Act
        var session = _service.StartSession(sessionId);

        // Assert
        session.Should().NotBeNull();
        session.Id.Should().Be(sessionId);
        session.Configuration.Should().NotBeNull();
    }

    [Fact]
    public void StartSession_ShouldAcceptCustomConfiguration()
    {
        // Arrange
        var sessionId = "session-1";
        var config = new EditSessionConfiguration
        {
            AllowCreate = false,
            AllowUpdate = true,
            AllowDelete = false,
            RequireValidation = true
        };

        // Act
        var session = _service.StartSession(sessionId, config);

        // Assert
        session.Configuration.AllowCreate.Should().BeFalse();
        session.Configuration.AllowUpdate.Should().BeTrue();
        session.Configuration.AllowDelete.Should().BeFalse();
        session.Configuration.RequireValidation.Should().BeTrue();
    }

    [Fact]
    public void GetSession_ShouldReturnExistingSession()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId);

        // Act
        var session = _service.GetSession(sessionId);

        // Assert
        session.Should().NotBeNull();
        session!.Id.Should().Be(sessionId);
    }

    [Fact]
    public void GetSession_ShouldReturnNull_ForNonExistentSession()
    {
        // Act
        var session = _service.GetSession("non-existent");

        // Assert
        session.Should().BeNull();
    }

    [Fact]
    public void EndSession_ShouldCloseSession()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        // Act
        _service.EndSession(sessionId, saveChanges: false);

        // Assert
        session.IsActive.Should().BeFalse();
    }

    [Fact]
    public void EndSession_ShouldSaveChanges_WhenRequested()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        // Add an unsynced operation
        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature(),
            IsSynced = false
        });

        // Act
        _service.EndSession(sessionId, saveChanges: true);

        // Assert
        session.IsActive.Should().BeFalse();
    }

    #endregion

    #region Validation Rules Tests

    [Fact]
    public void SetValidationRules_ShouldStoreRules()
    {
        // Arrange
        var layerId = "test-layer";
        var rules = new List<ValidationRule>
        {
            new ValidationRule
            {
                FieldName = "name",
                Type = ValidationType.String,
                IsRequired = true,
                MaxLength = 100
            }
        };

        // Act
        _service.SetValidationRules(layerId, rules);
        var retrieved = _service.GetValidationRules(layerId);

        // Assert
        retrieved.Should().HaveCount(1);
        retrieved[0].FieldName.Should().Be("name");
    }

    [Fact]
    public void GetValidationRules_ShouldReturnEmptyList_WhenNoRulesSet()
    {
        // Act
        var rules = _service.GetValidationRules("non-existent-layer");

        // Assert
        rules.Should().NotBeNull();
        rules.Should().BeEmpty();
    }

    [Fact]
    public void ValidateFeature_ShouldReturnErrors_ForInvalidFeature()
    {
        // Arrange
        var feature = CreateTestFeature();
        var rules = new List<ValidationRule>
        {
            new ValidationRule
            {
                FieldName = "email",
                Type = ValidationType.Email,
                IsRequired = true
            }
        };

        _service.SetValidationRules(feature.LayerId!, rules);

        // Act
        var errors = _service.ValidateFeature(feature);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Field == "email");
    }

    [Fact]
    public void ValidateFeature_ShouldReturnEmpty_ForValidFeature()
    {
        // Arrange
        var feature = CreateTestFeature();
        var rules = new List<ValidationRule>
        {
            new ValidationRule
            {
                FieldName = "name",
                Type = ValidationType.String,
                IsRequired = true
            }
        };

        _service.SetValidationRules(feature.LayerId!, rules);

        // Act
        var errors = _service.ValidateFeature(feature);

        // Assert
        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateGeometry_ShouldReturnError_ForNullGeometry()
    {
        // Arrange
        var feature = CreateTestFeature();
        feature.Geometry = null!;

        // Act
        var errors = _service.ValidateGeometry(feature);

        // Assert
        errors.Should().NotBeEmpty();
        errors.Should().Contain(e => e.Message.Contains("Geometry cannot be null"));
    }

    [Fact]
    public void ValidateGeometry_ShouldReturnEmpty_ForValidGeometry()
    {
        // Arrange
        var feature = CreateTestFeature();

        // Act
        var errors = _service.ValidateGeometry(feature);

        // Assert
        errors.Should().BeEmpty();
    }

    #endregion

    #region Create Feature Tests

    [Fact]
    public async Task CreateFeatureAsync_ShouldCreateFeature_WithoutValidation()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId, new EditSessionConfiguration
        {
            AllowCreate = true,
            RequireValidation = false
        });

        var feature = CreateTestFeature();

        // Act
        var result = await _service.CreateFeatureAsync(sessionId, feature);

        // Assert
        result.Should().NotBeNull();
        session.Operations.Should().HaveCount(1);
        session.Operations[0].Type.Should().Be(EditOperationType.Create);
    }

    [Fact]
    public async Task CreateFeatureAsync_ShouldThrowException_WhenSessionNotFound()
    {
        // Arrange
        var feature = CreateTestFeature();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.CreateFeatureAsync("non-existent", feature));
    }

    [Fact]
    public async Task CreateFeatureAsync_ShouldThrowException_WhenCreateNotAllowed()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId, new EditSessionConfiguration { AllowCreate = false });
        var feature = CreateTestFeature();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.CreateFeatureAsync(sessionId, feature));
    }

    [Fact]
    public async Task CreateFeatureAsync_ShouldValidate_WhenValidationRequired()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId, new EditSessionConfiguration
        {
            AllowCreate = true,
            RequireValidation = true
        });

        var feature = CreateTestFeature();
        var rules = new List<ValidationRule>
        {
            new ValidationRule
            {
                FieldName = "required_field",
                Type = ValidationType.String,
                IsRequired = true
            }
        };

        _service.SetValidationRules(feature.LayerId!, rules);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(async () =>
            await _service.CreateFeatureAsync(sessionId, feature));
    }

    [Fact]
    public async Task CreateFeatureAsync_ShouldCallAPI_WhenEndpointProvided()
    {
        // Arrange
        var mockHttp = MockHttpMessageHandler.CreateJsonHandler(JsonSerializer.Serialize(CreateTestFeature()));
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        service.StartSession(sessionId, new EditSessionConfiguration
        {
            AllowCreate = true,
            RequireValidation = false
        });

        var feature = CreateTestFeature();

        // Act
        await service.CreateFeatureAsync(sessionId, feature, "/api/features");

        // Assert
        mockHttp.Requests.Should().HaveCount(1);
        mockHttp.Requests[0].Method.Should().Be(HttpMethod.Post);
    }

    #endregion

    #region Update Feature Tests

    [Fact]
    public async Task UpdateFeatureAsync_ShouldUpdateFeature()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId, new EditSessionConfiguration
        {
            AllowUpdate = true,
            RequireValidation = false
        });

        var feature = CreateTestFeature();
        var previousState = feature.Clone();
        feature.Attributes["name"] = "Updated Name";

        // Act
        var result = await _service.UpdateFeatureAsync(sessionId, feature, previousState);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be(1); // Version incremented
        session.Operations.Should().HaveCount(1);
        session.Operations[0].Type.Should().Be(EditOperationType.Update);
    }

    [Fact]
    public async Task UpdateFeatureAsync_ShouldThrowException_WhenSessionNotFound()
    {
        // Arrange
        var feature = CreateTestFeature();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UpdateFeatureAsync("non-existent", feature));
    }

    [Fact]
    public async Task UpdateFeatureAsync_ShouldThrowException_WhenUpdateNotAllowed()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId, new EditSessionConfiguration { AllowUpdate = false });
        var feature = CreateTestFeature();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UpdateFeatureAsync(sessionId, feature));
    }

    [Fact]
    public async Task UpdateFeatureAsync_ShouldCallAPI_WhenEndpointProvided()
    {
        // Arrange
        var mockHttp = MockHttpMessageHandler.CreateJsonHandler(JsonSerializer.Serialize(CreateTestFeature()));
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        service.StartSession(sessionId, new EditSessionConfiguration
        {
            AllowUpdate = true,
            RequireValidation = false
        });

        var feature = CreateTestFeature();

        // Act
        await service.UpdateFeatureAsync(sessionId, feature, null, "/api/features");

        // Assert
        mockHttp.Requests.Should().HaveCount(1);
        mockHttp.Requests[0].Method.Should().Be(HttpMethod.Put);
    }

    #endregion

    #region Delete Feature Tests

    [Fact]
    public async Task DeleteFeatureAsync_ShouldDeleteFeature()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId, new EditSessionConfiguration
        {
            AllowDelete = true
        });

        var feature = CreateTestFeature();

        // Act
        await _service.DeleteFeatureAsync(sessionId, feature);

        // Assert
        session.Operations.Should().HaveCount(1);
        session.Operations[0].Type.Should().Be(EditOperationType.Delete);
        session.Operations[0].PreviousState.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteFeatureAsync_ShouldThrowException_WhenSessionNotFound()
    {
        // Arrange
        var feature = CreateTestFeature();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.DeleteFeatureAsync("non-existent", feature));
    }

    [Fact]
    public async Task DeleteFeatureAsync_ShouldThrowException_WhenDeleteNotAllowed()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId, new EditSessionConfiguration { AllowDelete = false });
        var feature = CreateTestFeature();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.DeleteFeatureAsync(sessionId, feature));
    }

    [Fact]
    public async Task DeleteFeatureAsync_ShouldCallAPI_WhenEndpointProvided()
    {
        // Arrange
        var mockHttp = MockHttpMessageHandler.CreateJsonHandler("{}");
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        service.StartSession(sessionId, new EditSessionConfiguration { AllowDelete = true });

        var feature = CreateTestFeature();

        // Act
        await service.DeleteFeatureAsync(sessionId, feature, "/api/features");

        // Assert
        mockHttp.Requests.Should().HaveCount(1);
        mockHttp.Requests[0].Method.Should().Be(HttpMethod.Delete);
    }

    #endregion

    #region Undo/Redo Tests

    [Fact]
    public void Undo_ShouldReturnLastOperation()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature()
        });

        // Act
        var operation = _service.Undo(sessionId);

        // Assert
        operation.Should().NotBeNull();
        operation!.Id.Should().Be("op-1");
        session.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Undo_ShouldReturnNull_WhenNoOperations()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId);

        // Act
        var operation = _service.Undo(sessionId);

        // Assert
        operation.Should().BeNull();
    }

    [Fact]
    public void Redo_ShouldRestoreOperation()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature()
        });

        _service.Undo(sessionId);

        // Act
        var operation = _service.Redo(sessionId);

        // Assert
        operation.Should().NotBeNull();
        operation!.Id.Should().Be("op-1");
        session.CanUndo.Should().BeTrue();
        session.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Redo_ShouldReturnNull_WhenNothingToRedo()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId);

        // Act
        var operation = _service.Redo(sessionId);

        // Assert
        operation.Should().BeNull();
    }

    [Fact]
    public void UndoRedo_ShouldWorkWithMultipleOperations()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        session.AddOperation(new EditOperation { Id = "op-1", Type = EditOperationType.Create, Feature = CreateTestFeature("f1") });
        session.AddOperation(new EditOperation { Id = "op-2", Type = EditOperationType.Create, Feature = CreateTestFeature("f2") });
        session.AddOperation(new EditOperation { Id = "op-3", Type = EditOperationType.Create, Feature = CreateTestFeature("f3") });

        // Act & Assert
        _service.Undo(sessionId)!.Id.Should().Be("op-3");
        _service.Undo(sessionId)!.Id.Should().Be("op-2");
        _service.Redo(sessionId)!.Id.Should().Be("op-2");
        _service.Undo(sessionId)!.Id.Should().Be("op-2");
        _service.Undo(sessionId)!.Id.Should().Be("op-1");
    }

    #endregion

    #region Save Session Tests

    [Fact]
    public async Task SaveSessionAsync_ShouldReturnSuccess_WhenNoChanges()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId);

        // Act
        var result = await _service.SaveSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("No changes to save");
    }

    [Fact]
    public async Task SaveSessionAsync_ShouldThrowException_WhenSessionNotFound()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.SaveSessionAsync("non-existent"));
    }

    [Fact]
    public async Task SaveSessionAsync_ShouldMarkOperationsAsSynced_WhenNoEndpoint()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature(),
            IsSynced = false
        });

        // Act
        var result = await _service.SaveSessionAsync(sessionId);

        // Assert
        result.Success.Should().BeTrue();
        result.SavedCount.Should().Be(1);
        session.Operations[0].IsSynced.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSessionAsync_ShouldCallBatchEndpoint_WhenProvided()
    {
        // Arrange
        var mockHttp = MockHttpMessageHandler.CreateJsonHandler("{}");
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        var session = service.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature(),
            IsSynced = false
        });

        // Act
        var result = await service.SaveSessionAsync(sessionId, "/api/features");

        // Assert
        mockHttp.Requests.Should().HaveCount(1);
        mockHttp.Requests[0].RequestUri!.ToString().Should().Contain("/batch");
    }

    [Fact]
    public async Task SaveSessionAsync_ShouldHandleServerError()
    {
        // Arrange
        var mockHttp = MockHttpMessageHandler.CreateErrorHandler(HttpStatusCode.InternalServerError);
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        var session = service.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature(),
            IsSynced = false
        });

        // Act
        var result = await service.SaveSessionAsync(sessionId, "/api/features");

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    #endregion

    #region Rollback Tests

    [Fact]
    public void RollbackSession_ShouldClearOperations()
    {
        // Arrange
        var sessionId = "session-1";
        var session = _service.StartSession(sessionId);

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Create,
            Feature = CreateTestFeature()
        });

        // Act
        _service.RollbackSession(sessionId);

        // Assert
        session.Operations.Should().BeEmpty();
    }

    [Fact]
    public void RollbackSession_ShouldNotThrow_WhenSessionNotFound()
    {
        // Act & Assert - Should not throw
        _service.RollbackSession("non-existent");
        Assert.True(true);
    }

    #endregion

    #region Conflict Detection Tests

    [Fact]
    public async Task DetectConflictsAsync_ShouldReturnEmpty_WhenConflictDetectionDisabled()
    {
        // Arrange
        var sessionId = "session-1";
        _service.StartSession(sessionId, new EditSessionConfiguration
        {
            EnableConflictDetection = false
        });

        // Act
        var conflicts = await _service.DetectConflictsAsync(sessionId, "/api/features");

        // Assert
        conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectConflictsAsync_ShouldDetectVersionMismatch()
    {
        // Arrange
        var serverFeature = CreateTestFeature();
        serverFeature.Version = 5;

        var mockHttp = MockHttpMessageHandler.CreateJsonHandler(JsonSerializer.Serialize(serverFeature));
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        var session = service.StartSession(sessionId, new EditSessionConfiguration
        {
            EnableConflictDetection = true
        });

        var localFeature = CreateTestFeature();
        localFeature.Version = 3;

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Update,
            Feature = localFeature,
            IsSynced = false
        });

        // Act
        var conflicts = await service.DetectConflictsAsync(sessionId, "/api/features");

        // Assert
        conflicts.Should().HaveCount(1);
        conflicts[0].ConflictType.Should().Be(ConflictType.VersionMismatch);
        conflicts[0].LocalVersion.Should().Be(3);
        conflicts[0].ServerVersion.Should().Be(5);
    }

    [Fact]
    public async Task DetectConflictsAsync_ShouldReturnEmpty_WhenNoConflicts()
    {
        // Arrange
        var serverFeature = CreateTestFeature();
        serverFeature.Version = 3;

        var mockHttp = MockHttpMessageHandler.CreateJsonHandler(JsonSerializer.Serialize(serverFeature));
        using var httpClient = new HttpClient(mockHttp);
        var service = new FeatureEditService(httpClient);

        var sessionId = "session-1";
        var session = service.StartSession(sessionId, new EditSessionConfiguration
        {
            EnableConflictDetection = true
        });

        var localFeature = CreateTestFeature();
        localFeature.Version = 3;

        session.AddOperation(new EditOperation
        {
            Id = "op-1",
            Type = EditOperationType.Update,
            Feature = localFeature,
            IsSynced = false
        });

        // Act
        var conflicts = await service.DetectConflictsAsync(sessionId, "/api/features");

        // Assert
        conflicts.Should().BeEmpty();
    }

    #endregion
}
