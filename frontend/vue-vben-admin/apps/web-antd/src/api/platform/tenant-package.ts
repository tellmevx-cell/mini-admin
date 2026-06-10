import { requestClient } from '#/api/request';

export interface TenantPackageItem {
  id: string;
  isEnabled: boolean;
  maxStorageMb: number;
  maxUsers: number;
  menuCount: number;
  name: string;
  remark?: null | string;
}

export interface TenantPackageOption {
  id: string;
  isEnabled: boolean;
  name: string;
}

export interface TenantPackageListParams {
  isEnabled?: boolean;
  name?: string;
  page?: number;
  pageSize?: number;
}

export interface TenantPackageListResult {
  items: TenantPackageItem[];
  total: number;
}

export interface SaveTenantPackageParams {
  isEnabled: boolean;
  maxStorageMb: number;
  maxUsers: number;
  name: string;
  remark?: null | string;
}

export async function getTenantPackageListApi(params: TenantPackageListParams) {
  return requestClient.get<TenantPackageListResult>('/platform/tenant-package/list', {
    params,
  });
}

export async function getTenantPackageOptionsApi() {
  return requestClient.get<TenantPackageOption[]>('/platform/tenant-package/options');
}

export async function createTenantPackageApi(data: SaveTenantPackageParams) {
  return requestClient.post<TenantPackageItem>('/platform/tenant-package', data);
}

export async function updateTenantPackageApi(
  id: string,
  data: SaveTenantPackageParams,
) {
  return requestClient.put<TenantPackageItem>(`/platform/tenant-package/${id}`, data);
}

export async function enableTenantPackageApi(id: string) {
  return requestClient.post<TenantPackageItem>(
    `/platform/tenant-package/${id}/enable`,
  );
}

export async function disableTenantPackageApi(id: string) {
  return requestClient.post<TenantPackageItem>(
    `/platform/tenant-package/${id}/disable`,
  );
}

export async function getTenantPackageMenusApi(id: string) {
  return requestClient.get<string[]>(`/platform/tenant-package/${id}/menus`);
}

export async function updateTenantPackageMenusApi(
  id: string,
  menuIds: string[],
) {
  return requestClient.put<string[]>(`/platform/tenant-package/${id}/menus`, {
    menuIds,
  });
}
