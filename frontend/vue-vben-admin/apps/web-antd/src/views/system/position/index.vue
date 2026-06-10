<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Space,
  Switch,
  Table,
  Tag,
  message,
} from 'ant-design-vue';

import {
  createPositionApi,
  deletePositionApi,
  downloadPositionImportErrorReportApi,
  downloadPositionImportTemplateApi,
  exportPositionApi,
  getPositionListApi,
  importPositionApi,
  previewImportPositionApi,
  type PositionItem,
  type PositionImportResult,
  updatePositionApi,
} from '#/api/system/position';

const { hasAccessByCodes } = useAccess();

interface PositionFormState {
  code: string;
  isEnabled: boolean;
  name: string;
  order: number;
  remark: string;
}

const loading = ref(false);
const saving = ref(false);
const exportingPositions = ref(false);
const importingPositions = ref(false);
const previewingImport = ref(false);
const downloadingTemplate = ref(false);
const downloadingErrorReport = ref(false);
const modalOpen = ref(false);
const importPreviewModalOpen = ref(false);
const importInputRef = ref<HTMLInputElement>();
const importPreviewFile = ref<File>();
const importPreviewResult = ref<PositionImportResult>();
const editingPosition = ref<PositionItem>();
const positions = ref<PositionItem[]>([]);
const total = ref(0);
const query = reactive({
  code: '',
  name: '',
  page: 1,
  pageSize: 10,
});
const formState = reactive<PositionFormState>({
  code: '',
  isEnabled: true,
  name: '',
  order: 0,
  remark: '',
});

const columns = [
  { dataIndex: 'name', title: '岗位名称', width: 180 },
  { dataIndex: 'code', title: '岗位编码', width: 180 },
  { dataIndex: 'order', title: '排序', width: 90 },
  { dataIndex: 'state', title: '状态', width: 100 },
  { dataIndex: 'remark', title: '备注' },
  { dataIndex: 'action', title: '操作', width: 160 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));

const modalTitle = computed(() =>
  editingPosition.value ? '编辑岗位' : '新增岗位',
);
const canCreate = computed(() => hasAccessByCodes(['system:position:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:position:update']));
const canDelete = computed(() => hasAccessByCodes(['system:position:delete']));
const canImport = computed(() => hasAccessByCodes(['system:position:import']));
const canExport = computed(() => hasAccessByCodes(['system:position:export']));

