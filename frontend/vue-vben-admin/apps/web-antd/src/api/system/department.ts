import { requestClient } from '#/api/request';

export interface DepartmentItem {
  children: DepartmentItem[];
  code: string;
  id: string;
  isEnabled: boolean;
  leader?: null | string;
  name: string;
  order: number;
  parentId?: null | string;
  phone?: null | string;
}

export interface SaveDepartmentParams {
  code: string;
  isEnabled: boolean;
  leader?: null | string;
  name: string;
  order: number;
  parentId?: null | string;
  phone?: null | string;
}

export async function getDepartmentListApi() {
  return requestClient.get<DepartmentItem[]>('/system/department/list');
}

export async function createDepartmentApi(data: SaveDepartmentParams) {
  return requestClient.post<DepartmentItem>('/system/department', data);
}

export async function updateDepartmentApi(
  id: string,
  data: SaveDepartmentParams,
) {
  return requestClient.put<DepartmentItem>(`/system/department/${id}`, data);
}

export async function deleteDepartmentApi(id: string) {
  return requestClient.delete<boolean>(`/system/department/${id}`);
}
