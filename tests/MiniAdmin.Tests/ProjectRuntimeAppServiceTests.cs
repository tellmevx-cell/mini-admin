using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using MiniAdmin.Infrastructure.ProjectRuntimes;

namespace MiniAdmin.Tests;

public sealed class ProjectRuntimeAppServiceTests
{
    [Fact]
    public async Task Reads_Configured_Root_Log_File()
    {
        var rootPath = CreateTempRepository();
        var serviceId = Guid.Parse("93000000-0000-0000-0000-000000000003");
        await File.WriteAllLinesAsync(
            Path.Combine(rootPath, "backend-dev.log"),
            ["line-1", "line-2"]);
        await WriteProjectRuntimeConfigAsync(rootPath, serviceId);

        var service = new ProjectRuntimeAppService(new TestHostEnvironment(rootPath));

        var logs = await service.GetServiceLogsAsync(serviceId);

        Assert.Equal(Path.Combine(rootPath, "backend-dev.log"), logs.LogPath);
        Assert.Equal(["line-1", "line-2"], logs.Lines);
    }

    [Fact]
    public async Task Reads_Configured_Build_Log_File()
    {
        var rootPath = CreateTempRepository();
        var serviceId = Guid.Parse("93000000-0000-0000-0000-000000000003");
        await File.WriteAllLinesAsync(
            Path.Combine(rootPath, "backend-build.log"),
            ["build-1", "build-2"]);
        await WriteProjectRuntimeConfigAsync(rootPath, serviceId);

        var service = new ProjectRuntimeAppService(new TestHostEnvironment(rootPath));

        var logs = await service.GetServiceBuildLogsAsync(serviceId);

        Assert.Equal(Path.Combine(rootPath, "backend-build.log"), logs.LogPath);
        Assert.Equal(["build-1", "build-2"], logs.Lines);
    }

    [Fact]
    public async Task Reports_Configured_Build_Artifact()
    {
        var rootPath = CreateTempRepository();
        var serviceId = Guid.Parse("93000000-0000-0000-0000-000000000003");
        var artifactPath = Path.Combine(rootPath, "artifacts", "publish", "mini-admin-api");
        Directory.CreateDirectory(artifactPath);
        await File.WriteAllTextAsync(Path.Combine(artifactPath, "MiniAdmin.Api.dll"), "artifact");
        await WriteProjectRuntimeConfigAsync(rootPath, serviceId);

        var service = new ProjectRuntimeAppService(new TestHostEnvironment(rootPath));

        var artifact = await service.GetServiceArtifactAsync(serviceId);

        Assert.True(artifact.Exists);
        Assert.Equal("Directory", artifact.Type);
        Assert.Equal(artifactPath, artifact.Path);
        Assert.True(artifact.SizeBytes > 0);
    }

    [Fact]
    public async Task Reads_Service_Build_History()
    {
        var rootPath = CreateTempRepository();
        var serviceId = Guid.Parse("93000000-0000-0000-0000-000000000003");
        await WriteProjectRuntimeConfigAsync(rootPath, serviceId);
        await WriteBuildHistoryAsync(rootPath, serviceId);

        var service = new ProjectRuntimeAppService(new TestHostEnvironment(rootPath));

        var history = await service.GetServiceBuildHistoryAsync(serviceId);

        var latest = Assert.Single(history);
        Assert.Equal("Succeeded", latest.Status);
        Assert.Equal(0, latest.ExitCode);
        Assert.Equal(Path.Combine(rootPath, "backend-build.log"), latest.LogPath);
    }

    private static string CreateTempRepository()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "mini-admin-runtime-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        File.WriteAllText(Path.Combine(rootPath, "MiniAdmin.slnx"), string.Empty);
        Directory.CreateDirectory(Path.Combine(rootPath, "data", "project-runtime"));
        return rootPath;
    }

    private static Task WriteProjectRuntimeConfigAsync(string rootPath, Guid serviceId)
    {
        var json = $$"""
        {
          "projects": [
            {
              "id": "93000000-0000-0000-0000-000000000001",
              "name": "MiniAdmin",
              "code": "mini-admin",
              "rootPath": "{{EscapeJson(rootPath)}}",
              "order": 1,
              "isEnabled": true,
              "workspaces": [
                {
                  "id": "93000000-0000-0000-0000-000000000002",
                  "name": "main",
                  "path": "{{EscapeJson(rootPath)}}",
                  "branchName": "main",
                  "profileName": "Development",
                  "isDefault": true,
                  "isEnabled": true,
                  "order": 1,
                  "services": [
                    {
                      "id": "{{serviceId}}",
                      "name": "MiniAdmin API",
                      "serviceType": "Api",
                      "command": "dotnet",
                      "arguments": "run",
                      "workingDirectory": ".",
                      "port": 5320,
                      "healthUrl": "http://localhost:5320/health",
                      "url": "http://localhost:5320/health",
                      "logFileName": "mini-admin-api.log",
                      "logPath": "backend-dev.log",
                      "buildCommand": "dotnet",
                      "buildArguments": "publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api",
                      "buildWorkingDirectory": ".",
                      "buildLogFileName": "mini-admin-api-build.log",
                      "buildLogPath": "backend-build.log",
                      "buildArtifactPath": "artifacts/publish/mini-admin-api",
                      "isEnabled": true,
                      "order": 1
                    }
                  ]
                }
              ]
            }
          ]
        }
        """;
        return File.WriteAllTextAsync(Path.Combine(rootPath, "data", "project-runtime", "projects.json"), json);
    }

    private static Task WriteBuildHistoryAsync(string rootPath, Guid serviceId)
    {
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-5);
        var endedAt = DateTimeOffset.UtcNow;
        var json = $$"""
        {
          "records": [
            {
              "id": "94000000-0000-0000-0000-000000000001",
              "serviceId": "{{serviceId}}",
              "serviceName": "MiniAdmin API",
              "status": "Succeeded",
              "commandLine": "dotnet publish",
              "workingDirectory": "{{EscapeJson(rootPath)}}",
              "logPath": "{{EscapeJson(Path.Combine(rootPath, "backend-build.log"))}}",
              "artifactPath": "{{EscapeJson(Path.Combine(rootPath, "artifacts", "publish", "mini-admin-api"))}}",
              "startedAt": "{{startedAt:O}}",
              "endedAt": "{{endedAt:O}}",
              "durationMilliseconds": 5000,
              "exitCode": 0,
              "message": "打包成功."
            }
          ]
        }
        """;
        return File.WriteAllTextAsync(Path.Combine(rootPath, "data", "project-runtime", "build-history.json"), json);
    }

    private static string EscapeJson(string value)
    {
        return value.Replace(@"\", @"\\", StringComparison.Ordinal);
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string ApplicationName { get; set; } = "MiniAdmin.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = Environments.Development;
    }
}
