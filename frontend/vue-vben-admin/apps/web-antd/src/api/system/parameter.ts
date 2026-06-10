import { requestClient } from '#/api/request';

export interface SystemParameterItem {
  group: string;
  id: string;
  isEnabled: boolean;
  key: string;
  name: string;
  order: number;
  remark?: null | string;
  value: string;
}

export interface SystemParameterListParams {
  group?: string;
  key?: string;
  name?: string;
  page?: number;
  pageSize?: number;
}

export interface SystemParameterListResult {
  items: SystemParameterItem[];
  total: number;
}

export interface SaveSystemParameterParams {
  group: string;
  isEnabled: boolean;
  key: string;
  name: string;
  order: number;
  remark?: null | string;
  value: string;
}

export async function getSystemParameterListApi(
  params: SystemParameterListParams,
) {
  return requestClient.get<SystemParameterListResult>('/system/parameter/list', {
    params,
  });
}

export async function createSystemParameterApi(
  data: SaveSystemParameterParams,
) {
  return requestClient.post<SystemParameterItem>('/system/parameter', data);
}

export async function updateSystemParameterApi(
  id: string,
  data: SaveSystemParameterParams,
) {
  return requestClient.put<SystemParameterItem>(`/system/parameter/${id}`, data);
}

export async function deleteSystemParameterApi(id: string) {
  return requestClient.delete<boolean>(`/system/parameter/${id}`);
}
