<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Checkbox,
  Drawer,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
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
  cleanupCodeGeneratorArtifactApi,
  generateCodeApi,
  getCodeGeneratorArtifactsApi,
  getCodeGenerationHistoryApi,
  getCodeGenerationHistoryDetailApi,
  getCodeGeneratorTableApi,
  getCodeGeneratorTablesApi,
  previewCodeGeneratorApi,
  registerCodeGeneratorArtifactHistoryApi,
  rollbackCodeGenerationHistoryApi,
  type CodeGeneratorArtifactGovernance,
  type CodeGenerationHistoryDetail,
  type CodeGenerationHistory,
  type CodeGeneratorColumn,
  type CodeGeneratorFieldConfig,
  type CodeGeneratorInstallPlan,
  type CodeGeneratorPreviewFile,
  type CodeGeneratorPreviewRequest,
  type CodeGeneratorTable,
} from '#/api/system/code-generator';

const { hasAccessByCodes } = useAccess();
const router = useRouter();
const tableLoading = ref(false);
const previewLoading = ref(false);
const generating = ref(false);
const historyLoading = ref(false);
const historyDetailLoading = ref(false);
const historyDetailOpen = ref(false);
const workflowGuideOpen = ref(false);
const artifactLoading = ref(false);
const artifactActionModule = ref('');
const rollbackingId = ref('');
const rollbackModalOpen = ref(false);
const rollbackTarget = ref<CodeGenerationHistory | CodeGenerationHistoryDetail>();
const lastGeneratedHistory = ref<CodeGenerationHistory>();
const rollbackDropTable = ref(false);
const activeTab = ref('builder');
const tables = ref<CodeGeneratorTable[]>([]);
const tableFieldSelections = ref<TableFieldSelection[]>([]);
const fields = ref<CodeGeneratorFieldConfig[]>([]);
const previewFiles = ref<CodeGeneratorPreviewFile[]>([]);
const installPlan = ref<CodeGeneratorInstallPlan>();
const histories = ref<CodeGenerationHistory[]>([]);
const artifacts = ref<CodeGeneratorArtifactGovernance[]>([]);
const historyDetail = ref<CodeGenerationHistoryDetail>();
const historyTotal = ref(0);
const selectedPreviewFile = ref<CodeGeneratorPreviewFile>();
const selectedTableName = ref('');
const autoInstall = ref(true);
const historyQuery = reactive({
  moduleName: '',
  page: 1,
  pageSize: 6,
});
const formState = reactive({
  businessName: '',
  dataScopeField: undefined as string | undefined,
  dataScopeMode: 'None',
  enableAudit: true,
  enableImportExport: true,
  enableWorkflow: false,
  moduleName: '',
  permissionPrefix: '',
  routePath: '',
  tableName: '',
  tenantMode: 'Tenant',
  workflowBusinessType: '',
});

interface TableFieldSelection extends CodeGeneratorColumn {
  selected: boolean;
  systemField: boolean;
}

const tableColumns = [
  { dataIndex: 'tableName', title: '表名' },
  { dataIndex: 'existingModule', title: '状态', width: 90 },
  { dataIndex: 'tableComment', title: '说明' },
];

const tableFieldColumns = [
  { dataIndex: 'selected', title: '生成', width: 70 },
  { dataIndex: 'columnName', title: '列名', width: 170 },
  { dataIndex: 'columnType', title: '数据库类型', width: 150 },
  { dataIndex: 'dotNetType', title: '.NET', width: 110 },
  { dataIndex: 'tsType', title: 'TS', width: 90 },
  { dataIndex: 'meta', title: '属性', width: 130 },
  { dataIndex: 'columnComment', title: '说明' },
];

const fieldColumns = [
  { dataIndex: 'sort', title: '序号', width: 72 },
  { dataIndex: 'columnName', title: '列名', width: 170 },
  { dataIndex: 'propertyName', title: '属性名', width: 170 },
  { dataIndex: 'displayName', title: '显示名', width: 160 },
  { dataIndex: 'dotNetType', title: '.NET', width: 120 },
  { dataIndex: 'controlType', title: '控件', width: 130 },
  { dataIndex: 'queryMode', title: '查询方式', width: 130 },
  { dataIndex: 'maxLength', title: '长度', width: 100 },
  { dataIndex: 'dictionaryCode', title: '字典', width: 150 },
  { dataIndex: 'defaultValue', title: '默认值', width: 150 },
  { dataIndex: 'flags', title: '生成位置', width: 320 },
];

const previewColumns = [
  { dataIndex: 'relativePath', title: '文件路径' },
  { dataIndex: 'state', title: '状态', width: 90 },
];

