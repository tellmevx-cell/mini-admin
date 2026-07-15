import { requestClient } from '#/api/request';

export interface SampleOrderItem {
  id: string;
  workflowInstanceId?: null | string;
  originalName: string;
  storedName: string;
  contentType: string;
  size: number;
  storageProvider: string;
  storagePath: string;
  status: string;
  createdAt: string;
}

export interface SampleOrderListResult {
  items: SampleOrderItem[];
  total: number;
}

export interface SaveSampleOrderParams {
  originalName: string;
  storedName: string;
  contentType: string;
  size: number;
  storageProvider: string;
  storagePath: string;
  status: string;
}

export interface SubmitSampleOrderWorkflowParams {
  comment?: null | string;
  definitionId: string;
}

export interface WithdrawSampleOrderWorkflowParams {
  comment?: null | string;
}

export async function getSampleOrderListApi(params: Record<string, unknown>) {
  return requestClient.get<SampleOrderListResult>('/business/sample-order/list', {
    params,
    responseReturn: 'body',
  });
}

export async function createSampleOrderApi(data: SaveSampleOrderParams) {
  return requestClient.post<SampleOrderItem>('/business/sample-order', data, {
    responseReturn: 'body',
  });
}

export async function updateSampleOrderApi(id: string, data: SaveSampleOrderParams) {
  return requestClient.put<SampleOrderItem>(`/business/sample-order/${id}`, data, {
    responseReturn: 'body',
  });
}

export async function deleteSampleOrderApi(id: string) {
  return requestClient.delete<boolean>(`/business/sample-order/${id}`, {
    responseReturn: 'body',
  });
}

export async function submitSampleOrderWorkflowApi(
  id: string,
  data: SubmitSampleOrderWorkflowParams,
) {
  return requestClient.post<SampleOrderItem>(
    `/business/sample-order/${id}/submit-workflow`,
    data,
    { responseReturn: 'body' },
  );
}

export async function withdrawSampleOrderWorkflowApi(
  id: string,
  data: WithdrawSampleOrderWorkflowParams,
) {
  return requestClient.post<SampleOrderItem>(
    `/business/sample-order/${id}/withdraw-workflow`,
    data,
    { responseReturn: 'body' },
  );
}
