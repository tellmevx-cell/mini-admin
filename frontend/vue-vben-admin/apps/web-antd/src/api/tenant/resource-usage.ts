import { requestClient } from '#/api/request';

export type TenantQuotaStatus =
  | 'Exhausted'
  | 'Normal'
  | 'Unlimited'
  | 'Warning';

export interface TenantResourceMetric {
  displayName: string;
  lastNotifiedAt?: null | string;
  limitValue: number;
  managementPath: string;
  resourceType: 'Storage' | 'Users';
  status: TenantQuotaStatus;
  usagePercent: number;
  usedValue: number;
}

export interface TenantResourceUsage {
  checkedAt: string;
  overallStatus: TenantQuotaStatus;
  packageName?: null | string;
  storage: TenantResourceMetric;
  tenantCode: string;
  tenantId: string;
  tenantName: string;
  users: TenantResourceMetric;
}

export async function getCurrentTenantResourceUsageApi() {
  return requestClient.get<null | TenantResourceUsage>(
    '/tenant/resource-usage',
  );
}
