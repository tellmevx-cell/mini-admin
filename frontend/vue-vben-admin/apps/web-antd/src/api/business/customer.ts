import { requestClient } from '#/api/request';

export interface CustomerItem {
  id: string;
  title: string;
  type: string;
  content: string;
  isPublished: number;
  publishedAt: string;
  createdAt: string;
}

export interface CustomerListResult {
  items: CustomerItem[];
  total: number;
}

export interface SaveCustomerParams {
  content: string;
  isPublished: number;
  publishedAt?: null | string;
  title: string;
  type: string;
}

export async function getCustomerListApi(params: Record<string, unknown>) {
  return requestClient.get<CustomerListResult>('/business/customer/list', { params });
}

export async function createCustomerApi(data: SaveCustomerParams) {
  return requestClient.post<CustomerItem>('/business/customer', data);
}

export async function updateCustomerApi(id: string, data: SaveCustomerParams) {
  return requestClient.put<CustomerItem>(`/business/customer/${id}`, data);
}

export async function deleteCustomerApi(id: string) {
  return requestClient.delete<boolean>(`/business/customer/${id}`);
}