const historyColumns = [
  { dataIndex: 'moduleName', title: '模块', width: 150 },
  { dataIndex: 'tableName', title: '表名', width: 180 },
  { dataIndex: 'status', title: '状态', width: 90 },
  { dataIndex: 'createdAt', title: '生成时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 150 },
];

const artifactColumns = [
  { dataIndex: 'moduleName', title: '模块', width: 160 },
  { dataIndex: 'tableName', title: '表名', width: 190 },
  { dataIndex: 'state', title: '状态', width: 210 },
  { dataIndex: 'riskReason', title: '风险说明' },
  { dataIndex: 'files', title: '文件', width: 90 },
  { dataIndex: 'action', title: '操作', width: 190 },
];

const historyPagination = computed<TablePaginationConfig>(() => ({
  current: historyQuery.page,
  pageSize: historyQuery.pageSize,
  showSizeChanger: false,
  total: historyTotal.value,
}));
const tableOptions = computed(() =>
  tables.value.map((table) => ({
    label: `${table.existingModule ? '[已映射] ' : ''}${
      table.tableComment
      ? `${table.tableName} - ${table.tableComment}`
      : table.tableName
    }`,
    value: table.tableName,
  })),
);
const selectedTable = computed(() =>
  tables.value.find((table) => table.tableName === selectedTableName.value),
);
const selectedExistingModule = computed(
  () => selectedTable.value?.existingModule ?? undefined,
);
const selectedGenerationBlockReason = computed(
  () => selectedTable.value?.generationBlockReason ?? undefined,
);
const dataScopeFieldOptions = computed(() =>
  fields.value
    .filter((field) => !field.isPrimaryKey)
    .map((field) => ({
      label: `${field.propertyName} - ${field.displayName}`,
      value: field.propertyName,
    })),
);
const enterpriseTemplateTags = computed(() => [
  formState.tenantMode === 'Tenant' ? '租户隔离' : '平台/共享数据',
  formState.dataScopeMode === 'None'
    ? '未启用数据权限'
    : `数据权限：${formState.dataScopeMode}`,
  formState.enableAudit ? '统一审计' : '不提示审计',
  formState.enableImportExport ? '导入导出' : '基础 CRUD',
  formState.enableWorkflow ? '审批接入' : '未接入审批',
]);
const generatedTargets = computed(() => ({
  api: formState.routePath
    ? `frontend/vue-vben-admin/apps/web-antd/src/api${formState.routePath}.ts`
    : '-',
  view: formState.routePath
    ? `frontend/vue-vben-admin/apps/web-antd/src/views${formState.routePath}/index.vue`
    : '-',
  backend: formState.moduleName
    ? `src/MiniAdmin.*/*${formState.moduleName}*`
    : '-',
}));

const canPreview = computed(() =>
  hasAccessByCodes(['system:code-generator:preview']),
);
const canGenerate = computed(() =>
  hasAccessByCodes(['system:code-generator:generate']),
);
const canRollback = computed(() =>
  canGenerate.value || hasAccessByCodes(['system:code-generator:rollback']),
);
const hasConflicts = computed(() =>
  previewFiles.value.some((file) => file.hasConflict),
);
const hasExistingModule = computed(() => Boolean(selectedExistingModule.value));
const hasGenerationBlock = computed(
  () => hasExistingModule.value || Boolean(selectedGenerationBlockReason.value),
);
const conflictFileCount = computed(
  () => previewFiles.value.filter((file) => file.hasConflict).length,
);
const permissionCodes = computed(() => [
  `${formState.permissionPrefix}:query`,
  `${formState.permissionPrefix}:create`,
  `${formState.permissionPrefix}:update`,
  `${formState.permissionPrefix}:delete`,
  ...(formState.enableImportExport
    ? [
        `${formState.permissionPrefix}:import`,
        `${formState.permissionPrefix}:export`,
      ]
    : []),
  ...(formState.enableWorkflow
    ? [
        `${formState.permissionPrefix}:submit-workflow`,
        `${formState.permissionPrefix}:withdraw-workflow`,
      ]
    : []),
]);
const selectedTableFieldCount = computed(
  () => tableFieldSelections.value.filter((field) => field.selected).length,
);
const historyDetailFileCount = computed(
  () => historyDetail.value?.files.length ?? 0,
);
const historyDetailConflictCount = computed(
  () => historyDetail.value?.files.filter((file) => file.hasConflict).length ?? 0,
);
const historyDetailRoutePath = computed(
  () => historyDetail.value?.preview.routePath || '-',
);
const rollbackOkButtonProps = computed(() => ({
  disabled:
    rollbackTarget.value?.status === 'RolledBack' && !rollbackDropTable.value,
}));
const selectableTableFields = computed(() =>
  tableFieldSelections.value.filter((field) => !field.systemField),
);

function createDefaultField(): CodeGeneratorFieldConfig {
  return {
    columnName: 'order_name',
    controlType: 'Input',
    createVisible: true,
    dictionaryCode: null,
    displayName: '订单名称',
    dotNetType: 'string',
    defaultValue: null,
    isPrimaryKey: false,
    isRequired: true,
    isUnique: false,
    listVisible: true,
    maxLength: 80,
    propertyName: 'OrderName',
    queryMode: 'Contains',
    queryVisible: true,
    sort: 1,
    tsType: 'string',
    updateVisible: true,
  };
}

function createFieldFromColumn(column: CodeGeneratorColumn): CodeGeneratorFieldConfig {
  return {
    columnName: column.columnName,
    controlType: getDefaultControlType(column),
    createVisible: !column.isPrimaryKey,
    dictionaryCode: null,
    displayName: column.columnComment || column.columnName,
    dotNetType: column.dotNetType,
    defaultValue: null,
    isPrimaryKey: column.isPrimaryKey,
    isRequired: !column.isNullable,
    isUnique: false,
    listVisible: !column.isPrimaryKey,
    maxLength: column.tsType === 'string' ? getColumnMaxLength(column) : null,
    propertyName: toPascalCase(column.columnName),
    queryMode: getDefaultQueryMode(column),
    queryVisible: !column.isPrimaryKey,
    sort: column.sort,
    tsType: column.tsType,
    updateVisible: !column.isPrimaryKey,
  };
}

function buildRequest(): CodeGeneratorPreviewRequest {
  return {
    businessName: formState.businessName,
    dataScopeField: formState.dataScopeField ?? null,
    dataScopeMode: formState.dataScopeMode,
    enableAudit: formState.enableAudit,
    enableImportExport: formState.enableImportExport,
    enableWorkflow: formState.enableWorkflow,
    fields: fields.value,
    moduleName: formState.moduleName,
    parentMenuId: null,
    permissionPrefix: formState.permissionPrefix,
    routePath: formState.routePath,
    tableName: formState.tableName,
    tenantMode: formState.tenantMode,
    workflowBusinessType: formState.workflowBusinessType || null,
  };
}

async function loadTables() {
  tableLoading.value = true;
  try {
    tables.value = await getCodeGeneratorTablesApi();
  } finally {
    tableLoading.value = false;
  }
}

async function selectTable(record: CodeGeneratorTable) {
  selectedTableName.value = record.tableName;
  formState.tableName = record.tableName;
  applyNamingFromTable(record);
  const table = await getCodeGeneratorTableApi(record.tableName);
  formState.businessName = table.tableComment || record.tableName;
  tableFieldSelections.value = table.columns.map((column) => {
    const systemField = isSystemColumn(column);
    return {
      ...column,
      selected: !systemField,
      systemField,
    };
  });
  fields.value = tableFieldSelections.value
    .filter((column) => column.selected)
    .map(createFieldFromColumn);
  syncDataScopeField();
  clearPreviewState();
}

async function handleTableNameChange(value: unknown) {
  const tableName = typeof value === 'string' ? value : '';
  const table = tables.value.find((item) => item.tableName === tableName);
  if (table) {
    await selectTable(table);
    return;
  }

  formState.tableName = tableName;
  selectedTableName.value = '';
  tableFieldSelections.value = [];
  clearPreviewState();
}

function createTableRow(record: CodeGeneratorTable) {
  return {
    onClick: () => {
      void selectTable(record);
    },
  };
}

function createPreviewFileRow(record: CodeGeneratorPreviewFile) {
  return {
    onClick: () => {
      selectedPreviewFile.value = record;
    },
  };
}

function clearPreviewState() {
  previewFiles.value = [];
  installPlan.value = undefined;
  selectedPreviewFile.value = undefined;
}

function addField() {
  fields.value = [
    ...fields.value,
    {
      ...createDefaultField(),
      columnName: `custom_column_${fields.value.length + 1}`,
      displayName: `自定义字段${fields.value.length + 1}`,
      propertyName: `CustomField${fields.value.length + 1}`,
      sort: getNextFieldSort(),
    },
  ];
}

function removeField(index: number) {
  const removedField = fields.value[index];
  fields.value = fields.value.filter((_, currentIndex) => currentIndex !== index);
  if (!removedField) {
    return;
  }

  tableFieldSelections.value = tableFieldSelections.value.map((field) =>
    field.columnName === removedField.columnName
      ? {
          ...field,
          selected: false,
        }
      : field,
  );
}

function toggleTableField(record: TableFieldSelection, checked: boolean) {
  tableFieldSelections.value = tableFieldSelections.value.map((field) =>
    field.columnName === record.columnName
      ? {
          ...field,
          selected: checked,
        }
      : field,
  );

  if (checked) {
    if (!fields.value.some((field) => field.columnName === record.columnName)) {
      fields.value = [...fields.value, createFieldFromColumn(record)].sort(
        (left, right) => left.sort - right.sort,
      );
    }
    return;
  }

  fields.value = fields.value.filter(
    (field) => field.columnName !== record.columnName,
  );
}

function handleTableFieldSelectionChange(
  record: Record<string, any> | TableFieldSelection,
  event: { target?: { checked?: boolean } },
) {
  toggleTableField(record as TableFieldSelection, Boolean(event.target?.checked));
}

function selectAllTableFields(checked: boolean) {
  const selectableNames = new Set(
    selectableTableFields.value.map((field) => field.columnName),
  );

  tableFieldSelections.value = tableFieldSelections.value.map((field) =>
    selectableNames.has(field.columnName)
      ? {
          ...field,
          selected: checked,
        }
      : field,
  );

  if (checked) {
    const existingNames = new Set(fields.value.map((field) => field.columnName));
    const newFields = selectableTableFields.value
      .filter((field) => !existingNames.has(field.columnName))
      .map(createFieldFromColumn);
    fields.value = [...fields.value, ...newFields].sort(
      (left, right) => left.sort - right.sort,
    );
    return;
  }

  fields.value = fields.value.filter(
    (field) => !selectableNames.has(field.columnName),
  );
}

async function previewCode() {
  if (selectedGenerationBlockReason.value) {
    message.warning(selectedGenerationBlockReason.value);
    return;
  }

  if (hasExistingModule.value) {
    message.warning('当前数据表已有关联模块，请维护已有功能或换一张业务表');
    return;
  }

  if (!formState.moduleName.trim() || fields.value.length === 0) {
    message.warning('请填写模块信息和字段配置');
    return;
  }

  if (formState.enableWorkflow && !formState.workflowBusinessType.trim()) {
    message.warning('启用审批时请填写业务类型编码');
    return;
  }

  previewLoading.value = true;
  try {
    const result = await previewCodeGeneratorApi(buildRequest());
    previewFiles.value = result.files;
    installPlan.value = result.installPlan;
    selectedPreviewFile.value = result.files[0];
    message.success(
      result.hasConflicts
        ? `预览完成，存在 ${conflictFileCount.value} 个冲突文件`
        : '预览完成',
    );
  } finally {
    previewLoading.value = false;
  }
}

async function generateCode() {
  if (selectedGenerationBlockReason.value) {
    message.warning(selectedGenerationBlockReason.value);
    return;
  }

  if (hasExistingModule.value) {
    message.warning('当前数据表已有关联模块，不能重复生成');
    return;
  }

  if (previewFiles.value.length === 0) {
    await previewCode();
    if (previewFiles.value.length === 0) {
      return;
    }
  }

  if (hasConflicts.value) {
    message.warning('存在冲突文件，当前默认不覆盖');
    return;
  }

  generating.value = true;
  try {
    const history = await generateCodeApi(buildRequest(), false, autoInstall.value);
    lastGeneratedHistory.value = history;
    message.success(
      autoInstall.value
        ? '代码已生成，数据库表和菜单权限已自动安装。前端开发服务可能自动刷新，重启后端后加载新增接口'
        : '代码已生成，请按安装指引手工安装数据库表和菜单权限。前端开发服务可能自动刷新',
    );
    historyQuery.moduleName = history.moduleName;
    historyQuery.page = 1;
    await loadHistory();
    activeTab.value = 'history';
    if (formState.enableWorkflow) {
      workflowGuideOpen.value = true;
    }
  } finally {
    generating.value = false;
  }
}

async function loadHistory() {
  historyLoading.value = true;
  try {
    const result = await getCodeGenerationHistoryApi({
      moduleName: historyQuery.moduleName || undefined,
      page: historyQuery.page,
      pageSize: historyQuery.pageSize,
    });
    histories.value = result.items;
    historyTotal.value = result.total;
  } finally {
    historyLoading.value = false;
  }
}

async function loadArtifacts() {
  artifactLoading.value = true;
  try {
    const result = await getCodeGeneratorArtifactsApi();
    artifacts.value = result.items;
  } finally {
    artifactLoading.value = false;
  }
}

async function openHistoryDetail(record: CodeGenerationHistory | Record<string, any>) {
  const currentRecord = record as CodeGenerationHistory;
  historyDetailOpen.value = true;
  historyDetailLoading.value = true;
  try {
    historyDetail.value = await getCodeGenerationHistoryDetailApi(currentRecord.id);
  } finally {
    historyDetailLoading.value = false;
  }
}

function openRollbackModal(
  record: CodeGenerationHistory | CodeGenerationHistoryDetail | Record<string, any>,
) {
  rollbackTarget.value = record as CodeGenerationHistory | CodeGenerationHistoryDetail;
  rollbackDropTable.value = false;
  rollbackModalOpen.value = true;
}

async function confirmRollback() {
  if (!rollbackTarget.value) {
    return;
  }

  if (rollbackTarget.value.status === 'RolledBack' && !rollbackDropTable.value) {
    message.warning('已回滚记录只能继续清理业务表，请先勾选删除业务表和数据');
    return;
  }

  await rollbackHistory(rollbackTarget.value, rollbackDropTable.value);
  rollbackModalOpen.value = false;
}

async function rollbackHistory(
  record: CodeGenerationHistory | CodeGenerationHistoryDetail,
  dropTable: boolean,
) {
  rollbackingId.value = record.id;
  try {
    const result = await rollbackCodeGenerationHistoryApi(record.id, {
      dropTable,
    });
    const tableMessage = result.tableDropMessage
      ? `，${result.tableDropMessage}`
      : '';
    const actionMessage =
      record.status === 'RolledBack'
        ? `已处理业务表清理${tableMessage}`
        : `已回滚，删除 ${result.deletedFileCount} 个文件、${result.deletedMenuCount} 个菜单权限${tableMessage}`;
    message.success(actionMessage);
    await loadHistory();
    if (historyDetail.value?.id === record.id) {
      historyDetail.value = await getCodeGenerationHistoryDetailApi(record.id);
    }
  } finally {
    rollbackingId.value = '';
  }
}

async function registerArtifactHistory(record: CodeGeneratorArtifactGovernance | Record<string, any>) {
  const artifact = record as CodeGeneratorArtifactGovernance;
  artifactActionModule.value = artifact.moduleName;
  try {
    await registerCodeGeneratorArtifactHistoryApi(artifact.moduleName);
    message.success(`已为 ${artifact.moduleName} 补登记生成历史`);
    await Promise.all([loadArtifacts(), loadHistory()]);
    activeTab.value = 'history';
    historyQuery.moduleName = artifact.moduleName;
    historyQuery.page = 1;
    await loadHistory();
  } finally {
    artifactActionModule.value = '';
  }
}

function cleanupArtifact(record: CodeGeneratorArtifactGovernance | Record<string, any>) {
  const artifact = record as CodeGeneratorArtifactGovernance;
  Modal.confirm({
    cancelText: '取消',
    content: `将删除 ${artifact.moduleName} 的生成文件和菜单权限，默认保留业务表和数据。此操作适合清理没有生成历史的半生成产物。`,
    okText: '确认清理',
    okType: 'danger',
    title: `清理 ${artifact.moduleName}`,
    async onOk() {
      artifactActionModule.value = artifact.moduleName;
      try {
        const result = await cleanupCodeGeneratorArtifactApi(artifact.moduleName);
        message.success(
          `已清理 ${result.deletedFileCount} 个文件、${result.deletedMenuCount} 个菜单权限`,
        );
        await loadArtifacts();
      } finally {
        artifactActionModule.value = '';
      }
    },
  });
}

function handleHistoryChange(pagination: TablePaginationConfig) {
  historyQuery.page = pagination.current ?? 1;
  void loadHistory();
}

function formatRequestJson(record?: CodeGenerationHistoryDetail) {
  return record ? JSON.stringify(record.preview, null, 2) : '';
}

function getHistoryStatusColor(status: string) {
  if (status === 'Success') {
    return 'green';
  }

  if (status === 'RolledBack') {
    return 'blue';
  }

  if (status === 'Preview') {
    return 'blue';
  }

  return 'red';
}

function canShowRollbackAction(
  record: CodeGenerationHistory | CodeGenerationHistoryDetail | Record<string, any>,
) {
  const currentRecord = record as CodeGenerationHistory | CodeGenerationHistoryDetail;
  return (
    canRollback.value &&
    (currentRecord.status === 'Success' || currentRecord.status === 'RolledBack')
  );
}

function getRollbackActionText(
  record: CodeGenerationHistory | CodeGenerationHistoryDetail | Record<string, any>,
) {
  const currentRecord = record as CodeGenerationHistory | CodeGenerationHistoryDetail;
  return currentRecord.status === 'RolledBack' ? '清理表' : '回滚';
}

function toPascalCase(value: string) {
  return value
    .split(/[_-]/)
    .filter(Boolean)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join('');
}

function toKebabCase(value: string) {
  return value
    .replaceAll('_', '-')
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '')
    .toLowerCase();
}

