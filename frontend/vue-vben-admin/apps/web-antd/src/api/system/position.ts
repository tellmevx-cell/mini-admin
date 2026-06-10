import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';

import { requestClient } from '#/api/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export interface PositionItem {
  code: string;
  id: string;
  isEnabled: boolean;
  name: string;
  order: number;
  remark?: null | string;
}

export interface PositionListParams {
  code?: string;
  name?: string;
  page?: number;
  pageSize?: number;
}

export interface PositionListResult {
  items: PositionItem[];
  total: number;
}

export interface SavePositionParams {
  code: string;
  isEnabled: boolean;
  name: string;
  order: number;
  remark?: null | string;
}

export interface PositionImportError {
  code: string;
  message: string;
  rowNumber: number;
}

export interface PositionImportResult {
  createdCount: number;
  errors: PositionImportError[];
}

interface ApiEnvelope<T> {
  code: number;
  data: T;
  message: string;
}

export async function getPositionListApi(params: PositionListParams) {
  return requestClient.get<PositionListResult>('/system/position/list', {
    params,
  });
}

export async function createPositionApi(data: SavePositionParams) {
  return requestClient.post<PositionItem>('/system/position', data);
}

export async function updatePositionApi(id: string, data: SavePositionParams) {
  return requestClient.put<PositionItem>(`/system/position/${id}`, data);
}

export async function deletePositionApi(id: string) {
  return requestClient.delete<boolean>(`/system/position/${id}`);
}

export async function exportPositionApi(params: PositionListParams) {
  return downloadPositionWorkbook('/system/position/export', params);
}

export async function downloadPositionImportTemplateApi() {
  return downloadPositionWorkbook('/system/position/import-template');
}

export async function importPositionApi(file: File) {
  return uploadPositionWorkbook('/system/position/import', file);
}

export async function previewImportPositionApi(file: File) {
  return uploadPositionWorkbook('/system/position/import/preview', file);
}

export async function downloadPositionImportErrorReportApi(file: File) {
  const accessStore = useAccessStore();
  const formData = new FormData();
  formData.append('file', file);
  const response = await fetch(
    `${apiURL.replace(/\/$/, '')}/system/position/import/error-report`,
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

async function uploadPositionWorkbook(path: string, file: File) {
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

  const result = (await response.json()) as ApiEnvelope<PositionImportResult>;
  if (result.code !== 0) {
    throw new Error(result.message || 'Import failed');
  }

  return result.data;
}

async function downloadPositionWorkbook(
  path: string,
  params?: PositionListParams,
) {
  const accessStore = useAccessStore();
  const url = new URL(
    `${apiURL.replace(/\/$/, '')}${path}`,
    window.location.origin,
  );

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
