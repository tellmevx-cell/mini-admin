import { requestClient } from '#/api/request';

export interface NoticeItem {
  content: string;
  createdAt: string;
  id: string;
  isPublished: boolean;
  publishedAt?: null | string;
  title: string;
  type: string;
}

export interface NoticeListParams {
  isPublished?: boolean;
  page?: number;
  pageSize?: number;
  title?: string;
  type?: string;
}

export interface NoticeListResult {
  items: NoticeItem[];
  total: number;
}

export interface SaveNoticeParams {
  content: string;
  isPublished: boolean;
  title: string;
  type: string;
}

export async function getNoticeListApi(params: NoticeListParams) {
  return requestClient.get<NoticeListResult>('/system/notice/list', {
    params,
  });
}

export async function createNoticeApi(data: SaveNoticeParams) {
  return requestClient.post<NoticeItem>('/system/notice', data);
}

export async function updateNoticeApi(id: string, data: SaveNoticeParams) {
  return requestClient.put<NoticeItem>(`/system/notice/${id}`, data);
}

export async function deleteNoticeApi(id: string) {
  return requestClient.delete<boolean>(`/system/notice/${id}`);
}
