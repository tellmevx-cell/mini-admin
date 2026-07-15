import { requestClient } from '#/api/request';

export interface SystemMonitorOverview {
  api: SystemMonitorApi;
  application: SystemMonitorApplication;
  cpu: SystemMonitorCpu;
  dependencies: SystemMonitorDependency[];
  disks: SystemMonitorDisk[];
  hardware: SystemMonitorHardware;
  memory: SystemMonitorMemory;
  networks: SystemMonitorNetwork[];
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
  garbageCollectionLatencyMode: string;
  is64BitProcess: boolean;
  processArchitecture: string;
  processId: number;
  runtimeVersion: string;
  serverGarbageCollection: boolean;
  startedAt: string;
  uptimeSeconds: number;
}

export interface SystemMonitorHardware {
  cpuModel: string;
  gpus: string[];
  manufacturer: string;
  model: string;
  motherboardManufacturer: string;
  motherboardModel: string;
}

export interface SystemMonitorDisk {
  availableBytes: number;
  driveType: string;
  fileSystem: string;
  isReady: boolean;
  name: string;
  rootPath: string;
  totalBytes: number;
  usedBytes: number;
  usedPercent: number;
}

export interface SystemMonitorNetwork {
  addresses: string[];
  bytesReceived: number;
  bytesSent: number;
  description: string;
  interfaceType: string;
  macAddress: string;
  name: string;
  speedBitsPerSecond: number;
  status: string;
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
