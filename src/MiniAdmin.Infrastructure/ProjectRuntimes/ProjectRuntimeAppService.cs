using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using MiniAdmin.Application.Contracts.ProjectRuntimes;

namespace MiniAdmin.Infrastructure.ProjectRuntimes;

public sealed class ProjectRuntimeAppService(IHostEnvironment hostEnvironment) : IProjectRuntimeAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim syncLock = new(1, 1);
    private readonly SemaphoreSlim historyLock = new(1, 1);
    private readonly Dictionary<Guid, RuntimeProcessState> runtimeStates = [];
    private readonly Dictionary<Guid, RuntimeProcessState> buildStates = [];
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(2)
    };

    public async Task<ProjectRuntimeOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            var configuration = await LoadConfigurationAsync(cancellationToken);
            var projects = new List<ProjectRuntimeProjectDto>();
            var runningCount = 0;
            var failedCount = 0;
            var serviceCount = 0;
            var workspaceCount = 0;
            var buildHistory = await LoadBuildHistoryAsync(cancellationToken);

            foreach (var project in configuration.Projects.OrderBy(x => x.Order).ThenBy(x => x.Name))
            {
                var workspaces = new List<ProjectRuntimeWorkspaceDto>();
                foreach (var workspace in project.Workspaces.OrderBy(x => x.Order).ThenBy(x => x.Name))
                {
                    workspaceCount++;
                    var services = new List<ProjectRuntimeServiceDto>();
                    foreach (var service in workspace.Services.OrderBy(x => x.Order).ThenBy(x => x.Name))
                    {
                        serviceCount++;
                        var state = await BuildServiceStateAsync(workspace, service, cancellationToken);
                        var buildState = BuildServiceBuildState(service);
                        var latestBuild = FindLatestBuild(buildHistory, service.Id);
                        var artifact = BuildArtifactDto(workspace, service);
                        if (state.Status == "Running")
                        {
                            runningCount++;
                        }
                        else if (state.Status == "Failed")
                        {
                            failedCount++;
                        }

                        services.Add(ToDto(workspace, service, state, buildState, latestBuild, artifact));
                    }

                    workspaces.Add(ToDto(project.Id, workspace, services));
                }

                projects.Add(ToDto(project, workspaces));
            }

            return new ProjectRuntimeOverviewDto(
                projects,
                new ProjectRuntimeSummaryDto(
                    projects.Count,
                    workspaceCount,
                    serviceCount,
                    runningCount,
                    failedCount));
        }
        finally
        {
            syncLock.Release();
        }
    }

    public async Task<ProjectRuntimeProjectDto> CreateProjectAsync(
        SaveProjectRuntimeProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            var configuration = await LoadConfigurationAsync(cancellationToken);
            var project = BuildProject(request, Guid.NewGuid());
            configuration.Projects.Add(project);
            await SaveConfigurationAsync(configuration, cancellationToken);

            return ToDto(project, project.Workspaces
                .OrderBy(x => x.Order)
                .Select(workspace => ToDto(
                    project.Id,
                    workspace,
                    workspace.Services
                        .OrderBy(service => service.Order)
                        .Select(service => ToDto(
                            workspace,
                            service,
                            new ProjectRuntimeServiceStateDto(
                                "Stopped",
                                null,
                                null,
                                null,
                                "服务尚未启动.",
                                false,
                                false,
                                DateTimeOffset.UtcNow),
                            BuildServiceBuildState(service),
                            null,
                            BuildArtifactDto(workspace, service)))
                        .ToArray()))
                .ToArray());
        }
        finally
        {
            syncLock.Release();
        }
    }

    public async Task<ProjectRuntimeProjectDto?> UpdateProjectAsync(
        Guid projectId,
        SaveProjectRuntimeProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            var configuration = await LoadConfigurationAsync(cancellationToken);
            var index = configuration.Projects.FindIndex(x => x.Id == projectId);
            if (index < 0)
            {
                return null;
            }

            configuration.Projects[index] = BuildProject(request, projectId);
            await SaveConfigurationAsync(configuration, cancellationToken);

            var project = configuration.Projects[index];
            return ToDto(project, project.Workspaces
                .Select(workspace => ToDto(
                    project.Id,
                    workspace,
                    workspace.Services.Select(service => ToDto(
                        workspace,
                        service,
                        new ProjectRuntimeServiceStateDto(
                            "Stopped",
                            null,
                            null,
                            null,
                            "配置已更新.",
                            false,
                            false,
                            DateTimeOffset.UtcNow),
                        BuildServiceBuildState(service),
                        null,
                        BuildArtifactDto(workspace, service))).ToArray()))
                .ToArray());
        }
        finally
        {
            syncLock.Release();
        }
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            var configuration = await LoadConfigurationAsync(cancellationToken);
            var project = configuration.Projects.SingleOrDefault(x => x.Id == projectId);
            if (project is null)
            {
                return false;
            }

            var serviceIds = project.Workspaces.SelectMany(x => x.Services).Select(x => x.Id).ToHashSet();
            foreach (var serviceId in serviceIds)
            {
                if (runtimeStates.TryGetValue(serviceId, out var state) &&
                    state.Process is { HasExited: false } process)
                {
                    process.Kill(entireProcessTree: true);
                }

                runtimeStates.Remove(serviceId);
                buildStates.Remove(serviceId);
            }

            configuration.Projects.Remove(project);
            await SaveConfigurationAsync(configuration, cancellationToken);
            return true;
        }
        finally
        {
            syncLock.Release();
        }
    }

    public async Task<IReadOnlyList<ProjectRuntimeActionResultDto>> StartWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProjectRuntimeActionResultDto>();
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var workspace = FindWorkspace(configuration, workspaceId);
        if (workspace is null)
        {
            return results;
        }

        foreach (var service in workspace.Services.Where(x => x.IsEnabled).OrderBy(x => x.Order))
        {
            results.Add(await StartServiceCoreAsync(workspace, service, cancellationToken));
        }

        return results;
    }

    public async Task<IReadOnlyList<ProjectRuntimeActionResultDto>> StopWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProjectRuntimeActionResultDto>();
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var workspace = FindWorkspace(configuration, workspaceId);
        if (workspace is null)
        {
            return results;
        }

        foreach (var service in workspace.Services.OrderByDescending(x => x.Order))
        {
            results.Add(await StopServiceCoreAsync(service, cancellationToken));
        }

        return results;
    }

    public async Task<ProjectRuntimeActionResultDto> StartServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        return located is null
            ? new ProjectRuntimeActionResultDto(serviceId, string.Empty, "NotFound", null, "服务不存在.")
            : await StartServiceCoreAsync(located.Value.Workspace, located.Value.Service, cancellationToken);
    }

    public async Task<ProjectRuntimeActionResultDto> StopServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        return located is null
            ? new ProjectRuntimeActionResultDto(serviceId, string.Empty, "NotFound", null, "服务不存在.")
            : await StopServiceCoreAsync(located.Value.Service, cancellationToken);
    }

    public async Task<ProjectRuntimeActionResultDto> RestartServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        if (located is null)
        {
            return new ProjectRuntimeActionResultDto(serviceId, string.Empty, "NotFound", null, "服务不存在.");
        }

        _ = await StopServiceCoreAsync(located.Value.Service, cancellationToken);
        return await StartServiceCoreAsync(located.Value.Workspace, located.Value.Service, cancellationToken);
    }

    public async Task<ProjectRuntimeActionResultDto> BuildServiceAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        return located is null
            ? new ProjectRuntimeActionResultDto(serviceId, string.Empty, "NotFound", null, "服务不存在.")
            : await BuildServiceCoreAsync(located.Value.Workspace, located.Value.Service, cancellationToken);
    }

    public async Task<ProjectRuntimeLogDto> GetServiceLogsAsync(
        Guid serviceId,
        int lines = 200,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        if (located is null)
        {
            return new ProjectRuntimeLogDto(serviceId, string.Empty, string.Empty, [], DateTimeOffset.UtcNow);
        }

        var logPath = GetLogPath(located.Value.Workspace, located.Value.Service);
        return new ProjectRuntimeLogDto(
            serviceId,
            located.Value.Service.Name,
            logPath,
            await ReadLastLinesAsync(logPath, Math.Clamp(lines, 20, 1000), cancellationToken),
            DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<ProjectRuntimeBuildHistoryDto>> GetServiceBuildHistoryAsync(
        Guid serviceId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var history = await LoadBuildHistoryAsync(cancellationToken);
        return history.Records
            .Where(x => x.ServiceId == serviceId)
            .OrderByDescending(x => x.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(ToDto)
            .ToArray();
    }

    public async Task<ProjectRuntimeArtifactDto> GetServiceArtifactAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        return located is null
            ? new ProjectRuntimeArtifactDto(serviceId, string.Empty, null, false, "Missing", null, null)
            : BuildArtifactDto(located.Value.Workspace, located.Value.Service);
    }

    public async Task<ProjectRuntimeActionResultDto> OpenServiceArtifactAsync(
        Guid serviceId,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        if (located is null)
        {
            return new ProjectRuntimeActionResultDto(serviceId, string.Empty, "NotFound", null, "服务不存在.");
        }

        var artifact = BuildArtifactDto(located.Value.Workspace, located.Value.Service);
        if (!artifact.Exists || string.IsNullOrWhiteSpace(artifact.Path))
        {
            return ActionResult(located.Value.Service, "Failed", null, "构建产物不存在.");
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                var arguments = artifact.Type == "File"
                    ? $"/select,\"{artifact.Path}\""
                    : $"\"{artifact.Path}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", arguments)
                {
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo(artifact.Path)
                {
                    UseShellExecute = true
                });
            }

            return ActionResult(located.Value.Service, "Succeeded", null, "已打开构建产物.");
        }
        catch (Exception exception)
        {
            return ActionResult(located.Value.Service, "Failed", null, exception.Message);
        }
    }

    public async Task<ProjectRuntimeLogDto> GetServiceBuildLogsAsync(
        Guid serviceId,
        int lines = 200,
        CancellationToken cancellationToken = default)
    {
        var configuration = await LoadConfigurationAsync(cancellationToken);
        var located = FindService(configuration, serviceId);
        if (located is null)
        {
            return new ProjectRuntimeLogDto(serviceId, string.Empty, string.Empty, [], DateTimeOffset.UtcNow);
        }

        var logPath = GetBuildLogPath(located.Value.Workspace, located.Value.Service);
        return new ProjectRuntimeLogDto(
            serviceId,
            located.Value.Service.Name,
            logPath,
            await ReadLastLinesAsync(logPath, Math.Clamp(lines, 20, 1000), cancellationToken),
            DateTimeOffset.UtcNow);
    }

    private async Task<ProjectRuntimeActionResultDto> StartServiceCoreAsync(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service,
        CancellationToken cancellationToken)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            if (!service.IsEnabled)
            {
                return ActionResult(service, "Disabled", null, "服务未启用.");
            }

            if (runtimeStates.TryGetValue(service.Id, out var runningState) &&
                runningState.Process is { HasExited: false } runningProcess)
            {
                return ActionResult(service, "Running", runningProcess.Id, "服务已在本面板中运行.");
            }

            if (service.Port.HasValue && await IsPortOpenAsync(service.Port.Value, cancellationToken))
            {
                return ActionResult(service, "External", null, $"端口 {service.Port.Value} 已被占用，未重复启动.");
            }

            var workingDirectory = ResolvePath(workspace.Path, service.WorkingDirectory);
            if (!Directory.Exists(workingDirectory))
            {
                return ActionResult(service, "Failed", null, $"工作目录不存在：{workingDirectory}");
            }

            if (string.IsNullOrWhiteSpace(service.Command))
            {
                return ActionResult(service, "Failed", null, "启动命令不能为空.");
            }

            var logPath = GetLogPath(workspace, service);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.AppendAllTextAsync(
                logPath,
                $"{Environment.NewLine}[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 启动 {service.Name}{Environment.NewLine}",
                cancellationToken);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveExecutable(service.Command),
                    Arguments = service.Arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            process.OutputDataReceived += (_, args) => WriteLogLine(writer, args.Data);
            process.ErrorDataReceived += (_, args) => WriteLogLine(writer, args.Data);
            process.Exited += (_, _) =>
            {
                WriteLogLine(writer, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 进程退出，ExitCode={process.ExitCode}");
                writer.Dispose();
            };

            if (!process.Start())
            {
                writer.Dispose();
                return ActionResult(service, "Failed", null, "进程启动失败.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            runtimeStates[service.Id] = new RuntimeProcessState(process, DateTimeOffset.UtcNow, null);

            return ActionResult(service, "Running", process.Id, "服务启动命令已执行.");
        }
        catch (Exception exception)
        {
            runtimeStates[service.Id] = new RuntimeProcessState(null, null, exception.Message);
            return ActionResult(service, "Failed", null, exception.Message);
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task<ProjectRuntimeActionResultDto> StopServiceCoreAsync(
        ProjectRuntimeServiceConfiguration service,
        CancellationToken cancellationToken)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            if (!runtimeStates.TryGetValue(service.Id, out var state) || state.Process is null)
            {
                return ActionResult(service, "Stopped", null, "服务未由本面板启动，无需停止.");
            }

            if (state.Process.HasExited)
            {
                runtimeStates.Remove(service.Id);
                return ActionResult(service, "Stopped", state.Process.Id, "服务进程已退出.");
            }

            state.Process.Kill(entireProcessTree: true);
            await state.Process.WaitForExitAsync(cancellationToken);
            runtimeStates.Remove(service.Id);
            return ActionResult(service, "Stopped", state.Process.Id, "服务已停止.");
        }
        catch (Exception exception)
        {
            return ActionResult(service, "Failed", null, exception.Message);
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task<ProjectRuntimeActionResultDto> BuildServiceCoreAsync(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service,
        CancellationToken cancellationToken)
    {
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            if (!service.IsEnabled)
            {
                return ActionResult(service, "Disabled", null, "服务未启用.");
            }

            if (buildStates.TryGetValue(service.Id, out var runningState) &&
                runningState.Process is { HasExited: false } runningProcess)
            {
                return ActionResult(service, "Running", runningProcess.Id, "打包任务正在运行.");
            }

            if (string.IsNullOrWhiteSpace(service.BuildCommand))
            {
                return ActionResult(service, "Failed", null, "未配置打包命令.");
            }

            var buildWorkingDirectory = string.IsNullOrWhiteSpace(service.BuildWorkingDirectory)
                ? service.WorkingDirectory
                : service.BuildWorkingDirectory;
            var workingDirectory = ResolvePath(workspace.Path, buildWorkingDirectory);
            if (!Directory.Exists(workingDirectory))
            {
                return ActionResult(service, "Failed", null, $"打包目录不存在：{workingDirectory}");
            }

            var logPath = GetBuildLogPath(workspace, service);
            var artifactPath = GetArtifactPath(workspace, service);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            await File.AppendAllTextAsync(
                logPath,
                $"{Environment.NewLine}[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 开始打包 {service.Name}{Environment.NewLine}",
                cancellationToken);
            var historyId = await AddBuildHistoryAsync(
                service,
                workingDirectory,
                logPath,
                artifactPath,
                cancellationToken);

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ResolveExecutable(service.BuildCommand),
                    Arguments = service.BuildArguments ?? string.Empty,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            var writer = new StreamWriter(new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
            {
                AutoFlush = true
            };
            process.OutputDataReceived += (_, args) => WriteLogLine(writer, args.Data);
            process.ErrorDataReceived += (_, args) => WriteLogLine(writer, args.Data);
            process.Exited += (_, _) =>
            {
                WriteLogLine(writer, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] 打包进程退出，ExitCode={process.ExitCode}");
                writer.Dispose();
                _ = Task.Run(() => CompleteBuildHistoryAsync(
                    historyId,
                    process.ExitCode,
                    process.ExitCode == 0 ? "打包成功." : "打包失败，请查看打包日志.",
                    CancellationToken.None));
            };

            if (!process.Start())
            {
                writer.Dispose();
                await CompleteBuildHistoryAsync(historyId, -1, "打包进程启动失败.", cancellationToken);
                return ActionResult(service, "Failed", null, "打包进程启动失败.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            buildStates[service.Id] = new RuntimeProcessState(process, DateTimeOffset.UtcNow, null, historyId);

            return ActionResult(service, "Running", process.Id, "打包命令已执行.");
        }
        catch (Exception exception)
        {
            buildStates[service.Id] = new RuntimeProcessState(null, null, exception.Message);
            return ActionResult(service, "Failed", null, exception.Message);
        }
        finally
        {
            syncLock.Release();
        }
    }

    private async Task<ProjectRuntimeServiceStateDto> BuildServiceStateAsync(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var portOpen = service.Port.HasValue && await IsPortOpenAsync(service.Port.Value, cancellationToken);
        var healthOk = !string.IsNullOrWhiteSpace(service.HealthUrl) &&
            await IsHealthOkAsync(service.HealthUrl, cancellationToken);

        if (runtimeStates.TryGetValue(service.Id, out var state))
        {
            if (state.Process is { HasExited: false } process)
            {
                return new ProjectRuntimeServiceStateDto(
                    "Running",
                    process.Id,
                    state.StartedAt,
                    null,
                    healthOk ? "健康检查正常." : "进程运行中，等待健康检查.",
                    portOpen,
                    healthOk,
                    checkedAt);
            }

            if (state.Process is { HasExited: true } exitedProcess)
            {
                return new ProjectRuntimeServiceStateDto(
                    exitedProcess.ExitCode == 0 ? "Stopped" : "Failed",
                    exitedProcess.Id,
                    state.StartedAt,
                    exitedProcess.ExitCode,
                    exitedProcess.ExitCode == 0 ? "进程已正常退出." : "进程异常退出，请查看日志.",
                    portOpen,
                    healthOk,
                    checkedAt);
            }

            if (!string.IsNullOrWhiteSpace(state.LastError))
            {
                return new ProjectRuntimeServiceStateDto(
                    "Failed",
                    null,
                    state.StartedAt,
                    null,
                    state.LastError,
                    portOpen,
                    healthOk,
                    checkedAt);
            }
        }

        if (healthOk)
        {
            return new ProjectRuntimeServiceStateDto(
                "External",
                null,
                null,
                null,
                "健康地址可访问，服务可能由外部启动.",
                portOpen,
                true,
                checkedAt);
        }

        if (portOpen)
        {
            return new ProjectRuntimeServiceStateDto(
                "External",
                null,
                null,
                null,
                $"端口 {service.Port} 已被占用，服务可能由外部启动.",
                true,
                false,
                checkedAt);
        }

        var logPath = GetLogPath(workspace, service);
        return new ProjectRuntimeServiceStateDto(
            "Stopped",
            null,
            null,
            null,
            File.Exists(logPath) ? "服务未运行，可查看历史日志." : "服务尚未启动.",
            false,
            false,
            checkedAt);
    }

    private ProjectRuntimeBuildStateDto BuildServiceBuildState(ProjectRuntimeServiceConfiguration service)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        if (buildStates.TryGetValue(service.Id, out var state))
        {
            if (state.Process is { HasExited: false } process)
            {
                return new ProjectRuntimeBuildStateDto(
                    "Running",
                    process.Id,
                    state.StartedAt,
                    null,
                    "打包任务正在运行.",
                    checkedAt);
            }

            if (state.Process is { HasExited: true } exitedProcess)
            {
                return new ProjectRuntimeBuildStateDto(
                    exitedProcess.ExitCode == 0 ? "Succeeded" : "Failed",
                    exitedProcess.Id,
                    state.StartedAt,
                    exitedProcess.ExitCode,
                    exitedProcess.ExitCode == 0 ? "最近一次打包成功." : "最近一次打包失败，请查看打包日志.",
                    checkedAt);
            }

            if (!string.IsNullOrWhiteSpace(state.LastError))
            {
                return new ProjectRuntimeBuildStateDto(
                    "Failed",
                    null,
                    state.StartedAt,
                    null,
                    state.LastError,
                    checkedAt);
            }
        }

        return new ProjectRuntimeBuildStateDto(
            string.IsNullOrWhiteSpace(service.BuildCommand) ? "NotConfigured" : "Idle",
            null,
            null,
            null,
            string.IsNullOrWhiteSpace(service.BuildCommand) ? "未配置打包命令." : "可执行打包.",
            checkedAt);
    }

    private async Task<ProjectRuntimeConfiguration> LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        var path = GetConfigurationPath();
        if (!File.Exists(path))
        {
            var defaultConfiguration = CreateDefaultConfiguration();
            await SaveConfigurationAsync(defaultConfiguration, cancellationToken);
            return defaultConfiguration;
        }

        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var loaded = await JsonSerializer.DeserializeAsync<ProjectRuntimeConfiguration>(
            stream,
            JsonOptions,
            cancellationToken);

        var configuration = loaded ?? CreateDefaultConfiguration();
        NormalizeDefaultServiceSettings(configuration);
        return configuration;
    }

    private async Task SaveConfigurationAsync(
        ProjectRuntimeConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var path = GetConfigurationPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken);
    }

    private async Task<ProjectRuntimeBuildHistoryConfiguration> LoadBuildHistoryAsync(CancellationToken cancellationToken)
    {
        await historyLock.WaitAsync(cancellationToken);
        try
        {
            var path = GetBuildHistoryPath();
            if (!File.Exists(path))
            {
                return new ProjectRuntimeBuildHistoryConfiguration();
            }

            await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<ProjectRuntimeBuildHistoryConfiguration>(
                stream,
                JsonOptions,
                cancellationToken) ?? new ProjectRuntimeBuildHistoryConfiguration();
        }
        finally
        {
            historyLock.Release();
        }
    }

    private async Task SaveBuildHistoryAsync(
        ProjectRuntimeBuildHistoryConfiguration history,
        CancellationToken cancellationToken)
    {
        var path = GetBuildHistoryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        history.Records = history.Records
            .OrderByDescending(x => x.StartedAt)
            .Take(100)
            .ToList();

        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, history, JsonOptions, cancellationToken);
    }

    private async Task<Guid> AddBuildHistoryAsync(
        ProjectRuntimeServiceConfiguration service,
        string workingDirectory,
        string logPath,
        string? artifactPath,
        CancellationToken cancellationToken)
    {
        await historyLock.WaitAsync(cancellationToken);
        try
        {
            var history = await LoadBuildHistoryUnsafeAsync(cancellationToken);
            var record = new ProjectRuntimeBuildHistoryRecord
            {
                Id = Guid.NewGuid(),
                ServiceId = service.Id,
                ServiceName = service.Name,
                Status = "Running",
                CommandLine = $"{service.BuildCommand} {service.BuildArguments}".Trim(),
                WorkingDirectory = workingDirectory,
                LogPath = logPath,
                ArtifactPath = artifactPath,
                StartedAt = DateTimeOffset.UtcNow,
                Message = "打包任务正在运行."
            };
            history.Records.Add(record);
            await SaveBuildHistoryUnsafeAsync(history, cancellationToken);
            return record.Id;
        }
        finally
        {
            historyLock.Release();
        }
    }

    private async Task CompleteBuildHistoryAsync(
        Guid historyId,
        int exitCode,
        string message,
        CancellationToken cancellationToken)
    {
        await historyLock.WaitAsync(cancellationToken);
        try
        {
            var history = await LoadBuildHistoryUnsafeAsync(cancellationToken);
            var record = history.Records.SingleOrDefault(x => x.Id == historyId);
            if (record is null)
            {
                return;
            }

            var endedAt = DateTimeOffset.UtcNow;
            record.EndedAt = endedAt;
            record.ExitCode = exitCode;
            record.Status = exitCode == 0 ? "Succeeded" : "Failed";
            record.DurationMilliseconds = (long)(endedAt - record.StartedAt).TotalMilliseconds;
            record.Message = message;
            await SaveBuildHistoryUnsafeAsync(history, cancellationToken);
        }
        finally
        {
            historyLock.Release();
        }
    }

    private async Task<ProjectRuntimeBuildHistoryConfiguration> LoadBuildHistoryUnsafeAsync(CancellationToken cancellationToken)
    {
        var path = GetBuildHistoryPath();
        if (!File.Exists(path))
        {
            return new ProjectRuntimeBuildHistoryConfiguration();
        }

        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return await JsonSerializer.DeserializeAsync<ProjectRuntimeBuildHistoryConfiguration>(
            stream,
            JsonOptions,
            cancellationToken) ?? new ProjectRuntimeBuildHistoryConfiguration();
    }

    private async Task SaveBuildHistoryUnsafeAsync(
        ProjectRuntimeBuildHistoryConfiguration history,
        CancellationToken cancellationToken)
    {
        var path = GetBuildHistoryPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        history.Records = history.Records
            .OrderByDescending(x => x.StartedAt)
            .Take(100)
            .ToList();

        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(stream, history, JsonOptions, cancellationToken);
    }

    private ProjectRuntimeConfiguration CreateDefaultConfiguration()
    {
        var repoRoot = FindRepositoryRoot();
        var projectId = Guid.Parse("93000000-0000-0000-0000-000000000001");
        var workspaceId = Guid.Parse("93000000-0000-0000-0000-000000000002");

        return new ProjectRuntimeConfiguration
        {
            Projects =
            [
                new ProjectRuntimeProjectConfiguration
                {
                    Id = projectId,
                    Name = "MiniAdmin",
                    Code = "mini-admin",
                    RootPath = repoRoot,
                    RepositoryUrl = "https://github.com/your-org/mini-admin.git",
                    Description = "当前企业级后台管理系统.",
                    Order = 1,
                    IsEnabled = true,
                    Workspaces =
                    [
                        new ProjectRuntimeWorkspaceConfiguration
                        {
                            Id = workspaceId,
                            Name = "main",
                            Path = repoRoot,
                            BranchName = "main",
                            ProfileName = "Development",
                            IsDefault = true,
                            IsEnabled = true,
                            Order = 1,
                            Services =
                            [
                                new ProjectRuntimeServiceConfiguration
                                {
                                    Id = Guid.Parse("93000000-0000-0000-0000-000000000003"),
                                    Name = "MiniAdmin API",
                                    ServiceType = "Api",
                                    Command = "dotnet",
                                    Arguments = "run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5320",
                                    WorkingDirectory = ".",
                                    Port = 5320,
                                    HealthUrl = "http://localhost:5320/health",
                                    Url = "http://localhost:5320/health",
                                    LogFileName = "mini-admin-api.log",
                                    LogPath = "backend-dev.log",
                                    BuildCommand = "dotnet",
                                    BuildArguments = "publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api",
                                    BuildWorkingDirectory = ".",
                                    BuildLogFileName = "mini-admin-api-build.log",
                                    BuildLogPath = "backend-build.log",
                                    BuildArtifactPath = "artifacts/publish/mini-admin-api",
                                    IsEnabled = true,
                                    Order = 1
                                },
                                new ProjectRuntimeServiceConfiguration
                                {
                                    Id = Guid.Parse("93000000-0000-0000-0000-000000000004"),
                                    Name = "Vben Web",
                                    ServiceType = "Web",
                                    Command = "pnpm",
                                    Arguments = "run dev:antd -- --host 0.0.0.0 --port 5666",
                                    WorkingDirectory = "frontend/vue-vben-admin",
                                    Port = 5666,
                                    HealthUrl = "http://localhost:5666",
                                    Url = "http://localhost:5666",
                                    LogFileName = "vben-web.log",
                                    LogPath = "frontend-dev.log",
                                    BuildCommand = "pnpm",
                                    BuildArguments = "run build:antd",
                                    BuildWorkingDirectory = "frontend/vue-vben-admin",
                                    BuildLogFileName = "vben-web-build.log",
                                    BuildLogPath = "frontend-build.log",
                                    BuildArtifactPath = "frontend/vue-vben-admin/apps/web-antd/dist.zip",
                                    IsEnabled = true,
                                    Order = 2
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    private static void NormalizeDefaultServiceSettings(ProjectRuntimeConfiguration configuration)
    {
        foreach (var service in configuration.Projects.SelectMany(project => project.Workspaces).SelectMany(workspace => workspace.Services))
        {
            if (string.IsNullOrWhiteSpace(service.LogPath))
            {
                service.LogPath = service.Id == Guid.Parse("93000000-0000-0000-0000-000000000003")
                    ? "backend-dev.log"
                    : service.Id == Guid.Parse("93000000-0000-0000-0000-000000000004")
                        ? "frontend-dev.log"
                        : null;
            }

            ApplyDefaultBuildSettings(service);
        }
    }

    private static ProjectRuntimeProjectConfiguration BuildProject(
        SaveProjectRuntimeProjectRequest request,
        Guid projectId)
    {
        ValidateProjectRequest(request);
        var rootPath = Path.GetFullPath(request.RootPath.Trim());
        var workspaces = request.Workspaces is { Count: > 0 }
            ? request.Workspaces.Select(workspace => BuildWorkspace(workspace, rootPath)).ToList()
            : [BuildDetectedWorkspace(rootPath)];

        return new ProjectRuntimeProjectConfiguration
        {
            Id = projectId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            RootPath = rootPath,
            RepositoryUrl = NormalizeOptional(request.RepositoryUrl),
            Description = NormalizeOptional(request.Description),
            Order = request.Order,
            IsEnabled = request.IsEnabled,
            Workspaces = workspaces
        };
    }

    private static ProjectRuntimeWorkspaceConfiguration BuildWorkspace(
        SaveProjectRuntimeWorkspaceRequest request,
        string projectRootPath)
    {
        var workspacePath = Path.GetFullPath(string.IsNullOrWhiteSpace(request.Path)
            ? projectRootPath
            : request.Path.Trim());

        return new ProjectRuntimeWorkspaceConfiguration
        {
            Id = request.Id ?? Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(request.Name) ? "main" : request.Name.Trim(),
            Path = workspacePath,
            BranchName = NormalizeOptional(request.BranchName),
            ProfileName = NormalizeOptional(request.ProfileName),
            IsDefault = request.IsDefault,
            IsEnabled = request.IsEnabled,
            Order = request.Order,
            Services = request.Services?.Select(service => BuildService(service)).ToList() ?? []
        };
    }

    private static ProjectRuntimeServiceConfiguration BuildService(SaveProjectRuntimeServiceRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("服务名称不能为空.");
        }

        return new ProjectRuntimeServiceConfiguration
        {
            Id = request.Id ?? Guid.NewGuid(),
            Name = request.Name.Trim(),
            ServiceType = string.IsNullOrWhiteSpace(request.ServiceType) ? "Custom" : request.ServiceType.Trim(),
            Command = request.Command.Trim(),
            Arguments = request.Arguments.Trim(),
            WorkingDirectory = string.IsNullOrWhiteSpace(request.WorkingDirectory) ? "." : request.WorkingDirectory.Trim(),
            Port = request.Port,
            HealthUrl = NormalizeOptional(request.HealthUrl),
            Url = NormalizeOptional(request.Url),
            LogFileName = string.IsNullOrWhiteSpace(request.LogFileName)
                ? $"{request.Name.Trim().ToLowerInvariant().Replace(' ', '-')}.log"
                : Path.GetFileName(request.LogFileName.Trim()),
            LogPath = NormalizeOptional(request.LogPath),
            BuildCommand = NormalizeOptional(request.BuildCommand),
            BuildArguments = NormalizeOptional(request.BuildArguments),
            BuildWorkingDirectory = NormalizeOptional(request.BuildWorkingDirectory),
            BuildLogFileName = string.IsNullOrWhiteSpace(request.BuildLogFileName)
                ? $"{request.Name.Trim().ToLowerInvariant().Replace(' ', '-')}-build.log"
                : Path.GetFileName(request.BuildLogFileName.Trim()),
            BuildLogPath = NormalizeOptional(request.BuildLogPath),
            BuildArtifactPath = NormalizeOptional(request.BuildArtifactPath),
            IsEnabled = request.IsEnabled,
            Order = request.Order
        };
    }

    private static ProjectRuntimeWorkspaceConfiguration BuildDetectedWorkspace(string rootPath)
    {
        var workspace = new ProjectRuntimeWorkspaceConfiguration
        {
            Id = Guid.NewGuid(),
            Name = "main",
            Path = rootPath,
            BranchName = "main",
            ProfileName = "Development",
            IsDefault = true,
            IsEnabled = true,
            Order = 1,
            Services = []
        };

        if (File.Exists(Path.Combine(rootPath, "MiniAdmin.slnx")))
        {
            workspace.Services.Add(new ProjectRuntimeServiceConfiguration
            {
                Id = Guid.NewGuid(),
                Name = "MiniAdmin API",
                ServiceType = "Api",
                Command = "dotnet",
                Arguments = "run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5320",
                WorkingDirectory = ".",
                Port = 5320,
                HealthUrl = "http://localhost:5320/health",
                Url = "http://localhost:5320/health",
                LogFileName = "mini-admin-api.log",
                LogPath = "backend-dev.log",
                BuildCommand = "dotnet",
                BuildArguments = "publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api",
                BuildWorkingDirectory = ".",
                BuildLogFileName = "mini-admin-api-build.log",
                BuildLogPath = "backend-build.log",
                BuildArtifactPath = "artifacts/publish/mini-admin-api",
                IsEnabled = true,
                Order = 1
            });
        }

        if (File.Exists(Path.Combine(rootPath, "frontend", "vue-vben-admin", "package.json")))
        {
            workspace.Services.Add(new ProjectRuntimeServiceConfiguration
            {
                Id = Guid.NewGuid(),
                Name = "Vben Web",
                ServiceType = "Web",
                Command = "pnpm",
                Arguments = "run dev:antd -- --host 0.0.0.0 --port 5666",
                WorkingDirectory = "frontend/vue-vben-admin",
                Port = 5666,
                HealthUrl = "http://localhost:5666",
                Url = "http://localhost:5666",
                LogFileName = "vben-web.log",
                LogPath = "frontend-dev.log",
                BuildCommand = "pnpm",
                BuildArguments = "run build:antd",
                BuildWorkingDirectory = "frontend/vue-vben-admin",
                BuildLogFileName = "vben-web-build.log",
                BuildLogPath = "frontend-build.log",
                BuildArtifactPath = "frontend/vue-vben-admin/apps/web-antd/dist.zip",
                IsEnabled = true,
                Order = 2
            });
        }

        var rootPackageJson = Path.Combine(rootPath, "package.json");
        if (File.Exists(rootPackageJson) && DetectFrontendProject(rootPackageJson) is { } frontendProject)
        {
            workspace.Services.Add(new ProjectRuntimeServiceConfiguration
            {
                Id = Guid.NewGuid(),
                Name = frontendProject.Name,
                ServiceType = frontendProject.ServiceType,
                Command = "pnpm",
                Arguments = frontendProject.DevArguments,
                WorkingDirectory = ".",
                Url = null,
                LogFileName = $"{frontendProject.LogPrefix}.log",
                BuildCommand = "pnpm",
                BuildArguments = frontendProject.BuildArguments,
                BuildWorkingDirectory = ".",
                BuildLogFileName = $"{frontendProject.LogPrefix}-build.log",
                BuildArtifactPath = frontendProject.ArtifactPath,
                IsEnabled = true,
                Order = workspace.Services.Count + 1
            });
        }

        return workspace;
    }

    private static FrontendProjectInfo? DetectFrontendProject(string packageJsonPath)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
            var root = document.RootElement;
            var scripts = ReadPackageObjectKeys(root, "scripts");
            var dependencies = ReadPackageObjectKeys(root, "dependencies");
            dependencies.UnionWith(ReadPackageObjectKeys(root, "devDependencies"));

            if (!scripts.Contains("build") && !scripts.Contains("build:h5"))
            {
                return null;
            }

            if (dependencies.Contains("@dcloudio/uni-app") || scripts.Contains("build:h5"))
            {
                return new FrontendProjectInfo("uniapp 应用", "UniApp", "run dev:h5", "run build:h5", "uniapp", "dist");
            }

            if (dependencies.Contains("react") || dependencies.Contains("react-dom"))
            {
                return new FrontendProjectInfo("React Web", "React", "run dev", "run build", "react-web", "dist");
            }

            if (dependencies.Contains("vue"))
            {
                return new FrontendProjectInfo("Vue Web", "Vue", "run dev", "run build", "vue-web", "dist");
            }

            return new FrontendProjectInfo("前端应用", "Web", "run dev", "run build", "web", "dist");
        }
        catch
        {
            return null;
        }
    }

    private static HashSet<string> ReadPackageObjectKeys(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return property.EnumerateObject()
            .Select(item => item.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateProjectRequest(SaveProjectRuntimeProjectRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("项目名称不能为空.");
        }

        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ArgumentException("项目编码不能为空.");
        }

        if (string.IsNullOrWhiteSpace(request.RootPath))
        {
            throw new ArgumentException("项目目录不能为空.");
        }

        if (!Directory.Exists(request.RootPath))
        {
            throw new ArgumentException($"项目目录不存在：{request.RootPath}");
        }
    }

    private string GetConfigurationPath()
    {
        return Path.Combine(FindRepositoryRoot(), "data", "project-runtime", "projects.json");
    }

    private string GetBuildHistoryPath()
    {
        return Path.Combine(FindRepositoryRoot(), "data", "project-runtime", "build-history.json");
    }

    private string GetLogPath(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service)
    {
        var configuredLogPath = ResolveConfiguredLogPath(workspace, service);
        if (configuredLogPath is not null)
        {
            return configuredLogPath;
        }

        var safeWorkspace = SanitizeFileName(workspace.Name);
        var safeService = SanitizeFileName(service.LogFileName);
        return Path.Combine(FindRepositoryRoot(), "logs", "project-runtime", safeWorkspace, safeService);
    }

    private string GetBuildLogPath(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service)
    {
        var configuredLogPath = ResolveConfiguredBuildLogPath(workspace, service);
        if (configuredLogPath is not null)
        {
            return configuredLogPath;
        }

        var safeWorkspace = SanitizeFileName(workspace.Name);
        var safeService = SanitizeFileName(service.BuildLogFileName);
        return Path.Combine(FindRepositoryRoot(), "logs", "project-runtime", safeWorkspace, safeService);
    }

    private string? GetArtifactPath(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service)
    {
        if (string.IsNullOrWhiteSpace(service.BuildArtifactPath))
        {
            return null;
        }

        var workspacePath = Path.GetFullPath(workspace.Path);
        var candidate = Path.GetFullPath(Path.IsPathRooted(service.BuildArtifactPath)
            ? service.BuildArtifactPath
            : Path.Combine(workspacePath, service.BuildArtifactPath));

        return candidate.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }

    private static string? ResolveConfiguredLogPath(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service)
    {
        if (string.IsNullOrWhiteSpace(service.LogPath))
        {
            return null;
        }

        var workspacePath = Path.GetFullPath(workspace.Path);
        var candidate = Path.GetFullPath(Path.IsPathRooted(service.LogPath)
            ? service.LogPath
            : Path.Combine(workspacePath, service.LogPath));

        return candidate.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }

    private static string? ResolveConfiguredBuildLogPath(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service)
    {
        if (string.IsNullOrWhiteSpace(service.BuildLogPath))
        {
            return null;
        }

        var workspacePath = Path.GetFullPath(workspace.Path);
        var candidate = Path.GetFullPath(Path.IsPathRooted(service.BuildLogPath)
            ? service.BuildLogPath
            : Path.Combine(workspacePath, service.BuildLogPath));

        return candidate.StartsWith(workspacePath, StringComparison.OrdinalIgnoreCase)
            ? candidate
            : null;
    }

    private static void ApplyDefaultBuildSettings(ProjectRuntimeServiceConfiguration service)
    {
        if (!string.IsNullOrWhiteSpace(service.BuildCommand))
        {
            ApplyDefaultArtifactPath(service);
            return;
        }

        if (service.Id == Guid.Parse("93000000-0000-0000-0000-000000000003"))
        {
            service.BuildCommand = "dotnet";
            service.BuildArguments = "publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api";
            service.BuildWorkingDirectory = ".";
            service.BuildLogFileName = string.IsNullOrWhiteSpace(service.BuildLogFileName)
                ? "mini-admin-api-build.log"
                : service.BuildLogFileName;
            service.BuildLogPath ??= "backend-build.log";
            service.BuildArtifactPath ??= "artifacts/publish/mini-admin-api";
            return;
        }

        if (service.ServiceType.Equals("Api", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceType.Equals("DotNet", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildCommand = "dotnet";
            service.BuildArguments = "publish -c Release";
            service.BuildWorkingDirectory = service.WorkingDirectory;
            service.BuildLogFileName = string.IsNullOrWhiteSpace(service.BuildLogFileName)
                ? "dotnet-build.log"
                : service.BuildLogFileName;
            service.BuildArtifactPath ??= "bin/Release";
            return;
        }

        if (service.Id == Guid.Parse("93000000-0000-0000-0000-000000000004") ||
            service.ServiceType.Equals("Web", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceType.Equals("Vue", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildCommand = "pnpm";
            service.BuildArguments = service.Name.Contains("Vben", StringComparison.OrdinalIgnoreCase)
                ? "run build:antd"
                : "run build";
            service.BuildWorkingDirectory = service.WorkingDirectory;
            service.BuildLogFileName = string.IsNullOrWhiteSpace(service.BuildLogFileName)
                ? "web-build.log"
                : service.BuildLogFileName;
            service.BuildLogPath ??= service.Name.Contains("Vben", StringComparison.OrdinalIgnoreCase)
                ? "frontend-build.log"
                : null;
            service.BuildArtifactPath ??= service.Name.Contains("Vben", StringComparison.OrdinalIgnoreCase)
                ? "frontend/vue-vben-admin/apps/web-antd/dist.zip"
                : Path.Combine(service.WorkingDirectory, "dist");
            return;
        }

        if (service.ServiceType.Equals("React", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildCommand = "pnpm";
            service.BuildArguments = "run build";
            service.BuildWorkingDirectory = service.WorkingDirectory;
            service.BuildLogFileName = string.IsNullOrWhiteSpace(service.BuildLogFileName)
                ? "react-build.log"
                : service.BuildLogFileName;
            service.BuildArtifactPath ??= Path.Combine(service.WorkingDirectory, "dist");
            return;
        }

        if (service.ServiceType.Equals("UniApp", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildCommand = "pnpm";
            service.BuildArguments = "run build:h5";
            service.BuildWorkingDirectory = service.WorkingDirectory;
            service.BuildLogFileName = string.IsNullOrWhiteSpace(service.BuildLogFileName)
                ? "uniapp-build.log"
                : service.BuildLogFileName;
            service.BuildArtifactPath ??= Path.Combine(service.WorkingDirectory, "dist");
        }
    }

    private static void ApplyDefaultArtifactPath(ProjectRuntimeServiceConfiguration service)
    {
        if (!string.IsNullOrWhiteSpace(service.BuildArtifactPath))
        {
            return;
        }

        if (service.Id == Guid.Parse("93000000-0000-0000-0000-000000000003"))
        {
            service.BuildArtifactPath = "artifacts/publish/mini-admin-api";
        }
        else if (service.Id == Guid.Parse("93000000-0000-0000-0000-000000000004") ||
            service.Name.Contains("Vben", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildArtifactPath = "frontend/vue-vben-admin/apps/web-antd/dist.zip";
        }
        else if (service.ServiceType.Equals("Web", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceType.Equals("Vue", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceType.Equals("React", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceType.Equals("UniApp", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildArtifactPath = Path.Combine(service.WorkingDirectory, "dist");
        }
        else if (service.ServiceType.Equals("Api", StringComparison.OrdinalIgnoreCase) ||
            service.ServiceType.Equals("DotNet", StringComparison.OrdinalIgnoreCase))
        {
            service.BuildArtifactPath = "bin/Release";
        }
    }

    private string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(hostEnvironment.ContentRootPath);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MiniAdmin.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return hostEnvironment.ContentRootPath;
    }

    private static ProjectRuntimeWorkspaceConfiguration? FindWorkspace(
        ProjectRuntimeConfiguration configuration,
        Guid workspaceId)
    {
        return configuration.Projects
            .SelectMany(project => project.Workspaces)
            .SingleOrDefault(workspace => workspace.Id == workspaceId);
    }

    private static LocatedService? FindService(ProjectRuntimeConfiguration configuration, Guid serviceId)
    {
        foreach (var workspace in configuration.Projects.SelectMany(project => project.Workspaces))
        {
            var service = workspace.Services.SingleOrDefault(x => x.Id == serviceId);
            if (service is not null)
            {
                return new LocatedService(workspace, service);
            }
        }

        return null;
    }

    private static ProjectRuntimeBuildHistoryDto? FindLatestBuild(
        ProjectRuntimeBuildHistoryConfiguration history,
        Guid serviceId)
    {
        var record = history.Records
            .Where(x => x.ServiceId == serviceId)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault();

        return record is null ? null : ToDto(record);
    }

    private ProjectRuntimeArtifactDto BuildArtifactDto(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service)
    {
        var path = GetArtifactPath(workspace, service);
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ProjectRuntimeArtifactDto(service.Id, service.Name, null, false, "Missing", null, null);
        }

        if (File.Exists(path))
        {
            var fileInfo = new FileInfo(path);
            return new ProjectRuntimeArtifactDto(
                service.Id,
                service.Name,
                path,
                true,
                "File",
                fileInfo.Length,
                new DateTimeOffset(fileInfo.LastWriteTimeUtc));
        }

        if (Directory.Exists(path))
        {
            var directoryInfo = new DirectoryInfo(path);
            return new ProjectRuntimeArtifactDto(
                service.Id,
                service.Name,
                path,
                true,
                "Directory",
                GetDirectorySize(directoryInfo),
                new DateTimeOffset(directoryInfo.LastWriteTimeUtc));
        }

        return new ProjectRuntimeArtifactDto(service.Id, service.Name, path, false, "Missing", null, null);
    }

    private static ProjectRuntimeProjectDto ToDto(
        ProjectRuntimeProjectConfiguration project,
        IReadOnlyList<ProjectRuntimeWorkspaceDto> workspaces)
    {
        return new ProjectRuntimeProjectDto(
            project.Id,
            project.Name,
            project.Code,
            project.RootPath,
            project.RepositoryUrl,
            project.Description,
            project.Order,
            project.IsEnabled,
            workspaces);
    }

    private static ProjectRuntimeWorkspaceDto ToDto(
        Guid projectId,
        ProjectRuntimeWorkspaceConfiguration workspace,
        IReadOnlyList<ProjectRuntimeServiceDto> services)
    {
        return new ProjectRuntimeWorkspaceDto(
            workspace.Id,
            projectId,
            workspace.Name,
            workspace.Path,
            workspace.BranchName,
            workspace.ProfileName,
            workspace.IsDefault,
            workspace.IsEnabled,
            workspace.Order,
            services);
    }

    private static ProjectRuntimeServiceDto ToDto(
        ProjectRuntimeWorkspaceConfiguration workspace,
        ProjectRuntimeServiceConfiguration service,
        ProjectRuntimeServiceStateDto state,
        ProjectRuntimeBuildStateDto buildState,
        ProjectRuntimeBuildHistoryDto? latestBuild,
        ProjectRuntimeArtifactDto artifact)
    {
        return new ProjectRuntimeServiceDto(
            service.Id,
            workspace.Id,
            service.Name,
            service.ServiceType,
            service.Command,
            service.Arguments,
            service.WorkingDirectory,
            service.Port,
            service.HealthUrl,
            service.Url,
            service.LogFileName,
            service.LogPath,
            service.BuildCommand,
            service.BuildArguments,
            service.BuildWorkingDirectory,
            service.BuildLogFileName,
            service.BuildLogPath,
            service.BuildArtifactPath,
            service.IsEnabled,
            service.Order,
            state,
            buildState,
            latestBuild,
            artifact);
    }

    private static ProjectRuntimeBuildHistoryDto ToDto(ProjectRuntimeBuildHistoryRecord record)
    {
        return new ProjectRuntimeBuildHistoryDto(
            record.Id,
            record.ServiceId,
            record.ServiceName,
            record.Status,
            record.CommandLine,
            record.WorkingDirectory,
            record.LogPath,
            record.ArtifactPath,
            record.StartedAt,
            record.EndedAt,
            record.DurationMilliseconds,
            record.ExitCode,
            record.Message);
    }

    private static ProjectRuntimeActionResultDto ActionResult(
        ProjectRuntimeServiceConfiguration service,
        string status,
        int? processId,
        string message)
    {
        return new ProjectRuntimeActionResultDto(service.Id, service.Name, status, processId, message);
    }

    private static async Task<IReadOnlyList<string>> ReadLastLinesAsync(
        string path,
        int lines,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var allLines = new List<string>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            allLines.Add(line);
        }

        return allLines.Count <= lines
            ? allLines
            : allLines.Skip(allLines.Count - lines).ToArray();
    }

    private async Task<bool> IsHealthOkAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync("127.0.0.1", port, cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string ResolvePath(string workspacePath, string path)
    {
        return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(workspacePath, path));
    }

    private static string ResolveExecutable(string command)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(command) || command.Contains(Path.DirectorySeparatorChar))
        {
            return command;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var extension in new[] { ".exe", ".cmd", ".bat" })
            {
                var candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return command;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray();
        return new string(chars);
    }

    private static long GetDirectorySize(DirectoryInfo directory)
    {
        try
        {
            return directory
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(file => file.Length);
        }
        catch
        {
            return 0;
        }
    }

    private static void WriteLogLine(StreamWriter writer, string? line)
    {
        if (line is null)
        {
            return;
        }

        lock (writer)
        {
            writer.WriteLine(line);
        }
    }

    private sealed class ProjectRuntimeConfiguration
    {
        public List<ProjectRuntimeProjectConfiguration> Projects { get; set; } = [];
    }

    private sealed class ProjectRuntimeBuildHistoryConfiguration
    {
        public List<ProjectRuntimeBuildHistoryRecord> Records { get; set; } = [];
    }

    private sealed class ProjectRuntimeBuildHistoryRecord
    {
        public Guid Id { get; set; }
        public Guid ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string Status { get; set; } = "Running";
        public string CommandLine { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string LogPath { get; set; } = string.Empty;
        public string? ArtifactPath { get; set; }
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? EndedAt { get; set; }
        public long? DurationMilliseconds { get; set; }
        public int? ExitCode { get; set; }
        public string? Message { get; set; }
    }

    private sealed class ProjectRuntimeProjectConfiguration
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string RootPath { get; set; } = string.Empty;
        public string? RepositoryUrl { get; set; }
        public string? Description { get; set; }
        public int Order { get; set; }
        public bool IsEnabled { get; set; } = true;
        public List<ProjectRuntimeWorkspaceConfiguration> Workspaces { get; set; } = [];
    }

    private sealed class ProjectRuntimeWorkspaceConfiguration
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? ProfileName { get; set; }
        public bool IsDefault { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int Order { get; set; }
        public List<ProjectRuntimeServiceConfiguration> Services { get; set; } = [];
    }

    private sealed class ProjectRuntimeServiceConfiguration
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ServiceType { get; set; } = "Custom";
        public string Command { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = ".";
        public int? Port { get; set; }
        public string? HealthUrl { get; set; }
        public string? Url { get; set; }
        public string LogFileName { get; set; } = "service.log";
        public string? LogPath { get; set; }
        public string? BuildCommand { get; set; }
        public string? BuildArguments { get; set; }
        public string? BuildWorkingDirectory { get; set; }
        public string BuildLogFileName { get; set; } = "service-build.log";
        public string? BuildLogPath { get; set; }
        public string? BuildArtifactPath { get; set; }
        public bool IsEnabled { get; set; } = true;
        public int Order { get; set; }
    }

    private sealed record RuntimeProcessState(
        Process? Process,
        DateTimeOffset? StartedAt,
        string? LastError,
        Guid? HistoryId = null);

    private sealed record FrontendProjectInfo(
        string Name,
        string ServiceType,
        string DevArguments,
        string BuildArguments,
        string LogPrefix,
        string ArtifactPath);

    private readonly record struct LocatedService(
        ProjectRuntimeWorkspaceConfiguration Workspace,
        ProjectRuntimeServiceConfiguration Service);
}
