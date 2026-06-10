import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';

import { requestClient } from '#/api/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export interface FileItem {
  contentType: string;
  createdAt: string;
  id: string;
  originalName: string;
  size: number;
  status: string;
  storagePath: string;
  storageProvider: string;
  storedName: string;
}

export interface FileListParams {
  originalName?: string;
  page?: number;
  pageSize?: number;
  storageProvider?: string;
}

export interface FileListResult {
  items: FileItem[];
  total: number;
}

interface ApiEnvelope<T> {
  code: number;
  data: T;
  message: string;
}

export async function getFileListApi(params: FileListParams) {
  return requestClient.get<FileListResult>('/system/file/list', { params });
}

export async function uploadFileApi(file: File) {
  const accessStore = useAccessStore();
  const formData = new FormData();
  formData.append('file', file);
  const url = new URL(
    `${apiURL.replace(/\/$/, '')}/system/file/upload`,
    window.location.origin,
  );
  const response = await fetch(url, {
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
    throw new Error(`Upload failed: ${response.status}`);
  }

  const result = (await response.json()) as ApiEnvelope<FileItem>;
  if (result.code !== 0) {
    throw new Error(result.message || 'Upload failed');
  }

  return result.data;
}

export async function deleteFileApi(id: string) {
  return requestClient.delete<boolean>(`/system/file/${id}`);
}

export async function markFileInvalidApi(id: string) {
  return requestClient.post<FileItem>(`/system/file/${id}/mark-invalid`);
}

export async function downloadFileApi(id: string) {
  const accessStore = useAccessStore();
  const url = new URL(
    `${apiURL.replace(/\/$/, '')}/system/file/${id}/download`,
    window.location.origin,
  );
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
