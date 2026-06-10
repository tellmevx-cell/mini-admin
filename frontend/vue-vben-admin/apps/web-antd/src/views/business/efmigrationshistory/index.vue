<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';


import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import {
  Button,
  Form,
  FormItem,
  Input,
  Modal,
  Popconfirm,
  Space,
  Table,
  Tag,
  message,
} from 'ant-design-vue';
import {
  createEfmigrationshistoryApi,
  deleteEfmigrationshistoryApi,
  downloadEfmigrationshistoryImportErrorReportApi,
  downloadEfmigrationshistoryImportTemplateApi,
  exportEfmigrationshistoryApi,
  importEfmigrationshistoryApi,
  previewImportEfmigrationshistoryApi,
  type EfmigrationshistoryImportResult,
  getEfmigrationshistoryListApi,
  submitEfmigrationshistoryWorkflowApi,
  withdrawEfmigrationshistoryWorkflowApi,
  type EfmigrationshistoryItem,
  updateEfmigrationshistoryApi,
} from '#/api/business/efmigrationshistory';

interface EfmigrationshistoryFormState {
  productVersion: string;
}

const loading = ref(false);
const saving = ref(false);
const exportingItems = ref(false);
const importingItems = ref(false);
const previewingImport = ref(false);
const downloadingTemplate = ref(false);
const downloadingErrorReport = ref(false);
const importPreviewModalOpen = ref(false);
const importInputRef = ref<HTMLInputElement>();
const importPreviewFile = ref<File>();
const importPreviewResult = ref<EfmigrationshistoryImportResult>();
 const submittingWorkflowId = ref('');
 const withdrawingWorkflowId = ref('');
const modalOpen = ref(false);
const editingItem = ref<EfmigrationshistoryItem>();
const items = ref<EfmigrationshistoryItem[]>([]);
const total = ref(0);
const query = reactive({
  keyword: '',
  productVersion: '',
  page: 1,
  pageSize: 10,
});
const formState = reactive<EfmigrationshistoryFormState>({
  productVersion: '',
});
const { hasAccessByCodes } = useAccess();
const router = useRouter();

