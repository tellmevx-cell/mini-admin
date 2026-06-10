import { requestClient } from '#/api/request';

export interface RoleListItem {
  code: string;
  customDepartmentIds?: null | string[];
  dataScope: string;
  id: string;
  name: string;
  status: number;
}

export interface RoleListParams {
  code?: string;
  name?: string;
  page?: number;
  pageSize?: number;
}

export interface RoleListResult {
  items: RoleListItem[];
  total: number;
}

export interface CreateRoleParams {
  code: string;
  customDepartmentIds?: string[];
  dataScope: string;
  isEnabled: boolean;
  name: string;
}

export interface UpdateRoleParams {
  customDepartmentIds?: string[];
  dataScope: string;
  isEnabled: boolean;
  name: string;
}

export async function getRoleListApi(params: RoleListParams) {
  return requestClient.get<RoleListResult>('/system/role/list', { params });
}

export async function createRoleApi(data: CreateRoleParams) {
  return requestClient.post<RoleListItem>('/system/role', data);
}

export async function updateRoleApi(id: string, data: UpdateRoleParams) {
  return requestClient.put<RoleListItem>(`/system/role/${id}`, data);
}

export async function deleteRoleApi(id: string) {
  return requestClient.delete<boolean>(`/system/role/${id}`);
}

export async function getRoleMenusApi(id: string) {
  return requestClient.get<string[]>(`/system/role/${id}/menus`);
}

export async function updateRoleMenusApi(id: string, menuIds: string[]) {
  return requestClient.put<string[]>(`/system/role/${id}/menus`, { menuIds });
}
