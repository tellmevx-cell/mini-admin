import { requestClient } from '#/api/request';

export interface UserNotificationItem {
  category: string;
  createdAt: string;
  id: string;
  isRead: boolean;
  level: string;
  link?: null | string;
  message: string;
  readAt?: null | string;
  sourceId: string;
  sourceType: string;
  title: string;
}

export interface UserNotificationListResult {
  items: UserNotificationItem[];
  total: number;
  unreadCount: number;
}

export interface UserNotificationListParams {
  category?: string;
  isRead?: boolean;
  page?: number;
  pageSize?: number;
  sourceType?: string;
  take?: number;
}

export interface NotificationDeliveryItem {
  channel: string;
  createdAt: string;
  errorMessage?: null | string;
  id: string;
  recipientAddress: string;
  retryCount: number;
  sentAt?: null | string;
  sourceId: string;
  sourceType: string;
  status: string;
  title: string;
  userId: string;
}

export interface NotificationDeliveryListParams {
  channel?: string;
  page?: number;
  pageSize?: number;
  sourceType?: string;
  status?: string;
}

export interface NotificationDeliveryListResult {
  items: NotificationDeliveryItem[];
  total: number;
}

export interface NotificationChannelStatusItem {
  channel: string;
  description: string;
  displayName: string;
  failedCount: number;
  isEnabled: boolean;
  pendingCount: number;
  succeededCount: number;
}

export interface NotificationChannelOverview {
  channels: NotificationChannelStatusItem[];
  totalNotificationCount: number;
  unreadNotificationCount: number;
}

export interface NotificationTemplateItem {
  category: string;
  channel?: null | string;
  code: string;
  createdAt: string;
  id: string;
  isEnabled: boolean;
  level: string;
  linkTemplate?: null | string;
  messageTemplate: string;
  name: string;
  remark?: null | string;
  titleTemplate: string;
  updatedAt: string;
}

export interface NotificationTemplateListParams {
  category?: string;
  code?: string;
  isEnabled?: boolean;
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export interface NotificationTemplateListResult {
  items: NotificationTemplateItem[];
  total: number;
}

export interface SaveNotificationTemplateRequest {
  category: string;
  channel?: null | string;
  isEnabled: boolean;
  level: string;
  linkTemplate?: null | string;
  messageTemplate: string;
  name: string;
  remark?: null | string;
  titleTemplate: string;
}

export interface PreviewNotificationTemplateRequest {
  linkTemplate?: null | string;
  messageTemplate: string;
  titleTemplate: string;
  variables?: Record<string, string>;
}

export interface NotificationTemplatePreview {
  link?: null | string;
  message: string;
  title: string;
}

export interface NotificationPolicyItem {
  category: string;
  createdAt: string;
  enableEmail: boolean;
  enableInApp: boolean;
  enableWebhook: boolean;
  eventCode: string;
  eventName: string;
  id: string;
  isEnabled: boolean;
  recipientStrategy: string;
  remark?: null | string;
  updatedAt: string;
}

export interface NotificationPolicyListParams {
  category?: string;
  eventCode?: string;
  isEnabled?: boolean;
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export interface NotificationPolicyListResult {
  items: NotificationPolicyItem[];
  total: number;
}

export interface SaveNotificationPolicyRequest {
  category: string;
  enableEmail: boolean;
  enableInApp: boolean;
  enableWebhook: boolean;
  eventName: string;
  isEnabled: boolean;
  recipientStrategy: string;
  remark?: null | string;
}

export interface NotificationSubscriptionItem {
  category: string;
  enableEmail: boolean;
  enableInApp: boolean;
  enableWebhook: boolean;
  eventCode: string;
  eventName: string;
  hasCustomPreference: boolean;
  id?: null | string;
  isEnabled: boolean;
  policyEnableEmail: boolean;
  policyEnableInApp: boolean;
  policyEnableWebhook: boolean;
  updatedAt?: null | string;
}

export interface NotificationSubscriptionListParams {
  category?: string;
  keyword?: string;
}

export interface NotificationSubscriptionListResult {
  items: NotificationSubscriptionItem[];
  total: number;
}

export interface SaveNotificationSubscriptionRequest {
  enableEmail: boolean;
  enableInApp: boolean;
  enableWebhook: boolean;
  isEnabled: boolean;
}

export async function getMyNotificationsApi(
  params: number | UserNotificationListParams = 20,
) {
  const query = typeof params === 'number' ? { take: params } : params;
  return requestClient.get<UserNotificationListResult>('/notification/my', {
    params: query,
  });
}

export async function markNotificationReadApi(id: string) {
  return requestClient.post<boolean>(`/notification/${id}/read`);
}

export async function markAllNotificationsReadApi() {
  return requestClient.post<boolean>('/notification/read-all');
}

export async function deleteNotificationApi(id: string) {
  return requestClient.delete<boolean>(`/notification/${id}`);
}

export async function clearNotificationsApi() {
  return requestClient.delete<boolean>('/notification/all');
}

export async function getNotificationChannelOverviewApi() {
  return requestClient.get<NotificationChannelOverview>(
    '/notification/channels/overview',
  );
}

export async function getNotificationDeliveriesApi(
  params: NotificationDeliveryListParams,
) {
  return requestClient.get<NotificationDeliveryListResult>(
    '/notification/deliveries',
    {
      params,
    },
  );
}

export async function retryNotificationDeliveryApi(id: string) {
  return requestClient.post<NotificationDeliveryItem>(
    `/notification/deliveries/${id}/retry`,
  );
}

export async function getNotificationTemplatesApi(
  params: NotificationTemplateListParams,
) {
  return requestClient.get<NotificationTemplateListResult>(
    '/notification/templates',
    {
      params,
    },
  );
}

export async function previewNotificationTemplateApi(
  payload: PreviewNotificationTemplateRequest,
) {
  return requestClient.post<NotificationTemplatePreview>(
    '/notification/templates/preview',
    payload,
  );
}

export async function updateNotificationTemplateApi(
  id: string,
  payload: SaveNotificationTemplateRequest,
) {
  return requestClient.post<NotificationTemplateItem>(
    `/notification/templates/${id}`,
    payload,
  );
}

export async function getNotificationPoliciesApi(
  params: NotificationPolicyListParams,
) {
  return requestClient.get<NotificationPolicyListResult>(
    '/notification/policies',
    {
      params,
    },
  );
}

export async function updateNotificationPolicyApi(
  id: string,
  payload: SaveNotificationPolicyRequest,
) {
  return requestClient.post<NotificationPolicyItem>(
    `/notification/policies/${id}`,
    payload,
  );
}

export async function getMyNotificationSubscriptionsApi(
  params: NotificationSubscriptionListParams,
) {
  return requestClient.get<NotificationSubscriptionListResult>(
    '/notification/subscriptions/my',
    {
      params,
    },
  );
}

export async function saveMyNotificationSubscriptionApi(
  eventCode: string,
  payload: SaveNotificationSubscriptionRequest,
) {
  return requestClient.post<NotificationSubscriptionItem>(
    `/notification/subscriptions/my/${eventCode}`,
    payload,
  );
}

export async function resetMyNotificationSubscriptionApi(eventCode: string) {
  return requestClient.delete<boolean>(
    `/notification/subscriptions/my/${eventCode}`,
  );
}

export async function resetAllMyNotificationSubscriptionsApi() {
  return requestClient.delete<number>('/notification/subscriptions/my');
}
