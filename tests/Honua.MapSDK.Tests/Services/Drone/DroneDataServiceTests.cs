using Honua.MapSDK.Models.Drone;
using Honua.MapSDK.Services.Drone;
using Honua.Server.Core.DataOperations.Drone;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Honua.MapSDK.Tests.Services.Drone;

public class DroneDataServiceTests
{
    private readonly Mock<IDroneDataRepository> _mockRepository;
    private readonly Mock<ILogger<DroneDataService>> _mockLogger;
    private readonly DroneDataService _service;

    public DroneDataServiceTests()
    {
        _mockRepository = new Mock<IDroneDataRepository>();
        _mockLogger = new Mock<ILogger<DroneDataService>>();
        _service = new DroneDataService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task CreateSurveyAsync_ValidDto_ReturnsSurvey()
    {
        // Arrange
        var dto = new CreateDroneSurveyDto
        {
            Name = "Test Survey",
            SurveyDate = DateTime.UtcNow,
            FlightAltitudeM = 100,
            GroundResolutionCm = 2.5
        };

        var expectedSurvey = new DroneSurvey
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            SurveyDate = dto.SurveyDate,
            FlightAltitudeM = dto.FlightAltitudeM,
            GroundResolutionCm = dto.GroundResolutionCm
        };

        _mockRepository
            .Setup(r => r.CreateSurveyAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSurvey);

        // Act
        var result = await _service.CreateSurveyAsync(dto);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedSurvey.Id, result.Id);
        Assert.Equal(expectedSurvey.Name, result.Name);
        _mockRepository.Verify(
            r => r.CreateSurveyAsync(dto, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSurveyAsync_ExistingSurvey_ReturnsSurvey()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var expectedSurvey = new DroneSurvey
        {
            Id = surveyId,
            Name = "Test Survey",
            SurveyDate = DateTime.UtcNow
        };

        _mockRepository
            .Setup(r => r.GetSurveyAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSurvey);

        // Act
        var result = await _service.GetSurveyAsync(surveyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(surveyId, result.Id);
    }

    [Fact]
    public async Task GetSurveyAsync_NonExistingSurvey_ReturnsNull()
    {
        // Arrange
        var surveyId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetSurveyAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DroneSurvey?)null);

        // Act
        var result = await _service.GetSurveyAsync(surveyId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListSurveysAsync_ReturnsListOfSurveys()
    {
        // Arrange
        var expectedSurveys = new List<DroneSurveySummary>
        {
            new() { Id = Guid.NewGuid(), Name = "Survey 1", SurveyDate = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Survey 2", SurveyDate = DateTime.UtcNow }
        };

        _mockRepository
            .Setup(r => r.ListSurveysAsync(100, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSurveys);

        // Act
        var result = await _service.ListSurveysAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
    }

    [Fact]
    public async Task DeleteSurveyAsync_ExistingSurvey_ReturnsTrue()
    {
        // Arrange
        var surveyId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteSurveyAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteSurveyAsync(surveyId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task DeleteSurveyAsync_NonExistingSurvey_ReturnsFalse()
    {
        // Arrange
        var surveyId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.DeleteSurveyAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _service.DeleteSurveyAsync(surveyId);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetSurveyStatisticsAsync_ValidSurvey_ReturnsStatistics()
    {
        // Arrange
        var surveyId = Guid.NewGuid();
        var survey = new DroneSurvey { Id = surveyId, Name = "Test" };
        var pcStats = new PointCloudStatistics { TotalPoints = 1000000 };
        var orthomosaics = new List<DroneOrthomosaic>
        {
            new() { Id = Guid.NewGuid(), SurveyId = surveyId }
        };
        var models = new List<Drone3DModel>();

        _mockRepository
            .Setup(r => r.GetSurveyAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(survey);

        _mockRepository
            .Setup(r => r.GetPointCloudStatisticsAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pcStats);

        _mockRepository
            .Setup(r => r.ListOrthomosaicsAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orthomosaics);

        _mockRepository
            .Setup(r => r.List3DModelsAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(models);

        // Act
        var result = await _service.GetSurveyStatisticsAsync(surveyId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(survey.Id, result.Survey.Id);
        Assert.Equal(1000000, result.PointCloudStats.TotalPoints);
        Assert.Equal(1, result.OrthomosaicCount);
        Assert.Equal(0, result.Model3DCount);
    }

    [Fact]
    public async Task GetSurveyStatisticsAsync_NonExistingSurvey_ThrowsKeyNotFoundException()
    {
        // Arrange
        var surveyId = Guid.NewGuid();

        _mockRepository
            .Setup(r => r.GetSurveyAsync(surveyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DroneSurvey?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _service.GetSurveyStatisticsAsync(surveyId));
    }

    [Fact]
    public async Task ImportSurveyAsync_ValidRequest_CreatesSurvey()
    {
        // Arrange
        var request = new ImportDroneSurveyRequest
        {
            Name = "Imported Survey",
            SurveyDate = DateTime.UtcNow,
            FlightAltitudeM = 120,
            GroundResolutionCm = 3.0
        };

        var expectedSurvey = new DroneSurvey
        {
            Id = Guid.NewGuid(),
            Name = request.Name
        };

        _mockRepository
            .Setup(r => r.CreateSurveyAsync(It.IsAny<CreateDroneSurveyDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSurvey);

        // Act
        var result = await _service.ImportSurveyAsync(request);

        // Assert
        Assert.Equal(expectedSurvey.Id, result);
        _mockRepository.Verify(
            r => r.CreateSurveyAsync(It.IsAny<CreateDroneSurveyDto>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
