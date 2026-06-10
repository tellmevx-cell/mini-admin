import { useAppConfig } from '@vben/hooks';
import { preferences } from '@vben/preferences';
import { useAccessStore } from '@vben/stores';

import { requestClient } from '#/api/request';

const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);

export interface WorkflowNodeItem {
  approvalMode: string;
  approverRoleId?: null | string;
  approverRoleName?: null | string;
  approverType: string;
  approverUserId?: null | string;
  approverUserName?: null | string;
  designerNodeId: string;
  id: string;
  isEnabled: boolean;
  name: string;
  nodeType: string;
  order: number;
  slaMinutes?: null | number;
}

export interface WorkflowDefinitionItem {
  code: string;
  createdAt: string;
  description?: null | string;
  designerJson: string;
  formSchemaJson: string;
  formName?: null | string;
  id: string;
  isEnabled: boolean;
  name: string;
  nodes: WorkflowNodeItem[];
  publishedAt?: null | string;
  publishStatus: string;
  updatedAt: string;
  version: number;
}

export interface WorkflowDefinitionOption {
  code: string;
  formSchemaJson: string;
  formName?: null | string;
  id: string;
  name: string;
  version: number;
}

export interface WorkflowBusinessBindingItem {
  businessName: string;
  businessType: string;
  createdAt: string;
  definitionCode: string;
  definitionId: string;
  definitionName: string;
  definitionPublishStatus: string;
  definitionVersion: number;
  id: string;
  isEnabled: boolean;
  remark?: null | string;
  updatedAt: string;
}

export interface WorkflowBusinessBindingListResult {
  items: WorkflowBusinessBindingItem[];
  total: number;
}

export interface SaveWorkflowBusinessBindingRequest {
  businessName: string;
  businessType: string;
  definitionId: string;
  isEnabled: boolean;
  remark?: null | string;
}

export interface WorkflowApproverUserOption {
  id: string;
  realName: string;
  userName: string;
}

export interface WorkflowApproverRoleOption {
  code: string;
  enabledUserCount: number;
  id: string;
  name: string;
}

export interface WorkflowDefinitionListResult {
  items: WorkflowDefinitionItem[];
  total: number;
}

export interface SaveWorkflowNodeRequest {
  approvalMode?: string;
  approverRoleId?: null | string;
  approverType: string;
  approverUserId?: null | string;
  designerNodeId?: null | string;
  isEnabled: boolean;
  name: string;
  nodeType?: string;
  order: number;
  slaMinutes?: null | number;
}

export interface SaveWorkflowDefinitionRequest {
  code: string;
  description?: null | string;
  designerJson?: null | string;
  formSchemaJson?: null | string;
  formName?: null | string;
  isEnabled: boolean;
  name: string;
  nodes: SaveWorkflowNodeRequest[];
}

export interface WorkflowActionLogItem {
  action: string;
  comment?: null | string;
  createdAt: string;
  id: string;
  nodeId?: null | string;
  nodeName?: null | string;
  operatorUserId: string;
  operatorUserName: string;
}

export interface WorkflowAttachmentItem {
  contentType: string;
  createdAt: string;
  fileId: string;
  id: string;
  instanceId: string;
  originalName: string;
  remark?: null | string;
  size: number;
  storagePath: string;
  storageProvider: string;
  uploaderUserId: string;
  uploaderUserName: string;
}

export interface WorkflowCommentItem {
  authorUserId: string;
  authorUserName: string;
  content: string;
  createdAt: string;
  id: string;
  instanceId: string;
}

export interface WorkflowTaskItem {
  approverUserId: string;
  approverUserName: string;
  comment?: null | string;
  completedAt?: null | string;
  createdAt: string;
  definitionName: string;
  id: string;
  instanceId: string;
  instanceTitle: string;
  nodeId: string;
  nodeName: string;
  dueAt?: null | string;
  isOverdue: boolean;
  lastAutoRemindedAt?: null | string;
  status: string;
}