function stripTablePrefix(value: string) {
  return value.replace(/^(biz|mini)_/i, '');
}

function applyNamingFromTable(record: CodeGeneratorTable) {
  const businessTableName = stripTablePrefix(record.tableName);
  const routeSegment = toKebabCase(businessTableName);

  formState.moduleName = toPascalCase(businessTableName);
  formState.routePath = `/business/${routeSegment}`;
  formState.permissionPrefix = `business:${routeSegment}`;
  formState.workflowBusinessType = routeSegment.replaceAll('-', '_');
}

function getDefaultControlType(column: CodeGeneratorColumn) {
  if (column.tsType === 'boolean') {
    return 'Switch';
  }

  if (column.tsType === 'number') {
    return 'InputNumber';
  }

  if (
    ['date', 'datetime', 'timestamp', 'time'].some((part) =>
      column.columnType.toLowerCase().includes(part),
    )
  ) {
    return 'DatePicker';
  }

  return 'Input';
}

function getDefaultQueryMode(column: CodeGeneratorColumn) {
  if (
    ['date', 'datetime', 'timestamp', 'time'].some((part) =>
      column.columnType.toLowerCase().includes(part),
    )
  ) {
    return 'Range';
  }

  return column.tsType === 'string' ? 'Contains' : 'Equals';
}

