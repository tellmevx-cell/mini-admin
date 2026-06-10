import { requestClient } from '#/api/request';

export type TenantStatus = 'Active' | 'Disabled' | 'Expired' | 'Pending';
export type TenantInitializationStatus = 'Failed' | 'Pending' | 'Success';

export interface TenantItem {
  code: string;
  contactEmail?: null | string;
  contactName?: null | string;
  contactPhone?: null | string;
  createdAt: string;
  expireAt?: null | string;
  id: string;
  initializationError?: null | string;
  initializationStatus: TenantInitializationStatus;
  initializationTemplateCode: string;
  initializedAt?: null | string;
  name: string;
  packageId?: null | string;
  packageName?: null | string;
  remark?: null | string;
  status: TenantStatus;
  updatedAt: string;
}

export interface TenantListParams {
  code?: string;
  name?: string;
  page?: number;
  pageSize?: number;
  status?: TenantStatus;
}

export interface TenantListResult {
  items: TenantItem[];
  total: number;
}

export interface SaveTenantBaseParams {
  contactEmail?: null | string;
  contactName?: null | string;
  contactPhone?: null | string;
  expireAt?: null | string;
  name: string;
  packageId?: null | string;
  remark?: null | string;
}

export interface CreateTenantParams extends SaveTenantBaseParams {
  adminEmail?: null | string;
  adminPassword: string;
  adminRealName: string;
  adminUserName: string;
  code: string;
  initializationTemplateCode?: null | string;
}

export type UpdateTenantParams = SaveTenantBaseParams;

export interface TenantInitializationTemplate {
  code: string;
  description: string;
  isDefault: boolean;
  name: string;
}

export async function getTenantListApi(params: TenantListParams) {
  return requestClient.get<TenantListResult>('/platform/tenant/list', {
    params,
  });
}

export async function getTenantInitializationTemplatesApi() {
  return requestClient.get<TenantInitializationTemplate[]>(
    '/platform/tenant/initialization-templates',
  );
}

export async function createTenantApi(data: CreateTenantParams) {
  return requestClient.post<TenantItem>('/platform/tenant', data);
}

export async function updateTenantApi(id: string, data: UpdateTenantParams) {
  return requestClient.put<TenantItem>(`/platform/tenant/${id}`, data);
}

export async function enableTenantApi(id: string) {
  return requestClient.post<TenantItem>(`/platform/tenant/${id}/enable`);
}

export async function disableTenantApi(id: string) {
  return requestClient.post<TenantItem>(`/platform/tenant/${id}/disable`);
}
