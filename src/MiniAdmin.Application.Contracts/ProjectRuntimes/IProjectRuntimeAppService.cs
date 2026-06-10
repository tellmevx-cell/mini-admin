namespace MiniAdmin.Application.Contracts.ProjectRuntimes;

public interface IProjectRuntimeAppService
{
    Task<ProjectRuntimeOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<ProjectRuntimeProjectDto> CreateProjectAsync(
        SaveProjectRuntimeProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeProjectDto?> UpdateProjectAsync(
        Guid projectId,
        SaveProjectRuntimeProjectRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectRuntimeActionResultDto>> StartWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectRuntimeActionResultDto>> StopWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeActionResultDto> StartServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeActionResultDto> StopServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeActionResultDto> RestartServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeActionResultDto> BuildServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeLogDto> GetServiceLogsAsync(
        Guid serviceId,
        int lines = 200,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeLogDto> GetServiceBuildLogsAsync(
        Guid serviceId,
        int lines = 200,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectRuntimeBuildHistoryDto>> GetServiceBuildHistoryAsync(
        Guid serviceId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeArtifactDto> GetServiceArtifactAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);

    Task<ProjectRuntimeActionResultDto> OpenServiceArtifactAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default);
}