function getColumnMaxLength(column: CodeGeneratorColumn) {
  const matched = /\((\d+)\)/.exec(column.columnType);
  return matched?.[1] ? Number(matched[1]) : 256;
}

function getNextFieldSort() {
  return fields.value.length === 0
    ? 1
    : Math.max(...fields.value.map((field) => field.sort)) + 1;
}

function isSystemColumn(column: CodeGeneratorColumn) {
  const systemColumns = new Set([
    'id',
    'tenant_id',
    'workflow_instance_id',
    'approval_status',
    'created_at',
    'updated_at',
    'deleted_at',
    'create_time',
    'update_time',
    'create_by',
    'update_by',
    'is_deleted',
  ]);
  return column.isPrimaryKey || systemColumns.has(column.columnName.toLowerCase());
}

function syncDataScopeField() {
  if (formState.dataScopeMode === 'None') {
    formState.dataScopeField = undefined;
    return;
  }

  const preferredNames =
    formState.dataScopeMode === 'Department'
      ? ['DepartmentId', 'DeptId']
      : ['OwnerUserId', 'UserId', 'CreatedByUserId'];
  const preferred = fields.value.find((field) =>
    preferredNames.includes(field.propertyName),
  );
  formState.dataScopeField =
    preferred?.propertyName ?? dataScopeFieldOptions.value[0]?.value;
}

function getInstallStepColor(status: string) {
  if (status === 'Done') {
    return 'green';
  }

  if (status === 'Warning') {
    return 'orange';
  }

  return 'blue';
}

function getExistingModuleKindText(moduleKind?: string) {
  return moduleKind === 'Generated' ? '代码生成' : '系统内置';
}

function openExistingModuleRoute() {
  if (!selectedExistingModule.value?.routePath) {
    message.info('当前模块没有可直接跳转的前端路由');
    return;
  }

  void router.push(selectedExistingModule.value.routePath);
}

function openWorkflowCenter() {
  workflowGuideOpen.value = false;
  void router.push('/workflow/center');
}

watch(
  [
    () => formState.businessName,
    () => formState.moduleName,
    () => formState.permissionPrefix,
    () => formState.routePath,
    () => formState.tableName,
    () => formState.tenantMode,
    () => formState.dataScopeMode,
    () => formState.dataScopeField,
    () => formState.enableAudit,
    () => formState.enableImportExport,
    () => formState.enableWorkflow,
    () => formState.workflowBusinessType,
    fields,
  ],
  () => {
    clearPreviewState();
  },
  { deep: true },
);

watch(
  () => formState.dataScopeMode,
  (value) => {
    if (value === 'None') {
      formState.dataScopeField = undefined;
      return;
    }

    syncDataScopeField();
  },
);

onMounted(() => {
  fields.value = [createDefaultField()];
  void loadTables();
  void loadHistory();
  void loadArtifacts();
});
</script>

