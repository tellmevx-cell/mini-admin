import { requestClient } from '#/api/request';

export interface ScheduledJobItem {
  description?: null | string;
  id: string;
  intervalSeconds: number;
  isEnabled: boolean;
  jobKey: string;
  lastMessage?: null | string;
  lastRunAt?: null | string;
  lastStatus: string;
  name: string;
  nextRunAt?: null | string;
}

export interface ScheduledJobListParams {
  isEnabled?: boolean;
  jobKey?: string;
  name?: string;
  page?: number;
  pageSize?: number;
}

export interface ScheduledJobListResult {
  items: ScheduledJobItem[];
  total: number;
}

export interface SaveScheduledJobRequest {
  description?: null | string;
  intervalSeconds: number;
  isEnabled: boolean;
  name: string;
}

export interface ScheduledJobRunResult {
  elapsedMilliseconds: number;
  jobId: string;
  jobKey: string;
  message: string;
  status: string;
}

export interface ScheduledJobLogItem {
  elapsedMilliseconds: number;
  finishedAt: string;
  id: string;
  jobId: string;
  jobKey: string;
  jobName: string;
  message: string;
  startedAt: string;
  status: string;
  triggerType: string;
}

export interface ScheduledJobLogDetailItem {
  createdAt: string;
  detailType: string;
  id: string;
  jobId: string;
  jobKey: string;
  logId: string;
  message: string;
  status: string;
  storagePath?: null | string;
  storageProvider?: null | string;
  targetId?: null | string;
  targetName?: null | string;
  targetType: string;
}

export interface ScheduledJobLogListResult {
  items: ScheduledJobLogItem[];
  total: number;
}

export interface ScheduledJobLogDetailListResult {
  items: ScheduledJobLogDetailItem[];
  total: number;
}

export async function getScheduledJobListApi(
  params: ScheduledJobListParams,
) {
  return requestClient.get<ScheduledJobListResult>(
    '/system/scheduled-job/list',
    { params },
  );
}

export async function updateScheduledJobApi(
  id: string,
  data: SaveScheduledJobRequest,
) {
  return requestClient.put<ScheduledJobItem>(
    `/system/scheduled-job/${id}`,
    data,
  );
}

export async function runScheduledJobApi(id: string) {
  return requestClient.post<ScheduledJobRunResult>(
    `/system/scheduled-job/${id}/run`,
  );
}

export async function getScheduledJobLogsApi(
  id: string,
  params: { page?: number; pageSize?: number },
) {
  return requestClient.get<ScheduledJobLogListResult>(
    `/system/scheduled-job/${id}/logs`,
    { params },
  );
}

export async function getScheduledJobLogDetailsApi(
  logId: string,
  params: { page?: number; pageSize?: number },
) {
  return requestClient.get<ScheduledJobLogDetailListResult>(
    `/system/scheduled-job/logs/${logId}/details`,
    { params },
  );
}
