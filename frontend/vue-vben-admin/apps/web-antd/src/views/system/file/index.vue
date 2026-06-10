<script setup lang="ts">
import type { UploadRequestOption } from 'ant-design-vue/es/vc-upload/interface';
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Input,
  message,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Upload,
} from 'ant-design-vue';

import {
  deleteFileApi,
  downloadFileApi,
  type FileItem,
  getFileListApi,
  markFileInvalidApi,
  uploadFileApi,
} from '#/api/system/file';

const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const uploading = ref(false);
const files = ref<FileItem[]>([]);
const total = ref(0);
const query = reactive({
  originalName: '',
  page: 1,
  pageSize: 10,
  storageProvider: undefined as string | undefined,
});

const storageProviderOptions = [
  { label: '本地', value: 'local' },
  { label: 'MinIO', value: 'minio' },
];

const columns = [
  { dataIndex: 'originalName', title: '文件名' },
  { dataIndex: 'contentType', title: '类型', width: 180 },
  { dataIndex: 'size', title: '大小', width: 110 },
  { dataIndex: 'storageProvider', title: '存储', width: 100 },
  { dataIndex: 'status', title: '状态', width: 110 },
  { dataIndex: 'storagePath', title: '存储路径', width: 260 },
  { dataIndex: 'createdAt', title: '上传时间', width: 190 },
  { dataIndex: 'action', title: '操作', width: 230 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));
const canUpload = computed(() => hasAccessByCodes(['system:file:upload']));
const canDownload = computed(() => hasAccessByCodes(['system:file:download']));
const canDelete = computed(() => hasAccessByCodes(['system:file:delete']));
const canMarkInvalid = computed(() =>
  hasAccessByCodes(['system:file:mark-invalid']),
);

async function loadFiles() {
  loading.value = true;
  try {
    const result = await getFileListApi({
      originalName: query.originalName.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
      storageProvider: query.storageProvider,
    });
    files.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadFiles();
}

function handleReset() {
  query.originalName = '';
  query.page = 1;
  query.storageProvider = undefined;
  void loadFiles();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadFiles();
}

async function uploadFile(option: UploadRequestOption) {
  const rawFile = option.file as File;
  uploading.value = true;
  try {
    await uploadFileApi(rawFile);
    message.success('文件已上传');
    option.onSuccess?.({});
    await loadFiles();
  } catch (error) {
    option.onError?.(error as Error);
    message.error('文件上传失败');
  } finally {
    uploading.value = false;
  }
}

async function downloadFile(file: FileItem | Record<string, any>) {
  const currentFile = file as FileItem;
  if (currentFile.status !== 'Normal') {
    message.warning('文件当前不可下载');
    return;
  }

  const blob = await downloadFileApi(currentFile.id);
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = currentFile.originalName;
  anchor.click();
  URL.revokeObjectURL(url);
}

async function markInvalid(file: FileItem | Record<string, any>) {
  const currentFile = file as FileItem;
  await markFileInvalidApi(currentFile.id);
  message.success('文件已标记无效');
  await loadFiles();
}

async function removeFile(file: FileItem | Record<string, any>) {
  const currentFile = file as FileItem;
  const deleted = await deleteFileApi(currentFile.id);
  if (deleted) {
    message.success('文件已删除');
  }
  await loadFiles();
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function formatSize(size: number) {
  if (size < 1024) {
    return `${size} B`;
  }

  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`;
  }

  return `${(size / 1024 / 1024).toFixed(1)} MB`;
}

function getProviderLabel(provider: string) {
  return storageProviderOptions.find((item) => item.value === provider)?.label ?? provider;
}

function getStatusLabel(status: string) {
  const labels: Record<string, string> = {
    Invalid: '已标记无效',
    Missing: '存储缺失',
    Normal: '正常',
  };

  return labels[status] ?? status;
}

function getStatusColor(status: string) {
  if (status === 'Normal') {
    return 'green';
  }

  if (status === 'Missing') {
    return 'orange';
  }

  if (status === 'Invalid') {
    return 'red';
  }

  return 'default';
}

onMounted(loadFiles);
</script>

<template>
  <Page auto-content-height>
    <div class="file-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">文件名</span>
          <Input
            v-model:value="query.originalName"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">存储</span>
          <Select
            v-model:value="query.storageProvider"
            allow-clear
            class="query-select"
            :options="storageProviderOptions"
            placeholder="请选择"
          />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>

      <div class="table-shell">
        <div class="table-toolbar">
          <h3>文件列表</h3>
          <Space>
            <Button @click="loadFiles">刷新</Button>
            <Upload
              v-if="canUpload"
              :custom-request="uploadFile"
              :show-upload-list="false"
            >
              <Button :loading="uploading" type="primary">上传</Button>
            </Upload>
          </Space>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="files"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 1280 }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'originalName'">
              <span class="name-cell">{{ record.originalName }}</span>
            </template>
            <template v-if="column.dataIndex === 'size'">
              {{ formatSize(record.size) }}
            </template>
            <template v-if="column.dataIndex === 'storageProvider'">
              <Tag :color="record.storageProvider === 'minio' ? 'blue' : 'green'">
                {{ getProviderLabel(record.storageProvider) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'status'">
              <Tag :color="getStatusColor(record.status)">
                {{ getStatusLabel(record.status) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'storagePath'">
              <span class="path-cell">{{ record.storagePath }}</span>
            </template>
            <template v-if="column.dataIndex === 'createdAt'">
              {{ formatTime(record.createdAt) }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Space>
                <Button
                  v-if="canDownload"
                  class="table-action edit"
                  :disabled="record.status !== 'Normal'"
                  size="small"
                  @click="downloadFile(record)"
                >
                  下载
                </Button>
                <Popconfirm
                  v-if="canMarkInvalid && record.status !== 'Invalid'"
                  title="确认将该文件标记为无效？"
                  @confirm="markInvalid(record)"
                >
                  <Button class="table-action warn" size="small">标记无效</Button>
                </Popconfirm>
                <Popconfirm
                  v-if="canDelete"
                  title="确认删除该文件？"
                  @confirm="removeFile(record)"
                >
                  <Button class="table-action danger" size="small">删除</Button>
                </Popconfirm>
                <span v-if="!canDownload && !canDelete">-</span>
              </Space>
            </template>
          </template>
        </Table>
      </div>
    </div>
  </Page>
</template>

<style scoped>
.file-workspace {
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
  width: 240px;
}

.query-select {
  width: 160px;
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

.name-cell,
.path-cell {
  display: inline-block;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: bottom;
  white-space: nowrap;
}

.name-cell {
  max-width: 360px;
}

.path-cell {
  max-width: 240px;
}

.table-action {
  height: 24px;
  padding: 0 8px;
  border-radius: 4px;
  background: transparent;
}

.table-action.edit {
  border-color: #4f8cff;
  color: #1f66e5;
}

.table-action.warn {
  border-color: #ffb020;
  color: #ad6800;
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

@media (max-width: 1000px) {
  .query-bar {
    align-items: flex-start;
    flex-direction: column;
  }

  .query-input {
    width: 200px;
  }
}
</style>
