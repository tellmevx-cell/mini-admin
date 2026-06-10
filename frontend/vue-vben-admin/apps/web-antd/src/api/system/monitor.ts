import { requestClient } from '#/api/request';

export interface SystemMonitorOverview {
  api: SystemMonitorApi;
  application: SystemMonitorApplication;
  cpu: SystemMonitorCpu;
  dependencies: SystemMonitorDependency[];
  memory: SystemMonitorMemory;
  recent: SystemMonitorRecent;
  server: SystemMonitorServer;
}

export interface SystemMonitorApi {
  status: string;
  timestamp: string;
}

export interface SystemMonitorCpu {
  processCpuPercent: number;
  processorCount: number;
  threadCount: number;
}

export interface SystemMonitorMemory {
  availablePhysicalMemoryBytes: number;
  gcTotalMemoryBytes: number;
  gen0Collections: number;
  gen1Collections: number;
  gen2Collections: number;
  managedHeapBytes: number;
  physicalMemoryUsedPercent: number;
  totalPhysicalMemoryBytes: number;
  usedPhysicalMemoryBytes: number;
  workingSetBytes: number;
}

export interface SystemMonitorApplication {
  contentRootPath: string;
  environment: string;
  runtimeVersion: string;
  startedAt: string;
  uptimeSeconds: number;
}

export interface SystemMonitorServer {
  architecture: string;
  machineName: string;
  operatingSystem: string;
}

export interface SystemMonitorDependency {
  description: string;
  elapsedMilliseconds?: null | number;
  name: string;
  status: string;
}

export interface SystemMonitorRecent {
  abnormalFileCount: number;
  failedAuditLogCount: number;
  failedScheduledJobCount: number;
  onlineUserCount: number;
}

export async function getSystemMonitorOverviewApi() {
  return requestClient.get<SystemMonitorOverview>('/system/monitor/overview');
}
