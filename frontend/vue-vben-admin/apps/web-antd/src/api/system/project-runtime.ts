import { requestClient } from '#/api/request';

export interface ProjectRuntimeOverview {
  projects: ProjectRuntimeProject[];
  summary: ProjectRuntimeSummary;
}

export interface ProjectRuntimeSummary {
  failedServiceCount: number;
  projectCount: number;
  runningServiceCount: number;
  serviceCount: number;
  workspaceCount: number;
}

export interface ProjectRuntimeProject {
  code: string;
  description?: null | string;
  id: string;
  isEnabled: boolean;
  name: string;
  order: number;
  repositoryUrl?: null | string;
  rootPath: string;
  workspaces: ProjectRuntimeWorkspace[];
}

export interface ProjectRuntimeWorkspace {
  branchName?: null | string;
  id: string;
  isDefault: boolean;
  isEnabled: boolean;
  name: string;
  order: number;
  path: string;
  profileName?: null | string;
  projectId: string;
  services: ProjectRuntimeService[];
}

export interface ProjectRuntimeService {
  arguments: string;
  buildArguments?: null | string;
  buildCommand?: null | string;
  buildArtifactPath?: null | string;
  buildLogFileName: string;
  buildLogPath?: null | string;
  buildState: ProjectRuntimeBuildState;
  buildWorkingDirectory?: null | string;
  command: string;
  healthUrl?: null | string;
  id: string;
  isEnabled: boolean;
  logFileName: string;
  logPath?: null | string;
  name: string;
  order: number;
  port?: null | number;
  serviceType: string;
  state: ProjectRuntimeServiceState;
  url?: null | string;
  workingDirectory: string;
  workspaceId: string;
  latestBuild?: null | ProjectRuntimeBuildHistory;
  artifact: ProjectRuntimeArtifact;
}

export interface ProjectRuntimeBuildState {
  checkedAt: string;
  exitCode?: null | number;
  message?: null | string;
  processId?: null | number;
  startedAt?: null | string;
  status: string;
}

export interface ProjectRuntimeBuildHistory {
  artifactPath?: null | string;
  commandLine: string;
  durationMilliseconds?: null | number;
  endedAt?: null | string;
  exitCode?: null | number;
  id: string;
  logPath: string;
  message?: null | string;
  serviceId: string;
  serviceName: string;
  startedAt: string;
  status: string;
  workingDirectory: string;
}

export interface ProjectRuntimeArtifact {
  exists: boolean;
  lastModifiedAt?: null | string;
  path?: null | string;
  serviceId: string;
  serviceName: string;
  sizeBytes?: null | number;
  type: string;
}

export interface ProjectRuntimeServiceState {
  checkedAt: string;
  exitCode?: null | number;
  healthOk: boolean;
  message?: null | string;
  portOpen: boolean;
  processId?: null | number;
  startedAt?: null | string;
  status: string;
}

export interface SaveProjectRuntimeProjectRequest {
  code: string;
  description?: null | string;
  isEnabled: boolean;
  name: string;
  order: number;
  repositoryUrl?: null | string;
  rootPath: string;
  workspaces?: SaveProjectRuntimeWorkspaceRequest[];
}

export interface SaveProjectRuntimeWorkspaceRequest {
  branchName?: null | string;
  id?: string;
  isDefault: boolean;
  isEnabled: boolean;
  name: string;
  order: number;
  path: string;
  profileName?: null | string;
  services?: SaveProjectRuntimeServiceRequest[];
}

export interface SaveProjectRuntimeServiceRequest {
  arguments: string;
  buildArtifactPath?: null | string;
  buildArguments?: null | string;
  buildCommand?: null | string;
  buildLogFileName?: null | string;
  buildLogPath?: null | string;
  buildWorkingDirectory?: null | string;
  command: string;
  healthUrl?: null | string;
  id?: string;
  isEnabled: boolean;
  logFileName?: null | string;
  logPath?: null | string;
  name: string;
  order: number;
  port?: null | number;
  serviceType: string;
  url?: null | string;
  workingDirectory: string;
}

export interface ProjectRuntimeActionResult {
  message: string;
  processId?: null | number;
  serviceId: string;
  serviceName: string;
  status: string;
}

export interface ProjectRuntimeLog {
  lines: string[];
  logPath: string;
  readAt: string;
  serviceId: string;
  serviceName: string;
}

export async function getProjectRuntimeOverviewApi() {
  return requestClient.get<ProjectRuntimeOverview>(
    '/system/project-runtime/overview',
  );
}

export async function createProjectRuntimeProjectApi(
  data: SaveProjectRuntimeProjectRequest,
) {
  return requestClient.post<ProjectRuntimeProject>(
    '/system/project-runtime/projects',
    data,
  );
}

export async function updateProjectRuntimeProjectApi(
  id: string,
  data: SaveProjectRuntimeProjectRequest,
) {
  return requestClient.put<ProjectRuntimeProject>(
    `/system/project-runtime/projects/${id}`,
    data,
  );
}

export async function deleteProjectRuntimeProjectApi(id: string) {
  return requestClient.delete<boolean>(`/system/project-runtime/projects/${id}`);
}

export async function startProjectRuntimeWorkspaceApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult[]>(
    `/system/project-runtime/workspaces/${id}/start`,
  );
}

export async function stopProjectRuntimeWorkspaceApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult[]>(
    `/system/project-runtime/workspaces/${id}/stop`,
  );
}

export async function startProjectRuntimeServiceApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult>(
    `/system/project-runtime/services/${id}/start`,
  );
}

export async function stopProjectRuntimeServiceApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult>(
    `/system/project-runtime/services/${id}/stop`,
  );
}

export async function restartProjectRuntimeServiceApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult>(
    `/system/project-runtime/services/${id}/restart`,
  );
}

export async function buildProjectRuntimeServiceApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult>(
    `/system/project-runtime/services/${id}/build`,
  );
}

export async function getProjectRuntimeServiceLogsApi(
  id: string,
  lines = 200,
) {
  return requestClient.get<ProjectRuntimeLog>(
    `/system/project-runtime/services/${id}/logs`,
    { params: { lines } },
  );
}

export async function getProjectRuntimeServiceBuildLogsApi(
  id: string,
  lines = 200,
) {
  return requestClient.get<ProjectRuntimeLog>(
    `/system/project-runtime/services/${id}/build-logs`,
    { params: { lines } },
  );
}

export async function getProjectRuntimeServiceBuildHistoryApi(
  id: string,
  take = 20,
) {
  return requestClient.get<ProjectRuntimeBuildHistory[]>(
    `/system/project-runtime/services/${id}/build-history`,
    { params: { take } },
  );
}

export async function getProjectRuntimeServiceArtifactApi(id: string) {
  return requestClient.get<ProjectRuntimeArtifact>(
    `/system/project-runtime/services/${id}/artifact`,
  );
}

export async function openProjectRuntimeServiceArtifactApi(id: string) {
  return requestClient.post<ProjectRuntimeActionResult>(
    `/system/project-runtime/services/${id}/artifact/open`,
  );
}
