import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';

import { requestClient } from '#/api/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export interface AuditLogItem {
  action: string;
  createdAt: string;
  elapsedMilliseconds: number;
  entityChanges: AuditEntityChangeItem[];
  errorMessage?: null | string;
  id: string;
  ipAddress?: null | string;
  isSuccess: boolean;
  method: string;
  module: string;
  path: string;
  queryString?: null | string;
  requestBody: string;
  resourceId?: null | string;
  statusCode: number;
  userAgent?: null | string;
  userId?: null | string;
  userName?: null | string;
}

export interface AuditEntityChangeItem {
  afterJson?: null | string;
  auditLogId: string;
  beforeJson?: null | string;
  createdAt: string;
  diffJson: string;
  entityId: string;
  entityName: string;
  id: string;
  operationType: string;
}

export interface AuditLogListParams {
  action?: string;
  endCreatedAt?: string;
  isSuccess?: boolean;
  method?: string;
  module?: string;
  page?: number;
  pageSize?: number;
  path?: string;
  startCreatedAt?: string;
  userName?: string;
}

export interface AuditLogListResult {
  items: AuditLogItem[];
  total: number;
}

export async function getAuditLogListApi(params: AuditLogListParams) {
  return requestClient.get<AuditLogListResult>('/system/audit-log/list', {
    params,
  });
}

export async function exportAuditLogCsvApi(params: AuditLogListParams) {
  const accessStore = useAccessStore();
  const url = new URL(
    `${apiURL.replace(/\/$/, '')}/system/audit-log/export`,
    window.location.origin,
  );

  Object.entries(params).forEach(([key, value]) => {
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
    throw new Error(`Export failed: ${response.status}`);
  }

  return response.blob();
}
