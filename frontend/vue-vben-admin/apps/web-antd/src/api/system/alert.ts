import { requestClient } from '#/api/request';

export interface AlertItem {
  acknowledgeRemark?: null | string;
  acknowledgedAt?: null | string;
  acknowledgedBy?: null | string;
  content: string;
  firstTriggeredAt: string;
  id: string;
  lastTriggeredAt: string;
  level: string;
  recoveredAt?: null | string;
  source: string;
  status: string;
  title: string;
  triggerCount: number;
  type: string;
}

export interface AlertListParams {
  level?: string;
  page?: number;
  pageSize?: number;
  status?: string;
  type?: string;
}

export interface AlertListResult {
  items: AlertItem[];
  total: number;
}

export interface AcknowledgeAlertParams {
  remark?: null | string;
}

export async function getAlertListApi(params: AlertListParams) {
  return requestClient.get<AlertListResult>('/system/alert/list', {
    params,
  });
}

export async function acknowledgeAlertApi(
  id: string,
  data: AcknowledgeAlertParams,
) {
  return requestClient.post<AlertItem>(
    `/system/alert/${id}/acknowledge`,
    data,
  );
}
