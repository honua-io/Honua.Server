using Honua.MapSDK.Models.Drone;

namespace Honua.Server.Core.DataOperations.Drone;

/// <summary>
/// Repository interface for drone data operations
/// </summary>
public interface IDroneDataRepository
{
    // Survey operations
    Task<DroneSurvey> CreateSurveyAsync(CreateDroneSurveyDto dto, CancellationToken cancellationToken = default);
    Task<DroneSurvey?> GetSurveyAsync(Guid surveyId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DroneSurveySummary>> ListSurveysAsync(int limit = 100, int offset = 0, CancellationToken cancellationToken = default);
    Task<bool> DeleteSurveyAsync(Guid surveyId, CancellationToken cancellationToken = default);
    Task UpdateSurveyStatisticsAsync(Guid surveyId, CancellationToken cancellationToken = default);

    // Point cloud operations
    IAsyncEnumerable<PointCloudPoint> QueryPointCloudAsync(
        Guid surveyId,
        PointCloudQueryOptions options,
        CancellationToken cancellationToken = default);

    Task<PointCloudStatistics> GetPointCloudStatisticsAsync(
        Guid surveyId,
        CancellationToken cancellationToken = default);

    Task<long> InsertPointCloudDataAsync(
        Guid surveyId,
        IEnumerable<PointCloudPoint> points,
        int lodLevel = 0,
        CancellationToken cancellationToken = default);

    // Orthomosaic operations
    Task<DroneOrthomosaic> CreateOrthomosaicAsync(CreateOrthomosaicDto dto, CancellationToken cancellationToken = default);
    Task<DroneOrthomosaic?> GetOrthomosaicAsync(Guid orthomosaicId, CancellationToken cancellationToken = default);
    Task<IEnumerable<DroneOrthomosaic>> ListOrthomosaicsAsync(Guid surveyId, CancellationToken cancellationToken = default);

    // 3D Model operations
    Task<Drone3DModel> Create3DModelAsync(Create3DModelDto dto, CancellationToken cancellationToken = default);
    Task<Drone3DModel?> Get3DModelAsync(Guid modelId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Drone3DModel>> List3DModelsAsync(Guid surveyId, CancellationToken cancellationToken = default);
}
