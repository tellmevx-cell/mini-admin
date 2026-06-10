import { requestClient } from '#/api/request';

export interface DictionaryItem {
  color?: null | string;
  id: string;
  isEnabled: boolean;
  label: string;
  order: number;
  typeId: string;
  value: string;
}

export interface DictionaryType {
  code: string;
  id: string;
  isEnabled: boolean;
  items: DictionaryItem[];
  name: string;
  order: number;
}

export interface SaveDictionaryTypeParams {
  code: string;
  isEnabled: boolean;
  name: string;
  order: number;
}

export interface SaveDictionaryItemParams {
  color?: null | string;
  isEnabled: boolean;
  label: string;
  order: number;
  typeId: string;
  value: string;
}

export async function getDictionaryListApi() {
  return requestClient.get<DictionaryType[]>('/system/dictionary/list');
}

export async function createDictionaryTypeApi(data: SaveDictionaryTypeParams) {
  return requestClient.post<DictionaryType>('/system/dictionary/type', data);
}

export async function updateDictionaryTypeApi(
  id: string,
  data: SaveDictionaryTypeParams,
) {
  return requestClient.put<DictionaryType>(`/system/dictionary/type/${id}`, data);
}

export async function deleteDictionaryTypeApi(id: string) {
  return requestClient.delete<boolean>(`/system/dictionary/type/${id}`);
}

export async function createDictionaryItemApi(data: SaveDictionaryItemParams) {
  return requestClient.post<DictionaryItem>('/system/dictionary/item', data);
}

export async function updateDictionaryItemApi(
  id: string,
  data: SaveDictionaryItemParams,
) {
  return requestClient.put<DictionaryItem>(`/system/dictionary/item/${id}`, data);
}

export async function deleteDictionaryItemApi(id: string) {
  return requestClient.delete<boolean>(`/system/dictionary/item/${id}`);
}
