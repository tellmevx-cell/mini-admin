import { requestClient } from '#/api/request';

const rawResponse = { responseReturn: 'body' as const };

export interface PlatformPermissionDefinition {
  action: string;
  code: string;
  i18nKey: string;
  resource: string;
  title: {
    enUs: string;
    zhCn: string;
  };
}

export interface PlatformPageDefinition {
  component?: null | string;
  icon?: null | string;
  i18nKey: string;
  isVisible: boolean;
  key: string;
  order: number;
  parentKey?: null | string;
  path: string;
  permissions: PlatformPermissionDefinition[];
  redirect?: null | string;
  title: {
    enUs: string;
    zhCn: string;
  };
}

export interface AbacPolicyItem {
  action: string;
  conditionsJson: string;
  createdAt: string;
  description?: null | string;
  effect: 'Allow' | 'Deny';
  id: string;
  isEnabled: boolean;
  name: string;
  priority: number;
  resource: string;
  subjectId?: null | string;
  subjectType: 'Any' | 'Application' | 'Role' | 'User';
  tenantId?: null | string;
  updatedAt: string;
}

export interface SaveAbacPolicyParams {
  action: string;
  conditionsJson?: null | string;
  description?: null | string;
  effect: 'Allow' | 'Deny';
  isEnabled: boolean;
  name: string;
  priority: number;
  resource: string;
  subjectId?: null | string;
  subjectType: 'Any' | 'Application' | 'Role' | 'User';
  tenantId?: null | string;
}

export interface PlatformCacheEntry {
  category: string;
  expiresAt?: null | string;
  lastAccessedAt: string;
  logicalKey: string;
  physicalKey: string;
  tags: string[];
  tenantId?: null | string;
}

export interface PlatformCacheOperationResult {
  message: string;
  success: boolean;
}

export async function getPlatformPagesApi() {
  return requestClient.get<PlatformPageDefinition[]>(
    '/platform/metadata/pages',
    rawResponse,
  );
}

export async function getAbacPoliciesApi() {
  return requestClient.get<AbacPolicyItem[]>(
    '/platform/abac-policies',
    rawResponse,
  );
}

export async function createAbacPolicyApi(data: SaveAbacPolicyParams) {
  return requestClient.post<AbacPolicyItem>(
    '/platform/abac-policies',
    data,
    rawResponse,
  );
}

export async function updateAbacPolicyApi(
  id: string,
  data: SaveAbacPolicyParams,
) {
  return requestClient.put<AbacPolicyItem>(
    `/platform/abac-policies/${id}`,
    data,
    rawResponse,
  );
}

export async function deleteAbacPolicyApi(id: string) {
  return requestClient.delete<boolean>(
    `/platform/abac-policies/${id}`,
    rawResponse,
  );
}

export async function getPlatformCacheEntriesApi(category?: string) {
  return requestClient.get<PlatformCacheEntry[]>('/platform/cache/entries', {
    ...rawResponse,
    params: { category: category || undefined },
  });
}

export async function invalidatePlatformCacheTagsApi(tags: string[]) {
  return requestClient.post<PlatformCacheOperationResult>(
    '/platform/cache/invalidate-tags',
    { tags, tenantId: null },
    rawResponse,
  );
}

export async function removePlatformCacheEntryApi(
  category: string,
  logicalKey: string,
) {
  return requestClient.post<PlatformCacheOperationResult>(
    '/platform/cache/remove-entry',
    { category, logicalKey, tenantId: null },
    rawResponse,
  );
}
