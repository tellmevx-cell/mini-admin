<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Badge,
  Button,
  Empty,
  Form,
  FormItem,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';

import {
  getMyNotificationsApi,
  getNotificationChannelOverviewApi,
  getNotificationDeliveriesApi,
  getNotificationPoliciesApi,
  getNotificationTemplatesApi,
  getMyNotificationSubscriptionsApi,
  type NotificationChannelOverview,
  type NotificationChannelStatusItem,
  type NotificationDeliveryItem,
  type NotificationPolicyItem,
  type NotificationSubscriptionItem,
  type NotificationTemplateItem,
  previewNotificationTemplateApi,
  retryNotificationDeliveryApi,
  resetAllMyNotificationSubscriptionsApi,
  resetMyNotificationSubscriptionApi,
  type SaveNotificationSubscriptionRequest,
  saveMyNotificationSubscriptionApi,
  updateNotificationPolicyApi,
  updateNotificationTemplateApi,
  type UserNotificationItem,
} from '#/api';
import { createRouteLocationFromLink } from '#/router/link';
import { useNotificationStore } from '#/store';

const router = useRouter();
const route = useRoute();
const { hasAccessByCodes } = useAccess();
const notificationStore = useNotificationStore();

interface NotificationPolicyForm {
  category: string;
  enableEmail: boolean;
  enableInApp: boolean;
  enableWebhook: boolean;
  eventName: string;
  isEnabled: boolean;
  recipientStrategy: string;
  remark: string;
}

interface NotificationTemplateForm {
  category: string;
  channel?: string;
  isEnabled: boolean;
  level: string;
  linkTemplate: string;
  messageTemplate: string;
  name: string;
  remark: string;
  titleTemplate: string;
}

const activeTab = ref('messages');
const notificationLoading = ref(false);
const deliveryLoading = ref(false);
const overviewLoading = ref(false);
const policyLoading = ref(false);
const policySaving = ref(false);
const subscriptionLoading = ref(false);
const resettingSubscriptions = ref(false);
const savingSubscriptionCode = ref('');
const templateLoading = ref(false);
const templateSaving = ref(false);
const retryingDeliveryId = ref('');

const notifications = ref<UserNotificationItem[]>([]);
const deliveries = ref<NotificationDeliveryItem[]>([]);
const policies = ref<NotificationPolicyItem[]>([]);
const subscriptions = ref<NotificationSubscriptionItem[]>([]);
const templates = ref<NotificationTemplateItem[]>([]);
const total = ref(0);
const unreadCount = ref(0);
const deliveryTotal = ref(0);
const policyTotal = ref(0);
const subscriptionTotal = ref(0);
const templateTotal = ref(0);
const policyModalOpen = ref(false);
const templateModalOpen = ref(false);
const previewModalOpen = ref(false);
const editingPolicy = ref<NotificationPolicyItem | null>(null);
const editingTemplate = ref<NotificationTemplateItem | null>(null);
const previewResult = ref({
  link: '' as null | string,
  message: '',
  title: '',
});
const overview = ref<NotificationChannelOverview>({
  channels: [],
  totalNotificationCount: 0,
  unreadNotificationCount: 0,
});

const messageQuery = reactive({
  category: undefined as string | undefined,
  isRead: undefined as string | undefined,
  page: 1,
  pageSize: 10,
  sourceType: undefined as string | undefined,
});

const deliveryQuery = reactive({
  channel: undefined as string | undefined,
  page: 1,
  pageSize: 10,
  sourceType: undefined as string | undefined,
  status: undefined as string | undefined,
});

const templateQuery = reactive({
  category: undefined as string | undefined,
  isEnabled: undefined as string | undefined,
  keyword: '',
  page: 1,
  pageSize: 10,
});

const policyQuery = reactive({
  category: 'Workflow' as string | undefined,
  isEnabled: undefined as string | undefined,
  keyword: '',
  page: 1,
  pageSize: 10,
});

const subscriptionQuery = reactive({
  category: 'Workflow' as string | undefined,
  keyword: '',
});

const templateForm = reactive<NotificationTemplateForm>({
  category: 'Workflow',
  channel: 'InApp',
  isEnabled: true,
  level: 'Info',
  linkTemplate: '',
  messageTemplate: '',
  name: '',
  remark: '',
  titleTemplate: '',
});

const policyForm = reactive<NotificationPolicyForm>({
  category: 'Workflow',
  enableEmail: false,
  enableInApp: true,
  enableWebhook: false,
  eventName: '',
  isEnabled: true,
  recipientStrategy: 'WorkflowDefault',
  remark: '',
});