const columns = [
  { dataIndex: 'productVersion', title: 'ProductVersion' },
  { dataIndex: 'approvalStatus', title: '审批状态', width: 120 },
  { dataIndex: 'createdAt', title: '创建时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 260 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));
const modalTitle = computed(() => editingItem.value ? '编辑__efmigrationshistory' : '新增__efmigrationshistory');
const canCreate = computed(() => hasAccessByCodes(['business:efmigrationshistory:create']));
const canUpdate = computed(() => hasAccessByCodes(['business:efmigrationshistory:update']));
const canDelete = computed(() => hasAccessByCodes(['business:efmigrationshistory:delete']));
const canImport = computed(() => hasAccessByCodes(['business:efmigrationshistory:import']));
const canExport = computed(() => hasAccessByCodes(['business:efmigrationshistory:export']));
const canSubmitWorkflow = computed(() => hasAccessByCodes(['business:efmigrationshistory:submit-workflow']));
const canWithdrawWorkflow = computed(() => hasAccessByCodes(['business:efmigrationshistory:withdraw-workflow']));

async function loadData() {
  loading.value = true;
  try {
    const result = await getEfmigrationshistoryListApi({
      keyword: query.keyword || undefined,
      productVersion: query.productVersion || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    items.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadData();
}

function handleReset() {
  query.keyword = '';
  query.productVersion = '';
  query.page = 1;
  void loadData();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadData();
}

function resetForm() {
  editingItem.value = undefined;
  formState.productVersion = '';
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(item: EfmigrationshistoryItem | Record<string, any>) {
  const currentItem = item as EfmigrationshistoryItem;
  editingItem.value = currentItem;
  formState.productVersion = currentItem.productVersion;
  modalOpen.value = true;
}

async function submitItem() {
  saving.value = true;
  try {
    const payload = {
      productVersion: formState.productVersion,
    };

    if (editingItem.value) {
      await updateEfmigrationshistoryApi(editingItem.value.id, payload);
      message.success('__efmigrationshistory已更新');
    } else {
      await createEfmigrationshistoryApi(payload);
      message.success('__efmigrationshistory已新增');
    }

    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeItem(item: EfmigrationshistoryItem | Record<string, any>) {
  const currentItem = item as EfmigrationshistoryItem;
  const deleted = await deleteEfmigrationshistoryApi(currentItem.id);
  if (deleted) {
    message.success('__efmigrationshistory已删除');
  }
  await loadData();
}

async function exportItems() {
  exportingItems.value = true;
  try {
    const blob = await exportEfmigrationshistoryApi({
      keyword: query.keyword || undefined,
      productVersion: query.productVersion || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    downloadBlob(blob, 'mini-admin-efmigrationshistory.xlsx');
    message.success('__efmigrationshistory已导出');
  } finally {
    exportingItems.value = false;
  }
}

async function downloadImportTemplate() {
  downloadingTemplate.value = true;
  try {
    const blob = await downloadEfmigrationshistoryImportTemplateApi();
    downloadBlob(blob, 'mini-admin-efmigrationshistory-import-template.xlsx');
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
    importPreviewResult.value = await previewImportEfmigrationshistoryApi(file);
    importPreviewModalOpen.value = true;
  } finally {
    previewingImport.value = false;
  }
}

async function confirmImportItems() {
  if (!importPreviewFile.value || !importPreviewResult.value) {
    return;
  }

  if (importPreviewResult.value.errors.length > 0) {
    message.warning('请先修正失败行后再导入');
    return;
  }

  importingItems.value = true;
  try {
    const result = await importEfmigrationshistoryApi(importPreviewFile.value);
    message.success(`导入成功 ${result.createdCount} 条数据`);
    importPreviewModalOpen.value = false;
    importPreviewFile.value = undefined;
    importPreviewResult.value = undefined;
    await loadData();
  } finally {
    importingItems.value = false;
  }
}

async function downloadImportErrorReport() {
  if (!importPreviewFile.value) {
    return;
  }

  downloadingErrorReport.value = true;
  try {
    const blob = await downloadEfmigrationshistoryImportErrorReportApi(importPreviewFile.value);
    downloadBlob(blob, 'mini-admin-efmigrationshistory-import-errors.xlsx');
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

function getApprovalStatusColor(status: string) {
  const colors: Record<string, string> = {
    Approved: 'green',
    Draft: 'default',
    Pending: 'blue',
    Rejected: 'red',
    Withdrawn: 'orange',
  };
  return colors[status] ?? 'default';
}

function getApprovalStatusText(status: string) {
  const texts: Record<string, string> = {
    Approved: '已通过',
    Draft: '草稿',
    Pending: '审批中',
    Rejected: '已驳回',
    Withdrawn: '已撤回',
  };
  return texts[status] ?? status;
}

function canSubmitRecord(record: EfmigrationshistoryItem | Record<string, any>) {
  const currentRecord = record as EfmigrationshistoryItem;
  return !['Approved', 'Pending'].includes(currentRecord.approvalStatus);
}

function canWithdrawRecord(record: EfmigrationshistoryItem | Record<string, any>) {
  const currentRecord = record as EfmigrationshistoryItem;
  return currentRecord.approvalStatus === 'Pending' && Boolean(currentRecord.workflowInstanceId);
}

async function submitWorkflow(record: EfmigrationshistoryItem | Record<string, any>) {
  const currentRecord = record as EfmigrationshistoryItem;
  submittingWorkflowId.value = currentRecord.id;
  try {
    await submitEfmigrationshistoryWorkflowApi(currentRecord.id, {});
    message.success('__efmigrationshistory已提交审批');
    await loadData();
  } finally {
    submittingWorkflowId.value = '';
  }
}

async function withdrawWorkflow(record: EfmigrationshistoryItem | Record<string, any>) {
  const currentRecord = record as EfmigrationshistoryItem;
  withdrawingWorkflowId.value = currentRecord.id;
  try {
    await withdrawEfmigrationshistoryWorkflowApi(currentRecord.id, {});
    message.success('__efmigrationshistory已撤回审批');
    await loadData();
  } finally {
    withdrawingWorkflowId.value = '';
  }
}

function openWorkflowCenter() {
  void router.push('/workflow/center');
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <div class="generated-page">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">关键词</span>
          <Input
            v-model:value="query.keyword"
            allow-clear
            class="query-input"
            placeholder="请输入"
          />
      <span class="query-label">ProductVersion</span>
      <Input
        v-model:value="query.productVersion"
        allow-clear
        class="query-input"
        placeholder="请输入ProductVersion"
      />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>
      <div class="toolbar">
        <h3>__efmigrationshistory列表</h3>
        <Space>
               <Button v-if="canExport" :loading="exportingItems" @click="exportItems">导出</Button>
               <Button v-if="canImport" :loading="downloadingTemplate" @click="downloadImportTemplate">下载模板</Button>
               <Button v-if="canImport" :loading="previewingImport" @click="openImportFilePicker">导入</Button>
               <input ref="importInputRef" accept=".xlsx" class="hidden-import-input" type="file" @change="handleImportFile" />
          <Button v-if="canCreate" type="primary" @click="openCreateModal">新增</Button>
        </Space>
      </div>
      <Table
        row-key="id"
        size="small"
        bordered
        :columns="columns"
        :data-source="items"
        :loading="loading"
        :pagination="pagination"
        @change="handleTableChange"
      >
      <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'approvalStatus'">
            <Tag :color="getApprovalStatusColor(record.approvalStatus)">
              {{ getApprovalStatusText(record.approvalStatus) }}
            </Tag>
          </template>
         <template v-if="column.dataIndex === 'action'">
           <Space>
              <Button
                v-if="canSubmitWorkflow && canSubmitRecord(record)"
                size="small"
                type="link"
                :loading="submittingWorkflowId === record.id"
                @click="submitWorkflow(record)"
              >
                提交
              </Button>
              <Popconfirm title="确认撤回这条审批？" @confirm="withdrawWorkflow(record)">
                <Button
                  v-if="canWithdrawWorkflow && canWithdrawRecord(record)"
                  size="small"
                  type="link"
                  :loading="withdrawingWorkflowId === record.id"
                >
                  撤回
                </Button>
              </Popconfirm>
              <Button
                v-if="record.workflowInstanceId"
                size="small"
                type="link"
                @click="openWorkflowCenter"
              >
                流程
              </Button>
             <Button v-if="canUpdate" type="link" size="small" @click="openEditModal(record)">编辑</Button>
              <Popconfirm title="确认删除这条数据？" @confirm="removeItem(record)">
                <Button v-if="canDelete" danger type="link" size="small">删除</Button>
              </Popconfirm>
            </Space>
          </template>
        </template>
      </Table>
    </div>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="modalTitle"
      @ok="submitItem"
    >
      <Form :model="formState" layout="vertical">
      <FormItem label="ProductVersion">
        <Input v-model:value="formState.productVersion" allow-clear />
      </FormItem>
      </Form>
    </Modal>

     <Modal
       v-model:open="importPreviewModalOpen"
       :confirm-loading="importingItems"
       :ok-button-props="{ disabled: Boolean(importPreviewResult?.errors.length) }"
       title="导入预检"
       @ok="confirmImportItems"
     >
       <div class="import-preview-summary">
         <Tag color="green">预计成功 {{ importPreviewResult?.createdCount ?? 0 }} 条</Tag>
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
           { dataIndex: 'field', title: '字段', width: 140 },
           { dataIndex: 'message', title: '失败原因' },
         ]"
         :data-source="importPreviewResult.errors"
         :pagination="{ pageSize: 5 }"
       />
       <Button
         v-if="importPreviewResult?.errors.length"
         class="mt-3"
         :loading="downloadingErrorReport"
         @click="downloadImportErrorReport"
       >
         下载失败明细
       </Button>
     </Modal>
  </Page>
</template>

<style scoped>
.generated-page {
  min-height: calc(100vh - 150px);
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px;
}

.query-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  padding-bottom: 10px;
}

.query-label {
  color: hsl(var(--muted-foreground));
  font-size: 13px;
}

.query-input {
  width: 220px;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-bottom: 10px;
}

.toolbar h3 {
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
</style>
