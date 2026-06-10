import { requestClient } from '#/api/request';

export interface AlertRuleItem {
  code: string;
  createdAt: string;
  description: string;
  emailEnabled: boolean;
  enabled: boolean;
  id: string;
  level: string;
  metric: string;
  name: string;
  notifyEnabled: boolean;
  operator: string;
  recipients: AlertRuleRecipient[];
  remark?: null | string;
  sort: number;
  threshold: number;
  updatedAt: string;
  windowMinutes: number;
}

export interface AlertRuleRecipient {
  id: string;
  recipientId: string;
  recipientName: string;
  recipientType: 'Role' | 'User';
}

export interface AlertRuleListParams {
  enabled?: boolean;
  keyword?: string;
  level?: string;
  page?: number;
  pageSize?: number;
}

export interface AlertRuleListResult {
  items: AlertRuleItem[];
  total: number;
}

export interface UpdateAlertRuleParams {
  emailEnabled: boolean;
  enabled: boolean;
  level: string;
  notifyEnabled: boolean;
  recipientRoleIds: string[];
  recipientUserIds: string[];
  remark?: null | string;
  threshold: number;
  windowMinutes: number;
}

export async function getAlertRuleListApi(params: AlertRuleListParams) {
  return requestClient.get<AlertRuleListResult>('/system/alert-rule/list', {
    params,
  });
}

export async function updateAlertRuleApi(
  id: string,
  data: UpdateAlertRuleParams,
) {
  return requestClient.put<AlertRuleItem>(`/system/alert-rule/${id}`, data);
}