export interface WorkflowCcRecordItem {
  businessKey?: null | string;
  createdAt: string;
  currentNodeName?: null | string;
  definitionName: string;
  id: string;
  initiatorUserId: string;
  initiatorUserName: string;
  instanceId: string;
  instanceStatus: string;
  instanceTitle: string;
  isRead: boolean;
  nodeId: string;
  nodeName: string;
  readAt?: null | string;
  readStatus: string;
  recipientUserId: string;
  recipientUserName: string;
  senderUserId?: null | string;
  senderUserName?: null | string;
  startedAt: string;
}

export interface WorkflowInstanceItem {
  actionLogs: WorkflowActionLogItem[];
  attachments: WorkflowAttachmentItem[];
  businessKey?: null | string;
  ccRecords: WorkflowCcRecordItem[];
  completedAt?: null | string;
  comments: WorkflowCommentItem[];
  currentNodeId?: null | string;
  currentNodeName?: null | string;
  definitionCode: string;
  definitionId: string;
  definitionName: string;
  definitionSnapshotJson: string;
  definitionVersion: number;
  formDataJson: string;
  id: string;
  initiatorUserId: string;
  initiatorUserName: string;
  startedAt: string;
  status: string;
  tasks: WorkflowTaskItem[];
  title: string;
}

export interface WorkflowInstanceListResult {
  items: WorkflowInstanceItem[];
  total: number;
}

export interface WorkflowCcRecordListResult {
  items: WorkflowCcRecordItem[];
  total: number;
}

export interface StartWorkflowInstanceRequest {
  attachmentFileIds?: string[];
  businessKey?: null | string;
  definitionId: string;
  formDataJson?: null | string;
  title: string;
}

export interface WorkflowTransferTaskRequest {
  comment?: null | string;
  targetUserId: string;
}

export interface WorkflowRemindTaskRequest {
  comment?: null | string;
}

export interface WorkflowAttachmentRequest {
  fileId: string;
  remark?: null | string;
}

export interface WorkflowCommentRequest {
  content: string;
}

export function getWorkflowDefinitionsApi(params: {
  isEnabled?: boolean;
  keyword?: string;
  page?: number;
  pageSize?: number;
}) {
  return requestClient.get<WorkflowDefinitionListResult>(
    '/workflow/definition/list',
    { params },
  );
}

export function getWorkflowDefinitionOptionsApi() {
  return requestClient.get<WorkflowDefinitionOption[]>(
    '/workflow/definition/options',
  );
}

export function getWorkflowBusinessBindingsApi(params: {
  isEnabled?: boolean;
  keyword?: string;
  page?: number;
  pageSize?: number;
}) {
  return requestClient.get<WorkflowBusinessBindingListResult>(
    '/workflow/business-binding/list',
    { params },
  );
}

export function createWorkflowBusinessBindingApi(
  data: SaveWorkflowBusinessBindingRequest,
) {
  return requestClient.post<WorkflowBusinessBindingItem>(
    '/workflow/business-binding',
    data,
  );
}

export function updateWorkflowBusinessBindingApi(
  id: string,
  data: SaveWorkflowBusinessBindingRequest,
) {
  return requestClient.put<WorkflowBusinessBindingItem>(
    `/workflow/business-binding/${id}`,
    data,
  );
}

export function deleteWorkflowBusinessBindingApi(id: string) {
  return requestClient.delete<boolean>(`/workflow/business-binding/${id}`);
}

export function getWorkflowApproverUsersApi() {
  return requestClient.get<WorkflowApproverUserOption[]>(
    '/workflow/approver/users',
  );
}

export function getWorkflowApproverRolesApi() {
  return requestClient.get<WorkflowApproverRoleOption[]>(
    '/workflow/approver/roles',
  );
}

export function createWorkflowDefinitionApi(
  data: SaveWorkflowDefinitionRequest,
) {
  return requestClient.post<WorkflowDefinitionItem>(
    '/workflow/definition',
    data,
  );
}

export function updateWorkflowDefinitionApi(
  id: string,
  data: SaveWorkflowDefinitionRequest,
) {
  return requestClient.put<WorkflowDefinitionItem>(
    `/workflow/definition/${id}`,
    data,
  );
}

export function publishWorkflowDefinitionApi(id: string) {
  return requestClient.post<WorkflowDefinitionItem>(
    `/workflow/definition/${id}/publish`,
  );
}

