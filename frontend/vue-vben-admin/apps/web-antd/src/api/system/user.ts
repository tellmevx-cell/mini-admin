import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';

import { requestClient } from '#/api/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export interface UserListItem {
  departmentId?: null | string;
  departmentName?: null | string;
  email?: null | string;
  id: string;
  loginLockRemainingSeconds?: null | number;
  positionId?: null | string;
  positionName?: null | string;
  realName: string;
  roles: string[];
  status: number;
  userName: string;
}

export interface UserListParams {
  departmentId?: string;
  page?: number;
  pageSize?: number;
  positionId?: string;
  userName?: string;
}

export interface UserListResult {
  items: UserListItem[];
  total: number;
}

export interface CreateUserParams {
  departmentId?: null | string;
  email?: null | string;
  isEnabled: boolean;
  password: string;
  positionId?: null | string;
  realName: string;
  roleIds: string[];
  userName: string;
}

export interface UpdateUserParams {
  departmentId?: null | string;
  email?: null | string;
  isEnabled: boolean;
  password?: null | string;
  positionId?: null | string;
  realName: string;
  roleIds: string[];
}

export interface ResetUserPasswordParams {
  confirmPassword: string;
  newPassword: string;
}

export interface UserImportError {
  message: string;
  rowNumber: number;
  userName: string;
}

export interface UserImportResult {
  createdCount: number;
  errors: UserImportError[];
}

interface ApiEnvelope<T> {
  code: number;
  data: T;
  message: string;
}

export async function getUserListApi(params: UserListParams) {
  return requestClient.get<UserListResult>('/system/user/list', { params });
}

export async function createUserApi(data: CreateUserParams) {
  return requestClient.post<UserListItem>('/system/user', data);
}

export async function updateUserApi(id: string, data: UpdateUserParams) {
  return requestClient.put<UserListItem>(`/system/user/${id}`, data);
}

export async function deleteUserApi(id: string) {
  return requestClient.delete<boolean>(`/system/user/${id}`);
}

export async function unlockUserLoginApi(userName: string) {
  return requestClient.post<boolean>(
    `/system/user/${encodeURIComponent(userName)}/unlock-login`,
  );
}

export async function resetUserPasswordApi(
  id: string,
  data: ResetUserPasswordParams,
) {
  return requestClient.post<boolean>(
    `/system/user/${encodeURIComponent(id)}/reset-password`,
    data,
  );
}

export async function exportUserApi(params: UserListParams) {
  return downloadUserWorkbook('/system/user/export', params);
}

export async function downloadUserImportTemplateApi() {
  return downloadUserWorkbook('/system/user/import-template');
}

export async function importUserApi(file: File) {
  return uploadUserWorkbook('/system/user/import', file);
}

export async function previewImportUserApi(file: File) {
  return uploadUserWorkbook('/system/user/import/preview', file);
}

export async function downloadUserImportErrorReportApi(file: File) {
  const accessStore = useAccessStore();
  const formData = new FormData();
  formData.append('file', file);
  const response = await fetch(
    `${apiURL.replace(/\/$/, '')}/system/user/import/error-report`,
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

async function uploadUserWorkbook(path: string, file: File) {
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

  const result = (await response.json()) as ApiEnvelope<UserImportResult>;
  if (result.code !== 0) {
    throw new Error(result.message || 'Import failed');
  }

  return result.data;
}

async function downloadUserWorkbook(path: string, params?: UserListParams) {
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
