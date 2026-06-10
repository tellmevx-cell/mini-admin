namespace MiniAdmin.Application.Contracts.ProjectRuntimes;

public sealed record ProjectRuntimeOverviewDto(
    IReadOnlyList<ProjectRuntimeProjectDto> Projects,
    ProjectRuntimeSummaryDto Summary);

public sealed record ProjectRuntimeSummaryDto(
    int ProjectCount,
    int WorkspaceCount,
    int ServiceCount,
    int RunningServiceCount,
    int FailedServiceCount);

public sealed record ProjectRuntimeProjectDto(
    Guid Id,
    string Name,
    string Code,
    string RootPath,
    string? RepositoryUrl,
    string? Description,
    int Order,
    bool IsEnabled,
    IReadOnlyList<ProjectRuntimeWorkspaceDto> Workspaces);

public sealed record ProjectRuntimeWorkspaceDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Path,
    string? BranchName,
    string? ProfileName,
    bool IsDefault,
    bool IsEnabled,
    int Order,
    IReadOnlyList<ProjectRuntimeServiceDto> Services);

public sealed record ProjectRuntimeServiceDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string ServiceType,
    string Command,
    string Arguments,
    string WorkingDirectory,
    int? Port,
    string? HealthUrl,
    string? Url,
    string LogFileName,
    string? LogPath,
    string? BuildCommand,
    string? BuildArguments,
    string? BuildWorkingDirectory,
    string BuildLogFileName,
    string? BuildLogPath,
    string? BuildArtifactPath,
    bool IsEnabled,
    int Order,
    ProjectRuntimeServiceStateDto State,
    ProjectRuntimeBuildStateDto BuildState,
    ProjectRuntimeBuildHistoryDto? LatestBuild,
    ProjectRuntimeArtifactDto Artifact);

public sealed record ProjectRuntimeServiceStateDto(
    string Status,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    int? ExitCode,
    string? Message,
    bool PortOpen,
    bool HealthOk,
    DateTimeOffset CheckedAt);

public sealed record ProjectRuntimeBuildStateDto(
    string Status,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    int? ExitCode,
    string? Message,
    DateTimeOffset CheckedAt);

public sealed record ProjectRuntimeBuildHistoryDto(
    Guid Id,
    Guid ServiceId,
    string ServiceName,
    string Status,
    string CommandLine,
    string WorkingDirectory,
    string LogPath,
    string? ArtifactPath,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    long? DurationMilliseconds,
    int? ExitCode,
    string? Message);

public sealed record ProjectRuntimeArtifactDto(
    Guid ServiceId,
    string ServiceName,
    string? Path,
    bool Exists,
    string Type,
    long? SizeBytes,
    DateTimeOffset? LastModifiedAt);

public sealed record SaveProjectRuntimeProjectRequest(
    string Name,
    string Code,
    string RootPath,
    string? RepositoryUrl,
    string? Description,
    bool IsEnabled,
    int Order,
    IReadOnlyList<SaveProjectRuntimeWorkspaceRequest>? Workspaces);

public sealed record SaveProjectRuntimeWorkspaceRequest(
    Guid? Id,
    string Name,
    string Path,
    string? BranchName,
    string? ProfileName,
    bool IsDefault,
    bool IsEnabled,
    int Order,
    IReadOnlyList<SaveProjectRuntimeServiceRequest>? Services);

public sealed record SaveProjectRuntimeServiceRequest(
    Guid? Id,
    string Name,
    string ServiceType,
    string Command,
    string Arguments,
    string WorkingDirectory,
    int? Port,
    string? HealthUrl,
    string? Url,
    string? LogFileName,
    string? LogPath,
    string? BuildCommand,
    string? BuildArguments,
    string? BuildWorkingDirectory,
    string? BuildLogFileName,
    string? BuildLogPath,
    string? BuildArtifactPath,
    bool IsEnabled,
    int Order);

public sealed record ProjectRuntimeActionResultDto(
    Guid ServiceId,
    string ServiceName,
    string Status,
    int? ProcessId,
    string Message);

public sealed record ProjectRuntimeLogDto(
    Guid ServiceId,
    string ServiceName,
    string LogPath,
    IReadOnlyList<string> Lines,
    DateTimeOffset ReadAt);