<template>
  <Page auto-content-height>
    <div class="code-generator-workspace">
      <Tabs v-model:active-key="activeTab" class="workspace-tabs">
        <Tabs.TabPane key="builder" tab="生成配置">
          <div class="builder-stack">
            <div class="config-band">
              <div class="config-heading">
                <div>
                  <h3>代码生成配置</h3>
                  <p>选择数据表后确认模块信息，再勾选字段并预览生成结果</p>
                </div>
                <Space>
                  <Space v-if="canGenerate" align="center" class="auto-install-toggle">
                    <Switch v-model:checked="autoInstall" size="small" />
                    <span>生成后自动安装数据库表和菜单权限</span>
                  </Space>
                  <Button
                    v-if="canPreview"
                    :disabled="hasGenerationBlock"
                    :loading="previewLoading"
                    type="primary"
                    @click="previewCode"
                  >
                    预览
                  </Button>
                  <Button
                    v-if="canGenerate"
                    :disabled="hasConflicts || hasGenerationBlock"
                    :loading="generating"
                    @click="generateCode"
                  >
                    生成
                  </Button>
                </Space>
              </div>
              <Form layout="vertical">
                <div class="config-grid">
                  <FormItem label="表名">
                    <Select
                      v-model:value="formState.tableName"
                      allow-clear
                      :loading="tableLoading"
                      placeholder="请选择或搜索数据表"
                      show-search
                      :filter-option="true"
                      :options="tableOptions"
                      @change="handleTableNameChange"
                    />
                  </FormItem>
                  <FormItem label="模块名">
                    <Input v-model:value="formState.moduleName" />
                  </FormItem>
                  <FormItem label="业务名称">
                    <Input v-model:value="formState.businessName" />
                  </FormItem>
                  <FormItem label="路由">
                    <Input v-model:value="formState.routePath" />
                  </FormItem>
                  <FormItem label="权限前缀">
                    <Input v-model:value="formState.permissionPrefix" />
                  </FormItem>
                  <FormItem label="租户模式">
                    <Select v-model:value="formState.tenantMode">
                      <Select.Option value="Tenant">租户隔离</Select.Option>
                      <Select.Option value="PlatformOnly">仅平台</Select.Option>
                      <Select.Option value="None">不隔离</Select.Option>
                    </Select>
                  </FormItem>
                  <FormItem label="数据权限">
                    <Select v-model:value="formState.dataScopeMode">
                      <Select.Option value="None">不启用</Select.Option>
                      <Select.Option value="Department">按部门字段</Select.Option>
                      <Select.Option value="Self">按用户字段</Select.Option>
                    </Select>
                  </FormItem>
                  <FormItem label="权限字段">
                    <Select
                      v-model:value="formState.dataScopeField"
                      allow-clear
                      :disabled="formState.dataScopeMode === 'None'"
                      :options="dataScopeFieldOptions"
                      placeholder="选择 DepartmentId 或 OwnerUserId"
                    />
                  </FormItem>
                  <FormItem label="审计提示">
                    <Switch
                      v-model:checked="formState.enableAudit"
                      checked-children="开启"
                      un-checked-children="关闭"
                    />
                  </FormItem>
                  <FormItem label="导入导出">
                    <Switch
                      v-model:checked="formState.enableImportExport"
                      checked-children="开启"
                      un-checked-children="关闭"
                    />
                  </FormItem>
                  <FormItem label="审批接入">
                    <Switch
                      v-model:checked="formState.enableWorkflow"
                      checked-children="开启"
                      un-checked-children="关闭"
                    />
                  </FormItem>
                  <FormItem label="业务类型">
                    <Input
                      v-model:value="formState.workflowBusinessType"
                      :disabled="!formState.enableWorkflow"
                      placeholder="例如 purchase_order"
                    />
                  </FormItem>
                </div>
              </Form>
        <div class="config-footer">
          <span>权限码</span>
          <div class="permission-row">
            <Tag v-for="code in permissionCodes" :key="code" color="blue">
              {{ code }}
            </Tag>
          </div>
        </div>
        <div class="config-footer">
          <span>企业模板</span>
          <div class="permission-row">
            <Tag v-for="tag in enterpriseTemplateTags" :key="tag" color="cyan">
              {{ tag }}
            </Tag>
          </div>
        </div>
        <div class="target-preview">
          <span>生成目标</span>
          <strong>{{ generatedTargets.api }}</strong>
          <strong>{{ generatedTargets.view }}</strong>
          <strong>{{ generatedTargets.backend }}</strong>
        </div>
        <div v-if="selectedExistingModule" class="existing-module-card">
          <div class="existing-module-main">
            <div>
              <span>已有模块</span>
              <strong>
                {{ selectedExistingModule.moduleName }}
                · {{ getExistingModuleKindText(selectedExistingModule.moduleKind) }}
              </strong>
              <small>
                数据表已经被当前系统实体映射，生成器不会重复覆盖。可以直接维护现有功能，或换一张业务表生成新模块。
              </small>
            </div>
            <Button
              size="small"
              :disabled="!selectedExistingModule.routePath"
              @click="openExistingModuleRoute"
            >
              打开页面
            </Button>
          </div>
          <div class="existing-module-meta">
            <Tag v-if="selectedExistingModule.routePath" color="blue">
              {{ selectedExistingModule.routePath }}
            </Tag>
            <Tag v-if="selectedExistingModule.component" color="cyan">
              {{ selectedExistingModule.component }}
            </Tag>
          </div>
          <div
            v-if="selectedExistingModule.files.length > 0"
            class="existing-module-files"
          >
            <span>关联文件</span>
            <code v-for="file in selectedExistingModule.files" :key="file">
              {{ file }}
            </code>
          </div>
        </div>
        <div
          v-else-if="selectedGenerationBlockReason"
          class="existing-module-card reserved-table-card"
        >
          <div class="existing-module-main">
            <div>
              <span>禁止生成</span>
              <strong>{{ formState.tableName }}</strong>
              <small>{{ selectedGenerationBlockReason }}</small>
            </div>
          </div>
        </div>
      </div>

            <div class="main-grid">
              <section class="panel table-panel">
                <div class="panel-header">
                  <h3>数据表</h3>
                  <Button size="small" @click="loadTables">刷新</Button>
                </div>
                <div v-if="selectedTableName" class="selected-table-card">
                  <span>当前表</span>
                  <strong>{{ selectedTableName }}</strong>
                  <small>{{ selectedTable?.tableComment || '暂无表说明' }}</small>
                </div>
                <Table
                  row-key="tableName"
                  bordered
                  size="small"
                  :columns="tableColumns"
                  :data-source="tables"
                  :loading="tableLoading"
                  :pagination="false"
                  :row-class-name="
                    (record) =>
                      record.tableName === selectedTableName ? 'selected-row' : ''
                  "
                  :custom-row="createTableRow"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.dataIndex === 'existingModule'">
                      <Tag
                        :color="
                          record.existingModule
                            ? 'orange'
                            : record.generationBlockReason
                              ? 'red'
                              : 'green'
                        "
                      >
                        {{
                          record.existingModule
                            ? '已映射'
                            : record.generationBlockReason
                              ? '禁用'
                              : '可生成'
                        }}
                      </Tag>
                    </template>
                  </template>
                </Table>
                <Empty v-if="!tableLoading && tables.length === 0" description="未读取到 MySQL 表，可先手工配置" />
              </section>

              <section class="panel field-panel">
                <div class="panel-header">
                  <h3>字段生成</h3>
                  <Space>
                    <Button size="small" @click="addField">新增自定义字段</Button>
                  </Space>
                </div>

                <div class="field-section">
                  <div class="field-section-header">
                    <div>
                      <h4>表字段选择</h4>
                      <span>
                        已选择 {{ selectedTableFieldCount }} 个字段，系统字段默认不参与生成
                      </span>
                    </div>
                    <Space>
                      <Button
                        :disabled="selectableTableFields.length === 0"
                        size="small"
                        @click="selectAllTableFields(true)"
                      >
                        全选业务字段
                      </Button>
                      <Button
                        :disabled="selectableTableFields.length === 0"
                        size="small"
                        @click="selectAllTableFields(false)"
                      >
                        取消全选
                      </Button>
                    </Space>
                  </div>
                  <Table
                    row-key="columnName"
                    bordered
                    size="small"
                    :columns="tableFieldColumns"
                    :data-source="tableFieldSelections"
                    :pagination="false"
                    :scroll="{ x: 960, y: 220 }"
                  >
                    <template #bodyCell="{ column, record }">
                      <template v-if="column.dataIndex === 'selected'">
                        <Checkbox
                          :checked="record.selected"
                          @change="(event) => handleTableFieldSelectionChange(record, event)"
                        />
                      </template>
                      <template v-if="column.dataIndex === 'meta'">
                        <Space wrap>
                          <Tag v-if="record.isPrimaryKey" color="gold">主键</Tag>
                          <Tag v-if="record.isNullable" color="default">可空</Tag>
                          <Tag v-if="record.systemField" color="blue">系统</Tag>
                        </Space>
                      </template>
                    </template>
                  </Table>
                  <Empty
                    v-if="tableFieldSelections.length === 0"
                    description="选择数据表后可勾选需要生成的字段"
                  />
                </div>

                <div class="field-section">
                  <div class="field-section-header compact">
                    <div>
                      <h4>字段配置</h4>
                      <span>这里只配置已勾选字段和自定义字段</span>
                    </div>
                  </div>
                <Table
                  row-key="sort"
                  bordered
                  size="small"
                  :columns="fieldColumns"
                  :data-source="fields"
                  :pagination="false"
                  :scroll="{ x: 1500 }"
                >
                  <template #bodyCell="{ column, index, record }">
                    <template v-if="column.dataIndex === 'sort'">
                      <InputNumber v-model:value="record.sort" class="mini-input" />
                    </template>
                    <template v-if="column.dataIndex === 'columnName'">
                      <Input v-model:value="record.columnName" />
                    </template>
                    <template v-if="column.dataIndex === 'propertyName'">
                      <Input v-model:value="record.propertyName" />
                    </template>
                    <template v-if="column.dataIndex === 'displayName'">
                      <Input v-model:value="record.displayName" />
                    </template>
                    <template v-if="column.dataIndex === 'dotNetType'">
                      <Input v-model:value="record.dotNetType" />
                    </template>
                    <template v-if="column.dataIndex === 'controlType'">
                      <Select v-model:value="record.controlType" class="control-select">
                        <Select.Option value="Input">输入框</Select.Option>
                        <Select.Option value="InputNumber">数字</Select.Option>
                        <Select.Option value="Select">下拉</Select.Option>
                        <Select.Option value="Switch">开关</Select.Option>
                        <Select.Option value="DatePicker">日期</Select.Option>
                        <Select.Option value="Textarea">多行文本</Select.Option>
                      </Select>
                    </template>
                    <template v-if="column.dataIndex === 'queryMode'">
                      <Select v-model:value="record.queryMode" class="control-select">
                        <Select.Option value="Contains">包含</Select.Option>
                        <Select.Option value="Equals">等于</Select.Option>
                        <Select.Option value="Range">范围</Select.Option>
                        <Select.Option value="None">不查询</Select.Option>
                      </Select>
                    </template>
                    <template v-if="column.dataIndex === 'maxLength'">
                      <InputNumber
                        v-model:value="record.maxLength"
                        :disabled="record.tsType !== 'string'"
                        class="length-input"
                        :min="1"
                      />
                    </template>
                    <template v-if="column.dataIndex === 'dictionaryCode'">
                      <Input
                        v-model:value="record.dictionaryCode"
                        allow-clear
                        placeholder="如 user_status"
                      />
                    </template>
                    <template v-if="column.dataIndex === 'defaultValue'">
                      <Input
                        v-model:value="record.defaultValue"
                        allow-clear
                        placeholder="可选"
                      />
                    </template>
                    <template v-if="column.dataIndex === 'flags'">
                      <Space wrap>
                        <Switch v-model:checked="record.listVisible" size="small" />
                        <span>列</span>
                        <Switch v-model:checked="record.queryVisible" size="small" />
                        <span>查</span>
                        <Switch v-model:checked="record.createVisible" size="small" />
                        <span>增</span>
                        <Switch v-model:checked="record.updateVisible" size="small" />
                        <span>改</span>
                        <Switch v-model:checked="record.isUnique" size="small" />
                        <span>唯一</span>
                        <Button danger size="small" type="link" @click="removeField(index)">
                          删除
                        </Button>
                      </Space>
                    </template>
                  </template>
                </Table>
                <Empty
                  v-if="fields.length === 0"
                  description="暂无生成字段，可在上方勾选字段或新增自定义字段"
                />
                </div>
              </section>

              <section class="panel preview-panel">
                <div class="panel-header">
                  <h3>生成预览</h3>
                  <span class="panel-hint">先预览文件，再确认生成</span>
                </div>
                <div v-if="installPlan" class="install-plan">
                  <div class="install-plan-main">
                    <div>
                      <h4>安装检查</h4>
                      <span>
                        {{
                          installPlan.tableExists
                            ? '目标数据表已存在，可直接生成代码'
                            : '目标数据表不存在，生成时可自动安装或手工执行脚本'
                        }}
                      </span>
                    </div>
                    <Tag :color="installPlan.tableExists ? 'green' : 'orange'">
                      {{ installPlan.tableExists ? '表已存在' : '需建表' }}
                    </Tag>
                  </div>
                  <div class="install-steps">
                    <div
                      v-for="step in installPlan.steps"
                      :key="step.key"
                      class="install-step"
                    >
                      <Tag :color="getInstallStepColor(step.status)">
                        {{ step.status }}
                      </Tag>
                      <div>
                        <strong>{{ step.title }}</strong>
                        <span>{{ step.description }}</span>
                      </div>
                    </div>
                  </div>
                  <div v-if="installPlan.createTableSql" class="sql-draft">
                    <div class="sql-draft-title">MySQL 建表草稿</div>
                    <Textarea
                      readonly
                      :rows="8"
                      :value="installPlan.createTableSql"
                    />
                  </div>
                </div>
                <Table
                  row-key="relativePath"
                  bordered
                  size="small"
                  :columns="previewColumns"
                  :data-source="previewFiles"
                  :pagination="false"
                  :custom-row="createPreviewFileRow"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.dataIndex === 'state'">
                      <Tag :color="record.hasConflict ? 'red' : 'green'">
                        {{ record.hasConflict ? '冲突' : '可写' }}
                      </Tag>
                    </template>
                  </template>
                </Table>
                <Textarea
                  class="code-preview"
                  readonly
                  :rows="12"
                  :value="selectedPreviewFile?.content || ''"
                />
              </section>
            </div>
          </div>
        </Tabs.TabPane>

        <Tabs.TabPane key="history" tab="生成记录">
          <div class="history-panel panel">
            <div class="panel-header">
              <div>
                <h3>生成记录</h3>
                <span class="panel-hint">共 {{ historyTotal }} 条，可查看生成文件和安装指引</span>
              </div>
              <Space>
                <Input
                  v-model:value="historyQuery.moduleName"
                  allow-clear
                  class="history-query"
                  placeholder="模块名"
                />
                <Button @click="loadHistory">查询</Button>
              </Space>
            </div>
            <Table
              row-key="id"
              bordered
              size="small"
              :columns="historyColumns"
              :data-source="histories"
              :loading="historyLoading"
              :pagination="historyPagination"
              @change="handleHistoryChange"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'status'">
                  <Tag :color="getHistoryStatusColor(record.status)">
                    {{ record.status }}
                  </Tag>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <Space size="small">
                    <Button size="small" type="link" @click="openHistoryDetail(record)">
                      详情
                    </Button>
                    <Button
                      v-if="canShowRollbackAction(record)"
                      danger
                      size="small"
                      type="link"
                      :loading="rollbackingId === record.id"
                      @click="openRollbackModal(record)"
                    >
                      {{ getRollbackActionText(record) }}
                    </Button>
                  </Space>
                </template>
              </template>
            </Table>
          </div>
        </Tabs.TabPane>

        <Tabs.TabPane key="artifacts" tab="产物治理">
          <div class="history-panel panel">
            <div class="panel-header">
              <div>
                <h3>产物治理</h3>
                <span class="panel-hint">
                  扫描已有生成文件，处理没有生成历史的半生成模块和误生成模块
                </span>
              </div>
              <Button :loading="artifactLoading" @click="loadArtifacts">
                刷新
              </Button>
            </div>
            <Table
              row-key="moduleName"
              bordered
              size="small"
              :columns="artifactColumns"
              :data-source="artifacts"
              :loading="artifactLoading"
              :pagination="false"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.dataIndex === 'state'">
                  <Space wrap>
                    <Tag :color="record.hasHistory ? 'green' : 'orange'">
                      {{ record.hasHistory ? '有历史' : '无历史' }}
                    </Tag>
                    <Tag :color="record.hasMenuPermissions ? 'green' : 'default'">
                      {{ record.hasMenuPermissions ? '有菜单' : '无菜单' }}
                    </Tag>
                    <Tag v-if="record.isReservedTable" color="red">
                      系统表
                    </Tag>
                  </Space>
                </template>
                <template v-if="column.dataIndex === 'riskReason'">
                  <span class="artifact-risk">
                    {{ record.riskReason || '正常' }}
                  </span>
                </template>
                <template v-if="column.dataIndex === 'files'">
                  <Tag color="blue">{{ record.files.length }} 个</Tag>
                </template>
                <template v-if="column.dataIndex === 'action'">
                  <Space size="small">
                    <Button
                      size="small"
                      type="link"
                      :disabled="record.hasHistory || record.isReservedTable"
                      :loading="artifactActionModule === record.moduleName"
                      @click="registerArtifactHistory(record)"
                    >
                      补登记
                    </Button>
                    <Button
                      danger
                      size="small"
                      type="link"
                      :disabled="record.hasHistory"
                      :loading="artifactActionModule === record.moduleName"
                      @click="cleanupArtifact(record)"
                    >
                      清理
                    </Button>
                  </Space>
                </template>
              </template>
            </Table>
            <Empty
              v-if="!artifactLoading && artifacts.length === 0"
              description="暂无生成产物"
            />
          </div>
        </Tabs.TabPane>
      </Tabs>

      <Modal
        v-model:open="rollbackModalOpen"
        title="回滚生成产物"
        :ok-text="rollbackTarget?.status === 'RolledBack' ? '清理业务表' : '确认回滚'"
        cancel-text="取消"
        :confirm-loading="rollbackingId === rollbackTarget?.id"
        :ok-button-props="rollbackOkButtonProps"
        @ok="confirmRollback"
      >
        <div class="rollback-confirm">
          <p v-if="rollbackTarget?.status === 'RolledBack'">
            本记录已经回滚过生成文件、菜单和按钮权限，这次只会继续清理业务表。
          </p>
          <p v-else>
            本次回滚会删除生成文件、菜单和按钮权限，并把生成记录标记为已回滚。
          </p>
          <Checkbox v-model:checked="rollbackDropTable">
            同时删除业务表和表内数据
          </Checkbox>
          <p v-if="rollbackDropTable" class="rollback-danger">
            将尝试删除表 {{ rollbackTarget?.tableName }}，表内数据不可恢复，请确认该表确实由本次生成创建。
          </p>
          <p v-else class="rollback-tip">
            默认保留业务表和业务数据，适合只撤回代码产物和菜单授权。
          </p>
        </div>
      </Modal>

      <Modal
        v-model:open="workflowGuideOpen"
        title="审批接入下一步"
        :footer="null"
      >
        <div class="workflow-guide">
          <div class="workflow-guide-summary">
            <span>已生成模块</span>
            <strong>{{ lastGeneratedHistory?.businessName || formState.businessName }}</strong>
            <small>
              代码生成器已经把业务表接入审批字段、提交接口、撤回接口和按钮权限。真正审批前，还需要完成流程绑定。
            </small>
          </div>
          <div class="workflow-guide-steps">
            <div>
              <Tag color="blue">1</Tag>
              <p>
                重启后端，让新增实体、接口和仓储进入运行时。
              </p>
            </div>
            <div>
              <Tag color="blue">2</Tag>
              <p>
                进入工作流中心，把业务类型
                <code>{{ formState.workflowBusinessType }}</code>
                绑定到一个已发布流程。
              </p>
            </div>
            <div>
              <Tag color="blue">3</Tag>
              <p>
                到新生成的业务页面新增单据，点击“提交审批”后会产生流程待办。
              </p>
            </div>
            <div>
              <Tag color="blue">4</Tag>
              <p>
                审批人登录后进入工作流中心的待办页签进行通过或驳回。
              </p>
            </div>
          </div>
          <div class="workflow-guide-actions">
            <Button type="primary" @click="openWorkflowCenter">
              打开工作流中心
            </Button>
            <Button @click="workflowGuideOpen = false">
              稍后处理
            </Button>
          </div>
        </div>
      </Modal>

      <Drawer
        v-model:open="historyDetailOpen"
        width="880"
        title="生成记录详情"
      >
        <div v-if="historyDetail" class="history-detail">
          <div class="detail-overview">
            <div class="detail-title-block">
              <span>模块</span>
              <div>
                <strong>{{ historyDetail.moduleName }}</strong>
                <Tag :color="getHistoryStatusColor(historyDetail.status)">
                  {{ historyDetail.status }}
                </Tag>
                <Button
                  v-if="canShowRollbackAction(historyDetail)"
                  danger
                  size="small"
                  type="link"
                  :loading="rollbackingId === historyDetail.id"
                  @click="openRollbackModal(historyDetail)"
                >
                  {{ getRollbackActionText(historyDetail) }}
                </Button>
              </div>
              <small>{{ historyDetailRoutePath }}</small>
            </div>
            <div class="detail-metrics">
              <div>
                <span>数据表</span>
                <strong>{{ historyDetail.tableName }}</strong>
              </div>
              <div>
                <span>文件</span>
                <strong>{{ historyDetailFileCount }}</strong>
              </div>
              <div>
                <span>冲突</span>
                <strong :class="{ danger: historyDetailConflictCount > 0 }">
                  {{ historyDetailConflictCount }}
                </strong>
              </div>
              <div>
                <span>操作者</span>
                <strong>{{ historyDetail.operatorUserName || '-' }}</strong>
              </div>
            </div>
          </div>

          <div class="detail-layout">
            <section class="detail-section install-guide">
              <div class="detail-section-head">
                <div>
                  <div class="detail-section-title">安装指引</div>
                  <span>按顺序检查，确认没有冲突后再写入或注册菜单</span>
                </div>
                <Tag :color="historyDetail.installPlan.tableExists ? 'green' : 'orange'">
                  {{ historyDetail.installPlan.tableExists ? '表已存在' : '需建表' }}
                </Tag>
              </div>
              <div class="detail-steps">
                <div
                  v-for="(step, index) in historyDetail.installPlan.steps"
                  :key="step.key"
                  class="detail-step"
                >
                  <div class="step-index">{{ index + 1 }}</div>
                  <div>
                    <div class="step-title-row">
                      <strong>{{ step.title }}</strong>
                      <Tag :color="getInstallStepColor(step.status)">
                        {{ step.status }}
                      </Tag>
                    </div>
                    <span>{{ step.description }}</span>
                  </div>
                </div>
              </div>
            </section>

            <section class="detail-section">
              <div class="detail-section-head">
                <div>
                  <div class="detail-section-title">生成文件</div>
                  <span>
                    {{ historyDetailFileCount }} 个文件，
                    {{ historyDetailConflictCount }} 个冲突
                  </span>
                </div>
              </div>
              <Table
                row-key="relativePath"
                bordered
                size="small"
                :columns="previewColumns"
                :data-source="historyDetail.files"
                :pagination="false"
                :scroll="{ y: 220 }"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.dataIndex === 'state'">
                    <Tag :color="record.hasConflict ? 'red' : 'green'">
                      {{ record.hasConflict ? '冲突' : '可写' }}
                    </Tag>
                  </template>
                </template>
              </Table>
            </section>
          </div>

          <section
            v-if="historyDetail.installPlan.createTableSql"
            class="detail-section"
          >
            <div class="detail-section-head">
              <div>
                <div class="detail-section-title">MySQL 建表草稿</div>
                <span>目标表不存在时使用，执行前请先人工审核</span>
              </div>
            </div>
            <Textarea
              readonly
              :rows="8"
              :value="historyDetail.installPlan.createTableSql"
            />
          </section>

          <section class="detail-section request-section">
            <div class="detail-section-head">
              <div>
                <div class="detail-section-title">生成参数</div>
                <span>用于追溯本次生成时的字段、路由和权限配置</span>
              </div>
            </div>
            <Textarea
              readonly
              :rows="8"
              :value="formatRequestJson(historyDetail)"
            />
          </section>
        </div>
        <Empty v-else-if="!historyDetailLoading" description="暂无详情" />
      </Drawer>
    </div>
  </Page>
