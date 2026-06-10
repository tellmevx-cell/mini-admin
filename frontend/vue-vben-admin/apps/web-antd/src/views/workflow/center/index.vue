<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Descriptions,
  DescriptionsItem,
  Drawer,
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
  TabPane,
  Tag,
  Textarea,
  Timeline,
  TimelineItem,
  message,
} from 'ant-design-vue';
import { Handle, MarkerType, Position, VueFlow } from '@vue-flow/core';
import '@vue-flow/core/dist/style.css';
import '@vue-flow/core/dist/theme-default.css';

import { uploadFileApi, type FileItem } from '#/api/system/file';
import {
  addWorkflowAttachmentApi,
  addWorkflowCommentApi,
  approveWorkflowInstanceApi,
  createWorkflowBusinessBindingApi,
  createWorkflowDefinitionVersionApi,
  createWorkflowDefinitionApi,
  deleteWorkflowBusinessBindingApi,
  deleteWorkflowDefinitionApi,
  getWorkflowBusinessBindingsApi,
  getWorkflowApproverRolesApi,
  getWorkflowApproverUsersApi,
  getWorkflowCcInstancesApi,
  getWorkflowDefinitionOptionsApi,
  getWorkflowDefinitionsApi,
  getWorkflowDoneTasksApi,
  downloadWorkflowAttachmentApi,
  getWorkflowInstanceApi,
  getWorkflowInstancesApi,
  getWorkflowStartedByMeApi,
  getWorkflowTodoTasksApi,
  markWorkflowCcRecordReadApi,
  publishWorkflowDefinitionApi,
  rejectWorkflowInstanceApi,
  remindWorkflowTaskApi,
  startWorkflowInstanceApi,
  transferWorkflowTaskApi,
  type SaveWorkflowNodeRequest,
  type WorkflowApproverRoleOption,
  type WorkflowApproverUserOption,
  type WorkflowBusinessBindingItem,
  type WorkflowCcRecordItem,
  type WorkflowDefinitionItem,
  type WorkflowDefinitionOption,
  type WorkflowInstanceItem,
  type WorkflowTaskItem,
  updateWorkflowBusinessBindingApi,
  updateWorkflowDefinitionApi,
  withdrawWorkflowInstanceApi,
} from '#/api/workflow/center';

const { hasAccessByCodes } = useAccess();
const route = useRoute();
const router = useRouter();

const activeTab = ref('todo');
const definitionLoading = ref(false);
const instanceLoading = ref(false);
const taskLoading = ref(false);
const bindingLoading = ref(false);
const saving = ref(false);
const actionSaving = ref(false);
const remindSavingTaskId = ref('');
const detailModalOpen = ref(false);
const detailLoading = ref(false);
const actionModalOpen = ref(false);
const transferModalOpen = ref(false);
const bindingModalOpen = ref(false);
const startGuideOpen = ref(false);
const definitionGuideOpen = ref(false);
const definitionGuideStep = ref(0);
const definitionGuideSavedDefinitionId = ref('');
const actionType = ref<'approve' | 'reject'>('approve');
const actionTarget = ref<WorkflowTaskItem>();
const actionComment = ref('');
const ccReadSavingId = ref('');
const transferTarget = ref<WorkflowTaskItem>();
const transferForm = reactive({
  comment: '',
  targetUserId: '',
});
const editingDefinition = ref<WorkflowDefinitionItem>();
const selectedInstance = ref<WorkflowInstanceItem>();
const highlightedWorkflowTaskId = ref('');
const startAttachmentInputRef = ref<HTMLInputElement>();
const detailAttachmentInputRef = ref<HTMLInputElement>();
const startAttachments = ref<FileItem[]>([]);
const uploadingStartAttachment = ref(false);
const uploadingDetailAttachment = ref(false);
const commentContent = ref('');
const commentSaving = ref(false);
const users = ref<WorkflowApproverUserOption[]>([]);
const roles = ref<WorkflowApproverRoleOption[]>([]);
const definitions = ref<WorkflowDefinitionItem[]>([]);
const definitionOptions = ref<WorkflowDefinitionOption[]>([]);
const businessBindings = ref<WorkflowBusinessBindingItem[]>([]);
const instances = ref<WorkflowInstanceItem[]>([]);
const startedInstances = ref<WorkflowInstanceItem[]>([]);
const ccRecords = ref<WorkflowCcRecordItem[]>([]);
const todoTasks = ref<WorkflowTaskItem[]>([]);
const doneTasks = ref<WorkflowTaskItem[]>([]);
const definitionTotal = ref(0);
const bindingTotal = ref(0);
const instanceTotal = ref(0);
const startedTotal = ref(0);
const ccTotal = ref(0);
const selectedDefinitionId = ref('');
const editingBinding = ref<WorkflowBusinessBindingItem>();

type WorkflowFormComponent = 'date' | 'number' | 'select' | 'text' | 'textarea';

interface WorkflowFormSchemaOption {
  label: string;
  value: string;
}

interface WorkflowFormSchemaField {
  component: WorkflowFormComponent;
  defaultValue?: string;
  field: string;
  label: string;
  options?: WorkflowFormSchemaOption[];
  optionsText?: string;
  placeholder?: string;
  required: boolean;
}

interface WorkflowDefinitionSnapshot {
  formSchemaJson?: string;
}

const canManageDefinition = computed(() =>
  hasAccessByCodes(['workflow:definition:manage']),
);
const canStart = computed(() => hasAccessByCodes(['workflow:instance:start']));
const canApprove = computed(() => hasAccessByCodes(['workflow:task:approve']));
const todoTaskIds = computed(() => new Set(todoTasks.value.map((task) => task.id)));
const detailTodoTask = computed(() =>
  selectedInstance.value?.tasks.find(
    (task) => task.status === 'Pending' && todoTaskIds.value.has(task.id),
  ),
);
const detailPendingTask = computed(() =>
  selectedInstance.value ? getPendingTask(selectedInstance.value) : undefined,
);
const canRemindSelectedInstance = computed(
  () =>
    canStart.value &&
    selectedInstance.value?.status === 'Pending' &&
    !!detailPendingTask.value,
);
const canWithdrawSelectedInstance = computed(
  () => canStart.value && selectedInstance.value?.status === 'Pending',
);

const definitionQuery = reactive({
  isEnabled: undefined as string | undefined,
  keyword: '',
  page: 1,
  pageSize: 10,
});
const bindingQuery = reactive({
  isEnabled: undefined as string | undefined,
  keyword: '',
  page: 1,
  pageSize: 10,
});
const instanceQuery = reactive({
  keyword: '',
  page: 1,
  pageSize: 10,
  scope: 'all',
  status: undefined as string | undefined,
});
const startedQuery = reactive({
  keyword: '',
  page: 1,
  pageSize: 10,
  status: undefined as string | undefined,
});
const ccQuery = reactive({
  keyword: '',
  page: 1,
  pageSize: 10,
  readStatus: undefined as string | undefined,
  status: undefined as string | undefined,
});
const definitionForm = reactive({
  code: '',
  description: '',
  formName: '',
  isEnabled: true,
  name: '',
  nodes: [] as SaveWorkflowNodeRequest[],
});
const formSchemaFields = ref<WorkflowFormSchemaField[]>([]);
const flowNodes = ref<any[]>([]);
const flowEdges = ref<any[]>([]);
const selectedDesignerNodeId = ref('');
const selectedDesignerEdgeId = ref('');
const startForm = reactive({
  businessKey: '',
  definitionId: '',
  formDataJson: '{\n  "reason": ""\n}',
  title: '',
});
const startFormData = reactive<Record<string, any>>({});
const bindingForm = reactive({
  businessName: '',
  businessType: '',
  definitionId: '',
  isEnabled: true,
  remark: '',
});

