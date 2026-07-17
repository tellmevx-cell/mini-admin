import { requestClient } from '#/api/request';

export type TenantStatus = 'Active' | 'Disabled' | 'Expired' | 'Pending';
export type TenantInitializationStatus = 'Failed' | 'Pending' | 'Success';
export type TenantResourceQuotaStatus =
  | 'Exhausted'
  | 'Normal'
  | 'Unlimited'
  | 'Warning';
export type TenantLifecycleEventType =
  | 'Created'
  | 'Disabled'
  | 'Enabled'
  | 'ExpirationChanged'
  | 'Expired'
  | 'ExpiryReminder'
  | 'PackageChanged'
  | 'Renewed'
  | 'Updated';

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
  quotaLastNotifiedAt?: null | string;
  maxStorageBytes: number;
  maxUsers: number;
  remark?: null | string;
  status: TenantStatus;
  updatedAt: string;
  storageQuotaStatus: TenantResourceQuotaStatus;
  userQuotaStatus: TenantResourceQuotaStatus;
  usedStorageBytes: number;
  usedUsers: number;
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

export interface TenantLifecycleRecord {
  createdAt: string;
  description: string;
  eventType: TenantLifecycleEventType;
  fromStatus?: null | string;
  id: string;
  newExpireAt?: null | string;
  newPackageId?: null | string;
  operatorUserId?: null | string;
  operatorUserName?: null | string;
  previousExpireAt?: null | string;
  previousPackageId?: null | string;
  reminderDays?: null | number;
  source: 'Manual' | 'Scheduled' | 'System';
  tenantId: string;
  toStatus?: null | string;
}

export interface TenantLifecycleRecordListResult {
  items: TenantLifecycleRecord[];
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

export interface RenewTenantParams {
  expireAt: string;
  reactivate: boolean;
  remark?: null | string;
}

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

export async function renewTenantApi(id: string, data: RenewTenantParams) {
  return requestClient.post<TenantItem>(`/platform/tenant/${id}/renew`, data);
}

export async function getTenantLifecycleRecordsApi(
  id: string,
  params?: { eventType?: TenantLifecycleEventType; page?: number; pageSize?: number },
) {
  return requestClient.get<TenantLifecycleRecordListResult>(
    `/platform/tenant/${id}/lifecycle-records`,
    { params },
  );
}