</template>

<style scoped>
.code-generator-workspace {
  display: flex;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 10px;
}

.workspace-tabs {
  min-width: 0;
}

.rollback-confirm {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.rollback-confirm p {
  margin: 0;
  color: hsl(var(--muted-foreground));
  line-height: 1.6;
}

.rollback-confirm .rollback-danger {
  padding: 8px 10px;
  color: #b42318;
  background: #fff1f0;
  border: 1px solid #ffccc7;
  border-radius: 6px;
}

.rollback-confirm .rollback-tip {
  color: #64748b;
}

.workflow-guide,
.workflow-guide-summary,
.workflow-guide-steps {
  display: grid;
  gap: 10px;
}

.workflow-guide-summary {
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  background: hsl(var(--muted) / 24%);
  padding: 10px;
}

.workflow-guide-summary span,
.workflow-guide-summary small {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.workflow-guide-summary strong {
  font-size: 15px;
  font-weight: 600;
}

.workflow-guide-steps > div {
  display: grid;
  align-items: flex-start;
  gap: 8px;
  grid-template-columns: auto 1fr;
}

.workflow-guide-steps p {
  margin: 0;
  color: hsl(var(--foreground));
  line-height: 1.7;
}

.workflow-guide-steps code {
  color: #1d4ed8;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
}

.workflow-guide-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding-top: 4px;
}

.builder-stack {
  display: grid;
  gap: 10px;
}

.config-band,
.panel {
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px;
}

.config-heading {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 12px;
}

.config-heading h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
}