const taskColumns = [
  { dataIndex: 'instanceTitle', title: '审批标题', width: 220 },
  { dataIndex: 'definitionName', title: '流程', width: 150 },
  { dataIndex: 'nodeName', title: '当前节点', width: 140 },
  { dataIndex: 'createdAt', title: '到达时间', width: 180 },
  { dataIndex: 'dueAt', title: '截止时间', width: 190 },
  { dataIndex: 'status', title: '状态', width: 110 },
  { dataIndex: 'action', title: '操作', width: 260 },
];
const instanceColumns = [
  { dataIndex: 'title', title: '标题', width: 220 },
  { dataIndex: 'definitionName', title: '流程', width: 150 },
  { dataIndex: 'currentNodeName', title: '当前节点', width: 140 },
  { dataIndex: 'initiatorUserName', title: '发起人', width: 120 },
  { dataIndex: 'status', title: '状态', width: 110 },
  { dataIndex: 'startedAt', title: '发起时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 230 },
];
const ccColumns = [
  { dataIndex: 'instanceTitle', title: '审批标题', width: 220 },
  { dataIndex: 'definitionName', title: '流程', width: 150 },
  { dataIndex: 'nodeName', title: '抄送节点', width: 140 },
  { dataIndex: 'senderUserName', title: '抄送来源', width: 130 },
  { dataIndex: 'instanceStatus', title: '流程状态', width: 110 },
  { dataIndex: 'readStatus', title: '阅知状态', width: 110 },
  { dataIndex: 'createdAt', title: '抄送时间', width: 180 },
  { dataIndex: 'readAt', title: '阅读时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 220 },
];
const bindingColumns = [
  { dataIndex: 'businessName', title: '业务名称', width: 160 },
  { dataIndex: 'businessType', title: '业务类型', width: 150 },
  { dataIndex: 'definitionName', title: '绑定流程', width: 220 },
  { dataIndex: 'definitionPublishStatus', title: '流程状态', width: 110 },
  { dataIndex: 'isEnabled', title: '绑定状态', width: 110 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 180 },
];

const bindingPagination = computed<TablePaginationConfig>(() => ({
  current: bindingQuery.page,
  pageSize: bindingQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: bindingTotal.value,
}));
const instancePagination = computed<TablePaginationConfig>(() => ({
  current: instanceQuery.page,
  pageSize: instanceQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: instanceTotal.value,
}));
const startedPagination = computed<TablePaginationConfig>(() => ({
  current: startedQuery.page,
  pageSize: startedQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: startedTotal.value,
}));
const ccPagination = computed<TablePaginationConfig>(() => ({
  current: ccQuery.page,
  pageSize: ccQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: ccTotal.value,
}));
const userOptions = computed(() =>
  users.value.map((user) => ({
    label: `${user.realName || user.userName}（${user.userName}）`,
    value: user.id,
  })),
);
const roleOptions = computed(() =>
  roles.value.map((role) => ({
    label: `${role.name}（${role.code}，${role.enabledUserCount}人）`,
    value: role.id,
  })),
);
const selectedWorkflowNode = computed(() =>
  definitionForm.nodes.find(
    (node) => node.designerNodeId === selectedDesignerNodeId.value,
  ),
);
const selectedFlowNode = computed(() =>
  flowNodes.value.find((node) => node.id === selectedDesignerNodeId.value),
);
const selectedFlowEdge = computed(() =>
  flowEdges.value.find((edge) => edge.id === selectedDesignerEdgeId.value),
);
const selectedNodeKind = computed(
  () => selectedFlowNode.value?.data?.nodeType ?? selectedFlowNode.value?.type,
);
const selectedEdgeSourceKind = computed(() => {
  const edge = selectedFlowEdge.value;
  if (!edge) {
    return '';
  }

  const sourceNode = flowNodes.value.find((node) => node.id === edge.source);
  return sourceNode?.data?.nodeType ?? sourceNode?.type ?? '';
});
const selectedWorkflowNodeHint = computed(() => {
  const node = selectedWorkflowNode.value;
  if (!node || !node.isEnabled) {
    return '';
  }

  if (!node.name.trim()) {
    return '请填写审批节点名称。';
  }

  if (node.approverType === 'User' && !node.approverUserId) {
    return '请为该节点选择审批用户。';
  }

  if (node.approverType === 'Role' && !node.approverRoleId) {
    return '请为该节点选择审批角色。';
  }

  return '';
});
const definitionStats = computed(() => ({
  enabled: definitions.value.filter((item) => item.isEnabled).length,
  total: definitionTotal.value,
}));
const workflowOptions = computed(() =>
  definitionOptions.value.map((item) => ({
    label: `${item.name} v${item.version}（${item.code}）`,
    value: item.id,
  })),
);
const currentWorkflowOption = computed(() =>
  definitionOptions.value.find((item) => item.id === startForm.definitionId),
);
const startFormSchemaFields = computed(() =>
  parseFormSchema(currentWorkflowOption.value?.formSchemaJson),
);
const hasStartFormSchema = computed(() => startFormSchemaFields.value.length > 0);
const selectedInstanceDefinition = computed(() => {
  if (!selectedInstance.value) {
    return undefined;
  }

  return (
    definitionOptions.value.find(
      (item) => item.id === selectedInstance.value?.definitionId,
    ) ??
    definitions.value.find((item) => item.id === selectedInstance.value?.definitionId)
  );
});
const selectedInstanceDefinitionSnapshot = computed(() =>
  parseDefinitionSnapshot(selectedInstance.value?.definitionSnapshotJson),
);
const selectedInstanceDefinitionLabel = computed(() =>
  selectedInstance.value ? formatInstanceDefinition(selectedInstance.value) : '',
);
const selectedInstanceFormSchemaFields = computed(() =>
  parseFormSchema(
    selectedInstanceDefinitionSnapshot.value?.formSchemaJson ??
      selectedInstanceDefinition.value?.formSchemaJson,
  ),
);
const selectedInstanceFormData = computed(() =>
  parseJsonObject(selectedInstance.value?.formDataJson),
);
const selectedInstanceReadableFormItems = computed(() =>
  selectedInstanceFormSchemaFields.value.map((field) => ({
    label: field.label || field.field,
    value: formatFormValue(selectedInstanceFormData.value[field.field]),
  })),
);
const starterGuideDefinition = computed(
  () =>
    currentWorkflowOption.value ??
    definitionOptions.value.find((item) =>
      `${item.name}${item.code}${item.formName ?? ''}`.includes('请假'),
    ) ??
    definitionOptions.value[0],
);
const isEditingDefinitionReadonly = computed(
  () =>
    !!editingDefinition.value &&
    editingDefinition.value.publishStatus !== 'Draft',
);

const enabledOptions = [
  { label: '启用', value: 'true' },
  { label: '停用', value: 'false' },
];
const statusOptions = [
  { label: '审批中', value: 'Pending' },
  { label: '已通过', value: 'Approved' },
  { label: '已驳回', value: 'Rejected' },
  { label: '已撤回', value: 'Withdrawn' },
];
const readStatusOptions = [
  { label: '未读', value: 'unread' },
  { label: '已读', value: 'read' },
];
const scopeOptions = computed(() => [
  {
    label: canManageDefinition.value ? '全部管理视图' : '我参与的',
    value: 'all',
  },
  { label: '我发起的', value: 'startedByMe' },
]);
const approverTypeOptions = [
  { label: '指定用户', value: 'User' },
  { label: '指定角色', value: 'Role' },
];
const approvalModeOptions = [
  { label: '或签（任意一人通过）', value: 'Any' },
  { label: '会签（所有人通过）', value: 'All' },
];
const formComponentOptions = [
  { label: '单行文本', value: 'text' },
  { label: '多行文本', value: 'textarea' },
  { label: '数字', value: 'number' },
  { label: '日期', value: 'date' },
  { label: '下拉选择', value: 'select' },
];
const conditionOperatorOptions = [
  { label: '等于', value: 'Equals' },
  { label: '不等于', value: 'NotEquals' },
  { label: '大于', value: 'GreaterThan' },
  { label: '大于等于', value: 'GreaterThanOrEqual' },
  { label: '小于', value: 'LessThan' },
  { label: '小于等于', value: 'LessThanOrEqual' },
  { label: '包含', value: 'Contains' },
  { label: '为空', value: 'Empty' },
  { label: '不为空', value: 'NotEmpty' },
  { label: '总是命中', value: 'Always' },
];
const definitionGuideSteps = [
  {
    description: '先生成一个可编辑的请假流程草稿。',
    title: '生成草稿',
  },
  {
    description: '确认名称、编码、表单名称和启用状态。',
    title: '填写基础信息',
  },
  {
    description: '理解开始、审批、条件、抄送、结束这些节点。',
    title: '认识画布',
  },
  {
    description: '配置审批人和 days 大于 3 的条件分支。',
    title: '配置节点',
  },
  {
    description: '保存流程定义，让它进入可发起列表。',
    title: '保存流程',
  },
  {
    description: '填入请求示例，发起一条审批测试。',
    title: '发起测试',
  },
];
const designerNodePalette = [
  {
    description: '需要人工审批，后端会生成待办任务',
    label: '审批节点',
    type: 'approve',
  },
  {
    description: '用于表达分支规则，运行时会按出口条件选择下一步',
    label: '条件节点',
    type: 'condition',
  },
  {
    description: '用于表达通知、阅知或抄送动作，不生成待办并继续流转',
    label: '抄送节点',
    type: 'cc',
  },
  {
    description: '流程终点，可用于多出口流程表达',
    label: '结束节点',
    type: 'end',
  },
] as const;

type DesignerNodeType = (typeof designerNodePalette)[number]['type'] | 'start';

function nodeTypeLabel(type?: string) {
  const map: Record<string, string> = {
    approve: '审批',
    cc: '抄送',
    condition: '条件',
    end: '结束',
    start: '开始',
  };
  return type ? (map[type] ?? type) : '-';
}

function nodeTypeDescription(type?: string) {
  const map: Record<string, string> = {
    approve: '执行节点：会写入后端审批节点并参与流转',
    cc: '执行节点：会写入抄送记录，不生成待办并继续流转',
    condition: '分支节点：运行时会按出口线条件选择下一步，未命中时走默认分支',
    end: '控制节点：表示流程结束',
    start: '控制节点：流程入口',
  };
  return type ? (map[type] ?? '自定义设计节点') : '请选择画布节点';
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function formatFileSize(size?: number) {
  if (!size || size <= 0) {
    return '0 B';
  }

  const units = ['B', 'KB', 'MB', 'GB'];
  let value = size;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }

  return `${value.toFixed(unitIndex === 0 ? 0 : 1)} ${units[unitIndex]}`;
}

function parseBooleanSelectValue(value?: string) {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return undefined;
}

function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

async function uploadFiles(files: FileList | null) {
  if (!files || files.length === 0) {
    return [];
  }

  const uploaded: FileItem[] = [];
  for (const file of Array.from(files)) {
    uploaded.push(await uploadFileApi(file));
  }

  return uploaded;
}

function normalizeSlaMinutes(value?: null | number | string) {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const minutes = Number(value);
  return Number.isFinite(minutes) && minutes > 0 ? Math.floor(minutes) : null;
}

function updateSelectedNodeSlaMinutes(value?: null | number | string) {
  const node = selectedWorkflowNode.value;
  if (!node) {
    return;
  }

  node.slaMinutes = normalizeSlaMinutes(value);
}

function updateSelectedNodeApproverUserId(value: unknown) {
  const node = selectedWorkflowNode.value;
  if (!node) {
    return;
  }

  node.approverUserId = typeof value === 'string' ? value : null;
}

function updateSelectedNodeApproverRoleId(value: unknown) {
  const node = selectedWorkflowNode.value;
  if (!node) {
    return;
  }

  node.approverRoleId = typeof value === 'string' ? value : null;
}

function isWorkflowTaskOverdue(record: Record<string, any> | WorkflowTaskItem) {
  const task = record as WorkflowTaskItem;
  if (task.isOverdue) {
    return true;
  }

  return task.status === 'Pending' && !!task.dueAt && new Date(task.dueAt).getTime() <= Date.now();
}

function deadlineText(record: Record<string, any> | WorkflowTaskItem) {
  const task = record as WorkflowTaskItem;
  if (!task.dueAt) {
    return '-';
  }

  return formatTime(task.dueAt);
}

function statusMeta(status: string) {
  const map: Record<string, { color: string; label: string }> = {
    Approved: { color: 'green', label: '已通过' },
    Closed: { color: 'default', label: '已关闭' },
    Pending: { color: 'blue', label: '审批中' },
    Rejected: { color: 'red', label: '已驳回' },
    Withdrawn: { color: 'orange', label: '已撤回' },
  };
  return map[status] ?? { color: 'default', label: status };
}

function readStatusMeta(record: { isRead?: boolean }) {
  return record.isRead
    ? { color: 'green', label: '已读' }
    : { color: 'orange', label: '未读' };
}

function countReadCcReceipts(records?: WorkflowCcRecordItem[]) {
  return records?.filter((record) => record.isRead).length ?? 0;
}

function publishStatusMeta(status: string) {
  const map: Record<string, { color: string; label: string }> = {
    Archived: { color: 'default', label: '已归档' },
    Draft: { color: 'orange', label: '草稿' },
    Published: { color: 'green', label: '已发布' },
  };

  return map[status] ?? { color: 'default', label: status || '未知' };
}

function formatInstanceDefinition(record: Record<string, any> | WorkflowInstanceItem) {
  const instance = record as WorkflowInstanceItem;
  return instance.definitionVersion > 0
    ? `${instance.definitionName} v${instance.definitionVersion}`
    : instance.definitionName;
}

function asWorkflowTask(record: Record<string, any>) {
  return record as WorkflowTaskItem;
}

function asWorkflowInstance(record: Record<string, any>) {
  return record as WorkflowInstanceItem;
}

function asWorkflowBusinessBinding(record: Record<string, any>) {
  return record as WorkflowBusinessBindingItem;
}

function actionLabel(action: string) {
  const map: Record<string, string> = {
    Approve: '同意',
    Cc: '抄送',
    Reject: '驳回',
    Remind: '催办',
    Start: '发起',
    Transfer: '转办',
    Withdraw: '撤回',
  };
  return map[action] ?? action;
}

function createFormSchemaField(): WorkflowFormSchemaField {
  return {
    component: 'text',
    field: `field_${formSchemaFields.value.length + 1}`,
    label: `字段${formSchemaFields.value.length + 1}`,
    optionsText: '',
    placeholder: '',
    required: false,
  };
}

function normalizeFormComponent(value?: string): WorkflowFormComponent {
  return value === 'date' ||
    value === 'number' ||
    value === 'select' ||
    value === 'textarea'
    ? value
    : 'text';
}

function parseFormSchema(value?: null | string): WorkflowFormSchemaField[] {
  if (!value) {
    return [];
  }

  try {
    const parsed = JSON.parse(value);
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed
      .map((item) => {
        const field = String(item?.field ?? '').trim();
        const label = String(item?.label ?? field).trim();
        const component = normalizeFormComponent(item?.component);
        const options = Array.isArray(item?.options)
          ? item.options
              .map((option: any) => ({
                label: String(option?.label ?? option?.value ?? '').trim(),
                value: String(option?.value ?? option?.label ?? '').trim(),
              }))
              .filter((option: WorkflowFormSchemaOption) => option.value)
          : [];

        return {
          component,
          defaultValue:
            item?.defaultValue === undefined || item?.defaultValue === null
              ? ''
              : String(item.defaultValue),
          field,
          label: label || field,
          options,
          optionsText: optionsToText(options),
          placeholder:
            item?.placeholder === undefined || item?.placeholder === null
              ? ''
              : String(item.placeholder),
          required: !!item?.required,
        } satisfies WorkflowFormSchemaField;
      })
      .filter((field) => field.field && field.label);
  } catch {
    return [];
  }
}

function parseDefinitionSnapshot(value?: null | string): WorkflowDefinitionSnapshot {
  if (!value) {
    return {};
  }

  try {
    const parsed = JSON.parse(value);
    if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return {};
    }

    return {
      formSchemaJson:
        typeof parsed.formSchemaJson === 'string'
          ? parsed.formSchemaJson
          : undefined,
    };
  } catch {
    return {};
  }
}

function parseJsonObject(value?: null | string) {
  if (!value) {
    return {};
  }

  try {
    const parsed = JSON.parse(value);
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? parsed
      : {};
  } catch {
    return {};
  }
}

function formatFormValue(value: unknown) {
  if (value === undefined || value === null || value === '') {
    return '-';
  }

  if (typeof value === 'object') {
    return JSON.stringify(value);
  }

  return String(value);
}

function optionsToText(options?: WorkflowFormSchemaOption[]) {
  return (options ?? [])
    .map((option) =>
      option.label && option.label !== option.value
        ? `${option.value}=${option.label}`
        : option.value,
    )
    .join('\n');
}

function parseOptionsText(value?: string): WorkflowFormSchemaOption[] {
  return (value ?? '')
    .split(/\r?\n|,/)
    .map((item) => item.trim())
    .filter(Boolean)
    .map((item) => {
      const separatorIndex = ['=', ':', '：']
        .map((separator) => item.indexOf(separator))
        .filter((index) => index > -1)
        .sort((left, right) => left - right)[0];
      if (separatorIndex === undefined) {
        return { label: item, value: item };
      }

      const valueText = item.slice(0, separatorIndex).trim();
      const labelText = item.slice(separatorIndex + 1).trim();
      return {
        label: labelText || valueText,
        value: valueText || labelText,
      };
    })
    .filter((option) => option.value);
}

function addFormSchemaField() {
  formSchemaFields.value.push(createFormSchemaField());
}

function removeFormSchemaField(index: number) {
  formSchemaFields.value.splice(index, 1);
}

function serializeFormSchemaFields() {
  const fields = formSchemaFields.value.map((field) => {
    const options =
      field.component === 'select' ? parseOptionsText(field.optionsText) : [];
    return {
      component: field.component,
      defaultValue: field.defaultValue || undefined,
      field: field.field.trim(),
      label: field.label.trim(),
      options: field.component === 'select' ? options : undefined,
      placeholder: field.placeholder || undefined,
      required: field.required,
    };
  });

  return JSON.stringify(fields, null, 2);
}

function validateFormSchemaFields() {
  const fieldCodes = new Set<string>();
  for (const field of formSchemaFields.value) {
    const fieldCode = field.field.trim();
    if (!fieldCode || !field.label.trim()) {
      showDefinitionValidation('表单字段需要填写字段标签和字段编码。');
      return false;
    }

    if (!/^[A-Za-z_][\w]*$/.test(fieldCode)) {
      showDefinitionValidation(
        `表单字段编码「${fieldCode}」只能使用英文字母、数字和下划线，且不能以数字开头。`,
      );
      return false;
    }

    const lowerCode = fieldCode.toLowerCase();
    if (fieldCodes.has(lowerCode)) {
      showDefinitionValidation(`表单字段编码「${fieldCode}」重复。`);
      return false;
    }

    fieldCodes.add(lowerCode);

    if (field.component === 'select' && parseOptionsText(field.optionsText).length === 0) {
      showDefinitionValidation(`下拉字段「${field.label || fieldCode}」至少需要一个选项。`);
      return false;
    }
  }

  return true;
}

function buildStartFormData() {
  const result: Record<string, unknown> = {};
  startFormSchemaFields.value.forEach((field) => {
    const value = startFormData[field.field];
    if (field.component === 'number' && value !== '' && value !== undefined && value !== null) {
      result[field.field] = Number(value);
      return;
    }

    result[field.field] = value ?? '';
  });

  return result;
}

function syncStartJsonFromDynamicForm() {
  if (!hasStartFormSchema.value) {
    return;
  }

  startForm.formDataJson = JSON.stringify(buildStartFormData(), null, 2);
}

function resetStartFormDataFromSchema() {
  const schemaFields = startFormSchemaFields.value;
  Object.keys(startFormData).forEach((key) => {
    delete startFormData[key];
  });

  if (schemaFields.length === 0) {
    return;
  }

  schemaFields.forEach((field) => {
    startFormData[field.field] = field.defaultValue ?? '';
  });
  syncStartJsonFromDynamicForm();
}

function validateStartDynamicForm() {
  for (const field of startFormSchemaFields.value) {
    const value = startFormData[field.field];
    const empty = value === undefined || value === null || String(value).trim() === '';
    if (field.required && empty) {
      message.warning(`请填写${field.label}`);
      return false;
    }

    if (!empty && field.component === 'number' && Number.isNaN(Number(value))) {
      message.warning(`${field.label}必须是数字`);
      return false;
    }

    if (
      !empty &&
      field.component === 'select' &&
      field.options?.length &&
      !field.options.some((option) => option.value === value)
    ) {
      message.warning(`${field.label}必须选择有效选项`);
      return false;
    }
  }

  return true;
}

function resetDefinitionForm() {
  editingDefinition.value = undefined;
  selectedDefinitionId.value = '';
  definitionForm.code = '';
  definitionForm.name = '';
  definitionForm.formName = '';
  definitionForm.description = '';
  definitionForm.isEnabled = true;
  definitionForm.nodes = [createEmptyNode()];
  formSchemaFields.value = [];
  buildLinearDesigner();
}

function createEmptyNode(nodeType = 'approve'): SaveWorkflowNodeRequest {
  const designerNodeId = `${nodeType}-${Date.now()}-${Math.random()
    .toString(16)
    .slice(2)}`;
  return {
    approvalMode: 'Any',
    approverRoleId: null,
    approverType: 'User',
    approverUserId: null,
    designerNodeId,
    isEnabled: true,
    name: '',
    nodeType,
    order: definitionForm.nodes.length + 1,
    slaMinutes: null,
  };
}

function openCreateDefinition() {
  activeTab.value = 'definitions';
  resetDefinitionForm();
}

function openEditDefinition(record: WorkflowDefinitionItem) {
  editingDefinition.value = record;
  selectedDefinitionId.value = record.id;
  definitionForm.code = record.code;
  definitionForm.name = record.name;
  definitionForm.formName = record.formName ?? '';
  definitionForm.description = record.description ?? '';
  definitionForm.isEnabled = record.isEnabled;
  formSchemaFields.value = parseFormSchema(record.formSchemaJson);
  definitionForm.nodes = record.nodes.map((node) => ({
    approverRoleId: node.approverRoleId ?? null,
    approvalMode: node.approvalMode ?? 'Any',
    approverType: node.approverType,
    approverUserId: node.approverUserId ?? null,
    designerNodeId: node.designerNodeId,
    isEnabled: node.isEnabled,
    name: node.name,
    nodeType: node.nodeType ?? 'approve',
    order: node.order,
    slaMinutes: node.slaMinutes ?? null,
  }));
  if (definitionForm.nodes.length === 0) {
    definitionForm.nodes = [createEmptyNode()];
  }
  loadDesigner(record.designerJson);
}

function selectDefinition(record: WorkflowDefinitionItem) {
  openEditDefinition(record);
}

function removeNode(index: number) {
  if (index < 0) {
    return;
  }

  const removedNodeId = definitionForm.nodes[index]?.designerNodeId;
  definitionForm.nodes.splice(index, 1);
  definitionForm.nodes.forEach((node, nodeIndex) => {
    node.order = nodeIndex + 1;
  });
  if (removedNodeId) {
    flowNodes.value = flowNodes.value.filter((node) => node.id !== removedNodeId);
    flowEdges.value = flowEdges.value.filter(
      (edge) => edge.source !== removedNodeId && edge.target !== removedNodeId,
    );
  }
  selectedDesignerNodeId.value =
    definitionForm.nodes[0]?.designerNodeId ?? 'start';
  selectedDesignerEdgeId.value = '';
}

function removeSelectedDesignNode() {
  const node = selectedFlowNode.value;
  if (!node || node.id === 'start') {
    return;
  }

  if (selectedWorkflowNode.value) {
    removeNode(
      definitionForm.nodes.findIndex(
        (item) => item.designerNodeId === selectedWorkflowNode.value?.designerNodeId,
      ),
    );
    return;
  }

  flowNodes.value = flowNodes.value.filter((item) => item.id !== node.id);
  flowEdges.value = flowEdges.value.filter(
    (edge) => edge.source !== node.id && edge.target !== node.id,
  );
  selectedDesignerNodeId.value = 'start';
  selectedDesignerEdgeId.value = '';
}

function removeSelectedEdge() {
  const edge = selectedFlowEdge.value;
  if (!edge) {
    return;
  }

  flowEdges.value = flowEdges.value.filter((item) => item.id !== edge.id);
  selectedDesignerEdgeId.value = '';
}

function addDesignerNode(type: DesignerNodeType) {
  const selectedNode = selectedFlowNode.value;
  const sourceNode =
    selectedNode && selectedNode.id !== 'end'
      ? selectedNode
      : flowNodes.value.find((node) => node.id === 'start');
  const firstOutgoingEdge = sourceNode
    ? flowEdges.value.find((edge) => edge.source === sourceNode.id)
    : undefined;
  const firstTargetNode = firstOutgoingEdge
    ? flowNodes.value.find((node) => node.id === firstOutgoingEdge.target)
    : undefined;
  const count = flowNodes.value.filter(
    (node) => (node.data?.nodeType ?? node.type) === type,
  ).length;
  const id = `${type}-${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const labelMap: Record<DesignerNodeType, string> = {
    approve: `审批节点${definitionForm.nodes.length + 1}`,
    cc: `抄送节点${count + 1}`,
    condition: `条件分支${count + 1}`,
    end: count === 0 ? '结束' : `结束${count + 1}`,
    start: '开始',
  };
  const position = {
    x:
      sourceNode?.position?.x === undefined
        ? 260 + flowNodes.value.length * 40
        : sourceNode.position.x + 220,
    y:
      sourceNode?.position?.y === undefined
        ? 120 + (flowNodes.value.length % 4) * 72
        : sourceNode.position.y +
          (type === 'condition' ? -90 : type === 'cc' ? 90 : 0),
  };

  if (type === 'approve' || type === 'cc') {
    const node = createEmptyNode(type);
    node.designerNodeId = id;
    node.name = labelMap[type];
    node.order = definitionForm.nodes.length + 1;
    definitionForm.nodes.push(node);
  }

  flowNodes.value = [
    ...flowNodes.value,
    {
      id,
      data: {
        label: labelMap[type],
        nodeType: type,
      },
      position,
      type,
    },
  ];

  if (sourceNode && sourceNode.id !== id) {
    flowEdges.value = [
      ...flowEdges.value.filter((edge) => edge.id !== firstOutgoingEdge?.id),
      createFlowEdge(sourceNode.id, id),
      ...(firstTargetNode && type !== 'end'
        ? [createFlowEdge(id, firstTargetNode.id)]
        : []),
    ];
  }

  selectedDesignerNodeId.value = id ?? '';
  selectedDesignerEdgeId.value = '';
}

function buildLinearDesigner() {
  const executionNodes = definitionForm.nodes
    .filter((node) => node.isEnabled)
    .sort((left, right) => left.order - right.order);
  flowNodes.value = [
    {
      id: 'start',
      data: { label: '开始', nodeType: 'start' },
      position: { x: 60, y: 150 },
      type: 'start',
    },
    ...executionNodes.map((node, index) => ({
      id: node.designerNodeId,
      data: {
        label: node.name || `${node.nodeType === 'cc' ? '抄送' : '审批'}节点${index + 1}`,
        nodeType: node.nodeType ?? 'approve',
      },
      position: { x: 260 + index * 240, y: 150 },
      type: node.nodeType ?? 'approve',
    })),
    {
      id: 'end',
      data: { label: '结束', nodeType: 'end' },
      position: { x: 300 + executionNodes.length * 240, y: 150 },
      type: 'end',
    },
  ];

  const sortedNodeIds = ['start', ...executionNodes.map((node) => node.designerNodeId), 'end'];
  flowEdges.value = sortedNodeIds.slice(0, -1).map((source, index) => {
    const target = sortedNodeIds[index + 1];
    return createFlowEdge(source ?? '', target ?? '');
  });
  selectedDesignerNodeId.value = executionNodes[0]?.designerNodeId ?? '';
}

function loadDesigner(designerJson?: null | string) {
  if (!designerJson) {
    buildLinearDesigner();
    return;
  }

  try {
    const graph = JSON.parse(designerJson) as {
      edges?: Array<{
        conditionField?: null | string;
        conditionOperator?: null | string;
        conditionValue?: null | string;
        id?: string;
        isDefault?: boolean;
        label?: null | string;
        sort?: number;
        source: string;
        target: string;
      }>;
      nodes?: Array<{
        id: string;
        label?: string;
        position?: { x: number; y: number };
        type?: string;
        x?: number;
        y?: number;
      }>;
    };
    flowNodes.value = (graph.nodes ?? []).map((node) => ({
      id: node.id,
      data: {
        label:
          node.label ||
          definitionForm.nodes.find((item) => item.designerNodeId === node.id)
            ?.name ||
          node.id,
        nodeType: normalizeDesignerNodeType(node.type),
      },
      position: node.position ?? { x: node.x ?? 120, y: node.y ?? 120 },
      type: normalizeDesignerNodeType(node.type),
    }));
    flowEdges.value = (graph.edges ?? []).map((edge) =>
      createFlowEdge(edge.source, edge.target, edge.id, {
        conditionField: edge.conditionField ?? '',
        conditionOperator: edge.conditionOperator ?? 'Equals',
        conditionValue: edge.conditionValue ?? '',
        isDefault: edge.isDefault ?? false,
        label: edge.label ?? '',
        sort: edge.sort ?? 0,
      }),
    );
    selectedDesignerNodeId.value =
      definitionForm.nodes[0]?.designerNodeId ?? '';
  } catch {
    buildLinearDesigner();
  }
}

function normalizeDesignerNodeType(value?: string): DesignerNodeType {
  return value === 'start' ||
    value === 'condition' ||
    value === 'cc' ||
    value === 'end'
    ? value
    : 'approve';
}

function createFlowEdge(
  source: string,
  target: string,
  id?: string,
  data?: Record<string, any>,
) {
  const edgeData = {
    conditionField: data?.conditionField ?? '',
    conditionOperator: data?.conditionOperator ?? 'Equals',
    conditionValue: data?.conditionValue ?? '',
    isDefault: data?.isDefault ?? false,
    label: data?.label ?? '',
    sort: data?.sort ?? 0,
  };

  return {
    data: edgeData,
    id: id || `edge-${source}-${target}`,
    interactionWidth: 24,
    label: edgeData.label,
    markerEnd: MarkerType.ArrowClosed,
    selectable: true,
    source,
    style: {
      strokeWidth: 2,
    },
    target,
    type: 'smoothstep',
  };
}

function handleConnect(connection: any) {
  if (!connection.source || !connection.target) {
    return;
  }

  flowEdges.value = [
    ...flowEdges.value.filter(
      (edge) => edge.source !== connection.source || edge.target !== connection.target,
    ),
    createFlowEdge(connection.source, connection.target),
  ];
}

function handleNodeClick(event: any) {
  const nodeId = event?.node?.id;
  if (nodeId) {
    selectedDesignerNodeId.value = nodeId;
    selectedDesignerEdgeId.value = '';
  }
}

function handleEdgeClick(event: any) {
  const edgeId = event?.edge?.id;
  if (edgeId) {
    selectedDesignerEdgeId.value = edgeId;
    selectedDesignerNodeId.value = '';
  }
}

function updateSelectedNodeLabel() {
  const selectedNode = selectedWorkflowNode.value;
  if (!selectedNode) {
    return;
  }

  flowNodes.value = flowNodes.value.map((node) =>
    node.id === selectedNode.designerNodeId
      ? {
          ...node,
          data: {
            ...node.data,
            label: selectedNode.name || '审批节点',
          },
        }
      : node,
  );
}

function updateSelectedDesignNodeLabel() {
  if (!selectedFlowNode.value || selectedWorkflowNode.value) {
    return;
  }

  flowNodes.value = flowNodes.value.map((node) =>
    node.id === selectedFlowNode.value?.id
      ? {
          ...node,
          data: {
            ...node.data,
            label: node.data?.label || nodeTypeLabel(node.data?.nodeType),
          },
        }
      : node,
  );
}

function updateSelectedEdgeLabel() {
  const edge = selectedFlowEdge.value;
  if (!edge) {
    return;
  }

  flowEdges.value = flowEdges.value.map((item) =>
    item.id === edge.id
      ? {
          ...item,
          label: edge.data?.label || undefined,
        }
      : item,
  );
}

function showDefinitionValidation(messageText: string, designerNodeId?: null | string) {
  if (designerNodeId) {
    selectedDesignerNodeId.value = designerNodeId;
    selectedDesignerEdgeId.value = '';
  }

  Modal.warning({
    content: messageText,
    okText: '去完善',
    title: '流程配置未完成',
  });
}

function showEdgeValidation(messageText: string, edgeId: string) {
  selectedDesignerEdgeId.value = edgeId;
  selectedDesignerNodeId.value = '';
  Modal.warning({
    content: messageText,
    okText: '去完善',
    title: '条件分支未完成',
  });
}

function getDesignerNodeKind(node?: any) {
  return node?.data?.nodeType ?? node?.type ?? '';
}

function getDesignerNodeLabel(node?: any) {
  return node?.data?.label ?? node?.label ?? node?.id ?? '未命名节点';
}

function findDesignerNode(nodeId?: null | string) {
  return nodeId ? flowNodes.value.find((node) => node.id === nodeId) : undefined;
}

function getOutgoingEdges(sourceNodeId: string) {
  return flowEdges.value.filter((edge) => edge.source === sourceNodeId);
}

function isInvalidConditionEdge(edge: any) {
  if (edge.data?.isDefault) {
    return false;
  }

  const conditionOperator = edge.data?.conditionOperator || 'Equals';
  if (conditionOperator === 'Always') {
    return false;
  }

  if (!edge.data?.conditionField?.trim()) {
    return true;
  }

  return !['Empty', 'NotEmpty'].includes(conditionOperator) &&
    !edge.data?.conditionValue?.trim();
}

function resolveReachableDesignerNodeIds(startNodeId: string) {
  const reachableNodeIds = new Set<string>();
  const pendingNodeIds = [startNodeId];

  while (pendingNodeIds.length > 0) {
    const nodeId = pendingNodeIds.shift();
    if (!nodeId || reachableNodeIds.has(nodeId)) {
      continue;
    }

    reachableNodeIds.add(nodeId);
    getOutgoingEdges(nodeId).forEach((edge) => {
      if (edge.target && !reachableNodeIds.has(edge.target)) {
        pendingNodeIds.push(edge.target);
      }
    });
  }

  return reachableNodeIds;
}

function validateDefinitionBeforeSave() {
  if (!definitionForm.name.trim()) {
    showDefinitionValidation('请先填写流程名称。');
    return false;
  }

  if (!definitionForm.code.trim()) {
    showDefinitionValidation('请先填写流程编码，例如 leave_apply。');
    return false;
  }

  if (!validateFormSchemaFields()) {
    return false;
  }

  const enabledNodes = definitionForm.nodes.filter((node) => node.isEnabled);
  if (enabledNodes.length === 0) {
    showDefinitionValidation('请至少保留一个启用的审批或抄送节点。');
    return false;
  }

  const invalidNode = enabledNodes.find((node) => {
    if (!node.name.trim()) {
      return true;
    }

    if (node.approverType === 'User') {
      return !node.approverUserId;
    }

    if (node.approverType === 'Role') {
      return !node.approverRoleId;
    }

    return true;
  });

  if (invalidNode) {
    const missing =
      !invalidNode.name.trim()
        ? '节点名称'
        : invalidNode.approverType === 'Role'
          ? '处理角色'
          : '处理用户';
    showDefinitionValidation(
      `节点「${invalidNode.name || '未命名节点'}」还没有完成配置，请补充${missing}后再保存。`,
      invalidNode.designerNodeId,
    );
    return false;
  }

  if (flowEdges.value.length === 0) {
    return true;
  }

  const danglingEdge = flowEdges.value.find(
    (edge) => !findDesignerNode(edge.source) || !findDesignerNode(edge.target),
  );
  if (danglingEdge) {
    showEdgeValidation('流程画布存在指向不存在节点的连线，请删除后重新连接。', danglingEdge.id);
    return false;
  }

  const startNode = flowNodes.value.find(
    (node) => node.id === 'start' || getDesignerNodeKind(node) === 'start',
  );
  if (!startNode) {
    showDefinitionValidation('流程画布缺少开始节点，请重新整理画布。');
    return false;
  }

  if (getOutgoingEdges(startNode.id).length === 0) {
    showDefinitionValidation('开始节点必须至少连接一个出口。', startNode.id);
    return false;
  }

  const conditionNodes = flowNodes.value.filter(
    (node) => getDesignerNodeKind(node) === 'condition',
  );
  for (const conditionNode of conditionNodes) {
    const outgoingEdges = getOutgoingEdges(conditionNode.id);
    if (outgoingEdges.length === 0) {
      showDefinitionValidation(
        `条件节点「${getDesignerNodeLabel(conditionNode)}」必须至少连接一个出口。`,
        conditionNode.id,
      );
      return false;
    }

    if (!outgoingEdges.some((edge) => edge.data?.isDefault)) {
      showDefinitionValidation(
        `条件节点「${getDesignerNodeLabel(conditionNode)}」必须配置默认分支，用来处理未命中的情况。`,
        conditionNode.id,
      );
      return false;
    }

    const invalidConditionEdge = outgoingEdges.find(isInvalidConditionEdge);
    if (invalidConditionEdge) {
      showEdgeValidation(
        '条件节点的出口线还没有完成判断规则，请填写字段路径、运算符和比较值，或者把它设为默认分支。',
        invalidConditionEdge.id,
      );
      return false;
    }
  }

  const reachableNodeIds = resolveReachableDesignerNodeIds(startNode.id);
  const unreachableNode = enabledNodes.find(
    (node) => !node.designerNodeId || !reachableNodeIds.has(node.designerNodeId),
  );
  if (unreachableNode) {
    showDefinitionValidation(
      `节点「${unreachableNode.name || '未命名节点'}」在画布中不可达，请从开始节点补齐连线。`,
      unreachableNode.designerNodeId,
    );
    return false;
  }

  return true;
}

function buildDesignerJson() {
  return JSON.stringify({
    edges: flowEdges.value.map((edge) => ({
      conditionField: edge.data?.conditionField || null,
      conditionOperator: edge.data?.conditionOperator || null,
      conditionValue: edge.data?.conditionValue || null,
      id: edge.id,
      isDefault: edge.data?.isDefault ?? false,
      label: edge.label || edge.data?.label || null,
      sort: edge.data?.sort ?? 0,
      source: edge.source,
      target: edge.target,
    })),
    nodes: flowNodes.value.map((node) => ({
      id: node.id,
      label: node.data?.label ?? node.id,
      type: node.data?.nodeType ?? 'approve',
      x: node.position?.x ?? 0,
      y: node.position?.y ?? 0,
    })),
  });
}

async function loadBaseOptions() {
  const [userResult, roleResult] = await Promise.all([
    getWorkflowApproverUsersApi(),
    getWorkflowApproverRolesApi(),
  ]);
  users.value = userResult;
  roles.value = roleResult;
}

async function loadDefinitions() {
  definitionLoading.value = true;
  try {
    const result = await getWorkflowDefinitionsApi({
      isEnabled: parseBooleanSelectValue(definitionQuery.isEnabled),
      keyword: definitionQuery.keyword || undefined,
      page: definitionQuery.page,
      pageSize: definitionQuery.pageSize,
    });
    definitions.value = result.items;
    definitionTotal.value = result.total;
    definitionOptions.value = await getWorkflowDefinitionOptionsApi();
    const firstDefinition = result.items[0];
    if (!selectedDefinitionId.value && firstDefinition) {
      openEditDefinition(firstDefinition);
    }
  } finally {
    definitionLoading.value = false;
  }
}

async function loadBusinessBindings() {
  bindingLoading.value = true;
  try {
    const result = await getWorkflowBusinessBindingsApi({
      isEnabled: parseBooleanSelectValue(bindingQuery.isEnabled),
      keyword: bindingQuery.keyword || undefined,
      page: bindingQuery.page,
      pageSize: bindingQuery.pageSize,
    });
    businessBindings.value = result.items;
    bindingTotal.value = result.total;
  } finally {
    bindingLoading.value = false;
  }
}

async function loadInstances() {
  instanceLoading.value = true;
  try {
    const [allResult, startedResult, ccResult] = await Promise.all([
      getWorkflowInstancesApi({
        keyword: instanceQuery.keyword || undefined,
        page: instanceQuery.page,
        pageSize: instanceQuery.pageSize,
        scope: instanceQuery.scope,
        status: instanceQuery.status,
      }),
      getWorkflowStartedByMeApi({
        keyword: startedQuery.keyword || undefined,
        page: startedQuery.page,
        pageSize: startedQuery.pageSize,
        status: startedQuery.status,
      }),
      getWorkflowCcInstancesApi({
        keyword: ccQuery.keyword || undefined,
        page: ccQuery.page,
        pageSize: ccQuery.pageSize,
        readStatus: ccQuery.readStatus,
        status: ccQuery.status,
      }),
    ]);
    instances.value = allResult.items;
    instanceTotal.value = allResult.total;
    startedInstances.value = startedResult.items;
    startedTotal.value = startedResult.total;
    ccRecords.value = ccResult.items;
    ccTotal.value = ccResult.total;
  } finally {
    instanceLoading.value = false;
  }
}

async function loadTasks() {
  taskLoading.value = true;
  try {
    const [todo, done] = await Promise.all([
      getWorkflowTodoTasksApi(),
      getWorkflowDoneTasksApi(),
    ]);
    todoTasks.value = todo;
    doneTasks.value = done;
  } finally {
    taskLoading.value = false;
  }
}

async function refreshAll() {
  await Promise.all([
    loadDefinitions(),
    loadBusinessBindings(),
    loadInstances(),
    loadTasks(),
  ]);
}

function searchDefinitions() {
  definitionQuery.keyword = definitionQuery.keyword.trim();
  definitionQuery.page = 1;
  void loadDefinitions();
}

function searchInstances() {
  instanceQuery.keyword = instanceQuery.keyword.trim();
  instanceQuery.page = 1;
  void loadInstances();
}

function searchStartedInstances() {
  startedQuery.keyword = startedQuery.keyword.trim();
  startedQuery.page = 1;
  void loadInstances();
}

function searchCcInstances() {
  ccQuery.keyword = ccQuery.keyword.trim();
  ccQuery.page = 1;
  void loadInstances();
}

function syncCcRecordInList(record: WorkflowCcRecordItem) {
  if (ccQuery.readStatus === 'unread' && record.isRead) {
    ccRecords.value = ccRecords.value.filter((item) => item.id !== record.id);
    ccTotal.value = Math.max(ccTotal.value - 1, 0);
    return;
  }

  ccRecords.value = ccRecords.value.map((item) =>
    item.id === record.id ? record : item,
  );
}

async function markWorkflowCcRead(
  ccRecordId: string,
  options: { silent?: boolean } = {},
) {
  if (!ccRecordId) {
    return;
  }

  const currentRecord = ccRecords.value.find((item) => item.id === ccRecordId);
  if (currentRecord?.isRead) {
    return;
  }

  ccReadSavingId.value = ccRecordId;
  try {
    const record = await markWorkflowCcRecordReadApi(ccRecordId);
    syncCcRecordInList(record);
    if (!options.silent) {
      message.success('已标记为已读');
    }
  } finally {
    ccReadSavingId.value = '';
  }
}

function openStartGuide() {
  startGuideOpen.value = true;
}

function openDefinitionGuide() {
  if (!canManageDefinition.value) {
    message.warning('当前账号没有流程定义管理权限，无法创建示例流程');
    return;
  }

  activeTab.value = 'definitions';
  definitionGuideStep.value = 0;
  definitionGuideOpen.value = true;
}

function getAvailableDefinitionCode(baseCode: string) {
  const codes = new Set(definitions.value.map((item) => item.code));
  if (!codes.has(baseCode)) {
    return baseCode;
  }

  let index = 2;
  while (codes.has(`${baseCode}_${index}`)) {
    index += 1;
  }

  return `${baseCode}_${index}`;
}

function getGuideApproverUserId(index: number) {
  return users.value[index]?.id ?? users.value[0]?.id ?? null;
}

function applyLeaveDefinitionExample() {
  const stamp = Date.now();
  const managerNodeId = `approve-manager-${stamp}`;
  const directorNodeId = `approve-director-${stamp}`;
  const conditionNodeId = `condition-days-${stamp}`;
  const ccNodeId = `cc-hr-${stamp}`;

  editingDefinition.value = undefined;
  selectedDefinitionId.value = '';
  definitionGuideSavedDefinitionId.value = '';
  definitionForm.name = '请假审批示例';
  definitionForm.code = getAvailableDefinitionCode('leave_apply_demo');
  definitionForm.formName = '请假申请单';
  definitionForm.description =
    '示例流程：员工提交请假申请，主管先审批；请假天数大于 3 天时进入部门负责人审批，并抄送人事。';
  definitionForm.isEnabled = true;
  formSchemaFields.value = [
    {
      component: 'number',
      defaultValue: '5',
      field: 'days',
      label: '请假天数',
      placeholder: '例如 5',
      required: true,
    },
    {
      component: 'select',
      defaultValue: '事假',
      field: 'type',
      label: '请假类型',
      optionsText: '事假\n年假\n病假',
      required: true,
    },
    {
      component: 'textarea',
      defaultValue: '家中有事，需要请假',
      field: 'reason',
      label: '请假原因',
      placeholder: '说明请假原因',
      required: true,
    },
  ];
  definitionForm.nodes = [
    {
      approverRoleId: null,
      approvalMode: 'Any',
      approverType: 'User',
      approverUserId: getGuideApproverUserId(0),
      designerNodeId: managerNodeId,
      isEnabled: true,
      name: '直属主管审批',
      nodeType: 'approve',
      order: 1,
      slaMinutes: 1440,
    },
    {
      approverRoleId: null,
      approvalMode: 'All',
      approverType: 'User',
      approverUserId: getGuideApproverUserId(1),
      designerNodeId: directorNodeId,
      isEnabled: true,
      name: '部门负责人审批',
      nodeType: 'approve',
      order: 2,
      slaMinutes: 2880,
    },
    {
      approverRoleId: null,
      approvalMode: 'Any',
      approverType: 'User',
      approverUserId: getGuideApproverUserId(2),
      designerNodeId: ccNodeId,
      isEnabled: true,
      name: '抄送人事',
      nodeType: 'cc',
      order: 3,
      slaMinutes: null,
    },
  ];

  flowNodes.value = [
    {
      id: 'start',
      data: { label: '开始', nodeType: 'start' },
      position: { x: 60, y: 170 },
      type: 'start',
    },
    {
      id: managerNodeId,
      data: { label: '直属主管审批', nodeType: 'approve' },
      position: { x: 280, y: 170 },
      type: 'approve',
    },
    {
      id: conditionNodeId,
      data: { label: '请假天数判断', nodeType: 'condition' },
      position: { x: 520, y: 170 },
      type: 'condition',
    },
    {
      id: directorNodeId,
      data: { label: '部门负责人审批', nodeType: 'approve' },
      position: { x: 760, y: 80 },
      type: 'approve',
    },
    {
      id: ccNodeId,
      data: { label: '抄送人事', nodeType: 'cc' },
      position: { x: 1000, y: 80 },
      type: 'cc',
    },
    {
      id: 'end',
      data: { label: '结束', nodeType: 'end' },
      position: { x: 1240, y: 170 },
      type: 'end',
    },
  ];
  flowEdges.value = [
    createFlowEdge('start', managerNodeId),
    createFlowEdge(managerNodeId, conditionNodeId),
    createFlowEdge(conditionNodeId, directorNodeId, undefined, {
      conditionField: 'days',
      conditionOperator: 'GreaterThan',
      conditionValue: '3',
      isDefault: false,
      label: '大于3天',
      sort: 1,
    }),
    createFlowEdge(directorNodeId, ccNodeId),
    createFlowEdge(ccNodeId, 'end'),
    createFlowEdge(conditionNodeId, 'end', undefined, {
      isDefault: true,
      label: '3天以内',
      sort: 2,
    }),
  ];
  selectedDesignerNodeId.value = managerNodeId;
  selectedDesignerEdgeId.value = '';
  definitionGuideStep.value = 1;
  message.success('已生成请假流程草稿，请按引导继续完善');
}

function setDefinitionGuideStep(step: number) {
  if (step > 0 && !definitionForm.code.trim()) {
    message.info('请先生成请假流程草稿，再继续下一步');
    definitionGuideStep.value = 0;
    return;
  }

  if (step === 5 && !definitionGuideSavedDefinitionId.value && !editingDefinition.value) {
    message.info('请先保存流程定义，再发起测试');
    definitionGuideStep.value = 4;
    return;
  }

  definitionGuideStep.value = step;
  activeTab.value = 'definitions';

  if (step === 2) {
    selectedDesignerNodeId.value =
      flowNodes.value.find((node) => node.data?.nodeType === 'condition')?.id ??
      selectedDesignerNodeId.value;
    selectedDesignerEdgeId.value = '';
  }

  if (step === 3) {
    const conditionEdge = flowEdges.value.find(
      (edge) => edge.data?.conditionField === 'days',
    );
    selectedDesignerEdgeId.value = conditionEdge?.id ?? '';
    selectedDesignerNodeId.value = conditionEdge ? '' : selectedDesignerNodeId.value;
  }
}

async function saveDefinitionFromGuide() {
  const saved = await submitDefinition();
  if (!saved) {
    return;
  }

  if (editingDefinition.value?.publishStatus === 'Draft') {
    await publishDefinition();
  }

  definitionGuideSavedDefinitionId.value =
    selectedDefinitionId.value || editingDefinition.value?.id || '';
  definitionGuideStep.value = 5;
}

function applyLeaveStartExample() {
  const definition = starterGuideDefinition.value;
  if (definition) {
    startForm.definitionId = definition.id;
  }

  startForm.businessKey = `LEAVE-${new Date()
    .toISOString()
    .slice(0, 10)
    .replaceAll('-', '')}-001`;
  startForm.title = '请假申请';
  resetStartFormDataFromSchema();
  if (hasStartFormSchema.value) {
    startFormData.days = 5;
    startFormData.reason = '家中有事，需要请假';
    startFormData.type = '事假';
    syncStartJsonFromDynamicForm();
  } else {
    startForm.formDataJson = JSON.stringify(
      {
        days: 5,
        reason: '家中有事，需要请假',
        type: '事假',
      },
      null,
      2,
    );
  }
  message.success('已填入请假审批示例');
}

function goWorkflowDefinitionGuide() {
  startGuideOpen.value = false;
  activeTab.value = 'definitions';
  openDefinitionGuide();
}

function goStartWithDefinitionExample() {
  const savedId =
    definitionGuideSavedDefinitionId.value ||
    selectedDefinitionId.value ||
    editingDefinition.value?.id ||
    '';
  if (savedId) {
    startForm.definitionId = savedId;
  }

  definitionGuideOpen.value = false;
  activeTab.value = 'start';
  applyLeaveStartExample();
}

function handleBindingTableChange(pagination: TablePaginationConfig) {
  bindingQuery.page = pagination.current ?? 1;
  bindingQuery.pageSize = pagination.pageSize ?? 10;
  void loadBusinessBindings();
}

function handleInstanceTableChange(pagination: TablePaginationConfig) {
  instanceQuery.page = pagination.current ?? 1;
  instanceQuery.pageSize = pagination.pageSize ?? 10;
  void loadInstances();
}

function handleStartedTableChange(pagination: TablePaginationConfig) {
  startedQuery.page = pagination.current ?? 1;
  startedQuery.pageSize = pagination.pageSize ?? 10;
  void loadInstances();
}

function handleCcTableChange(pagination: TablePaginationConfig) {
  ccQuery.page = pagination.current ?? 1;
  ccQuery.pageSize = pagination.pageSize ?? 10;
  void loadInstances();
}

function searchBusinessBindings() {
  bindingQuery.page = 1;
  void loadBusinessBindings();
}

function resetBindingForm() {
  editingBinding.value = undefined;
  bindingForm.businessType = '';
  bindingForm.businessName = '';
  bindingForm.definitionId = definitionOptions.value[0]?.id ?? '';
  bindingForm.isEnabled = true;
  bindingForm.remark = '';
}

function openCreateBinding() {
  resetBindingForm();
  bindingModalOpen.value = true;
}

function openEditBinding(record: Record<string, any> | WorkflowBusinessBindingItem) {
  const binding = asWorkflowBusinessBinding(record);
  editingBinding.value = binding;
  bindingForm.businessType = binding.businessType;
  bindingForm.businessName = binding.businessName;
  bindingForm.definitionId = binding.definitionId;
  bindingForm.isEnabled = binding.isEnabled;
  bindingForm.remark = binding.remark ?? '';
  bindingModalOpen.value = true;
}

async function submitBusinessBinding() {
  if (!bindingForm.businessType.trim()) {
    message.warning('请填写业务类型');
    return;
  }

  if (!bindingForm.businessName.trim()) {
    message.warning('请填写业务名称');
    return;
  }

  if (!bindingForm.definitionId) {
    message.warning('请选择已发布流程');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      businessName: bindingForm.businessName.trim(),
      businessType: bindingForm.businessType.trim(),
      definitionId: bindingForm.definitionId,
      isEnabled: bindingForm.isEnabled,
      remark: bindingForm.remark.trim() || null,
    };

    if (editingBinding.value) {
      await updateWorkflowBusinessBindingApi(editingBinding.value.id, payload);
      message.success('业务绑定已更新');
    } else {
      await createWorkflowBusinessBindingApi(payload);
      message.success('业务绑定已新增');
    }

    bindingModalOpen.value = false;
    await loadBusinessBindings();
  } finally {
    saving.value = false;
  }
}

async function removeBusinessBinding(
  record: Record<string, any> | WorkflowBusinessBindingItem,
) {
  const binding = asWorkflowBusinessBinding(record);
  const deleted = await deleteWorkflowBusinessBindingApi(binding.id);
  if (deleted) {
    message.success('业务绑定已删除');
  }
  await loadBusinessBindings();
}

async function submitDefinition() {
  if (isEditingDefinitionReadonly.value) {
    message.warning('已发布或归档的流程版本只读，请先创建新版本草稿后再调整。');
    return false;
  }

  if (!validateDefinitionBeforeSave()) {
    return false;
  }

  saving.value = true;
  try {
    const payload = {
      code: definitionForm.code,
      description: definitionForm.description || null,
      designerJson: buildDesignerJson(),
      formSchemaJson: serializeFormSchemaFields(),
      formName: definitionForm.formName || null,
      isEnabled: definitionForm.isEnabled,
      name: definitionForm.name,
      nodes: definitionForm.nodes.map((node, index) => ({
        approverRoleId:
          node.approverType === 'Role' ? node.approverRoleId || null : null,
        approvalMode: node.nodeType === 'approve' ? node.approvalMode || 'Any' : 'Any',
        approverType: node.approverType,
        approverUserId:
          node.approverType === 'User' ? node.approverUserId || null : null,
        designerNodeId: node.designerNodeId,
        isEnabled: node.isEnabled,
        name: node.name,
        nodeType: node.nodeType ?? 'approve',
        order: node.order || index + 1,
        slaMinutes:
          node.nodeType === 'approve' ? normalizeSlaMinutes(node.slaMinutes) : null,
      })),
    };

    if (editingDefinition.value) {
      const updated = await updateWorkflowDefinitionApi(editingDefinition.value.id, payload);
      editingDefinition.value = updated;
      selectedDefinitionId.value = updated.id;
      message.success('流程定义草稿已保存');
    } else {
      const created = await createWorkflowDefinitionApi(payload);
      editingDefinition.value = created;
      selectedDefinitionId.value = created.id;
      message.success('流程定义草稿已新增');
    }

    await loadDefinitions();
    return true;
  } finally {
    saving.value = false;
  }
}

async function publishDefinition() {
  if (!editingDefinition.value) {
    message.warning('请先保存流程定义草稿');
    return;
  }

  saving.value = true;
  try {
    const published = await publishWorkflowDefinitionApi(editingDefinition.value.id);
    editingDefinition.value = published;
    selectedDefinitionId.value = published.id;
    message.success(`流程定义 v${published.version} 已发布`);
    await loadDefinitions();
  } finally {
    saving.value = false;
  }
}

async function createNewDefinitionVersion() {
  if (!editingDefinition.value) {
    message.warning('请先选择流程定义');
    return;
  }

  saving.value = true;
  try {
    const draft = await createWorkflowDefinitionVersionApi(editingDefinition.value.id);
    message.success(`已创建 v${draft.version} 草稿版本`);
    await loadDefinitions();
    openEditDefinition(draft);
  } finally {
    saving.value = false;
  }
}

async function removeDefinition(record: WorkflowDefinitionItem) {
  const deleted = await deleteWorkflowDefinitionApi(record.id);
  if (deleted) {
    message.success('流程定义已删除');
  }
  await loadDefinitions();
}

function openStartAttachmentPicker() {
  startAttachmentInputRef.value?.click();
}

function openDetailAttachmentPicker() {
  detailAttachmentInputRef.value?.click();
}

async function handleStartAttachmentChange(event: Event) {
  const input = event.target as HTMLInputElement;
  uploadingStartAttachment.value = true;
  try {
    const uploaded = await uploadFiles(input.files);
    if (uploaded.length > 0) {
      const existingIds = new Set(startAttachments.value.map((item) => item.id));
      startAttachments.value = [
        ...startAttachments.value,
        ...uploaded.filter((item) => !existingIds.has(item.id)),
      ];
      message.success(`已上传 ${uploaded.length} 个附件`);
    }
  } finally {
    uploadingStartAttachment.value = false;
    input.value = '';
  }
}

function removeStartAttachment(fileId: string) {
  startAttachments.value = startAttachments.value.filter((item) => item.id !== fileId);
}

async function handleDetailAttachmentChange(event: Event) {
  const input = event.target as HTMLInputElement;
  if (!selectedInstance.value?.id) {
    input.value = '';
    return;
  }

  uploadingDetailAttachment.value = true;
  try {
    const uploaded = await uploadFiles(input.files);
    for (const file of uploaded) {
      await addWorkflowAttachmentApi(selectedInstance.value.id, {
        fileId: file.id,
        remark: null,
      });
    }
    if (uploaded.length > 0) {
      message.success(`已添加 ${uploaded.length} 个附件`);
      await reloadSelectedInstance();
    }
  } finally {
    uploadingDetailAttachment.value = false;
    input.value = '';
  }
}

async function downloadWorkflowAttachment(attachmentId: string, fileName: string) {
  if (!selectedInstance.value?.id) {
    return;
  }

  const blob = await downloadWorkflowAttachmentApi(
    selectedInstance.value.id,
    attachmentId,
  );
  saveBlob(blob, fileName);
}

async function submitWorkflowComment() {
  if (!selectedInstance.value?.id) {
    return;
  }

  const content = commentContent.value.trim();
  if (!content) {
    message.warning('请填写评论内容');
    return;
  }

  commentSaving.value = true;
  try {
    await addWorkflowCommentApi(selectedInstance.value.id, { content });
    commentContent.value = '';
    message.success('评论已发布');
    await reloadSelectedInstance();
  } finally {
    commentSaving.value = false;
  }
}

async function submitStart() {
  if (!startForm.definitionId || !startForm.title.trim()) {
    message.warning('请选择流程并填写审批标题');
    return;
  }

  let formDataJson = startForm.formDataJson || '{}';
  if (hasStartFormSchema.value) {
    if (!validateStartDynamicForm()) {
      return;
    }

    syncStartJsonFromDynamicForm();
    formDataJson = startForm.formDataJson;
  } else {
    try {
      JSON.parse(startForm.formDataJson || '{}');
    } catch {
      message.warning('表单数据必须是合法 JSON');
      return;
    }
  }

  saving.value = true;
  try {
    await startWorkflowInstanceApi({
      attachmentFileIds: startAttachments.value.map((file) => file.id),
      businessKey: startForm.businessKey || null,
      definitionId: startForm.definitionId,
      formDataJson,
      title: startForm.title,
    });
    message.success('审批已发起');
    startForm.businessKey = '';
    startForm.title = '';
    if (hasStartFormSchema.value) {
      resetStartFormDataFromSchema();
    } else {
      startForm.formDataJson = '{\n  "reason": ""\n}';
    }
    startAttachments.value = [];
    activeTab.value = 'instances';
    await Promise.all([loadInstances(), loadTasks()]);
  } finally {
    saving.value = false;
  }
}

function openActionModal(
  type: 'approve' | 'reject',
  record: Record<string, any> | WorkflowTaskItem,
) {
  const task = asWorkflowTask(record);
  actionType.value = type;
  actionTarget.value = task;
  actionComment.value = '';
  actionModalOpen.value = true;
}

function openTransferModal(record: Record<string, any> | WorkflowTaskItem) {
  const task = asWorkflowTask(record);
  transferTarget.value = task;
  transferForm.targetUserId = '';
  transferForm.comment = '';
  transferModalOpen.value = true;
}

function openDetailActionModal(type: 'approve' | 'reject') {
  if (!detailTodoTask.value) {
    message.warning('当前用户没有这条流程的待办任务');
    return;
  }

  openActionModal(type, detailTodoTask.value);
}

function openDetailTransferModal() {
  if (!detailTodoTask.value) {
    message.warning('当前用户没有这条流程的待办任务');
    return;
  }

  openTransferModal(detailTodoTask.value);
}

async function submitAction() {
  if (!actionTarget.value) {
    return;
  }

  actionSaving.value = true;
  try {
    if (actionType.value === 'approve') {
      await approveWorkflowInstanceApi(
        actionTarget.value.instanceId,
        actionComment.value,
      );
      message.success('已同意审批');
    } else {
      await rejectWorkflowInstanceApi(
        actionTarget.value.instanceId,
        actionComment.value,
      );
      message.success('已驳回审批');
    }
    actionModalOpen.value = false;
    await Promise.all([loadTasks(), loadInstances()]);
    await reloadSelectedInstance();
  } finally {
    actionSaving.value = false;
  }
}

async function submitTransfer() {
  if (!transferTarget.value) {
    return;
  }

  if (!transferForm.targetUserId) {
    message.warning('请选择转办接收人');
    return;
  }

  actionSaving.value = true;
  try {
    await transferWorkflowTaskApi(transferTarget.value.id, {
      comment: transferForm.comment || null,
      targetUserId: transferForm.targetUserId,
    });
    message.success('待办已转办');
    transferModalOpen.value = false;
    await Promise.all([loadTasks(), loadInstances()]);
    await reloadSelectedInstance();
  } finally {
    actionSaving.value = false;
  }
}

function getPendingTask(record: Record<string, any> | WorkflowInstanceItem) {
  const instance = asWorkflowInstance(record);
  return instance.tasks.find((task) => task.status === 'Pending');
}

function canRemindInstance(record: Record<string, any> | WorkflowInstanceItem) {
  const instance = asWorkflowInstance(record);
  return canStart.value && instance.status === 'Pending' && !!getPendingTask(instance);
}

async function remindInstance(record: Record<string, any> | WorkflowInstanceItem) {
  const instance = asWorkflowInstance(record);
  const pendingTask = getPendingTask(instance);
  if (!pendingTask) {
    message.warning('当前流程没有待处理任务');
    return;
  }

  remindSavingTaskId.value = pendingTask.id;
  try {
    await remindWorkflowTaskApi(pendingTask.id, {
      comment: `请及时处理：${instance.title}`,
    });
    message.success('已发送催办消息');
    await Promise.all([loadInstances(), loadTasks()]);
    await reloadSelectedInstance();
  } finally {
    remindSavingTaskId.value = '';
  }
}

async function withdrawInstance(record: Record<string, any> | WorkflowInstanceItem) {
  const instance = asWorkflowInstance(record);
  await withdrawWorkflowInstanceApi(instance.id, '发起人撤回');
  message.success('流程已撤回');
  await Promise.all([loadInstances(), loadTasks()]);
  await reloadSelectedInstance();
}

async function remindSelectedInstance() {
  if (!selectedInstance.value) {
    return;
  }

  await remindInstance(selectedInstance.value);
}

async function withdrawSelectedInstance() {
  if (!selectedInstance.value) {
    return;
  }

  await withdrawInstance(selectedInstance.value);
}

async function reloadSelectedInstance() {
  if (!detailModalOpen.value || !selectedInstance.value?.id) {
    return;
  }

  selectedInstance.value = await getWorkflowInstanceApi(selectedInstance.value.id);
}

function readRouteQueryValue(value: unknown) {
  if (Array.isArray(value)) {
    return typeof value[0] === 'string' ? value[0] : '';
  }

  return typeof value === 'string' ? value : '';
}

function syncWorkflowDetailRoute(
  instanceId?: string,
  taskId?: string,
  ccRecordId?: string,
) {
  const query = { ...route.query };

  if (instanceId) {
    query.workflowInstanceId = instanceId;
    if (taskId) {
      query.workflowTaskId = taskId;
    } else {
      delete query.workflowTaskId;
    }

    if (ccRecordId) {
      query.workflowCcId = ccRecordId;
    } else {
      delete query.workflowCcId;
    }
  } else {
    delete query.workflowInstanceId;
    delete query.workflowTaskId;
    delete query.workflowCcId;
  }

  void router.replace({
    path: route.path,
    query,
  });
}

async function openInstanceDetail(
  id: string,
  options: { ccRecordId?: string; syncRoute?: boolean; taskId?: string } = {},
) {
  if (!id) {
    return;
  }

  detailLoading.value = true;
  detailModalOpen.value = true;
  selectedInstance.value = undefined;
  highlightedWorkflowTaskId.value = options.taskId ?? '';
  commentContent.value = '';
  try {
    selectedInstance.value = await getWorkflowInstanceApi(id);
    if (options.ccRecordId) {
      await markWorkflowCcRead(options.ccRecordId, { silent: true });
    }
    if (options.syncRoute === true) {
      syncWorkflowDetailRoute(id, options.taskId, options.ccRecordId);
    }
  } finally {
    detailLoading.value = false;
  }
}

function openRouteWorkflowDetail() {
  const instanceId = readRouteQueryValue(route.query.workflowInstanceId);
  const taskId = readRouteQueryValue(route.query.workflowTaskId);
  const ccRecordId = readRouteQueryValue(route.query.workflowCcId);

  if (!instanceId) {
    detailModalOpen.value = false;
    selectedInstance.value = undefined;
    highlightedWorkflowTaskId.value = '';
    return;
  }

  void openInstanceDetail(instanceId, {
    ccRecordId,
    syncRoute: false,
    taskId,
  });
}

function closeInstanceDetail() {
  detailModalOpen.value = false;
  selectedInstance.value = undefined;
  highlightedWorkflowTaskId.value = '';
  commentContent.value = '';
  if (
    route.query.workflowInstanceId ||
    route.query.workflowTaskId ||
    route.query.workflowCcId
  ) {
    syncWorkflowDetailRoute();
  }
}

onMounted(async () => {
  await loadBaseOptions();
  await refreshAll();
  openRouteWorkflowDetail();
});

watch(
  () => currentWorkflowOption.value?.formSchemaJson ?? '',
  () => {
    resetStartFormDataFromSchema();
  },
  { flush: 'sync' },
);

watch(
  startFormData,
  () => {
    syncStartJsonFromDynamicForm();
  },
  { deep: true },
);

watch(
  () => [
    route.query.workflowInstanceId,
    route.query.workflowTaskId,
    route.query.workflowCcId,
  ],
  () => {
    openRouteWorkflowDetail();
  },
);
</script>

<template>
  <Page auto-content-height>
    <div class="workflow-page">
      <div class="workflow-header">
        <div>
          <h2>审批中心</h2>
          <p>维护通用流程，发起审批，并追踪待办与流转记录</p>
        </div>
        <Space>
          <Button @click="refreshAll">刷新</Button>
          <Button v-if="canManageDefinition" @click="openDefinitionGuide">
            示例引导
          </Button>
          <Button
            v-if="canManageDefinition"
            type="primary"
            @click="openCreateDefinition"
          >
            新增流程
          </Button>
        </Space>
      </div>

      <Tabs v-model:active-key="activeTab" class="workflow-tabs">
        <TabPane key="todo" tab="我的待办">
          <div class="table-shell">
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="taskColumns"
              :data-source="todoTasks"
              :loading="taskLoading"
              :pagination="false"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'status'">
                  <Tag :color="statusMeta(record.status).color">
                    {{ statusMeta(record.status).label }}
                  </Tag>
                </template>
                <template v-if="column.dataIndex === 'createdAt'">
                  {{ formatTime(record.createdAt) }}
                </template>
                <template v-if="column.dataIndex === 'dueAt'">
                  <Space size="small">
                    <span>{{ deadlineText(record) }}</span>
                    <Tag v-if="isWorkflowTaskOverdue(record)" color="red">
                      已超时
                    </Tag>
                  </Space>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <Space>
                    <Button
                      class="table-action"
                      size="small"
                      @click="
                        openInstanceDetail(record.instanceId, { taskId: record.id })
                      "
                    >
                      详情
                    </Button>
                    <Button
                      v-if="canApprove"
                      class="table-action success"
                      size="small"
                      @click="openActionModal('approve', record)"
                    >
                      同意
                    </Button>
                    <Button
                      v-if="canApprove"
                      class="table-action danger"
                      size="small"
                      @click="openActionModal('reject', record)"
                    >
                      驳回
                    </Button>
                    <Button
                      v-if="canApprove"
                      class="table-action"
                      size="small"
                      @click="openTransferModal(record)"
                    >
                      转办
                    </Button>
                  </Space>
                </template>
              </template>
            </Table>
          </div>
        </TabPane>

        <TabPane key="started" tab="我的申请">
          <div class="query-bar">
            <Space wrap>
              <span class="query-label">关键字</span>
              <Input
                v-model:value="startedQuery.keyword"
                allow-clear
                class="query-input"
                placeholder="标题/流程/业务标识"
                @press-enter="searchStartedInstances"
              />
              <span class="query-label">状态</span>
              <Select
                v-model:value="startedQuery.status"
                allow-clear
                class="query-select"
                :options="statusOptions"
                placeholder="请选择"
              />
            </Space>
            <Button type="primary" @click="searchStartedInstances">搜索</Button>
          </div>
          <div class="table-shell">
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="instanceColumns"
              :data-source="startedInstances"
              :loading="instanceLoading"
              :pagination="startedPagination"
              @change="handleStartedTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'definitionName'">
                  {{ formatInstanceDefinition(record) }}
                </template>
                <template v-if="column.dataIndex === 'currentNodeName'">
                  {{ record.currentNodeName || '-' }}
                </template>
                <template v-if="column.dataIndex === 'status'">
                  <Tag :color="statusMeta(record.status).color">
                    {{ statusMeta(record.status).label }}
                  </Tag>
                </template>
                <template v-if="column.dataIndex === 'startedAt'">
                  {{ formatTime(record.startedAt) }}
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <Space>
                    <Button
                      class="table-action"
                      size="small"
                      @click="openInstanceDetail(record.id)"
                    >
                      详情
                    </Button>
                    <Popconfirm
                      v-if="canRemindInstance(record)"
                      title="确认向当前审批人发送催办消息？"
                      @confirm="remindInstance(record)"
                    >
                      <Button
                        class="table-action warning"
                        size="small"
                        :loading="remindSavingTaskId === getPendingTask(record)?.id"
                      >
                        催办
                      </Button>
                    </Popconfirm>
                    <Popconfirm
                      v-if="canStart && record.status === 'Pending'"
                      title="确认撤回该流程？"
                      @confirm="withdrawInstance(record)"
                    >
                      <Button class="table-action danger" size="small">
                        撤回
                      </Button>
                    </Popconfirm>
                  </Space>
                </template>
              </template>
            </Table>
          </div>
        </TabPane>

        <TabPane key="cc" tab="我的抄送">
          <div class="query-bar">
            <Space wrap>
              <span class="query-label">关键字</span>
              <Input
                v-model:value="ccQuery.keyword"
                allow-clear
                class="query-input"
                placeholder="标题/流程/业务标识"
                @press-enter="searchCcInstances"
              />
              <span class="query-label">状态</span>
              <Select
                v-model:value="ccQuery.status"
                allow-clear
                class="query-select"
                :options="statusOptions"
                placeholder="流程状态"
              />
              <span class="query-label">阅知</span>
              <Select
                v-model:value="ccQuery.readStatus"
                allow-clear
                class="query-select"
                :options="readStatusOptions"
                placeholder="请选择"
              />
            </Space>
            <Button type="primary" @click="searchCcInstances">搜索</Button>
          </div>
          <div class="table-shell">
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="ccColumns"
              :data-source="ccRecords"
              :loading="instanceLoading"
              :pagination="ccPagination"
              @change="handleCcTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'instanceTitle'">
                  <div class="cc-title-cell">
                    <strong>{{ record.instanceTitle }}</strong>
                    <span v-if="record.businessKey">{{ record.businessKey }}</span>
                  </div>
                </template>
                <template v-if="column.dataIndex === 'definitionName'">
                  {{ record.definitionName }}
                </template>
                <template v-if="column.dataIndex === 'nodeName'">
                  {{ record.nodeName || '-' }}
                </template>
                <template v-if="column.dataIndex === 'senderUserName'">
                  {{ record.senderUserName || record.initiatorUserName || '-' }}
                </template>
                <template v-if="column.dataIndex === 'instanceStatus'">
                  <Tag :color="statusMeta(record.instanceStatus).color">
                    {{ statusMeta(record.instanceStatus).label }}
                  </Tag>
                </template>
                <template v-if="column.dataIndex === 'readStatus'">
                  <Tag :color="readStatusMeta(record).color">
                    {{ readStatusMeta(record).label }}
                  </Tag>
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
                      class="table-action"
                      size="small"
                      @click="
                        openInstanceDetail(record.instanceId, {
                          ccRecordId: record.id,
                        })
                      "
                    >
                      详情
                    </Button>
                    <Button
                      v-if="!record.isRead"
                      class="table-action success"
                      size="small"
                      :loading="ccReadSavingId === record.id"
                      @click="markWorkflowCcRead(record.id)"
                    >
                      标为已读
                    </Button>
                  </Space>
                </template>
              </template>
            </Table>
          </div>
        </TabPane>

        <TabPane key="start" tab="发起审批">
          <div class="form-shell">
            <div class="start-helper">
              <div>
                <h3>发起一个审批请求</h3>
                <p>不知道怎么填时，先看示例。示例会说明流程、业务标识、标题和 JSON 的关系。</p>
              </div>
              <Space>
                <Button @click="openStartGuide">示例引导</Button>
                <Button @click="applyLeaveStartExample">填入请假示例</Button>
              </Space>
            </div>
            <Form layout="vertical">
              <div class="grid grid-cols-2 gap-4">
                <FormItem label="流程" required>
                  <Select
                    v-model:value="startForm.definitionId"
                    show-search
                    :filter-option="true"
                    :options="workflowOptions"
                    placeholder="请选择流程"
                  />
                </FormItem>
                <FormItem label="业务标识">
                  <Input
                    v-model:value="startForm.businessKey"
                    placeholder="例如订单号、合同号"
                  />
                </FormItem>
              </div>
              <FormItem label="审批标题" required>
                <Input v-model:value="startForm.title" placeholder="请输入审批标题" />
              </FormItem>
              <div v-if="hasStartFormSchema" class="dynamic-form-card">
                <div class="dynamic-form-head">
                  <div>
                    <h3>{{ currentWorkflowOption?.formName || '流程表单' }}</h3>
                    <p>按流程定义配置的字段填写，系统会自动生成 JSON 供条件分支使用。</p>
                  </div>
                  <Tag>{{ startFormSchemaFields.length }} 个字段</Tag>
                </div>
                <div class="dynamic-form-grid">
                  <FormItem
                    v-for="field in startFormSchemaFields"
                    :key="field.field"
                    :label="field.label"
                    :required="field.required"
                  >
                    <Textarea
                      v-if="field.component === 'textarea'"
                      v-model:value="startFormData[field.field]"
                      :auto-size="{ minRows: 3, maxRows: 6 }"
                      :placeholder="field.placeholder || `请输入${field.label}`"
                    />
                    <Select
                      v-else-if="field.component === 'select'"
                      v-model:value="startFormData[field.field]"
                      allow-clear
                      :options="field.options"
                      :placeholder="field.placeholder || `请选择${field.label}`"
                    />
                    <Input
                      v-else
                      v-model:value="startFormData[field.field]"
                      :placeholder="field.placeholder || `请输入${field.label}`"
                      :type="
                        field.component === 'number'
                          ? 'number'
                          : field.component === 'date'
                            ? 'date'
                            : 'text'
                      "
                    />
                  </FormItem>
                </div>
                <FormItem label="JSON 预览">
                  <Textarea
                    v-model:value="startForm.formDataJson"
                    class="json-editor"
                    readonly
                    :auto-size="{ minRows: 5, maxRows: 10 }"
                  />
                </FormItem>
              </div>
              <FormItem v-else label="表单数据 JSON">
                <Textarea
                  v-model:value="startForm.formDataJson"
                  class="json-editor"
                  :auto-size="{ minRows: 8, maxRows: 14 }"
                />
              </FormItem>
              <FormItem label="审批附件">
                <div class="attachment-uploader">
                  <Space wrap>
                    <Button
                      :loading="uploadingStartAttachment"
                      @click="openStartAttachmentPicker"
                    >
                      上传附件
                    </Button>
                    <span class="field-hint">
                      可上传合同、报价单、截图等补充材料，提交后会随流程进入详情。
                    </span>
                  </Space>
                  <input
                    ref="startAttachmentInputRef"
                    class="hidden-file-input"
                    multiple
                    type="file"
                    @change="handleStartAttachmentChange"
                  />
                  <div
                    v-if="startAttachments.length > 0"
                    class="attachment-list compact"
                  >
                    <div
                      v-for="file in startAttachments"
                      :key="file.id"
                      class="attachment-item"
                    >
                      <div>
                        <strong>{{ file.originalName }}</strong>
                        <span>{{ formatFileSize(file.size) }}</span>
                      </div>
                      <Button
                        danger
                        size="small"
                        type="link"
                        @click="removeStartAttachment(file.id)"
                      >
                        移除
                      </Button>
                    </div>
                  </div>
                </div>
              </FormItem>
              <Space>
                <Button
                  v-if="canStart"
                  type="primary"
                  :loading="saving"
                  @click="submitStart"
                >
                  发起审批
                </Button>
                <Button @click="activeTab = 'instances'">查看实例</Button>
              </Space>
            </Form>
          </div>
        </TabPane>

        <TabPane key="instances" tab="流程实例">
          <div class="query-bar">
            <Space wrap>
              <span class="query-label">关键字</span>
              <Input
                v-model:value="instanceQuery.keyword"
                allow-clear
                class="query-input"
                placeholder="标题/流程/业务标识"
                @press-enter="searchInstances"
              />
              <span class="query-label">状态</span>
              <Select
                v-model:value="instanceQuery.status"
                allow-clear
                class="query-select"
                :options="statusOptions"
                placeholder="请选择"
              />
              <span class="query-label">范围</span>
              <Select
                v-model:value="instanceQuery.scope"
                class="query-select"
                :options="scopeOptions"
              />
            </Space>
            <Button type="primary" @click="searchInstances">搜索</Button>
          </div>
          <div class="table-shell">
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="instanceColumns"
              :data-source="instances"
              :loading="instanceLoading"
              :pagination="instancePagination"
              @change="handleInstanceTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'definitionName'">
                  {{ formatInstanceDefinition(record) }}
                </template>
                <template v-if="column.dataIndex === 'currentNodeName'">
                  {{ record.currentNodeName || '-' }}
                </template>
                <template v-if="column.dataIndex === 'status'">
                  <Tag :color="statusMeta(record.status).color">
                    {{ statusMeta(record.status).label }}
                  </Tag>
                </template>
                <template v-if="column.dataIndex === 'startedAt'">
                  {{ formatTime(record.startedAt) }}
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <Space>
                    <Button
                      class="table-action"
                      size="small"
                      @click="openInstanceDetail(record.id)"
                    >
                      详情
                    </Button>
                    <Popconfirm
                      v-if="canStart && record.status === 'Pending'"
                      title="确认撤回该流程？"
                      @confirm="withdrawInstance(record)"
                    >
                      <Button class="table-action danger" size="small">
                        撤回
                      </Button>
                    </Popconfirm>
                  </Space>
                </template>
              </template>
            </Table>
          </div>
        </TabPane>

        <TabPane key="definitions" tab="流程定义">
          <div class="definition-workbench">
            <aside
              class="definition-list-panel"
              :class="{ 'guide-focus': definitionGuideOpen && definitionGuideStep === 0 }"
            >
              <div class="panel-title-row">
                <div>
                  <h3>流程库</h3>
                  <p>{{ definitionStats.enabled }}/{{ definitionStats.total }} 个启用</p>
                </div>
                <Button
                  v-if="canManageDefinition"
                  size="small"
                  type="primary"
                  @click="openCreateDefinition"
                >
                  新增
                </Button>
              </div>
              <div class="definition-filter">
                <Input
                  v-model:value="definitionQuery.keyword"
                  allow-clear
                  placeholder="搜索名称/编码"
                  @press-enter="searchDefinitions"
                />
                <Select
                  v-model:value="definitionQuery.isEnabled"
                  allow-clear
                  :options="enabledOptions"
                  placeholder="状态"
                />
                <Button block @click="searchDefinitions">筛选</Button>
              </div>
              <div class="definition-list" :class="{ loading: definitionLoading }">
                <button
                  v-for="item in definitions"
                  :key="item.id"
                  class="definition-card"
                  :class="{ active: selectedDefinitionId === item.id }"
                  type="button"
                  @click="selectDefinition(item)"
                >
                  <span class="definition-card-title">
                    {{ item.name }}
                    <Tag color="blue">v{{ item.version }}</Tag>
                    <Tag :color="publishStatusMeta(item.publishStatus).color">
                      {{ publishStatusMeta(item.publishStatus).label }}
                    </Tag>
                    <Tag :color="item.isEnabled ? 'green' : 'default'">
                      {{ item.isEnabled ? '启用' : '停用' }}
                    </Tag>
                  </span>
                  <span class="definition-card-code">{{ item.code }}</span>
                  <span class="definition-card-meta">
                    {{ item.nodes.length }} 个审批节点 / {{ formatTime(item.updatedAt) }}
                  </span>
                </button>
              </div>
              <div class="definition-pagination">
                <Button
                  size="small"
                  :disabled="definitionQuery.page <= 1"
                  @click="
                    definitionQuery.page--;
                    loadDefinitions();
                  "
                >
                  上一页
                </Button>
                <span>第 {{ definitionQuery.page }} 页</span>
                <Button
                  size="small"
                  :disabled="
                    definitionQuery.page * definitionQuery.pageSize >=
                    definitionTotal
                  "
                  @click="
                    definitionQuery.page++;
                    loadDefinitions();
                  "
                >
                  下一页
                </Button>
              </div>
            </aside>

            <section
              class="designer-workspace"
              :class="{
                'guide-focus':
                  definitionGuideOpen &&
                  (definitionGuideStep === 2 || definitionGuideStep === 3),
              }"
            >
              <div class="designer-toolbar">
                <div>
                  <h3>{{ definitionForm.name || '未命名流程' }}</h3>
                  <p>
                    {{ definitionForm.code || '请先配置流程编码' }}
                    <span>/</span>
                    {{ definitionForm.formName || '未绑定表单' }}
                  </p>
                </div>
                <Space wrap>
                  <Button v-if="canManageDefinition" @click="openDefinitionGuide">
                    示例引导
                  </Button>
                  <Button
                    v-if="canManageDefinition"
                    @click="applyLeaveDefinitionExample"
                  >
                    生成请假示例
                  </Button>
                  <Button @click="buildLinearDesigner">整理连线</Button>
                  <Button
                    v-if="canManageDefinition"
                    type="primary"
                    :loading="saving"
                    :disabled="isEditingDefinitionReadonly"
                    :title="
                      isEditingDefinitionReadonly
                        ? '已发布或归档版本只读，请创建新版本草稿后再保存'
                        : undefined
                    "
                    @click="submitDefinition"
                  >
                    保存草稿
                  </Button>
                  <Button
                    v-if="
                      canManageDefinition &&
                      editingDefinition?.publishStatus === 'Draft'
                    "
                    type="primary"
                    :loading="saving"
                    @click="publishDefinition"
                  >
                    发布
                  </Button>
                  <Button
                    v-if="
                      canManageDefinition &&
                      editingDefinition &&
                      editingDefinition.publishStatus !== 'Draft'
                    "
                    :loading="saving"
                    @click="createNewDefinitionVersion"
                  >
                    新版本
                  </Button>
                </Space>
              </div>
              <div v-if="isEditingDefinitionReadonly" class="definition-readonly-banner">
                <span>
                  当前 v{{ editingDefinition?.version }} 为{{
                    publishStatusMeta(editingDefinition?.publishStatus || '').label
                  }}版本，已进入只读治理；如需调整节点或表单，请创建新版本草稿。
                </span>
                <Button
                  v-if="canManageDefinition"
                  size="small"
                  @click="createNewDefinitionVersion"
                >
                  创建新版本草稿
                </Button>
              </div>
              <div v-if="definitionGuideOpen" class="definition-guide-banner">
                <div>
                  <strong>
                    第 {{ definitionGuideStep + 1 }} 步：
                    {{ definitionGuideSteps[definitionGuideStep]?.title }}
                  </strong>
                  <span>
                    {{ definitionGuideSteps[definitionGuideStep]?.description }}
                  </span>
                </div>
                <Button size="small" @click="definitionGuideOpen = false">
                  收起
                </Button>
              </div>
              <div class="designer-main">
                <div class="designer-palette">
                  <h3>节点组件</h3>
                  <button
                    v-for="node in designerNodePalette"
                    :key="node.type"
                    class="node-palette-item"
                    type="button"
                    @click="addDesignerNode(node.type)"
                  >
                    <span>{{ node.label }}</span>
                    <small>{{ node.description }}</small>
                  </button>
                  <p class="designer-palette-tip">
                    先选中画布节点，再新增节点，会自动插入到该节点后面。
                  </p>
                </div>
                <div class="designer-canvas">
                  <VueFlow
                    v-model:edges="flowEdges"
                    v-model:nodes="flowNodes"
                    :default-viewport="{ x: 0, y: 0, zoom: 1 }"
                    fit-view-on-init
                    @connect="handleConnect"
                    @edge-click="handleEdgeClick"
                    @node-click="handleNodeClick"
                  >
                    <template #node-start="{ data, selected }">
                      <div class="flow-node flow-node-start" :class="{ selected }">
                        <Handle type="source" :position="Position.Right" />
                        <strong>{{ data.label }}</strong>
                        <span>流程入口</span>
                      </div>
                    </template>
                    <template #node-approve="{ data, selected }">
                      <div class="flow-node flow-node-approve" :class="{ selected }">
                        <Handle type="target" :position="Position.Left" />
                        <strong>{{ data.label }}</strong>
                        <span>审批任务</span>
                        <Handle type="source" :position="Position.Right" />
                      </div>
                    </template>
                    <template #node-condition="{ data, selected }">
                      <div class="flow-node flow-node-condition" :class="{ selected }">
                        <Handle type="target" :position="Position.Left" />
                        <strong>{{ data.label }}</strong>
                        <span>条件分支</span>
                        <Handle type="source" :position="Position.Right" />
                      </div>
                    </template>
                    <template #node-cc="{ data, selected }">
                      <div class="flow-node flow-node-cc" :class="{ selected }">
                        <Handle type="target" :position="Position.Left" />
                        <strong>{{ data.label }}</strong>
                        <span>通知阅知</span>
                        <Handle type="source" :position="Position.Right" />
                      </div>
                    </template>
                    <template #node-end="{ data, selected }">
                      <div class="flow-node flow-node-end" :class="{ selected }">
                        <Handle type="target" :position="Position.Left" />
                        <strong>{{ data.label }}</strong>
                        <span>流程结束</span>
                      </div>
                    </template>
                  </VueFlow>
                </div>
              </div>
            </section>

            <aside
              class="definition-property-panel"
              :class="{
                'guide-focus':
                  definitionGuideOpen &&
                  (definitionGuideStep === 1 || definitionGuideStep === 3),
              }"
            >
              <h3>属性配置</h3>
              <Form layout="vertical">
                <FormItem label="流程名称" required>
                  <Input v-model:value="definitionForm.name" placeholder="请输入" />
                </FormItem>
                <FormItem label="流程编码" required>
                  <Input
                    v-model:value="definitionForm.code"
                    placeholder="例如 leave_apply"
                  />
                </FormItem>
                <FormItem label="表单名称">
                  <Input
                    v-model:value="definitionForm.formName"
                    placeholder="例如 请假申请"
                  />
                </FormItem>
                <div class="property-row">
                  <span>启用流程</span>
                  <Switch v-model:checked="definitionForm.isEnabled" />
                </div>
                <FormItem label="说明">
                  <Textarea
                    v-model:value="definitionForm.description"
                    :auto-size="{ minRows: 2, maxRows: 4 }"
                  />
                </FormItem>
              </Form>

              <div class="node-property-card form-schema-card">
                <div class="node-property-head">
                  <h3>表单字段</h3>
                  <Button size="small" @click="addFormSchemaField">
                    新增字段
                  </Button>
                </div>
                <p>
                  字段编码会写入发起数据 JSON，条件分支可直接使用，例如 days。
                </p>
                <div v-if="formSchemaFields.length === 0" class="empty-hint">
                  暂未配置字段，发起审批时会继续显示 JSON 输入框。
                </div>
                <div
                  v-for="(field, index) in formSchemaFields"
                  :key="`${field.field}-${index}`"
                  class="form-field-card"
                >
                  <div class="form-field-card-head">
                    <strong>{{ field.label || `字段${index + 1}` }}</strong>
                    <Button
                      class="table-action danger"
                      size="small"
                      @click="removeFormSchemaField(index)"
                    >
                      删除
                    </Button>
                  </div>
                  <Form layout="vertical">
                    <div class="grid grid-cols-2 gap-2">
                      <FormItem label="字段标签" required>
                        <Input v-model:value="field.label" placeholder="请假天数" />
                      </FormItem>
                      <FormItem label="字段编码" required>
                        <Input v-model:value="field.field" placeholder="days" />
                      </FormItem>
                    </div>
                    <FormItem label="控件类型">
                      <Select
                        v-model:value="field.component"
                        :options="formComponentOptions"
                      />
                    </FormItem>
                    <div class="property-row">
                      <span>必填</span>
                      <Switch v-model:checked="field.required" />
                    </div>
                    <FormItem label="占位提示">
                      <Input
                        v-model:value="field.placeholder"
                        placeholder="请输入提示文案"
                      />
                    </FormItem>
                    <FormItem label="默认值">
                      <Input
                        v-model:value="field.defaultValue"
                        placeholder="可选，发起时自动带出"
                      />
                    </FormItem>
                    <FormItem
                      v-if="field.component === 'select'"
                      label="下拉选项"
                    >
                      <Textarea
                        v-model:value="field.optionsText"
                        :auto-size="{ minRows: 3, maxRows: 5 }"
                        placeholder="每行一个选项，例如：&#10;事假&#10;年假&#10;personal=事假"
                      />
                      <div class="field-help">
                        支持“值=显示名”，不写等号时值和显示名相同。
                      </div>
                    </FormItem>
                  </Form>
                </div>
              </div>

              <div class="node-property-card">
                <div class="node-property-head">
                  <h3>节点属性</h3>
                  <Tag>{{ nodeTypeLabel(selectedNodeKind) }}</Tag>
                </div>
                <p>{{ nodeTypeDescription(selectedNodeKind) }}</p>
                <template v-if="selectedWorkflowNode">
                  <div v-if="selectedWorkflowNodeHint" class="node-validation-hint">
                    {{ selectedWorkflowNodeHint }}
                  </div>
                  <Form layout="vertical">
                    <FormItem label="节点名称" required>
                      <Input
                        v-model:value="selectedWorkflowNode.name"
                        placeholder="节点名称"
                        @change="updateSelectedNodeLabel"
                      />
                    </FormItem>
                    <FormItem :label="selectedNodeKind === 'cc' ? '接收类型' : '审批类型'" required>
                      <Select
                        v-model:value="selectedWorkflowNode.approverType"
                        :options="approverTypeOptions"
                      />
                    </FormItem>
                    <FormItem
                      v-if="selectedWorkflowNode.nodeType === 'approve'"
                      label="审批方式"
                    >
                      <Select
                        v-model:value="selectedWorkflowNode.approvalMode"
                        :options="approvalModeOptions"
                      />
                      <div class="field-help">
                        或签：任意一人通过即可流转；会签：所有待办人通过后才流转。
                      </div>
                    </FormItem>
                    <FormItem
                      v-if="selectedWorkflowNode.nodeType === 'approve'"
                      label="处理时限（分钟）"
                    >
                      <Input
                        :value="selectedWorkflowNode.slaMinutes ?? undefined"
                        min="1"
                        placeholder="例如 1440，留空表示不限制"
                        type="number"
                        @update:value="updateSelectedNodeSlaMinutes"
                      />
                      <div class="field-help">
                        到达该节点后开始计时；超时后定时任务会自动给当前处理人发送审批超时消息。
                      </div>
                    </FormItem>
                    <FormItem :label="selectedNodeKind === 'cc' ? '接收人' : '审批人'" required>
                      <div
                        v-if="
                          selectedWorkflowNode.approverType === 'User' &&
                          !users.length
                        "
                        class="node-validation-hint"
                      >
                        当前流程范围内没有可用审批用户，请到系统管理的用户管理中创建或启用当前租户下用户。
                      </div>
                      <div
                        v-if="
                          selectedWorkflowNode.approverType === 'Role' &&
                          !roles.length
                        "
                        class="node-validation-hint"
                      >
                        当前流程范围内没有包含启用用户的可用角色，请先配置当前租户下的角色和用户。
                      </div>
                      <Select
                        v-if="selectedWorkflowNode.approverType === 'User'"
                        :value="selectedWorkflowNode.approverUserId ?? undefined"
                        show-search
                        :filter-option="true"
                        :options="userOptions"
                        placeholder="选择用户"
                        @update:value="updateSelectedNodeApproverUserId"
                      />
                      <Select
                        v-else
                        :value="selectedWorkflowNode.approverRoleId ?? undefined"
                        show-search
                        :filter-option="true"
                        :options="roleOptions"
                        placeholder="选择角色"
                        @update:value="updateSelectedNodeApproverRoleId"
                      />
                    </FormItem>
                    <div class="property-row">
                      <span>启用节点</span>
                      <Switch v-model:checked="selectedWorkflowNode.isEnabled" />
                    </div>
                  </Form>
                </template>
                <template v-else-if="selectedFlowNode">
                  <Form layout="vertical">
                    <FormItem label="节点名称">
                      <Input
                        v-model:value="selectedFlowNode.data.label"
                        @change="updateSelectedDesignNodeLabel"
                      />
                    </FormItem>
                  </Form>
                </template>
                <p v-else>请选择画布节点查看属性。</p>
                <Button
                  v-if="selectedFlowNode && selectedFlowNode.id !== 'start'"
                  class="table-action danger"
                  size="small"
                  @click="removeSelectedDesignNode"
                >
                  删除节点
                </Button>
              </div>

              <div class="node-property-card">
                <div class="node-property-head">
                  <h3>连线属性</h3>
                  <Tag>{{ selectedFlowEdge ? '分支' : '未选择' }}</Tag>
                </div>
                <template v-if="selectedFlowEdge">
                  <p>
                    {{ selectedFlowEdge.source }} -> {{ selectedFlowEdge.target }}
                  </p>
                  <div class="edge-help">
                    条件节点的出口线会在运行时判断；未命中任何条件时，会走默认分支。
                    要调整线条，先删除错误连线，再从节点右侧灰点拖到目标节点左侧灰点。
                  </div>
                  <Form layout="vertical">
                    <FormItem label="分支名称">
                      <Input
                        v-model:value="selectedFlowEdge.data.label"
                        placeholder="例如 大于3天 / 默认"
                        @change="updateSelectedEdgeLabel"
                      />
                    </FormItem>
                    <template v-if="selectedEdgeSourceKind === 'condition'">
                      <div class="property-row">
                        <span>默认分支</span>
                        <Switch v-model:checked="selectedFlowEdge.data.isDefault" />
                      </div>
                      <template v-if="!selectedFlowEdge.data.isDefault">
                        <FormItem label="字段路径">
                          <Input
                            v-model:value="selectedFlowEdge.data.conditionField"
                            placeholder="例如 days 或 amount.total"
                          />
                        </FormItem>
                        <FormItem label="运算符">
                          <Select
                            v-model:value="selectedFlowEdge.data.conditionOperator"
                            :options="conditionOperatorOptions"
                          />
                        </FormItem>
                        <FormItem label="比较值">
                          <Input
                            v-model:value="selectedFlowEdge.data.conditionValue"
                            placeholder="例如 3"
                          />
                        </FormItem>
                      </template>
                    </template>
                    <p v-else>
                      普通连线按画布方向流转，只有条件节点的出口线需要配置判断规则。
                    </p>
                  </Form>
                  <Button
                    class="table-action danger"
                    size="small"
                    @click="removeSelectedEdge"
                  >
                    删除连线
                  </Button>
                </template>
                <p v-else>点击画布上的连线，可配置条件分支。</p>
              </div>

              <Popconfirm
                v-if="canManageDefinition && editingDefinition"
                title="确认删除该流程定义？"
                @confirm="removeDefinition(editingDefinition)"
              >
                <Button danger block>删除流程定义</Button>
              </Popconfirm>
            </aside>
          </div>
        </TabPane>

        <TabPane key="bindings" tab="业务绑定">
          <div class="query-bar">
            <Space wrap>
              <span class="query-label">关键字</span>
              <Input
                v-model:value="bindingQuery.keyword"
                allow-clear
                class="query-input"
                placeholder="业务名称/类型/流程"
                @press-enter="searchBusinessBindings"
              />
              <span class="query-label">状态</span>
              <Select
                v-model:value="bindingQuery.isEnabled"
                allow-clear
                class="query-select"
                :options="enabledOptions"
                placeholder="请选择"
              />
            </Space>
            <Space>
              <Button @click="searchBusinessBindings">搜索</Button>
              <Button
                v-if="canManageDefinition"
                type="primary"
                @click="openCreateBinding"
              >
                新增绑定
              </Button>
            </Space>
          </div>
          <div class="table-shell">
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="bindingColumns"
              :data-source="businessBindings"
              :loading="bindingLoading"
              :pagination="bindingPagination"
              @change="handleBindingTableChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'businessType'">
                  <Tag>{{ record.businessType }}</Tag>
                </template>
                <template v-if="column.dataIndex === 'definitionName'">
                  <div class="binding-definition-cell">
                    <strong>
                      {{ record.definitionName }} v{{ record.definitionVersion }}
                    </strong>
                    <span>{{ record.definitionCode }}</span>
                  </div>
                </template>
                <template v-if="column.dataIndex === 'definitionPublishStatus'">
                  <Tag :color="publishStatusMeta(record.definitionPublishStatus).color">
                    {{ publishStatusMeta(record.definitionPublishStatus).label }}
                  </Tag>
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
                    <Button
                      v-if="canManageDefinition"
                      class="table-action"
                      size="small"
                      @click="openEditBinding(record)"
                    >
                      编辑
                    </Button>
                    <Popconfirm
                      v-if="canManageDefinition"
                      title="确认删除该业务绑定？"
                      @confirm="removeBusinessBinding(record)"
                    >
                      <Button class="table-action danger" size="small">
                        删除
                      </Button>
                    </Popconfirm>
                  </Space>
                </template>
              </template>
            </Table>
          </div>
        </TabPane>

        <TabPane key="done" tab="我的已办">
          <div class="table-shell">
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="taskColumns"
              :data-source="doneTasks"
              :loading="taskLoading"
              :pagination="{ pageSize: 12 }"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'status'">
                  <Tag :color="statusMeta(record.status).color">
                    {{ statusMeta(record.status).label }}
                  </Tag>
                </template>
                <template v-if="column.dataIndex === 'createdAt'">
                  {{ formatTime(record.completedAt || record.createdAt) }}
                </template>
                <template v-if="column.dataIndex === 'dueAt'">
                  <Space size="small">
                    <span>{{ deadlineText(record) }}</span>
                    <Tag v-if="isWorkflowTaskOverdue(record)" color="red">
                      已超时
                    </Tag>
                  </Space>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <Button
                    class="table-action"
                    size="small"
                    @click="
                      openInstanceDetail(record.instanceId, { taskId: record.id })
                    "
                  >
                    详情
                  </Button>
                </template>
              </template>
            </Table>
          </div>
        </TabPane>
      </Tabs>
    </div>

    <Modal
      v-model:open="actionModalOpen"
      :confirm-loading="actionSaving"
      :title="actionType === 'approve' ? '同意审批' : '驳回审批'"
      width="560px"
      @ok="submitAction"
    >
      <Form layout="vertical">
        <FormItem label="审批意见">
          <Textarea
            v-model:value="actionComment"
            :auto-size="{ minRows: 4, maxRows: 8 }"
            placeholder="请输入审批意见"
          />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="transferModalOpen"
      :confirm-loading="actionSaving"
      title="转办待办"
      width="560px"
      @ok="submitTransfer"
    >
      <Form layout="vertical">
        <FormItem label="当前待办">
          <Input
            :value="transferTarget?.instanceTitle || '-'"
            disabled
          />
        </FormItem>
        <FormItem label="转办给" required>
          <Select
            v-model:value="transferForm.targetUserId"
            show-search
            :filter-option="true"
            :options="userOptions"
            placeholder="请选择接收人"
          />
        </FormItem>
        <FormItem label="转办说明">
          <Textarea
            v-model:value="transferForm.comment"
            :auto-size="{ minRows: 3, maxRows: 6 }"
            placeholder="说明为什么转办，方便接收人理解上下文"
          />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="bindingModalOpen"
      :confirm-loading="saving"
      :title="editingBinding ? '编辑业务绑定' : '新增业务绑定'"
      width="680px"
      @ok="submitBusinessBinding"
    >
      <Form layout="vertical">
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="业务类型" required>
            <Input
              v-model:value="bindingForm.businessType"
              placeholder="例如 sample_order"
            />
          </FormItem>
          <FormItem label="业务名称" required>
            <Input
              v-model:value="bindingForm.businessName"
              placeholder="例如 示例订单"
            />
          </FormItem>
        </div>
        <FormItem label="绑定流程" required>
          <Select
            v-model:value="bindingForm.definitionId"
            show-search
            :filter-option="true"
            :options="workflowOptions"
            placeholder="请选择已发布流程"
          />
        </FormItem>
        <div class="property-row binding-switch-row">
          <span>启用绑定</span>
          <Switch v-model:checked="bindingForm.isEnabled" />
        </div>
        <FormItem label="说明">
          <Textarea
            v-model:value="bindingForm.remark"
            :auto-size="{ minRows: 3, maxRows: 6 }"
            placeholder="说明这个业务类型何时使用该流程"
          />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="startGuideOpen"
      title="审批请求示例引导"
      width="760px"
      :footer="null"
    >
      <div class="guide-panel">
        <div class="guide-summary">
          <h3>按这个顺序创建和发起</h3>
          <p>
            先确认流程定义能找到审批人，再发起请求。你刚才看到的“没有可用审批人”，通常是流程定义里的审批节点没有选择用户或角色下没有可用用户。
          </p>
        </div>

        <div class="guide-steps">
          <div class="guide-step">
            <span>1</span>
            <div>
              <h4>准备流程定义</h4>
              <p>
                到“流程定义”里选择或新增流程，审批节点必须配置审批用户，或配置有成员的审批角色。
              </p>
              <Button size="small" @click="goWorkflowDefinitionGuide">
                去流程定义
              </Button>
            </div>
          </div>
          <div class="guide-step">
            <span>2</span>
            <div>
              <h4>填写发起参数</h4>
              <p>
                流程决定走哪套审批；业务标识用于关联单据；审批标题会显示在待办里；JSON 是条件分支和业务数据的来源。
              </p>
              <pre>{
  "days": 5,
  "type": "事假",
  "reason": "家中有事，需要请假"
}</pre>
            </div>
          </div>
          <div class="guide-step">
            <span>3</span>
            <div>
              <h4>发起并验证</h4>
              <p>
                发起成功后，到“流程实例”看当前节点，到审批人的“我的待办”处理。如果条件节点配置了
                <Tag>days &gt; 3</Tag>
                ，这里的示例会进入长假分支。
              </p>
              <Button type="primary" size="small" @click="applyLeaveStartExample">
                一键填入请假示例
              </Button>
            </div>
          </div>
        </div>

        <div class="guide-checklist">
          <h4>发起前快速检查</h4>
          <ul>
            <li>流程定义已启用。</li>
            <li>至少有一个审批节点处于启用状态。</li>
            <li>审批节点选择了审批用户，或审批角色下存在启用用户。</li>
            <li>条件分支使用的字段，例如 <code>days</code>，已出现在表单 JSON 中。</li>
          </ul>
        </div>
      </div>
    </Modal>

    <Modal
      v-model:open="definitionGuideOpen"
      title="创建工作流示例引导"
      width="920px"
      :footer="null"
    >
      <div class="definition-guide-modal">
        <div class="definition-guide-steps">
          <button
            v-for="(step, index) in definitionGuideSteps"
            :key="step.title"
            class="definition-guide-step"
            :class="{ active: definitionGuideStep === index }"
            type="button"
            @click="setDefinitionGuideStep(index)"
          >
            <span>{{ index + 1 }}</span>
            <strong>{{ step.title }}</strong>
            <small>{{ step.description }}</small>
          </button>
        </div>

        <div class="definition-guide-content">
          <template v-if="definitionGuideStep === 0">
            <h3>先创建一个请假审批流程草稿</h3>
            <p>
              这个示例会生成“开始 -> 直属主管审批 -> 请假天数判断 -> 部门负责人审批/结束”的流程，并保留抄送人事节点。你可以把它当成第一次练手的标准模板。
            </p>
            <div class="guide-example-map">
              <span>开始</span>
              <span>直属主管审批</span>
              <span>请假天数判断</span>
              <span>部门负责人审批</span>
              <span>抄送人事</span>
              <span>结束</span>
            </div>
            <div class="guide-action-row">
              <Button type="primary" @click="applyLeaveDefinitionExample">
                生成请假流程草稿
              </Button>
            </div>
          </template>

          <template v-else-if="definitionGuideStep === 1">
            <h3>填写流程基础信息</h3>
            <p>
              右侧“属性配置”里需要确认流程名称、流程编码、表单名称和启用状态。流程编码会作为系统识别流程的唯一标识，建议使用英文小写和下划线。
            </p>
            <div class="guide-check-grid">
              <span>流程名称</span>
              <strong>{{ definitionForm.name || '未填写' }}</strong>
              <span>流程编码</span>
              <strong>{{ definitionForm.code || '未填写' }}</strong>
              <span>表单名称</span>
              <strong>{{ definitionForm.formName || '未填写' }}</strong>
              <span>启用状态</span>
              <strong>{{ definitionForm.isEnabled ? '启用' : '停用' }}</strong>
            </div>
            <div class="guide-action-row">
              <Button @click="setDefinitionGuideStep(0)">上一步</Button>
              <Button type="primary" @click="setDefinitionGuideStep(2)">
                下一步：认识画布
              </Button>
            </div>
          </template>

          <template v-else-if="definitionGuideStep === 2">
            <h3>认识画布节点</h3>
            <p>
              中间画布负责表达流程走向。左侧节点组件可以继续追加审批、条件、抄送或结束节点；选中画布节点后再新增，会自动插入到当前节点后面。
            </p>
            <div class="guide-node-list">
              <span class="guide-node-pill start">开始：流程入口</span>
              <span class="guide-node-pill approve">审批：生成待办任务</span>
              <span class="guide-node-pill condition">条件：按表单 JSON 分支</span>
              <span class="guide-node-pill cc">抄送：通知阅知</span>
              <span class="guide-node-pill end">结束：流程完成</span>
            </div>
            <div class="guide-action-row">
              <Button @click="setDefinitionGuideStep(1)">上一步</Button>
              <Button type="primary" @click="setDefinitionGuideStep(3)">
                下一步：配置节点
              </Button>
            </div>
          </template>

          <template v-else-if="definitionGuideStep === 3">
            <h3>配置审批人和条件分支</h3>
            <p>
              右侧“节点属性”配置审批人；“连线属性”配置条件分支。示例里长假分支使用字段
              <Tag>days</Tag>
              ，规则是
              <Tag>大于 3</Tag>
              ，所以发起时 JSON 里填
              <Tag>"days": 5</Tag>
              会进入部门负责人审批。
            </p>
            <div v-if="!users.length" class="guide-warning">
              当前流程范围内没有可选审批用户，请先在系统用户里创建或启用当前租户下用户，否则保存时会提示审批人未配置。
            </div>
            <div class="guide-check-grid">
              <span>直属主管审批人</span>
              <strong>{{ definitionForm.nodes[0]?.approverUserId ? '已选择' : '待选择' }}</strong>
              <span>部门负责人审批人</span>
              <strong>{{ definitionForm.nodes[1]?.approverUserId ? '已选择' : '待选择' }}</strong>
              <span>长假条件</span>
              <strong>days &gt; 3</strong>
              <span>默认分支</span>
              <strong>3 天以内直接结束</strong>
            </div>
            <div class="guide-action-row">
              <Button @click="setDefinitionGuideStep(2)">上一步</Button>
              <Button type="primary" @click="setDefinitionGuideStep(4)">
                下一步：保存流程
              </Button>
            </div>
          </template>

          <template v-else-if="definitionGuideStep === 4">
            <h3>保存流程定义</h3>
            <p>
              保存时系统会检查流程名称、编码、审批节点、审批人和条件线。如果缺少内容，会自动选中对应节点或连线，并提示你去补齐。
            </p>
            <div class="guide-action-row">
              <Button @click="setDefinitionGuideStep(3)">上一步</Button>
              <Button type="primary" :loading="saving" @click="saveDefinitionFromGuide">
                保存流程并继续
              </Button>
            </div>
          </template>

          <template v-else>
            <h3>发起一条审批测试</h3>
            <p>
              流程保存后，就可以去“发起审批”填入请假请求示例。这个示例会使用
              <Tag>days = 5</Tag>
              ，用于验证长假条件分支是否生效。
            </p>
            <pre>{
  "days": 5,
  "type": "事假",
  "reason": "家中有事，需要请假"
}</pre>
            <div class="guide-action-row">
              <Button @click="setDefinitionGuideStep(4)">上一步</Button>
              <Button type="primary" @click="goStartWithDefinitionExample">
                去发起审批并填入示例
              </Button>
            </div>
          </template>
        </div>
      </div>
    </Modal>

    <Drawer
      v-model:open="detailModalOpen"
      title="流程详情"
      width="min(1040px, calc(100vw - 32px))"
      @close="closeInstanceDetail"
    >
      <div v-if="detailLoading" class="detail-loading">正在加载流程详情...</div>
      <div v-else-if="selectedInstance" class="detail-layout">
        <div class="detail-action-bar">
          <div>
            <strong>{{ selectedInstance.title }}</strong>
            <span>
              {{ selectedInstanceDefinitionLabel }}
              <template v-if="selectedInstance.businessKey">
                / {{ selectedInstance.businessKey }}
              </template>
            </span>
          </div>
          <Space wrap>
            <Button
              v-if="detailTodoTask && canApprove"
              size="small"
              type="primary"
              @click="openDetailActionModal('approve')"
            >
              同意
            </Button>
            <Button
              v-if="detailTodoTask && canApprove"
              danger
              size="small"
              @click="openDetailActionModal('reject')"
            >
              驳回
            </Button>
            <Button
              v-if="detailTodoTask && canApprove"
              size="small"
              @click="openDetailTransferModal"
            >
              转办
            </Button>
            <Popconfirm
              v-if="canRemindSelectedInstance"
              title="确认向当前审批人发送催办消息？"
              @confirm="remindSelectedInstance"
            >
              <Button
                size="small"
                :loading="remindSavingTaskId === detailPendingTask?.id"
              >
                催办
              </Button>
            </Popconfirm>
            <Popconfirm
              v-if="canWithdrawSelectedInstance"
              title="确认撤回该流程？"
              @confirm="withdrawSelectedInstance"
            >
              <Button danger size="small">撤回</Button>
            </Popconfirm>
          </Space>
        </div>
        <Descriptions bordered size="small" :column="2">
          <DescriptionsItem label="标题">
            {{ selectedInstance.title }}
          </DescriptionsItem>
          <DescriptionsItem label="流程">
            {{ selectedInstance.definitionName }}
          </DescriptionsItem>
          <DescriptionsItem label="发起版本">
            <Space>
              <Tag color="blue">v{{ selectedInstance.definitionVersion || '-' }}</Tag>
              <span>{{ selectedInstance.definitionCode || '-' }}</span>
            </Space>
          </DescriptionsItem>
          <DescriptionsItem label="状态">
            <Tag :color="statusMeta(selectedInstance.status).color">
              {{ statusMeta(selectedInstance.status).label }}
            </Tag>
          </DescriptionsItem>
          <DescriptionsItem label="当前节点">
            {{ selectedInstance.currentNodeName || '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="业务标识">
            {{ selectedInstance.businessKey || '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="发起人">
            {{ selectedInstance.initiatorUserName }}
          </DescriptionsItem>
          <DescriptionsItem label="发起时间">
            {{ formatTime(selectedInstance.startedAt) }}
          </DescriptionsItem>
          <DescriptionsItem label="完成时间">
            {{ formatTime(selectedInstance.completedAt) }}
          </DescriptionsItem>
        </Descriptions>
        <div class="detail-grid">
          <div>
            <h3>表单数据</h3>
            <div
              v-if="selectedInstanceReadableFormItems.length > 0"
              class="form-data-list"
            >
              <div
                v-for="item in selectedInstanceReadableFormItems"
                :key="item.label"
              >
                <span>{{ item.label }}</span>
                <strong>{{ item.value }}</strong>
              </div>
            </div>
            <h4
              v-if="selectedInstanceReadableFormItems.length > 0"
              class="json-preview-title"
            >
              JSON 预览
            </h4>
            <pre>{{ selectedInstance.formDataJson }}</pre>
            <div class="detail-section">
              <div class="section-title-row">
                <h3>附件</h3>
                <Button
                  size="small"
                  :loading="uploadingDetailAttachment"
                  @click="openDetailAttachmentPicker"
                >
                  添加附件
                </Button>
              </div>
              <input
                ref="detailAttachmentInputRef"
                class="hidden-file-input"
                multiple
                type="file"
                @change="handleDetailAttachmentChange"
              />
              <div
                v-if="selectedInstance.attachments.length > 0"
                class="attachment-list"
              >
                <div
                  v-for="attachment in selectedInstance.attachments"
                  :key="attachment.id"
                  class="attachment-item"
                >
                  <div>
                    <strong>{{ attachment.originalName }}</strong>
                    <span>
                      {{ formatFileSize(attachment.size) }} /
                      {{ attachment.uploaderUserName }} /
                      {{ formatTime(attachment.createdAt) }}
                    </span>
                    <small v-if="attachment.remark">{{ attachment.remark }}</small>
                  </div>
                  <Button
                    size="small"
                    type="link"
                    @click="
                      downloadWorkflowAttachment(
                        attachment.id,
                        attachment.originalName,
                      )
                    "
                  >
                    下载
                  </Button>
                </div>
              </div>
              <div v-else class="empty-hint">暂无附件</div>
            </div>
          </div>
          <div>
            <h3>审批任务</h3>
            <div class="task-list">
              <div
                v-for="task in selectedInstance.tasks"
                :key="task.id"
                class="task-list-item"
                :class="{
                  'task-list-item-highlighted':
                    task.id === highlightedWorkflowTaskId,
                }"
              >
                <div>
                  <strong>{{ task.nodeName }}</strong>
                  <span>{{ task.approverUserName }}</span>
                </div>
                <Space class="task-status-group" size="small">
                  <Tag :color="statusMeta(task.status).color">
                    {{ statusMeta(task.status).label }}
                  </Tag>
                  <Tag v-if="isWorkflowTaskOverdue(task)" color="red">
                    已超时
                  </Tag>
                </Space>
                <small>
                  到达 {{ formatTime(task.createdAt) }}
                  <template v-if="task.dueAt">
                    / 截止 {{ formatTime(task.dueAt) }}
                  </template>
                  <template v-if="task.completedAt">
                    / 完成 {{ formatTime(task.completedAt) }}
                  </template>
                </small>
                <p v-if="task.comment">{{ task.comment }}</p>
              </div>
            </div>
            <div class="detail-section">
              <div class="section-title-row">
                <h3>抄送回执</h3>
                <Tag>
                  {{ countReadCcReceipts(selectedInstance.ccRecords) }} /
                  {{ selectedInstance.ccRecords.length }} 已读
                </Tag>
              </div>
              <div
                v-if="selectedInstance.ccRecords.length > 0"
                class="cc-receipt-list"
              >
                <div
                  v-for="receipt in selectedInstance.ccRecords"
                  :key="receipt.id"
                  class="cc-receipt-item"
                >
                  <div>
                    <strong>{{ receipt.recipientUserName }}</strong>
                    <span>{{ receipt.nodeName || '抄送节点' }}</span>
                  </div>
                  <Tag :color="readStatusMeta(receipt).color">
                    {{ readStatusMeta(receipt).label }}
                  </Tag>
                  <small>
                    抄送 {{ formatTime(receipt.createdAt) }}
                    <template v-if="receipt.readAt">
                      / 阅读 {{ formatTime(receipt.readAt) }}
                    </template>
                  </small>
                </div>
              </div>
              <div v-else class="empty-hint">暂无抄送回执</div>
            </div>
          </div>
          <div>
            <h3>流转记录</h3>
            <Timeline>
              <TimelineItem
                v-for="log in selectedInstance.actionLogs"
                :key="log.id"
              >
                <div class="timeline-title">
                  {{ actionLabel(log.action) }}
                  <span>{{ formatTime(log.createdAt) }}</span>
                </div>
                <p>
                  {{ log.operatorUserName }}
                  <template v-if="log.nodeName"> / {{ log.nodeName }}</template>
                </p>
                <p v-if="log.comment">{{ log.comment }}</p>
              </TimelineItem>
            </Timeline>
            <div class="detail-section">
              <div class="section-title-row">
                <h3>评论协作</h3>
                <Tag>{{ selectedInstance.comments.length }} 条</Tag>
              </div>
              <Textarea
                v-model:value="commentContent"
                :auto-size="{ minRows: 3, maxRows: 6 }"
                placeholder="写下补充说明、处理建议或需要对方补充的信息"
              />
              <div class="comment-action-row">
                <Button
                  type="primary"
                  size="small"
                  :loading="commentSaving"
                  @click="submitWorkflowComment"
                >
                  发布评论
                </Button>
              </div>
              <div
                v-if="selectedInstance.comments.length > 0"
                class="comment-list"
              >
                <div
                  v-for="comment in selectedInstance.comments"
                  :key="comment.id"
                  class="comment-item"
                >
                  <div>
                    <strong>{{ comment.authorUserName }}</strong>
                    <span>{{ formatTime(comment.createdAt) }}</span>
                  </div>
                  <p>{{ comment.content }}</p>
                </div>
              </div>
              <div v-else class="empty-hint">暂无评论，来当第一位补充上下文的人。</div>
            </div>
          </div>
        </div>
      </div>
      <div v-else class="detail-loading">未找到流程详情</div>
    </Drawer>
  </Page>
</template>

<style scoped>
.workflow-page {
  display: flex;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 10px;
}

.workflow-header,
.query-bar,
.form-shell,
.table-shell {
  border-radius: 4px;
  background: hsl(var(--background));
}

.workflow-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
}

.workflow-header h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 650;
}

.workflow-header p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 13px;
}

.workflow-tabs {
  min-height: 0;
  flex: 1;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 0 12px 12px;
}

.query-bar {
  display: flex;
  min-height: 58px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 10px;
  padding: 10px 0;
}

.query-label {
  font-weight: 600;
  white-space: nowrap;
}

.binding-definition-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  line-height: 1.35;
}

.binding-definition-cell span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.cc-title-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
  line-height: 1.35;
}

.cc-title-cell span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.binding-switch-row {
  margin-bottom: 16px;
}

.query-input {
  width: 220px;
}

.query-select {
  width: 150px;
}

.form-shell {
  max-width: 920px;
  padding: 12px 4px;
}

.start-helper {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 24%);
  margin-bottom: 14px;
  padding: 12px 14px;
}

.start-helper h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 650;
}

.start-helper p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.5;
}

.dynamic-form-card {
  display: flex;
  flex-direction: column;
  gap: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 18%);
  margin-bottom: 16px;
  padding: 12px;
}

.dynamic-form-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.dynamic-form-head h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 650;
}

.dynamic-form-head p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.5;
}

.dynamic-form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 14px;
}

.table-shell {
  min-height: 0;
}

.table-action {
  height: 24px;
  padding: 0 8px;
  border-radius: 4px;
  background: transparent;
}

.table-action.success {
  border-color: #51c56c;
  color: #24933f;
}

.table-action.warning {
  border-color: #f0b64f;
  color: #b36b00;
}

.table-action.danger {
  border-color: #ff6b93;
  color: #ff3868;
}

.definition-workbench {
  display: grid;
  min-height: calc(100vh - 250px);
  grid-template-columns: 280px minmax(560px, 1fr) 320px;
  gap: 12px;
}

.definition-list-panel,
.definition-property-panel,
.designer-workspace {
  min-height: 0;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--background));
}

.definition-list-panel,
.definition-property-panel {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 12px;
}

.panel-title-row,
.designer-toolbar,
.node-property-head,
.definition-pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.panel-title-row h3,
.designer-toolbar h3,
.designer-palette h3,
.definition-property-panel h3,
.detail-grid h3 {
  margin: 0;
  font-size: 14px;
  font-weight: 650;
}

.panel-title-row p,
.designer-toolbar p,
.node-property-card p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.55;
}

.definition-filter {
  display: grid;
  gap: 8px;
}

.definition-list {
  display: flex;
  min-height: 220px;
  flex: 1;
  flex-direction: column;
  gap: 8px;
  overflow: auto;
}

.definition-list.loading {
  opacity: 0.68;
}

.definition-card {
  display: flex;
  width: 100%;
  flex-direction: column;
  gap: 6px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 22%);
  cursor: pointer;
  padding: 10px;
  text-align: left;
  transition:
    border-color 0.16s ease,
    background 0.16s ease,
    transform 0.16s ease;
}

.definition-card:hover,
.definition-card.active {
  border-color: #4096ff;
  background: #eef6ff;
}

.definition-card.active {
  transform: translateX(2px);
}

.definition-card-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  color: hsl(var(--foreground));
  font-size: 13px;
  font-weight: 650;
}

.definition-card-code,
.definition-card-meta {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.definition-pagination {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.designer-workspace {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.designer-toolbar {
  border-bottom: 1px solid hsl(var(--border));
  padding: 12px 14px;
}

.definition-readonly-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border-bottom: 1px solid #ffe7ba;
  background: #fff7e6;
  color: #ad6800;
  font-size: 12px;
  line-height: 1.55;
  padding: 10px 14px;
}

.definition-guide-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border-bottom: 1px solid #d6e4ff;
  background: #f0f7ff;
  color: #1f3157;
  padding: 10px 14px;
}

.definition-guide-banner div {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 2px;
}

.definition-guide-banner strong {
  font-size: 13px;
  font-weight: 700;
}

.definition-guide-banner span {
  color: #4c5f7d;
  font-size: 12px;
}

.designer-toolbar p span {
  margin: 0 6px;
  color: hsl(var(--border));
}

.designer-main {
  display: grid;
  min-height: 620px;
  flex: 1;
  grid-template-columns: 180px minmax(0, 1fr);
}

.designer-palette {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border-right: 1px solid hsl(var(--border));
  background: hsl(var(--muted) / 28%);
  padding: 12px;
}

.node-palette-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--background));
  cursor: pointer;
  padding: 9px 10px;
  text-align: left;
}

.node-palette-item:hover {
  border-color: #4096ff;
}

.node-palette-item span {
  font-size: 13px;
  font-weight: 650;
}

.node-palette-item small {
  color: hsl(var(--muted-foreground));
  font-size: 11px;
  line-height: 1.4;
}

.designer-palette-tip {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.6;
  margin: 4px 0 0;
}

.designer-canvas {
  min-height: 620px;
  background:
    linear-gradient(hsl(var(--border) / 38%) 1px, transparent 1px),
    linear-gradient(90deg, hsl(var(--border) / 38%) 1px, transparent 1px);
  background-color: #fbfcfe;
  background-size: 20px 20px;
}

.definition-property-panel {
  overflow: auto;
}

.guide-focus {
  position: relative;
  outline: 3px solid rgb(22 119 255 / 20%);
  outline-offset: 2px;
}

.node-property-card {
  border-top: 1px solid hsl(var(--border));
  padding-top: 12px;
}

.form-schema-card {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.empty-hint {
  border: 1px dashed hsl(var(--border));
  border-radius: 6px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.6;
  padding: 10px;
}

.field-hint {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.hidden-file-input {
  display: none;
}

.attachment-uploader,
.detail-section {
  display: grid;
  gap: 10px;
}

.detail-section {
  margin-top: 16px;
}

.section-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
}

.section-title-row h3 {
  margin: 0;
}

.attachment-list,
.comment-list {
  display: grid;
  gap: 8px;
}

.attachment-list.compact {
  margin-top: 10px;
}

.attachment-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 18%);
  padding: 9px 10px;
}

.attachment-item > div {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.attachment-item strong {
  overflow: hidden;
  color: hsl(var(--foreground));
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.attachment-item span,
.attachment-item small {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.comment-action-row {
  display: flex;
  justify-content: flex-end;
}

.comment-item {
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--background));
  padding: 10px;
}

.comment-item > div {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 6px;
}

.comment-item strong {
  font-size: 13px;
}

.comment-item span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.comment-item p {
  margin: 0;
  color: hsl(var(--foreground));
  font-size: 13px;
  line-height: 1.65;
  white-space: pre-wrap;
}

.form-field-card {
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 18%);
  padding: 10px;
}

.form-field-card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 10px;
}

.form-field-card-head strong {
  min-width: 0;
  overflow: hidden;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.node-validation-hint {
  border: 1px solid #ffd591;
  border-radius: 6px;
  background: #fff7e6;
  color: #ad6800;
  font-size: 12px;
  line-height: 1.5;
  margin: 10px 0 12px;
  padding: 8px 10px;
}

.edge-help {
  border: 1px solid #d6e4ff;
  border-radius: 6px;
  background: #f0f5ff;
  color: #1d39c4;
  font-size: 12px;
  line-height: 1.5;
  margin: 8px 0 12px;
  padding: 8px 10px;
}

.field-help {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.5;
  margin-top: 6px;
}

.property-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

:deep(.vue-flow__node) {
  border: 0;
  background: transparent;
  box-shadow: none;
}

.flow-node {
  position: relative;
  display: flex;
  min-width: 136px;
  flex-direction: column;
  gap: 4px;
  border: 1px solid #b7c7d9;
  border-radius: 8px;
  background: #fff;
  box-shadow: 0 10px 24px rgb(15 23 42 / 10%);
  color: #172033;
  padding: 10px 14px;
  text-align: left;
}

.flow-node strong {
  max-width: 160px;
  overflow: hidden;
  font-size: 13px;
  font-weight: 700;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.flow-node span {
  color: #6b7280;
  font-size: 11px;
}

.flow-node.selected {
  border-color: #1677ff;
  box-shadow: 0 0 0 3px rgb(22 119 255 / 14%);
}

.flow-node-start {
  border-color: #6ac58d;
  background: #f1fff6;
}

.flow-node-approve {
  border-color: #6aa9ff;
  background: #f5f9ff;
}

.flow-node-condition {
  border-color: #f2bd5b;
  background: #fffaf0;
}

.flow-node-cc {
  border-color: #8b9bf5;
  background: #f7f7ff;
}

.flow-node-end {
  border-color: #ff9c9c;
  background: #fff6f6;
}

:deep(.vue-flow__edge.selected .vue-flow__edge-path) {
  stroke: #1677ff;
  stroke-width: 3;
}

.json-editor,
pre {
  font-family: Consolas, Monaco, monospace;
}

.detail-layout {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.form-data-list {
  display: grid;
  gap: 8px;
  margin-bottom: 12px;
}

.form-data-list div {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr);
  gap: 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 18%);
  padding: 8px 10px;
}

.form-data-list span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.form-data-list strong {
  min-width: 0;
  overflow-wrap: anywhere;
  font-size: 12px;
  font-weight: 600;
}

.json-preview-title {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  font-weight: 650;
  margin: 0 0 6px;
}

.detail-loading {
  display: flex;
  min-height: 220px;
  align-items: center;
  justify-content: center;
  color: hsl(var(--muted-foreground));
}

.detail-action-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 24%);
  padding: 12px;
}

.detail-action-bar > div {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 4px;
}

.detail-action-bar strong {
  font-size: 15px;
}

.detail-action-bar span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.guide-panel {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.guide-summary,
.guide-checklist {
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 24%);
  padding: 12px;
}

.guide-summary h3,
.guide-checklist h4,
.guide-step h4 {
  margin: 0;
  font-size: 14px;
  font-weight: 650;
}

.guide-summary p,
.guide-step p {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.7;
  margin: 6px 0 0;
}

.guide-steps {
  display: grid;
  gap: 10px;
}

.guide-step {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr);
  gap: 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  padding: 12px;
}

.guide-step > span {
  display: inline-flex;
  width: 24px;
  height: 24px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: #1677ff;
  color: #fff;
  font-size: 12px;
  font-weight: 700;
}

.guide-step pre {
  max-height: 180px;
  margin-bottom: 8px;
}

.guide-checklist ul {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.8;
  margin: 8px 0 0;
  padding-left: 18px;
}

.definition-guide-modal {
  display: grid;
  grid-template-columns: 220px minmax(0, 1fr);
  gap: 18px;
}

.definition-guide-steps {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.definition-guide-step {
  display: grid;
  grid-template-columns: 28px minmax(0, 1fr);
  gap: 4px 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--background));
  cursor: pointer;
  padding: 10px;
  text-align: left;
}

.definition-guide-step.active {
  border-color: #1677ff;
  background: #eef6ff;
}

.definition-guide-step span {
  display: inline-flex;
  width: 24px;
  height: 24px;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  background: hsl(var(--muted));
  color: hsl(var(--foreground));
  font-size: 12px;
  font-weight: 700;
  grid-row: span 2;
}

.definition-guide-step.active span {
  background: #1677ff;
  color: #fff;
}

.definition-guide-step strong {
  color: hsl(var(--foreground));
  font-size: 13px;
}

.definition-guide-step small {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.45;
}

.definition-guide-content {
  min-height: 390px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted) / 18%);
  padding: 18px;
}

.definition-guide-content h3 {
  margin: 0;
  font-size: 17px;
  font-weight: 700;
}

.definition-guide-content p {
  color: hsl(var(--muted-foreground));
  font-size: 13px;
  line-height: 1.8;
  margin: 10px 0 0;
}

.guide-example-map,
.guide-node-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 16px;
}

.guide-example-map span,
.guide-node-pill {
  border: 1px solid #d6e4ff;
  border-radius: 999px;
  background: #fff;
  color: #1f3157;
  font-size: 12px;
  padding: 6px 10px;
}

.guide-node-pill.start {
  border-color: #b7ebc6;
  background: #f6ffed;
}

.guide-node-pill.approve {
  border-color: #bae0ff;
  background: #f0f7ff;
}

.guide-node-pill.condition {
  border-color: #ffe7ba;
  background: #fff7e6;
}

.guide-node-pill.cc {
  border-color: #d6d9ff;
  background: #f7f7ff;
}

.guide-node-pill.end {
  border-color: #ffd6d6;
  background: #fff6f6;
}

.guide-check-grid {
  display: grid;
  grid-template-columns: 120px minmax(0, 1fr);
  gap: 10px 14px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: #fff;
  margin-top: 16px;
  padding: 12px;
}

.guide-check-grid span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.guide-check-grid strong {
  color: hsl(var(--foreground));
  font-size: 13px;
}

.guide-warning {
  border: 1px solid #ffd591;
  border-radius: 6px;
  background: #fff7e6;
  color: #ad6800;
  font-size: 12px;
  line-height: 1.6;
  margin-top: 12px;
  padding: 10px 12px;
}

.guide-action-row {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 18px;
}

.detail-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(320px, 0.8fr);
  gap: 18px;
}

.task-list {
  display: grid;
  gap: 8px;
  margin-top: 10px;
}

.task-list-item {
  display: grid;
  align-items: center;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 6px 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  padding: 10px;
}

.task-list-item :deep(.ant-tag) {
  justify-self: center;
  margin-inline-end: 0;
}

.task-list-item .task-status-group {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  flex-direction: row;
  gap: 6px;
}

.task-list-item-highlighted {
  border-color: #1677ff;
  background: #e6f4ff;
  box-shadow: 0 0 0 1px rgb(22 119 255 / 18%);
}

.task-list-item div:not(.task-status-group) {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 3px;
}

.task-list-item span,
.task-list-item small,
.task-list-item p {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.task-list-item p {
  grid-column: 1 / -1;
  margin: 0;
}

.cc-receipt-list {
  display: grid;
  gap: 8px;
}

.cc-receipt-item {
  display: grid;
  align-items: center;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 6px 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  background: hsl(var(--muted) / 18%);
  padding: 9px 10px;
}

.cc-receipt-item div {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 3px;
}

.cc-receipt-item span,
.cc-receipt-item small {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.cc-receipt-item small {
  grid-column: 1 / -1;
}

pre {
  max-height: 360px;
  overflow: auto;
  border-radius: 4px;
  background: hsl(var(--muted));
  margin: 10px 0 0;
  padding: 12px;
  white-space: pre-wrap;
}

.timeline-title {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-weight: 600;
}

.timeline-title span {
  color: hsl(var(--muted-foreground));
  font-weight: 400;
}

:deep(.ant-table) {
  font-size: 13px;
}

@media (max-width: 1000px) {
  .workflow-header,
  .query-bar {
    align-items: flex-start;
    flex-direction: column;
  }

  .definition-workbench,
  .designer-main,
  .definition-guide-modal,
  .dynamic-form-grid,
  .detail-grid {
    grid-template-columns: 1fr;
  }

  .designer-palette {
    border: 0;
  }
}
</style>