const messageColumns = [
  { dataIndex: 'isRead', title: '状态', width: 90 },
  { dataIndex: 'level', title: '等级', width: 90 },
  { dataIndex: 'title', title: '消息内容', width: 380 },
  { dataIndex: 'category', title: '分类', width: 130 },
  { dataIndex: 'sourceType', title: '来源', width: 140 },
  { dataIndex: 'createdAt', title: '创建时间', width: 180 },
  { dataIndex: 'readAt', title: '读取时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 180 },
];

const deliveryColumns = [
  { dataIndex: 'channel', title: '通道', width: 110 },
  { dataIndex: 'status', title: '状态', width: 110 },
  { dataIndex: 'title', title: '投递标题', width: 260 },
  { dataIndex: 'recipientAddress', title: '接收地址', width: 220 },
  { dataIndex: 'sourceType', title: '来源类型', width: 130 },
  { dataIndex: 'createdAt', title: '创建时间', width: 180 },
  { dataIndex: 'sentAt', title: '发送时间', width: 180 },
  { dataIndex: 'errorMessage', title: '结果说明', width: 280 },
  { dataIndex: 'action', title: '操作', width: 120 },
];

const templateColumns = [
  { dataIndex: 'code', title: '模板编码', width: 170 },
  { dataIndex: 'name', title: '模板名称', width: 180 },
  { dataIndex: 'category', title: '分类', width: 120 },
  { dataIndex: 'level', title: '等级', width: 90 },
  { dataIndex: 'titleTemplate', title: '标题模板', width: 260 },
  { dataIndex: 'messageTemplate', title: '内容模板', width: 360 },
  { dataIndex: 'isEnabled', title: '状态', width: 90 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 150 },
];

const policyColumns = [
  { dataIndex: 'eventName', title: '事件名称', width: 180 },
  { dataIndex: 'eventCode', title: '事件编码', width: 170 },
  { dataIndex: 'category', title: '分类', width: 120 },
  { dataIndex: 'recipientStrategy', title: '接收人策略', width: 150 },
  { dataIndex: 'enableInApp', title: '站内信', width: 90 },
  { dataIndex: 'enableEmail', title: '邮件', width: 90 },
  { dataIndex: 'enableWebhook', title: 'Webhook', width: 100 },
  { dataIndex: 'isEnabled', title: '策略状态', width: 100 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 120 },
];

const subscriptionColumns = [
  { dataIndex: 'eventName', title: '事件名称', width: 190 },
  { dataIndex: 'eventCode', title: '事件编码', width: 170 },
  { dataIndex: 'category', title: '分类', width: 120 },
  { dataIndex: 'isEnabled', title: '接收此事件', width: 120 },
  { dataIndex: 'enableInApp', title: '站内信', width: 110 },
  { dataIndex: 'enableEmail', title: '邮件', width: 110 },
  { dataIndex: 'enableWebhook', title: 'Webhook', width: 120 },
  { dataIndex: 'hasCustomPreference', title: '偏好状态', width: 120 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 120 },
];

const readOptions = [
  { label: '未读', value: 'false' },
  { label: '已读', value: 'true' },
];

const messageCategoryOptions = [
  { label: '工作流消息', value: 'Workflow' },
  { label: '系统告警', value: 'SystemAlert' },
  { label: '业务通知', value: 'Business' },
];

const messageSourceTypeOptions = [
  { label: '告警', value: 'Alert' },
  { label: '审批待办', value: 'WorkflowTask' },
  { label: '审批通过', value: 'WorkflowApprove' },
  { label: '审批驳回', value: 'WorkflowReject' },
  { label: '审批撤回', value: 'WorkflowWithdraw' },
  { label: '审批转办', value: 'WorkflowTransfer' },
  { label: '审批催办', value: 'WorkflowRemind' },
  { label: '审批超时', value: 'WorkflowOverdue' },
  { label: '流程抄送', value: 'WorkflowCc' },
  { label: '流程评论', value: 'WorkflowComment' },
  { label: '投递异常', value: 'NotificationDeliveryFailure' },
  { label: '业务', value: 'Business' },
];

const deliveryChannelOptions = [
  { label: '邮件', value: 'Email' },
  { label: 'Webhook', value: 'Webhook' },
];

const deliveryStatusOptions = [
  { label: '待发送', value: 'Pending' },
  { label: '发送成功', value: 'Succeeded' },
  { label: '发送失败', value: 'Failed' },
  { label: '已跳过', value: 'Skipped' },
];

const enabledOptions = [
  { label: '启用', value: 'true' },
  { label: '停用', value: 'false' },
];

const templateLevelOptions = [
  { label: '提示', value: 'Info' },
  { label: '警告', value: 'Warning' },
  { label: '严重', value: 'Critical' },
];

const templateChannelOptions = [
  { label: '站内信', value: 'InApp' },
  { label: '邮件', value: 'Email' },
  { label: 'Webhook', value: 'Webhook' },
];

const policyRecipientStrategyOptions = [
  { label: '流程默认接收人', value: 'WorkflowDefault' },
  { label: '流程发起人', value: 'Initiator' },
  { label: '当前处理人', value: 'Assignee' },
  { label: '流程参与人', value: 'Participants' },
];

const deliverySourceTypeOptions = [
  { label: '告警', value: 'Alert' },
  { label: '工作流', value: 'Workflow' },
  { label: '审批待办', value: 'WorkflowTask' },
  { label: '审批通过', value: 'WorkflowApprove' },
  { label: '审批驳回', value: 'WorkflowReject' },
  { label: '审批撤回', value: 'WorkflowWithdraw' },
  { label: '审批转办', value: 'WorkflowTransfer' },
  { label: '审批催办', value: 'WorkflowRemind' },
  { label: '审批超时', value: 'WorkflowOverdue' },
  { label: '流程抄送', value: 'WorkflowCc' },
  { label: '流程评论', value: 'WorkflowComment' },
];

const messagePagination = computed<TablePaginationConfig>(() => ({
  current: messageQuery.page,
  pageSize: messageQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条，未读 ${unreadCount.value} 条`,
  total: total.value,
}));

const deliveryPagination = computed<TablePaginationConfig>(() => ({
  current: deliveryQuery.page,
  pageSize: deliveryQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条投递记录`,
  total: deliveryTotal.value,
}));

const templatePagination = computed<TablePaginationConfig>(() => ({
  current: templateQuery.page,
  pageSize: templateQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 个模板`,
  total: templateTotal.value,
}));

const policyPagination = computed<TablePaginationConfig>(() => ({
  current: policyQuery.page,
  pageSize: policyQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条策略`,
  total: policyTotal.value,
}));

const canUpdateTemplate = computed(() =>
  hasAccessByCodes(['system:notification:template:update']),
);

const canUpdatePolicy = computed(() =>
  hasAccessByCodes(['system:notification:policy:update']),
);

const canRetryDelivery = computed(() =>
  hasAccessByCodes(['system:notification:retry']),
);

const emailChannel = computed(() =>
  overview.value.channels.find((item) => item.channel === 'Email'),
);

const webhookChannel = computed(() =>
  overview.value.channels.find((item) => item.channel === 'Webhook'),
);

const inAppChannel = computed(() =>
  overview.value.channels.find((item) => item.channel === 'InApp'),
);

const deliveryFailureCount = computed(
  () =>
    (emailChannel.value?.failedCount ?? 0) +
    (webhookChannel.value?.failedCount ?? 0),
);

const customSubscriptionCount = computed(
  () => subscriptions.value.filter((item) => item.hasCustomPreference).length,
);

function parseBooleanSelectValue(value?: string) {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return undefined;
}

async function loadOverview() {
  overviewLoading.value = true;
  try {
    overview.value = await getNotificationChannelOverviewApi();
    unreadCount.value = overview.value.unreadNotificationCount;
    notificationStore.syncUnreadCount(overview.value.unreadNotificationCount);
  } finally {
    overviewLoading.value = false;
  }
}

async function loadNotifications() {
  notificationLoading.value = true;
  try {
    const result = await getMyNotificationsApi({
      category: messageQuery.category,
      isRead: parseBooleanSelectValue(messageQuery.isRead),
      page: messageQuery.page,
      pageSize: messageQuery.pageSize,
      sourceType: messageQuery.sourceType,
    });
    notifications.value = result.items;
    total.value = result.total;
    unreadCount.value = result.unreadCount;
    notificationStore.syncUnreadCount(result.unreadCount);
  } finally {
    notificationLoading.value = false;
  }
}

async function refreshNotifications() {
  await Promise.all([
    loadNotifications(),
    notificationStore.loadRecent({ silent: true }),
  ]);
}

async function loadDeliveries() {
  deliveryLoading.value = true;
  try {
    const result = await getNotificationDeliveriesApi({
      channel: deliveryQuery.channel,
      page: deliveryQuery.page,
      pageSize: deliveryQuery.pageSize,
      sourceType: deliveryQuery.sourceType,
      status: deliveryQuery.status,
    });
    deliveries.value = result.items;
    deliveryTotal.value = result.total;
  } finally {
    deliveryLoading.value = false;
  }
}

async function loadTemplates() {
  templateLoading.value = true;
  try {
    const result = await getNotificationTemplatesApi({
      category: templateQuery.category,
      isEnabled: parseBooleanSelectValue(templateQuery.isEnabled),
      keyword: templateQuery.keyword || undefined,
      page: templateQuery.page,
      pageSize: templateQuery.pageSize,
    });
    templates.value = result.items;
    templateTotal.value = result.total;
  } finally {
    templateLoading.value = false;
  }
}

async function loadPolicies() {
  policyLoading.value = true;
  try {
    const result = await getNotificationPoliciesApi({
      category: policyQuery.category,
      isEnabled: parseBooleanSelectValue(policyQuery.isEnabled),
      keyword: policyQuery.keyword || undefined,
      page: policyQuery.page,
      pageSize: policyQuery.pageSize,
    });
    policies.value = result.items;
    policyTotal.value = result.total;
  } finally {
    policyLoading.value = false;
  }
}

async function loadSubscriptions() {
  subscriptionLoading.value = true;
  try {
    const result = await getMyNotificationSubscriptionsApi({
      category: subscriptionQuery.category,
      keyword: subscriptionQuery.keyword || undefined,
    });
    subscriptions.value = result.items;
    subscriptionTotal.value = result.total;
  } finally {
    subscriptionLoading.value = false;
  }
}

function handleMessageSearch() {
  messageQuery.page = 1;
  void loadNotifications();
}

function handleMessageReset() {
  messageQuery.category = undefined;
  messageQuery.isRead = undefined;
  messageQuery.page = 1;
  messageQuery.sourceType = undefined;
  void loadNotifications();
}

function handleDeliverySearch() {
  deliveryQuery.page = 1;
  void loadDeliveries();
}

function handleDeliveryReset() {
  deliveryQuery.channel = undefined;
  deliveryQuery.page = 1;
  deliveryQuery.sourceType = undefined;
  deliveryQuery.status = undefined;
  void loadDeliveries();
}

async function handleRetryDelivery(record: NotificationDeliveryItem | Record<string, any>) {
  const delivery = record as NotificationDeliveryItem;
  retryingDeliveryId.value = delivery.id;
  try {
    const result = await retryNotificationDeliveryApi(delivery.id);
    message.success(
      result.status === 'Succeeded'
        ? '重发成功'
        : `重发完成：${deliveryStatusText(result.status)}`,
    );
    await Promise.all([loadDeliveries(), loadOverview()]);
  } finally {
    retryingDeliveryId.value = '';
  }
}

function readRouteQueryValue(value: unknown) {
  if (Array.isArray(value)) {
    return typeof value[0] === 'string' ? value[0] : '';
  }

  return typeof value === 'string' ? value : '';
}

function applyRouteQueryIntent() {
  const nextTab = readRouteQueryValue(route.query.tab);
  if (
    ['channels', 'deliveries', 'messages', 'policies', 'subscriptions', 'templates'].includes(
      nextTab,
    )
  ) {
    activeTab.value = nextTab;
  }

  const deliveryStatus = readRouteQueryValue(route.query.deliveryStatus);
  if (deliveryStatusOptions.some((item) => item.value === deliveryStatus)) {
    const shouldReload = deliveryQuery.status !== deliveryStatus;
    deliveryQuery.status = deliveryStatus;
    deliveryQuery.page = 1;
    return shouldReload;
  }

  return false;
}

function showFailedDeliveries() {
  activeTab.value = 'deliveries';
  deliveryQuery.status = 'Failed';
  deliveryQuery.page = 1;
  void loadDeliveries();
}

function handleTemplateSearch() {
  templateQuery.page = 1;
  void loadTemplates();
}

function handlePolicySearch() {
  policyQuery.page = 1;
  void loadPolicies();
}

function handleSubscriptionSearch() {
  void loadSubscriptions();
}

function handleTemplateReset() {
  templateQuery.category = undefined;
  templateQuery.isEnabled = undefined;
  templateQuery.keyword = '';
  templateQuery.page = 1;
  void loadTemplates();
}

function handlePolicyReset() {
  policyQuery.category = 'Workflow';
  policyQuery.isEnabled = undefined;
  policyQuery.keyword = '';
  policyQuery.page = 1;
  void loadPolicies();
}

function handleSubscriptionReset() {
  subscriptionQuery.category = 'Workflow';
  subscriptionQuery.keyword = '';
  void loadSubscriptions();
}

function handleMessageTableChange(nextPagination: TablePaginationConfig) {
  messageQuery.page = nextPagination.current ?? 1;
  messageQuery.pageSize = nextPagination.pageSize ?? 10;
  void loadNotifications();
}

function handleDeliveryTableChange(nextPagination: TablePaginationConfig) {
  deliveryQuery.page = nextPagination.current ?? 1;
  deliveryQuery.pageSize = nextPagination.pageSize ?? 10;
  void loadDeliveries();
}

function handleTemplateTableChange(nextPagination: TablePaginationConfig) {
  templateQuery.page = nextPagination.current ?? 1;
  templateQuery.pageSize = nextPagination.pageSize ?? 10;
  void loadTemplates();
}

function handlePolicyTableChange(nextPagination: TablePaginationConfig) {
  policyQuery.page = nextPagination.current ?? 1;
  policyQuery.pageSize = nextPagination.pageSize ?? 10;
  void loadPolicies();
}

function fillTemplateForm(template: NotificationTemplateItem) {
  templateForm.name = template.name;
  templateForm.category = template.category;
  templateForm.level = template.level;
  templateForm.channel = template.channel ?? 'InApp';
  templateForm.titleTemplate = template.titleTemplate;
  templateForm.messageTemplate = template.messageTemplate;
  templateForm.linkTemplate = template.linkTemplate ?? '';
  templateForm.isEnabled = template.isEnabled;
  templateForm.remark = template.remark ?? '';
}

function fillPolicyForm(policy: NotificationPolicyItem) {
  policyForm.eventName = policy.eventName;
  policyForm.category = policy.category;
  policyForm.recipientStrategy = policy.recipientStrategy;
  policyForm.enableInApp = policy.enableInApp;
  policyForm.enableEmail = policy.enableEmail;
  policyForm.enableWebhook = policy.enableWebhook;
  policyForm.isEnabled = policy.isEnabled;
  policyForm.remark = policy.remark ?? '';
}

function openTemplateEditor(template: NotificationTemplateItem | Record<string, any>) {
  const currentTemplate = template as NotificationTemplateItem;
  editingTemplate.value = currentTemplate;
  fillTemplateForm(currentTemplate);
  templateModalOpen.value = true;
}

function openPolicyEditor(policy: NotificationPolicyItem | Record<string, any>) {
  const currentPolicy = policy as NotificationPolicyItem;
  editingPolicy.value = currentPolicy;
  fillPolicyForm(currentPolicy);
  policyModalOpen.value = true;
}

function buildPreviewVariables() {
  return {
    businessKey: 'LEAVE-20260608-001',
    comment: '资料已补充完整',
    content: '磁盘空间低于 10%，请及时处理。',
    definitionName: '请假审批',
    instanceTitle: '请假申请',
    level: 'Warning',
    levelText: '警告',
    nodeName: '直属主管审批',
    operatorUserName: 'admin',
    source: 'SystemMonitor',
    status: 'Active',
    targetUserName: 'zhangsan',
    title: '磁盘空间不足',
    type: 'System',
  };
}

async function previewTemplate(template?: NotificationTemplateItem | Record<string, any>) {
  const currentTemplate = template as NotificationTemplateItem | undefined;
  const payload = currentTemplate
    ? {
        linkTemplate: currentTemplate.linkTemplate,
        messageTemplate: currentTemplate.messageTemplate,
        titleTemplate: currentTemplate.titleTemplate,
        variables: buildPreviewVariables(),
      }
    : {
        linkTemplate: templateForm.linkTemplate,
        messageTemplate: templateForm.messageTemplate,
        titleTemplate: templateForm.titleTemplate,
        variables: buildPreviewVariables(),
      };
  const result = await previewNotificationTemplateApi(payload);
  previewResult.value = {
    link: result.link ?? null,
    message: result.message,
    title: result.title,
  };
  previewModalOpen.value = true;
}

async function saveTemplate() {
  if (!editingTemplate.value) {
    return;
  }

  templateSaving.value = true;
  try {
    await updateNotificationTemplateApi(editingTemplate.value.id, {
      category: templateForm.category,
      channel: templateForm.channel,
      isEnabled: templateForm.isEnabled,
      level: templateForm.level,
      linkTemplate: templateForm.linkTemplate,
      messageTemplate: templateForm.messageTemplate,
      name: templateForm.name,
      remark: templateForm.remark,
      titleTemplate: templateForm.titleTemplate,
    });
    message.success('模板已保存');
    templateModalOpen.value = false;
    await loadTemplates();
  } finally {
    templateSaving.value = false;
  }
}

async function savePolicy() {
  if (!editingPolicy.value) {
    return;
  }

  policySaving.value = true;
  try {
    await updateNotificationPolicyApi(editingPolicy.value.id, {
      category: policyForm.category,
      enableEmail: policyForm.enableEmail,
      enableInApp: policyForm.enableInApp,
      enableWebhook: policyForm.enableWebhook,
      eventName: policyForm.eventName,
      isEnabled: policyForm.isEnabled,
      recipientStrategy: policyForm.recipientStrategy,
      remark: policyForm.remark,
    });
    message.success('通知策略已保存');
    policyModalOpen.value = false;
    await loadPolicies();
  } finally {
    policySaving.value = false;
  }
}

async function saveSubscriptionPreference(
  record: NotificationSubscriptionItem | Record<string, any>,
  patch: Partial<SaveNotificationSubscriptionRequest>,
) {
  const subscription = record as NotificationSubscriptionItem;
  savingSubscriptionCode.value = subscription.eventCode;
  try {
    const nextPayload: SaveNotificationSubscriptionRequest = {
      enableEmail: subscription.enableEmail,
      enableInApp: subscription.enableInApp,
      enableWebhook: subscription.enableWebhook,
      isEnabled: subscription.isEnabled,
      ...patch,
    };
    const updated = await saveMyNotificationSubscriptionApi(
      subscription.eventCode,
      nextPayload,
    );
    subscriptions.value = subscriptions.value.map((item) =>
      item.eventCode === updated.eventCode ? updated : item,
    );
    message.success('订阅偏好已保存');
  } finally {
    savingSubscriptionCode.value = '';
  }
}

async function resetSubscriptionPreference(record: NotificationSubscriptionItem | Record<string, any>) {
  const subscription = record as NotificationSubscriptionItem;
  savingSubscriptionCode.value = subscription.eventCode;
  try {
    await resetMyNotificationSubscriptionApi(subscription.eventCode);
    message.success('已恢复全局默认');
    await loadSubscriptions();
  } finally {
    savingSubscriptionCode.value = '';
  }
}

async function resetAllSubscriptionPreferences() {
  resettingSubscriptions.value = true;
  try {
    const resetCount = await resetAllMyNotificationSubscriptionsApi();
    message.success(
      resetCount > 0
        ? `已恢复 ${resetCount} 个订阅默认值`
        : '当前没有自定义订阅偏好',
    );
    await loadSubscriptions();
  } finally {
    resettingSubscriptions.value = false;
  }
}

async function markRead(notification: Record<string, any> | UserNotificationItem) {
  const currentNotification = notification as UserNotificationItem;
  await notificationStore.markRead(currentNotification.id);
  message.success('已标记为已读');
  await Promise.all([loadNotifications(), loadOverview()]);
}

async function markAllRead() {
  await notificationStore.markAllRead();
  message.success('全部消息已标记为已读');
  await Promise.all([loadNotifications(), loadOverview()]);
}

async function removeNotification(notification: Record<string, any> | UserNotificationItem) {
  const currentNotification = notification as UserNotificationItem;
  await notificationStore.remove(currentNotification.id);
  message.success('消息已删除');
  await Promise.all([loadNotifications(), loadOverview()]);
}

async function clearAll() {
  await notificationStore.clearAll();
  message.success('消息已清空');
  messageQuery.page = 1;
  await Promise.all([loadNotifications(), loadOverview()]);
}

async function openLink(notification: Record<string, any> | UserNotificationItem) {
  const currentNotification = notification as UserNotificationItem;
  if (!currentNotification.link) {
    return;
  }

  if (!currentNotification.isRead) {
    await notificationStore.markRead(currentNotification.id);
    currentNotification.isRead = true;
    unreadCount.value = Math.max(0, unreadCount.value - 1);
    overview.value = {
      ...overview.value,
      unreadNotificationCount: unreadCount.value,
    };
  }

  router.push(createRouteLocationFromLink(currentNotification.link));
}

function handleTabChange(key: unknown) {
  const nextKey = String(key);
  activeTab.value = nextKey;
  if (nextKey === 'deliveries' && deliveries.value.length === 0) {
    void loadDeliveries();
  }

  if (nextKey === 'templates' && templates.value.length === 0) {
    void loadTemplates();
  }

  if (nextKey === 'policies' && policies.value.length === 0) {
    void loadPolicies();
  }

  if (nextKey === 'subscriptions' && subscriptions.value.length === 0) {
    void loadSubscriptions();
  }
}

function levelColor(level: string) {
  if (level === 'Critical') {
    return 'red';
  }

  if (level === 'Warning') {
    return 'orange';
  }

  return 'blue';
}

function levelText(level: string) {
  if (level === 'Critical') {
    return '严重';
  }

  if (level === 'Warning') {
    return '警告';
  }

  return '提示';
}

function deliveryStatusColor(status: string) {
  if (status === 'Succeeded') {
    return 'green';
  }

  if (status === 'Failed') {
    return 'red';
  }

  if (status === 'Skipped') {
    return 'gold';
  }

  return 'blue';
}

function deliveryStatusText(status: string) {
  return (
    deliveryStatusOptions.find((item) => item.value === status)?.label ?? status
  );
}

function channelText(channel: string) {
  return (
    deliveryChannelOptions.find((item) => item.value === channel)?.label ??
    channel
  );
}

function categoryText(category: string) {
  return (
    messageCategoryOptions.find((item) => item.value === category)?.label ??
    category
  );
}

function policyRecipientStrategyText(strategy: string) {
  return (
    policyRecipientStrategyOptions.find((item) => item.value === strategy)
      ?.label ?? strategy
  );
}

function messageSourceTypeText(sourceType: string) {
  return (
    messageSourceTypeOptions.find((item) => item.value === sourceType)?.label ??
    sourceType
  );
}

function deliverySourceTypeText(sourceType: string) {
  return (
    deliverySourceTypeOptions.find((item) => item.value === sourceType)?.label ??
    sourceType
  );
}

function canRetryDeliveryRecord(record: NotificationDeliveryItem | Record<string, any>) {
  const delivery = record as NotificationDeliveryItem;
  return (
    canRetryDelivery.value &&
    ['Email', 'Webhook'].includes(delivery.channel) &&
    ['Failed', 'Skipped'].includes(delivery.status)
  );
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function channelToneClass(channel: NotificationChannelStatusItem) {
  if (!channel.isEnabled) {
    return 'channel-panel channel-panel-muted';
  }

  if (channel.channel === 'Email') {
    return 'channel-panel channel-panel-email';
  }

  if (channel.channel === 'InApp') {
    return 'channel-panel channel-panel-inapp';
  }

  return 'channel-panel';
}

onMounted(async () => {
  applyRouteQueryIntent();
  await Promise.all([loadOverview(), loadNotifications(), loadDeliveries()]);
});

watch(
  () => [route.query.tab, route.query.deliveryStatus],
  () => {
    if (applyRouteQueryIntent()) {
      void loadDeliveries();
    }
  },
);
</script>

<template>
  <Page auto-content-height>
    <div class="message-center-page">
      <section class="hero-band">
        <div class="hero-copy">
          <p class="hero-eyebrow">System Monitor</p>
          <h2>消息通知中心</h2>
          <span>把审批提醒、系统告警和投递结果收进一个工作台。</span>
        </div>
        <Space>
          <Button @click="loadOverview">刷新概览</Button>
          <Button type="primary" @click="refreshNotifications">刷新消息</Button>
        </Space>
      </section>

      <section class="metrics-grid" :class="{ 'is-loading': overviewLoading }">
        <div class="metric-panel metric-panel-primary">
          <span>我的未读</span>
          <strong>{{ overview.unreadNotificationCount }}</strong>
          <small>需要尽快处理的站内消息</small>
        </div>
        <div class="metric-panel">
          <span>消息总量</span>
          <strong>{{ overview.totalNotificationCount }}</strong>
          <small>当前账号累计站内消息</small>
        </div>
        <div class="metric-panel">
          <span>邮件成功</span>
          <strong>{{ emailChannel?.succeededCount ?? 0 }}</strong>
          <small>已成功送达的邮件记录</small>
        </div>
        <div class="metric-panel">
          <span>邮件异常</span>
          <strong>{{ emailChannel?.failedCount ?? 0 }}</strong>
          <small>失败或跳过的邮件记录</small>
        </div>
      </section>

      <section v-if="deliveryFailureCount > 0" class="delivery-alert">
        <div>
          <strong>发现 {{ deliveryFailureCount }} 条外部投递异常</strong>
          <span>可在投递记录中查看失败原因，并对邮件或 Webhook 进行重发。</span>
        </div>
        <Button danger type="primary" @click="showFailedDeliveries">
          查看异常投递
        </Button>
      </section>

      <section class="workspace-shell">
        <Tabs :active-key="activeTab" @change="handleTabChange">
          <Tabs.TabPane key="messages" tab="我的消息">
            <div class="query-bar">
              <Space wrap>
                <span class="query-label">状态</span>
                <Select
                  v-model:value="messageQuery.isRead"
                  allow-clear
                  class="query-select"
                  :options="readOptions"
                  placeholder="全部"
                />
                <span class="query-label">来源组</span>
                <Select
                  v-model:value="messageQuery.category"
                  allow-clear
                  class="query-select"
                  :options="messageCategoryOptions"
                  placeholder="全部"
                />
                <span class="query-label">来源</span>
                <Select
                  v-model:value="messageQuery.sourceType"
                  allow-clear
                  class="query-select"
                  :options="messageSourceTypeOptions"
                  placeholder="全部"
                />
              </Space>
              <Space>
                <Button @click="handleMessageReset">重置</Button>
                <Button type="primary" @click="handleMessageSearch">搜索</Button>
              </Space>
            </div>

            <div class="table-shell">
              <div class="table-toolbar">
                <div class="toolbar-title">
                  <h3>站内消息</h3>
                  <span>
                    未读 {{ unreadCount }} 条，流程消息可直达审批详情
                  </span>
                </div>
                <Space>
                  <Button @click="refreshNotifications">刷新</Button>
                  <Button :disabled="unreadCount <= 0" @click="markAllRead">
                    全部已读
                  </Button>
                  <Popconfirm title="确认清空当前用户全部消息？" @confirm="clearAll">
                    <Button danger :disabled="total <= 0">清空消息</Button>
                  </Popconfirm>
                </Space>
              </div>

              <Table
                row-key="id"
                bordered
                size="small"
                :columns="messageColumns"
                :data-source="notifications"
                :loading="notificationLoading"
                :pagination="messagePagination"
                :scroll="{ x: 1380 }"
                @change="handleMessageTableChange"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'isRead'">
                    <Tag :color="record.isRead ? 'default' : 'green'">
                      {{ record.isRead ? '已读' : '未读' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'level'">
                    <Tag :color="levelColor(record.level)">
                      {{ levelText(record.level) }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'title'">
                    <div class="title-cell">
                      <strong>{{ record.title }}</strong>
                      <small>{{ record.message }}</small>
                    </div>
                  </template>
                  <template v-if="column.dataIndex === 'category'">
                    {{ categoryText(record.category) }}
                  </template>
                  <template v-if="column.dataIndex === 'sourceType'">
                    {{ messageSourceTypeText(record.sourceType) }}
                  </template>
                  <template v-if="column.dataIndex === 'createdAt'">
                    {{ formatTime(record.createdAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'readAt'">
                    {{ formatTime(record.readAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <Space>
                      <Button
                        v-if="!record.isRead"
                        size="small"
                        type="link"
                        @click="markRead(record)"
                      >
                        已读
                      </Button>
                      <Button
                        v-if="record.link"
                        size="small"
                        type="link"
                        @click="openLink(record)"
                      >
                        查看
                      </Button>
                      <Popconfirm title="确认删除这条消息？" @confirm="removeNotification(record)">
                        <Button danger size="small" type="link">删除</Button>
                      </Popconfirm>
                    </Space>
                  </template>
                </template>
              </Table>
            </div>
          </Tabs.TabPane>

          <Tabs.TabPane key="deliveries" tab="投递记录">
            <div class="query-bar">
              <Space wrap>
                <span class="query-label">通道</span>
                <Select
                  v-model:value="deliveryQuery.channel"
                  allow-clear
                  class="query-select"
                  :options="deliveryChannelOptions"
                  placeholder="全部"
                />
                <span class="query-label">状态</span>
                <Select
                  v-model:value="deliveryQuery.status"
                  allow-clear
                  class="query-select"
                  :options="deliveryStatusOptions"
                  placeholder="全部"
                />
                <span class="query-label">来源</span>
                <Select
                  v-model:value="deliveryQuery.sourceType"
                  allow-clear
                  class="query-select"
                  :options="deliverySourceTypeOptions"
                  placeholder="全部"
                />
              </Space>
              <Space>
                <Button @click="handleDeliveryReset">重置</Button>
                <Button type="primary" @click="handleDeliverySearch">搜索</Button>
              </Space>
            </div>

            <div class="table-shell">
              <div class="table-toolbar">
                <div class="toolbar-title">
                  <h3>通道投递记录</h3>
                  <span>展示邮件与 Webhook 投递结果，失败记录可手工重发</span>
                </div>
                <Button @click="loadDeliveries">刷新</Button>
              </div>

              <Table
                row-key="id"
                bordered
                size="small"
                :columns="deliveryColumns"
                :data-source="deliveries"
                :loading="deliveryLoading"
                :pagination="deliveryPagination"
                :scroll="{ x: 1570 }"
                @change="handleDeliveryTableChange"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'channel'">
                    <Tag color="geekblue">{{ channelText(record.channel) }}</Tag>
                  </template>
                  <template v-if="column.dataIndex === 'status'">
                    <Tag :color="deliveryStatusColor(record.status)">
                      {{ deliveryStatusText(record.status) }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'title'">
                    <div class="title-cell">
                      <strong>{{ record.title }}</strong>
                      <small>
                        {{ deliverySourceTypeText(record.sourceType) }} / {{ record.sourceId }}
                      </small>
                    </div>
                  </template>
                  <template v-if="column.dataIndex === 'sourceType'">
                    {{ deliverySourceTypeText(record.sourceType) }}
                  </template>
                  <template v-if="column.dataIndex === 'createdAt'">
                    {{ formatTime(record.createdAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'sentAt'">
                    {{ formatTime(record.sentAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'errorMessage'">
                    <span class="result-text">
                      {{ record.errorMessage || '投递完成' }}
                    </span>
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <Popconfirm
                      v-if="canRetryDeliveryRecord(record)"
                      title="确认重新发送这条投递记录？"
                      ok-text="重发"
                      cancel-text="取消"
                      @confirm="handleRetryDelivery(record)"
                    >
                      <Button
                        size="small"
                        type="link"
                        :loading="retryingDeliveryId === record.id"
                      >
                        重发
                      </Button>
                    </Popconfirm>
                    <span v-else class="muted-text">-</span>
                  </template>
                </template>
              </Table>
            </div>
          </Tabs.TabPane>

          <Tabs.TabPane key="policies" tab="通知策略">
            <div class="query-bar">
              <Space wrap>
                <span class="query-label">关键词</span>
                <Input
                  v-model:value="policyQuery.keyword"
                  class="query-input"
                  placeholder="事件编码 / 名称 / 说明"
                  @press-enter="handlePolicySearch"
                />
                <span class="query-label">分类</span>
                <Select
                  v-model:value="policyQuery.category"
                  allow-clear
                  class="query-select"
                  :options="messageCategoryOptions"
                  placeholder="全部"
                />
                <span class="query-label">状态</span>
                <Select
                  v-model:value="policyQuery.isEnabled"
                  allow-clear
                  class="query-select"
                  :options="enabledOptions"
                  placeholder="全部"
                />
              </Space>
              <Space>
                <Button @click="handlePolicyReset">重置</Button>
                <Button type="primary" @click="handlePolicySearch">搜索</Button>
              </Space>
            </div>

            <div class="table-shell">
              <div class="table-toolbar">
                <div class="toolbar-title">
                  <h3>通知策略</h3>
                  <span>策略管是否通知，模板管通知文案</span>
                </div>
                <Button @click="loadPolicies">刷新</Button>
              </div>

              <div class="policy-note">
                当前工作流已执行“策略启用 + 站内信 / 邮件 / Webhook
                开关”；外部通道会同步写入投递记录。
              </div>

              <Table
                row-key="id"
                bordered
                size="small"
                :columns="policyColumns"
                :data-source="policies"
                :loading="policyLoading"
                :pagination="policyPagination"
                :scroll="{ x: 1300 }"
                @change="handlePolicyTableChange"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'eventName'">
                    <div class="title-cell">
                      <strong>{{ record.eventName }}</strong>
                      <small>{{ record.remark || '暂无说明' }}</small>
                    </div>
                  </template>
                  <template v-if="column.dataIndex === 'eventCode'">
                    <Tag color="geekblue">{{ record.eventCode }}</Tag>
                  </template>
                  <template v-if="column.dataIndex === 'category'">
                    {{ categoryText(record.category) }}
                  </template>
                  <template v-if="column.dataIndex === 'recipientStrategy'">
                    {{ policyRecipientStrategyText(record.recipientStrategy) }}
                  </template>
                  <template v-if="column.dataIndex === 'enableInApp'">
                    <Tag :color="record.enableInApp ? 'green' : 'default'">
                      {{ record.enableInApp ? '开启' : '关闭' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'enableEmail'">
                    <Tag :color="record.enableEmail ? 'blue' : 'default'">
                      {{ record.enableEmail ? '开启' : '关闭' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'enableWebhook'">
                    <Tag :color="record.enableWebhook ? 'purple' : 'default'">
                      {{ record.enableWebhook ? '开启' : '关闭' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'isEnabled'">
                    <Tag :color="record.isEnabled ? 'green' : 'red'">
                      {{ record.isEnabled ? '启用' : '停用' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'updatedAt'">
                    {{ formatTime(record.updatedAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <Button
                      v-if="canUpdatePolicy"
                      size="small"
                      type="link"
                      @click="openPolicyEditor(record)"
                    >
                      编辑
                    </Button>
                  </template>
                </template>
              </Table>
            </div>
          </Tabs.TabPane>

          <Tabs.TabPane key="subscriptions" tab="订阅偏好">
            <div class="query-bar">
              <Space wrap>
                <span class="query-label">关键词</span>
                <Input
                  v-model:value="subscriptionQuery.keyword"
                  class="query-input"
                  placeholder="事件编码 / 名称"
                  @press-enter="handleSubscriptionSearch"
                />
                <span class="query-label">分类</span>
                <Select
                  v-model:value="subscriptionQuery.category"
                  allow-clear
                  class="query-select"
                  :options="messageCategoryOptions"
                  placeholder="全部"
                />
              </Space>
              <Space>
                <Button @click="handleSubscriptionReset">重置</Button>
                <Button type="primary" @click="handleSubscriptionSearch">
                  搜索
                </Button>
              </Space>
            </div>

            <div class="table-shell">
              <div class="table-toolbar">
                <div class="toolbar-title">
                  <h3>我的订阅偏好</h3>
                  <span>
                    共 {{ subscriptionTotal }} 个事件；{{ customSubscriptionCount }}
                    个已自定义；不设置时自动跟随全局通知策略
                  </span>
                </div>
                <Space>
                  <Button @click="loadSubscriptions">刷新</Button>
                  <Popconfirm
                    title="确认将当前账号所有订阅偏好恢复为全局默认？"
                    ok-text="恢复默认"
                    cancel-text="取消"
                    @confirm="resetAllSubscriptionPreferences"
                  >
                    <Button
                      :disabled="customSubscriptionCount <= 0"
                      :loading="resettingSubscriptions"
                    >
                      全部恢复默认
                    </Button>
                  </Popconfirm>
                </Space>
              </div>

              <div class="policy-note">
                这里控制当前登录账号自己的接收偏好；全局策略关闭的通道不会被个人偏好强制打开。
              </div>

              <Table
                row-key="eventCode"
                bordered
                size="small"
                :columns="subscriptionColumns"
                :data-source="subscriptions"
                :loading="subscriptionLoading"
                :pagination="false"
                :scroll="{ x: 1260 }"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'eventName'">
                    <div class="title-cell">
                      <strong>{{ record.eventName }}</strong>
                      <small>
                        默认：站内信
                        {{ record.policyEnableInApp ? '开' : '关' }} / 邮件
                        {{ record.policyEnableEmail ? '开' : '关' }} / Webhook
                        {{ record.policyEnableWebhook ? '开' : '关' }}
                      </small>
                    </div>
                  </template>
                  <template v-if="column.dataIndex === 'eventCode'">
                    <Tag color="geekblue">{{ record.eventCode }}</Tag>
                  </template>
                  <template v-if="column.dataIndex === 'category'">
                    {{ categoryText(record.category) }}
                  </template>
                  <template v-if="column.dataIndex === 'isEnabled'">
                    <Switch
                      :checked="record.isEnabled"
                      :loading="savingSubscriptionCode === record.eventCode"
                      @change="
                        (checked) =>
                          saveSubscriptionPreference(record, {
                            isEnabled: Boolean(checked),
                          })
                      "
                    />
                  </template>
                  <template v-if="column.dataIndex === 'enableInApp'">
                    <Switch
                      :checked="record.enableInApp"
                      :disabled="!record.policyEnableInApp"
                      :loading="savingSubscriptionCode === record.eventCode"
                      @change="
                        (checked) =>
                          saveSubscriptionPreference(record, {
                            enableInApp: Boolean(checked),
                          })
                      "
                    />
                  </template>
                  <template v-if="column.dataIndex === 'enableEmail'">
                    <Switch
                      :checked="record.enableEmail"
                      :disabled="!record.policyEnableEmail"
                      :loading="savingSubscriptionCode === record.eventCode"
                      @change="
                        (checked) =>
                          saveSubscriptionPreference(record, {
                            enableEmail: Boolean(checked),
                          })
                      "
                    />
                  </template>
                  <template v-if="column.dataIndex === 'enableWebhook'">
                    <Switch
                      :checked="record.enableWebhook"
                      :disabled="!record.policyEnableWebhook"
                      :loading="savingSubscriptionCode === record.eventCode"
                      @change="
                        (checked) =>
                          saveSubscriptionPreference(record, {
                            enableWebhook: Boolean(checked),
                          })
                      "
                    />
                  </template>
                  <template v-if="column.dataIndex === 'hasCustomPreference'">
                    <Tag :color="record.hasCustomPreference ? 'blue' : 'default'">
                      {{ record.hasCustomPreference ? '已自定义' : '跟随全局' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'updatedAt'">
                    {{ formatTime(record.updatedAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <Button
                      size="small"
                      type="link"
                      :disabled="!record.hasCustomPreference"
                      :loading="savingSubscriptionCode === record.eventCode"
                      @click="resetSubscriptionPreference(record)"
                    >
                      恢复默认
                    </Button>
                  </template>
                </template>
              </Table>
            </div>
          </Tabs.TabPane>

          <Tabs.TabPane key="templates" tab="模板配置">
            <div class="query-bar">
              <Space wrap>
                <span class="query-label">关键词</span>
                <Input
                  v-model:value="templateQuery.keyword"
                  class="query-input"
                  placeholder="编码 / 名称 / 标题"
                  @press-enter="handleTemplateSearch"
                />
                <span class="query-label">分类</span>
                <Select
                  v-model:value="templateQuery.category"
                  allow-clear
                  class="query-select"
                  :options="messageCategoryOptions"
                  placeholder="全部"
                />
                <span class="query-label">状态</span>
                <Select
                  v-model:value="templateQuery.isEnabled"
                  allow-clear
                  class="query-select"
                  :options="enabledOptions"
                  placeholder="全部"
                />
              </Space>
              <Space>
                <Button @click="handleTemplateReset">重置</Button>
                <Button type="primary" @click="handleTemplateSearch">搜索</Button>
              </Space>
            </div>

            <div class="table-shell">
              <div class="table-toolbar">
                <div class="toolbar-title">
                  <h3>消息模板</h3>
                  <span>支持站内信、邮件与 Webhook 通道文案管理</span>
                </div>
                <Button @click="loadTemplates">刷新</Button>
              </div>

              <Table
                row-key="id"
                bordered
                size="small"
                :columns="templateColumns"
                :data-source="templates"
                :loading="templateLoading"
                :pagination="templatePagination"
                :scroll="{ x: 1480 }"
                @change="handleTemplateTableChange"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'code'">
                    <div class="title-cell">
                      <strong>{{ record.code }}</strong>
                      <small>{{ record.channel || 'InApp' }}</small>
                    </div>
                  </template>
                  <template v-if="column.dataIndex === 'category'">
                    {{ categoryText(record.category) }}
                  </template>
                  <template v-if="column.dataIndex === 'level'">
                    <Tag :color="levelColor(record.level)">
                      {{ levelText(record.level) }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'titleTemplate'">
                    <span class="template-snippet">{{ record.titleTemplate }}</span>
                  </template>
                  <template v-if="column.dataIndex === 'messageTemplate'">
                    <span class="template-snippet">{{ record.messageTemplate }}</span>
                  </template>
                  <template v-if="column.dataIndex === 'isEnabled'">
                    <Tag :color="record.isEnabled ? 'green' : 'default'">
                      {{ record.isEnabled ? '启用' : '停用' }}
                    </Tag>
                  </template>
                  <template v-if="column.dataIndex === 'updatedAt'">
                    {{ formatTime(record.updatedAt) }}
                  </template>
                  <template v-if="column.dataIndex === 'action'">
                    <Space>
                      <Button size="small" type="link" @click="previewTemplate(record)">
                        预览
                      </Button>
                      <Button
                        v-if="canUpdateTemplate"
                        size="small"
                        type="link"
                        @click="openTemplateEditor(record)"
                      >
                        编辑
                      </Button>
                    </Space>
                  </template>
                </template>
              </Table>
            </div>
          </Tabs.TabPane>

          <Tabs.TabPane key="channels" tab="通道概览">
            <div class="channel-grid">
              <div
                v-for="channel in overview.channels"
                :key="channel.channel"
                :class="channelToneClass(channel)"
              >
                <div class="channel-head">
                  <div>
                    <h3>{{ channel.displayName }}</h3>
                    <p>{{ channel.description }}</p>
                  </div>
                  <Badge
                    :status="channel.isEnabled ? 'processing' : 'default'"
                    :text="channel.isEnabled ? '已启用' : '未启用'"
                  />
                </div>
                <div class="channel-stats">
                  <div>
                    <span>待处理</span>
                    <strong>{{ channel.pendingCount }}</strong>
                  </div>
                  <div>
                    <span>成功</span>
                    <strong>{{ channel.succeededCount }}</strong>
                  </div>
                  <div>
                    <span>异常</span>
                    <strong>{{ channel.failedCount }}</strong>
                  </div>
                </div>
              </div>
            </div>

            <div v-if="overview.channels.length === 0" class="empty-shell">
              <Empty description="暂无通道数据" />
            </div>

            <div class="channel-footnote">
              <span>
                站内信已覆盖审批待办、审批结果、流程抄送与部分系统告警。
              </span>
              <span>
                邮件沿用现有 SMTP 配置；Webhook 会向配置的外部地址推送 JSON。
              </span>
              <span v-if="inAppChannel">
                当前账号累计消息 {{ inAppChannel.succeededCount }} 条。
              </span>
            </div>
          </Tabs.TabPane>
        </Tabs>
      </section>

      <Modal
        v-model:open="policyModalOpen"
        :confirm-loading="policySaving"
        title="编辑通知策略"
        width="680px"
        @ok="savePolicy"
      >
        <Form layout="vertical">
          <div class="form-grid">
            <FormItem label="事件编码">
              <Input :value="editingPolicy?.eventCode" disabled />
            </FormItem>
            <FormItem label="事件名称" required>
              <Input v-model:value="policyForm.eventName" placeholder="请输入事件名称" />
            </FormItem>
            <FormItem label="分类" required>
              <Select
                v-model:value="policyForm.category"
                :options="messageCategoryOptions"
              />
            </FormItem>
            <FormItem label="接收人策略" required>
              <Select
                v-model:value="policyForm.recipientStrategy"
                :options="policyRecipientStrategyOptions"
              />
            </FormItem>
            <FormItem label="站内信">
              <Switch v-model:checked="policyForm.enableInApp" />
            </FormItem>
            <FormItem label="策略启用">
              <Switch v-model:checked="policyForm.isEnabled" />
            </FormItem>
            <FormItem label="邮件">
              <Switch v-model:checked="policyForm.enableEmail" />
            </FormItem>
            <FormItem label="Webhook">
              <Switch v-model:checked="policyForm.enableWebhook" />
            </FormItem>
          </div>
          <FormItem label="备注">
            <Textarea
              v-model:value="policyForm.remark"
              :auto-size="{ minRows: 3, maxRows: 5 }"
              placeholder="说明这个事件什么时候通知、通知谁"
            />
          </FormItem>
          <div class="template-help">
            工作流会实时消费这里的站内信、邮件和 Webhook
            开关；外部通道是否真正发送还取决于后端配置是否启用。
          </div>
        </Form>
      </Modal>

      <Modal
        v-model:open="templateModalOpen"
        :confirm-loading="templateSaving"
        title="编辑消息模板"
        width="760px"
        @ok="saveTemplate"
      >
        <Form layout="vertical">
          <div class="form-grid">
            <FormItem label="模板编码">
              <Input :value="editingTemplate?.code" disabled />
            </FormItem>
            <FormItem label="模板名称" required>
              <Input v-model:value="templateForm.name" placeholder="请输入模板名称" />
            </FormItem>
            <FormItem label="分类" required>
              <Select
                v-model:value="templateForm.category"
                :options="messageCategoryOptions"
              />
            </FormItem>
            <FormItem label="等级" required>
              <Select
                v-model:value="templateForm.level"
                :options="templateLevelOptions"
              />
            </FormItem>
            <FormItem label="通道">
              <Select
                v-model:value="templateForm.channel"
                allow-clear
                :options="templateChannelOptions"
              />
            </FormItem>
            <FormItem label="启用状态">
              <Switch v-model:checked="templateForm.isEnabled" />
            </FormItem>
          </div>
          <FormItem label="标题模板" required>
            <Input
              v-model:value="templateForm.titleTemplate"
              placeholder="例如 审批催办：{instanceTitle}"
            />
          </FormItem>
          <FormItem label="内容模板" required>
            <Textarea
              v-model:value="templateForm.messageTemplate"
              :auto-size="{ minRows: 4, maxRows: 8 }"
              placeholder="例如 {operatorUserName} 正在催办 {nodeName}"
            />
          </FormItem>
          <FormItem label="链接模板">
            <Input
              v-model:value="templateForm.linkTemplate"
              placeholder="例如 /workflow/center"
            />
          </FormItem>
          <FormItem label="备注">
            <Textarea
              v-model:value="templateForm.remark"
              :auto-size="{ minRows: 2, maxRows: 4 }"
            />
          </FormItem>
          <div class="template-help">
            可用示例变量：{title}、{content}、{levelText}、{instanceTitle}、{definitionName}、{nodeName}、{operatorUserName}、{comment}
          </div>
          <Button @click="previewTemplate()">预览当前模板</Button>
        </Form>
      </Modal>

      <Modal
        v-model:open="previewModalOpen"
        :footer="null"
        title="模板预览"
        width="620px"
      >
        <div class="preview-card">
          <span>标题</span>
          <strong>{{ previewResult.title }}</strong>
        </div>
        <div class="preview-card">
          <span>内容</span>
          <p>{{ previewResult.message }}</p>
        </div>
        <div class="preview-card">
          <span>链接</span>
          <p>{{ previewResult.link || '-' }}</p>
        </div>
      </Modal>
    </div>
  </Page>
</template>

<style scoped>
.message-center-page {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.hero-band,
.workspace-shell,
.query-bar,
.table-shell,
.metric-panel,
.channel-panel,
.delivery-alert {
  border: 1px solid color-mix(in srgb, hsl(var(--border)) 75%, transparent);
  border-radius: 8px;
  background: hsl(var(--background));
}

.hero-band {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding: 18px 20px;
  background:
    linear-gradient(135deg, rgba(17, 24, 39, 0.04), rgba(12, 74, 110, 0.08)),
    hsl(var(--background));
}

.hero-copy {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.hero-eyebrow {
  margin: 0;
  color: #0f766e;
  font-size: 12px;
  font-weight: 700;
  text-transform: uppercase;
}

.hero-copy h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 700;
}

.hero-copy span,
.toolbar-title span,
.title-cell small,
.channel-head p,
.metric-panel small,
.channel-footnote span,
.muted-text,
.result-text {
  color: hsl(var(--muted-foreground));
}

.metrics-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.metric-panel {
  display: flex;
  min-height: 118px;
  flex-direction: column;
  justify-content: space-between;
  padding: 16px;
}

.metric-panel span {
  font-size: 12px;
  font-weight: 700;
}

.metric-panel strong {
  font-size: 28px;
  line-height: 1;
}

.metric-panel-primary {
  background:
    linear-gradient(135deg, rgba(15, 118, 110, 0.1), rgba(2, 132, 199, 0.08)),
    hsl(var(--background));
}

.delivery-alert {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 14px;
  border-color: rgba(239, 68, 68, 0.28);
  background:
    linear-gradient(135deg, rgba(239, 68, 68, 0.08), rgba(251, 146, 60, 0.06)),
    hsl(var(--background));
}

.delivery-alert div {
  display: flex;
  flex-direction: column;
  gap: 3px;
}

.delivery-alert strong {
  color: #b91c1c;
}

.delivery-alert span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.workspace-shell {
  padding: 12px 14px 14px;
}

.query-bar {
  display: flex;
  min-height: 64px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 12px;
  padding: 10px 12px;
}

.query-label {
  font-weight: 600;
  white-space: nowrap;
}

.query-select {
  width: 160px;
}

.query-input {
  width: 220px;
}

.table-shell {
  min-height: 0;
  padding: 10px 10px 0;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 10px;
}

.toolbar-title {
  display: flex;
  align-items: baseline;
  gap: 10px;
}

.toolbar-title h3,
.channel-head h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.title-cell {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 2px;
}

.title-cell small {
  max-width: 520px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.template-snippet {
  display: inline-block;
  max-width: 320px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.form-grid {
  display: grid;
  gap: 0 14px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.template-help {
  margin: -4px 0 12px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.6;
}

.policy-note {
  border: 1px solid rgba(14, 165, 233, 0.18);
  border-radius: 8px;
  margin-bottom: 10px;
  padding: 10px 12px;
  background:
    linear-gradient(135deg, rgba(14, 165, 233, 0.08), rgba(20, 184, 166, 0.05)),
    hsl(var(--background));
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.7;
}

.preview-card {
  border: 1px solid color-mix(in srgb, hsl(var(--border)) 75%, transparent);
  border-radius: 8px;
  padding: 12px;
}

.preview-card + .preview-card {
  margin-top: 10px;
}

.preview-card span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.preview-card strong,
.preview-card p {
  display: block;
  margin: 6px 0 0;
  line-height: 1.7;
}

.channel-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.channel-panel {
  display: flex;
  min-height: 186px;
  flex-direction: column;
  justify-content: space-between;
  padding: 16px;
}

.channel-panel-inapp {
  background:
    linear-gradient(180deg, rgba(13, 148, 136, 0.07), transparent),
    hsl(var(--background));
}

.channel-panel-email {
  background:
    linear-gradient(180deg, rgba(37, 99, 235, 0.07), transparent),
    hsl(var(--background));
}

.channel-panel-muted {
  background:
    linear-gradient(180deg, rgba(100, 116, 139, 0.06), transparent),
    hsl(var(--background));
}

.channel-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.channel-head p {
  margin: 6px 0 0;
  font-size: 13px;
  line-height: 1.6;
}

.channel-stats {
  display: grid;
  gap: 10px;
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.channel-stats div {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.channel-stats span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.channel-stats strong {
  font-size: 24px;
  line-height: 1;
}

.channel-footnote {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 12px;
  padding: 12px 2px 2px;
}

.empty-shell {
  padding: 32px 0;
}

.is-loading {
  opacity: 0.7;
}

:deep(.ant-tabs-nav) {
  margin-bottom: 14px;
}

:deep(.ant-table) {
  font-size: 13px;
}

@media (max-width: 1280px) {
  .metrics-grid,
  .channel-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 900px) {
  .hero-band,
  .delivery-alert,
  .query-bar,
  .table-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .metrics-grid,
  .channel-grid,
  .channel-stats {
    grid-template-columns: minmax(0, 1fr);
  }

  .query-select {
    width: 140px;
  }

  .query-input {
    width: 100%;
  }

  .form-grid {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
