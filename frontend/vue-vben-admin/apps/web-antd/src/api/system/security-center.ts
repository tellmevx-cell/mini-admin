import { requestClient } from '#/api/request';

export interface SecurityCenterOverview {
  account: SecurityAccountSummary;
  login: SecurityLoginSummary;
  permission: SecurityPermissionSummary;
  recentEvents: SecurityEvent[];
  session: SecuritySessionSummary;
}

export interface SecurityAccountSummary {
  disabledUserCount: number;
  enabledUserCount: number;
  lockedUserCount: number;
  staleUserCount: number;
  totalUserCount: number;
}

export interface SecurityLoginSummary {
  failedIpCount24h: number;
  failedLoginCount24h: number;
  failedUserCount24h: number;
}

export interface SecurityPermissionSummary {
  permissionChangeCount24h: number;
  recentHighRiskEvents: SecurityEvent[];
}

export interface SecuritySessionSummary {
  onlineUserCount: number;
  recentForceLogoutEvents: SecurityEvent[];
}

export interface SecurityEvent {
  createdAt: string;
  description: string;
  eventType: string;
  id: string;
  ipAddress?: null | string;
  level: string;
  relatedEntityId?: null | string;
  relatedEntityType?: null | string;
  title: string;
  userAgent?: null | string;
  userId?: null | string;
  userName?: null | string;
}

export interface SecurityPolicy {
  captchaExpireSeconds: number;
  captchaRequiredFailures: number;
  lockoutFailures: number;
  lockoutMinutes: number;
  onlineActiveTimeoutMinutes: number;
  onlineTouchThrottleSeconds: number;
  staleUserDays: number;
}

export interface SecurityEventPage {
  items: SecurityEvent[];
  total: number;
}

export interface SecurityEventListParams {
  eventType?: string;
  level?: string;
  page?: number;
  pageSize?: number;
  userName?: string;
}

export async function getSecurityCenterOverviewApi() {
  return requestClient.get<SecurityCenterOverview>(
    '/system/security-center/overview',
  );
}

export async function getSecurityEventListApi(
  params: SecurityEventListParams,
) {
  return requestClient.get<SecurityEventPage>('/system/security-event/list', {
    params,
  });
}

export async function getSecurityPolicyApi() {
  return requestClient.get<SecurityPolicy>('/system/security-policy');
}

export async function updateSecurityPolicyApi(data: SecurityPolicy) {
  return requestClient.put<SecurityPolicy>('/system/security-policy', data);
}
