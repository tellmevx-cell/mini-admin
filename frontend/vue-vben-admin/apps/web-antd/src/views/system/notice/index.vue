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
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';

import {
  createNoticeApi,
  deleteNoticeApi,
  getNoticeListApi,
  type NoticeItem,
  updateNoticeApi,
} from '#/api/system/notice';

interface NoticeFormState {
  content: string;
  isPublished: boolean;
  title: string;
  type: string;
}

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const editingNotice = ref<NoticeItem>();
const notices = ref<NoticeItem[]>([]);
const total = ref(0);
const query = reactive({
  isPublished: undefined as string | undefined,
  page: 1,
  pageSize: 10,
  title: '',
  type: undefined as string | undefined,
});
const formState = reactive<NoticeFormState>({
  content: '',
  isPublished: false,
  title: '',
  type: 'notice',
});
const { hasAccessByCodes } = useAccess();

const noticeTypeOptions = [
  { label: '通知', value: 'notice' },
  { label: '公告', value: 'announcement' },
];

const publishOptions = [
  { label: '已发布', value: 'true' },
  { label: '未发布', value: 'false' },
];

const columns = [
  { dataIndex: 'title', title: '标题', width: 240 },
  { dataIndex: 'type', title: '类型', width: 110 },
  { dataIndex: 'state', title: '状态', width: 110 },
  { dataIndex: 'publishedAt', title: '发布时间', width: 190 },
  { dataIndex: 'createdAt', title: '创建时间', width: 190 },
  { dataIndex: 'content', title: '内容摘要' },
  { dataIndex: 'action', title: '操作', width: 160 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));

function parseBooleanSelectValue(value?: string) {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return undefined;
}

const modalTitle = computed(() =>
  editingNotice.value ? '编辑通知公告' : '新增通知公告',
);
const canCreate = computed(() => hasAccessByCodes(['system:notice:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:notice:update']));
const canDelete = computed(() => hasAccessByCodes(['system:notice:delete']));

async function loadNotices() {
  loading.value = true;
  try {
    const result = await getNoticeListApi({
      isPublished: parseBooleanSelectValue(query.isPublished),
      page: query.page,
      pageSize: query.pageSize,
      title: query.title || undefined,
      type: query.type,
    });
    notices.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.title = query.title.trim();
  query.page = 1;
  void loadNotices();
}

function handleReset() {
  query.title = '';
  query.type = undefined;
  query.isPublished = undefined;
  query.page = 1;
  void loadNotices();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadNotices();
}

function resetForm() {
  editingNotice.value = undefined;
  formState.title = '';
  formState.type = 'notice';
  formState.content = '';
  formState.isPublished = false;
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(notice: NoticeItem | Record<string, any>) {
  const currentNotice = notice as NoticeItem;
  editingNotice.value = currentNotice;
  formState.title = currentNotice.title;
  formState.type = currentNotice.type;
  formState.content = currentNotice.content;
  formState.isPublished = currentNotice.isPublished;
  modalOpen.value = true;
}

async function submitNotice() {
  if (!formState.title.trim() || !formState.content.trim()) {
    message.warning('请填写标题和内容');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      content: formState.content,
      isPublished: formState.isPublished,
      title: formState.title,
      type: formState.type,
    };

    if (editingNotice.value) {
      await updateNoticeApi(editingNotice.value.id, payload);
      message.success('通知公告已更新');
    } else {
      await createNoticeApi(payload);
      message.success('通知公告已新增');
    }

    modalOpen.value = false;
    await loadNotices();
  } finally {
    saving.value = false;
  }
}

async function removeNotice(notice: NoticeItem | Record<string, any>) {
  const currentNotice = notice as NoticeItem;
  const deleted = await deleteNoticeApi(currentNotice.id);
  if (deleted) {
    message.success('通知公告已删除');
  }
  await loadNotices();
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function getTypeLabel(type: string) {
  return noticeTypeOptions.find((item) => item.value === type)?.label ?? type;
}

onMounted(loadNotices);
</script>

<template>
  <Page auto-content-height>
    <div class="notice-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">标题</span>
          <Input
            v-model:value="query.title"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">类型</span>
          <Select
            v-model:value="query.type"
            allow-clear
            class="query-select"
            :options="noticeTypeOptions"
            placeholder="请选择"
          />
          <span class="query-label">状态</span>
          <Select
            v-model:value="query.isPublished"
            allow-clear
            class="query-select"
            :options="publishOptions"
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
          <h3>通知公告列表</h3>
          <Space>
            <Button @click="loadNotices">刷新</Button>
            <Button v-if="canCreate" type="primary" @click="openCreateModal">新增</Button>
          </Space>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="notices"
          :loading="loading"
          :pagination="pagination"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'type'">
              <Tag :color="record.type === 'announcement' ? 'purple' : 'blue'">
                {{ getTypeLabel(record.type) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'state'">
              <Tag :color="record.isPublished ? 'green' : 'default'">
                {{ record.isPublished ? '已发布' : '未发布' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'publishedAt'">
              {{ formatTime(record.publishedAt) }}
            </template>
            <template v-if="column.dataIndex === 'createdAt'">
              {{ formatTime(record.createdAt) }}
            </template>
            <template v-if="column.dataIndex === 'content'">
              <span class="content-cell">{{ record.content }}</span>
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
                  title="确认删除该通知公告？"
                  @confirm="removeNotice(record)"
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
      width="760px"
      @ok="submitNotice"
    >
      <Form layout="vertical">
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="标题" required>
            <Input v-model:value="formState.title" placeholder="请输入标题" />
          </FormItem>
          <FormItem label="类型" required>
            <Select
              v-model:value="formState.type"
              :options="noticeTypeOptions"
              placeholder="请选择类型"
            />
          </FormItem>
        </div>
        <FormItem label="内容" required>
          <Textarea
            v-model:value="formState.content"
            :auto-size="{ minRows: 5, maxRows: 10 }"
            placeholder="请输入通知公告内容"
          />
        </FormItem>
        <FormItem label="发布">
          <Switch v-model:checked="formState.isPublished" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.notice-workspace {
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

.content-cell {
  display: inline-block;
  max-width: 360px;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: bottom;
  white-space: nowrap;
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