.config-heading p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.config-grid {
  display: grid;
  gap: 10px;
  grid-template-columns: repeat(6, minmax(140px, 1fr));
}

.config-footer {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-top: 10px;
  padding-top: 10px;
  border-top: 1px solid hsl(var(--border));
}

.config-footer > span {
  flex: 0 0 auto;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.target-preview {
  display: grid;
  gap: 4px;
  margin-top: 10px;
  border-top: 1px solid hsl(var(--border));
  padding-top: 10px;
}

.target-preview span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.target-preview strong {
  overflow-wrap: anywhere;
  color: hsl(var(--foreground));
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  font-weight: 500;
}

.existing-module-card {
  display: grid;
  gap: 8px;
  margin-top: 10px;
  border: 1px solid #fed7aa;
  border-radius: 4px;
  background: #fff7ed;
  padding: 10px;
}

.reserved-table-card {
  border-color: #fecaca;
  background: #fef2f2;
}

.existing-module-main {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.existing-module-main > div,
.existing-module-files {
  display: grid;
  min-width: 0;
  gap: 4px;
}

.existing-module-main span,
.existing-module-files span {
  color: #9a3412;
  font-size: 12px;
}

.existing-module-main strong {
  color: #7c2d12;
  font-size: 14px;
  font-weight: 600;
}

.existing-module-main small {
  color: #9a3412;
  line-height: 1.6;
}

.existing-module-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.existing-module-files code {
  overflow-wrap: anywhere;
  color: #334155;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
}

.artifact-risk {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.main-grid {
  display: grid;
  min-height: 520px;
  gap: 10px;
  align-items: start;
  grid-template-columns: minmax(260px, 0.72fr) minmax(760px, 2fr);
}

.panel {
  min-width: 0;
}

.preview-panel {
  grid-column: 1 / -1;
}

.install-plan {
  display: grid;
  gap: 10px;
  margin-bottom: 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  background: hsl(var(--muted) / 22%);
  padding: 10px;
}

.install-plan-main {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.install-plan-main h4 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
}

.install-plan-main span,
.install-step span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.install-steps {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(5, minmax(160px, 1fr));
}

.install-step {
  display: flex;
  min-width: 0;
  align-items: flex-start;
  gap: 6px;
}

.install-step > div {
  display: grid;
  min-width: 0;
  gap: 2px;
}

.install-step strong,
.sql-draft-title {
  font-size: 12px;
  font-weight: 600;
}

.sql-draft {
  display: grid;
  gap: 6px;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 10px;
}

.panel-header h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.panel-hint {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.selected-table-card {
  display: grid;
  gap: 2px;
  margin-bottom: 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  background: hsl(var(--muted) / 35%);
  padding: 8px;
}

.selected-table-card span,
.selected-table-card small {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.selected-table-card strong {
  font-size: 13px;
  font-weight: 600;
}

.field-section {
  padding-top: 10px;
}

.field-section + .field-section {
  margin-top: 10px;
  border-top: 1px solid hsl(var(--border));
}

.field-section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-bottom: 10px;
}

.field-section-header.compact {
  padding-top: 2px;
}

.field-section-header h4 {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
}

.field-section-header span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.permission-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.code-preview {
  margin-top: 10px;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
}

.mini-input {
  width: 58px;
}

.control-select {
  width: 112px;
}

.length-input {
  width: 82px;
}

.history-query {
  width: 180px;
}

.history-detail {
  display: grid;
  gap: 12px;
}

.detail-overview,
.detail-section {
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  background: hsl(var(--background));
}

.detail-overview {
  display: grid;
  gap: 12px;
  grid-template-columns: minmax(220px, 1.2fr) minmax(360px, 2fr);
  padding: 12px;
}

.detail-title-block {
  display: grid;
  gap: 4px;
}

.detail-title-block > span,
.detail-metrics span,
.detail-section-head span,
.detail-step span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.detail-title-block > div {
  display: flex;
  align-items: center;
  gap: 8px;
}

.detail-title-block strong {
  overflow-wrap: anywhere;
  font-size: 18px;
  font-weight: 600;
}

.detail-title-block small {
  overflow-wrap: anywhere;
  color: hsl(var(--muted-foreground));
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
}

.detail-metrics {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.detail-metrics > div {
  display: grid;
  gap: 4px;
  border-left: 1px solid hsl(var(--border));
  padding-left: 10px;
}

.detail-metrics strong {
  overflow-wrap: anywhere;
  font-size: 13px;
  font-weight: 600;
}

.detail-metrics .danger {
  color: #cf1322;
}

.detail-layout {
  display: grid;
  gap: 12px;
  grid-template-columns: minmax(260px, 0.9fr) minmax(360px, 1.1fr);
}

.detail-section {
  display: grid;
  gap: 10px;
  padding: 12px;
}

.detail-section-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.detail-section-head > div {
  display: grid;
  gap: 3px;
}

.detail-section-title {
  font-size: 13px;
  font-weight: 600;
}

.detail-steps {
  display: grid;
  gap: 10px;
}

.detail-step {
  display: grid;
  gap: 8px;
  grid-template-columns: 24px 1fr;
}

.step-index {
  display: grid;
  width: 24px;
  height: 24px;
  place-items: center;
  border: 1px solid hsl(var(--border));
  border-radius: 999px;
  background: hsl(var(--muted) / 35%);
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.step-title-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.step-title-row strong {
  font-size: 13px;
  font-weight: 600;
}

.request-section :deep(textarea),
.detail-section :deep(textarea) {
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
}

:deep(.selected-row td) {
  background: rgb(230 244 255);
}

:deep(.ant-form-item) {
  margin-bottom: 0;
}

:deep(.ant-table) {
  font-size: 13px;
}

:deep(.ant-table-thead > tr > th) {
  font-weight: 600;
}

@media (max-width: 1400px) {
  .config-grid {
    grid-template-columns: repeat(3, minmax(160px, 1fr));
  }

  .main-grid {
    grid-template-columns: 1fr;
  }

  .install-steps {
    grid-template-columns: repeat(2, minmax(180px, 1fr));
  }
}

@media (max-width: 760px) {
  .config-heading,
  .config-footer,
  .panel-header,
  .detail-section-head {
    align-items: flex-start;
    flex-direction: column;
  }

  .config-footer {
    flex-wrap: wrap;
  }

  .config-grid,
  .detail-overview,
  .detail-layout,
  .detail-metrics {
    grid-template-columns: 1fr;
  }

  .detail-metrics > div {
    border-left: 0;
    border-top: 1px solid hsl(var(--border));
    padding-top: 8px;
    padding-left: 0;
  }

  .detail-title-block > div,
  .step-title-row {
    align-items: flex-start;
    flex-direction: column;
  }

  .install-steps {
    grid-template-columns: 1fr;
  }
}
</style>
