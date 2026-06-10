import { requestClient } from '#/api/request';

export interface LoginLogItem {
  createdAt: string;
  id: string;
  ipAddress?: null | string;
  isSuccess: boolean;
  message: string;
  realName?: null | string;
  userAgent?: null | string;
  userId?: null | string;
  userName: string;
}

export interface LoginLogListParams {
  endCreatedAt?: string;
  isSuccess?: boolean;
  page?: number;
  pageSize?: number;
  startCreatedAt?: string;
  userName?: string;
}

export interface LoginLogListResult {
  items: LoginLogItem[];
  total: number;
}

export async function getLoginLogListApi(params: LoginLogListParams) {
  return requestClient.get<LoginLogListResult>('/system/login-log/list', {
    params,
  });
}
