import { requestClient } from '#/api/request';

export interface MenuTreeNode {
  children: MenuTreeNode[];
  id: string;
  name: string;
  title: string;
}

export interface MenuManagementItem {
  affixTab: boolean;
  children: MenuManagementItem[];
  component?: null | string;
  icon?: null | string;
  id: string;
  isEnabled: boolean;
  isVisible: boolean;
  name: string;
  order: number;
  parentId?: null | string;
  path: string;
  permissionCode?: null | string;
  redirect?: null | string;
  title: string;
}

export interface SaveMenuParams {
  affixTab: boolean;
  component?: null | string;
  icon?: null | string;
  isEnabled: boolean;
  isVisible: boolean;
  name: string;
  order: number;
  parentId?: null | string;
  path: string;
  permissionCode?: null | string;
  redirect?: null | string;
  title: string;
}

export async function getMenuTreeApi() {
  return requestClient.get<MenuTreeNode[]>('/system/menu/tree');
}

export async function getMenuListApi() {
  return requestClient.get<MenuManagementItem[]>('/system/menu/list');
}

export async function createMenuApi(data: SaveMenuParams) {
  return requestClient.post<MenuManagementItem>('/system/menu', data);
}

export async function updateMenuApi(id: string, data: SaveMenuParams) {
  return requestClient.put<MenuManagementItem>(`/system/menu/${id}`, data);
}

export async function deleteMenuApi(id: string) {
  return requestClient.delete<boolean>(`/system/menu/${id}`);
}