async function loadPositions() {
  loading.value = true;
  try {
    const result = await getPositionListApi({
      code: query.code || undefined,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    positions.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadPositions();
}

function handleReset() {
  query.code = '';
  query.name = '';
  query.page = 1;
  void loadPositions();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadPositions();
}

function resetForm() {
  editingPosition.value = undefined;
  formState.code = '';
  formState.name = '';
  formState.order = 0;
  formState.remark = '';
  formState.isEnabled = true;
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(position: PositionItem | Record<string, any>) {
  const currentPosition = position as PositionItem;
  editingPosition.value = currentPosition;
  formState.code = currentPosition.code;
  formState.name = currentPosition.name;
  formState.order = currentPosition.order;
  formState.remark = currentPosition.remark ?? '';
  formState.isEnabled = currentPosition.isEnabled;
  modalOpen.value = true;
}

async function submitPosition() {
  if (!formState.code.trim() || !formState.name.trim()) {
    message.warning('请填写岗位编码和岗位名称');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      code: formState.code,
      isEnabled: formState.isEnabled,
      name: formState.name,
      order: formState.order,
      remark: formState.remark || null,
    };

    if (editingPosition.value) {
      await updatePositionApi(editingPosition.value.id, payload);
      message.success('岗位已更新');
    } else {
      await createPositionApi(payload);
      message.success('岗位已新增');
    }

    modalOpen.value = false;
    await loadPositions();
  } finally {
    saving.value = false;
  }
}

async function removePosition(position: PositionItem | Record<string, any>) {
  const currentPosition = position as PositionItem;
  const deleted = await deletePositionApi(currentPosition.id);
  if (deleted) {
    message.success('岗位已删除');
  } else {
    message.warning('该岗位已绑定用户，不能删除');
  }
  await loadPositions();
}

async function exportPositions() {
  exportingPositions.value = true;
  try {
    const blob = await exportPositionApi({
      code: query.code || undefined,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    downloadBlob(blob, 'mini-admin-positions.xlsx');
    message.success('岗位已导出');
  } finally {
    exportingPositions.value = false;
  }
}

async function downloadImportTemplate() {
  downloadingTemplate.value = true;
  try {
    const blob = await downloadPositionImportTemplateApi();
    downloadBlob(blob, 'mini-admin-position-import-template.xlsx');
  } finally {
    downloadingTemplate.value = false;
  }
}

function openImportFilePicker() {
  importInputRef.value?.click();
}

async function handleImportFile(event: Event) {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  input.value = '';
  if (!file) {
    return;
  }

  if (!file.name.toLowerCase().endsWith('.xlsx')) {
    message.warning('请上传 .xlsx 文件');
    return;
  }

  previewingImport.value = true;
  try {
    importPreviewFile.value = file;
    importPreviewResult.value = await previewImportPositionApi(file);
    importPreviewModalOpen.value = true;
  } finally {
    previewingImport.value = false;
  }
}

async function confirmImportPositions() {
  if (!importPreviewFile.value || !importPreviewResult.value) {
    return;
  }

  if (importPreviewResult.value.errors.length > 0) {
    message.warning('请先修正失败行后再导入');
    return;
  }

  importingPositions.value = true;
  try {
    const result = await importPositionApi(importPreviewFile.value);
    message.success(`导入成功 ${result.createdCount} 个岗位`);
    importPreviewModalOpen.value = false;
    importPreviewFile.value = undefined;
    importPreviewResult.value = undefined;
    await loadPositions();
  } finally {
    importingPositions.value = false;
  }
}

async function downloadImportErrorReport() {
  if (!importPreviewFile.value) {
    return;
  }

  downloadingErrorReport.value = true;
  try {
    const blob = await downloadPositionImportErrorReportApi(
      importPreviewFile.value,
    );
    downloadBlob(blob, 'mini-admin-position-import-errors.xlsx');
  } finally {
    downloadingErrorReport.value = false;
  }
}

function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = fileName;
  anchor.click();
  URL.revokeObjectURL(url);
}

onMounted(loadPositions);
</script>

<template>
  <Page auto-content-height>
    <div class="position-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">岗位名称</span>
          <Input
            v-model:value="query.name"
            allow-clear
            class="query-input"
            placeholder="请输入"
          />
          <span class="query-label">岗位编码</span>
          <Input
            v-model:value="query.code"
            allow-clear
            class="query-input"
            placeholder="请输入"
          />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
          <Button type="link">收起^</Button>
        </Space>
      </div>

      <div class="table-shell">
        <div class="table-toolbar">
          <h3>岗位列表</h3>
          <Space>
            <Button @click="loadPositions">刷新</Button>
            <Button
              v-if="canExport"
              :loading="exportingPositions"
              @click="exportPositions"
            >
              导出
            </Button>
            <Button
              v-if="canImport"
              :loading="downloadingTemplate"
              @click="downloadImportTemplate"
            >
              下载模板
            </Button>
            <Button
              v-if="canImport"
              :loading="previewingImport"
              @click="openImportFilePicker"
            >
              导入
            </Button>
            <Button disabled>删除</Button>
            <Button v-if="canCreate" type="primary" @click="openCreateModal">
              新增
            </Button>
            <input
              ref="importInputRef"
              accept=".xlsx"
              class="hidden-import-input"
              type="file"
              @change="handleImportFile"
            />
          </Space>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="positions"
          :loading="loading"
          :pagination="pagination"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'code'">
              <Tag color="blue">{{ record.code }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'state'">
              <Tag :color="record.isEnabled ? 'green' : 'default'">
                {{ record.isEnabled ? '正常' : '停用' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'remark'">
              {{ record.remark || '-' }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Space>
                <Button
                  v-if="canUpdate"
                  class="table-action edit"
                  size="small"
                  @click="openEditModal(record)"
                >
                  编辑
                </Button>
                <Popconfirm
                  v-if="canDelete"
                  title="确认删除该岗位？"
                  @confirm="removePosition(record)"
                >
                  <Button class="table-action danger" size="small">删除</Button>
                </Popconfirm>
                <span v-if="!canUpdate && !canDelete">-</span>
              </Space>
            </template>
          </template>
        </Table>
      </div>
    </div>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="modalTitle"
      width="640px"
      @ok="submitPosition"
    >
      <Form layout="vertical">
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="岗位编码" required>
            <Input v-model:value="formState.code" placeholder="例如 manager" />
          </FormItem>
          <FormItem label="岗位名称" required>
            <Input v-model:value="formState.name" placeholder="例如 管理员" />
          </FormItem>
          <FormItem label="排序">
            <InputNumber v-model:value="formState.order" class="w-full" />
          </FormItem>
          <FormItem label="启用">
            <Switch v-model:checked="formState.isEnabled" />
          </FormItem>
        </div>
        <FormItem label="备注">
          <Input v-model:value="formState.remark" placeholder="可选" />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="importPreviewModalOpen"
      :confirm-loading="importingPositions"
      :ok-button-props="{
        disabled: Boolean(importPreviewResult?.errors.length),
      }"
      title="导入预检"
      @ok="confirmImportPositions"
    >
      <div class="import-preview">
        <div class="import-preview-summary">
          <Tag color="green">
            预计成功 {{ importPreviewResult?.createdCount ?? 0 }} 条
          </Tag>
          <Tag :color="importPreviewResult?.errors.length ? 'red' : 'green'">
            失败 {{ importPreviewResult?.errors.length ?? 0 }} 条
          </Tag>
        </div>
        <Table
          v-if="importPreviewResult?.errors.length"
          class="mt-3"
          row-key="rowNumber"
          size="small"
          :columns="[
            { dataIndex: 'rowNumber', title: '行号', width: 80 },
            { dataIndex: 'code', title: '岗位编码', width: 160 },
            { dataIndex: 'message', title: '失败原因' },
          ]"
          :data-source="importPreviewResult.errors"
          :pagination="{ pageSize: 5 }"
        />
        <div v-if="importPreviewResult?.errors.length" class="mt-3">
          <Button
            :loading="downloadingErrorReport"
            @click="downloadImportErrorReport"
          >
            下载失败明细
          </Button>
        </div>
      </div>
    </Modal>
  </Page>
</template>

<style scoped>
.position-workspace {
  display: flex;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 10px;
}

.query-bar {
  display: flex;
  min-height: 64px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px 12px;
}

.query-label {
  font-weight: 600;
  white-space: nowrap;
}

.query-input {
  width: 220px;
}

.table-shell {
  min-height: 0;
  flex: 1;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px 10px 0;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 10px;
}

.table-toolbar h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.hidden-import-input {
  display: none;
}

.import-preview-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.table-action {
  height: 24px;
  padding: 0 8px;
  border-radius: 4px;
  background: transparent;
}

.table-action.edit {
  border-color: #f5b94b;
  color: #d89000;
}

.table-action.danger {
  border-color: #ff6b93;
  color: #ff3868;
}

:deep(.ant-table) {
  font-size: 13px;
}

:deep(.ant-table-thead > tr > th) {
  font-weight: 600;
}

:deep(.ant-pagination) {
  margin: 10px 0;
}

@media (max-width: 900px) {
  .query-bar {
    align-items: flex-start;
    flex-direction: column;
  }

  .query-input {
    width: 200px;
  }
}
</style>