export function createWorkflowDefinitionVersionApi(id: string) {
  return requestClient.post<WorkflowDefinitionItem>(
    `/workflow/definition/${id}/new-version`,
  );
}

export function deleteWorkflowDefinitionApi(id: string) {
  return requestClient.delete<boolean>(`/workflow/definition/${id}`);
}

export function getWorkflowInstancesApi(params: {
  keyword?: string;
  page?: number;
  pageSize?: number;
  scope?: string;
  status?: string;
}) {
  return requestClient.get<WorkflowInstanceListResult>(
    '/workflow/instance/list',
    { params },
  );
}

export function getWorkflowStartedByMeApi(params: {
  keyword?: string;
  page?: number;
  pageSize?: number;
  status?: string;
}) {
  return requestClient.get<WorkflowInstanceListResult>(
    '/workflow/instance/started-by-me',
    { params },
  );
}

export function getWorkflowCcInstancesApi(params: {
  keyword?: string;
  page?: number;
  pageSize?: number;
  readStatus?: string;
  status?: string;
}) {
  return requestClient.get<WorkflowCcRecordListResult>(
    '/workflow/instance/cc',
    { params },
  );
}

export function markWorkflowCcRecordReadApi(id: string) {
  return requestClient.post<WorkflowCcRecordItem>(`/workflow/cc/${id}/read`);
}

export function getWorkflowInstanceApi(id: string) {
  return requestClient.get<WorkflowInstanceItem>(`/workflow/instance/${id}`);
}

export function getWorkflowTodoTasksApi() {
  return requestClient.get<WorkflowTaskItem[]>('/workflow/task/todo');
}

export function getWorkflowDoneTasksApi() {
  return requestClient.get<WorkflowTaskItem[]>('/workflow/task/done');
}

export function startWorkflowInstanceApi(data: StartWorkflowInstanceRequest) {
  return requestClient.post<WorkflowInstanceItem>(
    '/workflow/instance/start',
    data,
  );
}

export function addWorkflowAttachmentApi(
  id: string,
  data: WorkflowAttachmentRequest,
) {
  return requestClient.post<WorkflowInstanceItem>(
    `/workflow/instance/${id}/attachments`,
    data,
  );
}

export function addWorkflowCommentApi(id: string, data: WorkflowCommentRequest) {
  return requestClient.post<WorkflowCommentItem>(
    `/workflow/instance/${id}/comments`,
    data,
  );
}

export async function downloadWorkflowAttachmentApi(
  instanceId: string,
  attachmentId: string,
) {
  const accessStore = useAccessStore();
  const url = new URL(
    `${apiURL.replace(/\/$/, '')}/workflow/instance/${instanceId}/attachments/${attachmentId}/download`,
    window.location.origin,
  );
  const response = await fetch(url, {
    headers: {
      'Accept-Language': preferences.app.locale,
      ...(accessStore.accessToken
        ? { Authorization: `Bearer ${accessStore.accessToken}` }
        : {}),
    },
  });

  if (!response.ok) {
    throw new Error(`Download failed: ${response.status}`);
  }

  return response.blob();
}

export function approveWorkflowInstanceApi(id: string, comment?: string) {
  return requestClient.post<WorkflowInstanceItem>(
    `/workflow/instance/${id}/approve`,
    { comment },
  );
}

export function rejectWorkflowInstanceApi(id: string, comment?: string) {
  return requestClient.post<WorkflowInstanceItem>(
    `/workflow/instance/${id}/reject`,
    { comment },
  );
}

export function transferWorkflowTaskApi(
  id: string,
  data: WorkflowTransferTaskRequest,
) {
  return requestClient.post<WorkflowTaskItem>(
    `/workflow/task/${id}/transfer`,
    data,
  );
}

export function remindWorkflowTaskApi(
  id: string,
  data: WorkflowRemindTaskRequest,
) {
  return requestClient.post<WorkflowTaskItem>(
    `/workflow/task/${id}/remind`,
    data,
  );
}

export function withdrawWorkflowInstanceApi(id: string, comment?: string) {
  return requestClient.post<WorkflowInstanceItem>(
    `/workflow/instance/${id}/withdraw`,
    { comment },
  );
}
