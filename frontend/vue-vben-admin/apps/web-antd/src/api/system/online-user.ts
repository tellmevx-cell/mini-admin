import { requestClient } from '#/api/request';

export interface OnlineUserItem {
  browserName?: null | string;
  deviceName?: null | string;
  ipAddress?: null | string;
  lastActiveAt: string;
  loginAt: string;
  realName: string;
  sessionId: string;
  userAgent?: null | string;
  userId: string;
  userName: string;
}

export interface OnlineUserListParams {
  page?: number;
  pageSize?: number;
  userName?: string;
}

export interface OnlineUserListResult {
  items: OnlineUserItem[];
  total: number;
}

export async function getOnlineUserListApi(params: OnlineUserListParams) {
  return requestClient.get<OnlineUserListResult>('/system/online-user/list', {
    params,
  });
}

export async function forceLogoutOnlineUserApi(userId: string) {
  return requestClient.post<boolean>(
    `/system/online-user/${userId}/force-logout`,
  );
}

export async function forceLogoutOnlineSessionApi(sessionId: string) {
  return requestClient.post<boolean>(
    `/system/online-user/session/${sessionId}/force-logout`,
  );
}
