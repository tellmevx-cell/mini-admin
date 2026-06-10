 import { useAppConfig } from '@vben/hooks';
 import { preferences } from '@vben/preferences';
 import { useAccessStore } from '@vben/stores';

import { requestClient } from '#/api/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export interface EfmigrationshistoryItem {
  id: string;
   approvalStatus: string;
   workflowInstanceId?: null | string;
  productVersion: string;
  createdAt: string;
}

export interface EfmigrationshistoryListResult {
  items: EfmigrationshistoryItem[];
  total: number;
}

export interface SaveEfmigrationshistoryParams {
  productVersion: string;
}

export interface SubmitEfmigrationshistoryWorkflowParams {
  comment?: null | string;
}

export interface WithdrawEfmigrationshistoryWorkflowParams {
  comment?: null | string;
}

export interface EfmigrationshistoryImportError {
  field: string;
  message: string;
  rowNumber: number;
}

export interface EfmigrationshistoryImportResult {
  createdCount: number;
  errors: EfmigrationshistoryImportError[];
}

interface ApiEnvelope<T> {
  code: number;
  data: T;
  message: string;
}

export async function getEfmigrationshistoryListApi(params: Record<string, unknown>) {
  return requestClient.get<EfmigrationshistoryListResult>('/business/efmigrationshistory/list', { params });
}

export async function createEfmigrationshistoryApi(data: SaveEfmigrationshistoryParams) {
  return requestClient.post<EfmigrationshistoryItem>('/business/efmigrationshistory', data);
}

export async function updateEfmigrationshistoryApi(id: string, data: SaveEfmigrationshistoryParams) {
  return requestClient.put<EfmigrationshistoryItem>(`/business/efmigrationshistory/${id}`, data);
}

export async function deleteEfmigrationshistoryApi(id: string) {
  return requestClient.delete<boolean>(`/business/efmigrationshistory/${id}`);
}

export async function submitEfmigrationshistoryWorkflowApi(
  id: string,
  data: SubmitEfmigrationshistoryWorkflowParams = {},
) {
  return requestClient.post<EfmigrationshistoryItem>(
    `/business/efmigrationshistory/${id}/submit-workflow`,
    data,
  );
}

export async function withdrawEfmigrationshistoryWorkflowApi(
  id: string,
  data: WithdrawEfmigrationshistoryWorkflowParams = {},
) {
  return requestClient.post<EfmigrationshistoryItem>(
    `/business/efmigrationshistory/${id}/withdraw-workflow`,
    data,
  );
}

export async function exportEfmigrationshistoryApi(params: Record<string, unknown>) {
  return downloadEfmigrationshistoryWorkbook('/business/efmigrationshistory/export', params);
}

export async function downloadEfmigrationshistoryImportTemplateApi() {
  return downloadEfmigrationshistoryWorkbook('/business/efmigrationshistory/import-template');
}

export async function importEfmigrationshistoryApi(file: File) {
  return uploadEfmigrationshistoryWorkbook('/business/efmigrationshistory/import', file);
}

export async function previewImportEfmigrationshistoryApi(file: File) {
  return uploadEfmigrationshistoryWorkbook('/business/efmigrationshistory/import/preview', file);
}

export async function downloadEfmigrationshistoryImportErrorReportApi(file: File) {
  const accessStore = useAccessStore();
  const formData = new FormData();
  formData.append('file', file);
  const response = await fetch(
    `${apiURL.replace(/\/$/, '')}/business/efmigrationshistory/import/error-report`,
    {
      body: formData,
      headers: {
        'Accept-Language': preferences.app.locale,
        ...(accessStore.accessToken
          ? { Authorization: `Bearer ${accessStore.accessToken}` }
          : {}),
      },
      method: 'POST',
    },
  );

  if (!response.ok) {
    throw new Error(`Download failed: ${response.status}`);
  }

  return response.blob();
}

async function uploadEfmigrationshistoryWorkbook(path: string, file: File) {
  const accessStore = useAccessStore();
  const formData = new FormData();
  formData.append('file', file);
  const response = await fetch(`${apiURL.replace(/\/$/, '')}${path}`, {
    body: formData,
    headers: {
      'Accept-Language': preferences.app.locale,
      ...(accessStore.accessToken
        ? { Authorization: `Bearer ${accessStore.accessToken}` }
        : {}),
    },
    method: 'POST',
  });

  if (!response.ok) {
    throw new Error(`Import failed: ${response.status}`);
  }

  const result = (await response.json()) as ApiEnvelope<EfmigrationshistoryImportResult>;
  if (result.code !== 0) {
    throw new Error(result.message || 'Import failed');
  }

  return result.data;
}

async function downloadEfmigrationshistoryWorkbook(path: string, params?: Record<string, unknown>) {
  const accessStore = useAccessStore();
  const url = new URL(`${apiURL.replace(/\/$/, '')}${path}`, window.location.origin);

  Object.entries(params ?? {}).forEach(([key, value]) => {
    if (value !== undefined && value !== '') {
      url.searchParams.set(key, String(value));
    }
  });

  const response = await fetch(url, {
    headers: {
      'Accept-Language': preferences.app.locale,
      ...(accessStore.accessToken
        ? { Authorization: `Bearer ${accessStore.accessToken}` }
        : {}),
    },
  });

  if (!response.ok) {
    throw new Error(`Download failed: ${response.status}`);
  }

  return response.blob();
}