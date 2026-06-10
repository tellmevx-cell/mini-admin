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
  Table,
  message,
} from 'ant-design-vue';
import {
  createCustomerApi,
  deleteCustomerApi,
  getCustomerListApi,
  type CustomerItem,
  updateCustomerApi,
} from '#/api/business/customer';

interface CustomerFormState {
  content: string;
  isPublished: number;
  publishedAt: string;
  title: string;
  type: string;
}

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const editingItem = ref<CustomerItem>();
const items = ref<CustomerItem[]>([]);
const total = ref(0);
const query = reactive({
  keyword: '',
  page: 1,
  pageSize: 10,
});
const formState = reactive<CustomerFormState>({
  title: '',
  type: '',
  content: '',
  isPublished: 0,
  publishedAt: '',
});
const { hasAccessByCodes } = useAccess();
const columns = [
  { dataIndex: 'title', title: '客户名称' },
  { dataIndex: 'type', title: '客户类型' },
  { dataIndex: 'content', title: '备注' },
  { dataIndex: 'isPublished', title: '启用状态', width: 100 },
  { dataIndex: 'publishedAt', title: '跟进时间', width: 180 },
  { dataIndex: 'createdAt', title: '创建时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 150 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));
const modalTitle = computed(() => editingItem.value ? '编辑客户资料' : '新增客户资料');
const canCreate = computed(() => hasAccessByCodes(['business:customer:create']));
const canUpdate = computed(() => hasAccessByCodes(['business:customer:update']));
const canDelete = computed(() => hasAccessByCodes(['business:customer:delete']));

async function loadData() {
  loading.value = true;
  try {
    const result = await getCustomerListApi({
      keyword: query.keyword || undefined,
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
  formState.title = '';
  formState.type = '';
  formState.content = '';
  formState.isPublished = 0;
  formState.publishedAt = '';
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(item: CustomerItem | Record<string, any>) {
  const currentItem = item as CustomerItem;
  editingItem.value = currentItem;
  formState.title = currentItem.title;
  formState.type = currentItem.type;
  formState.content = currentItem.content;
  formState.isPublished = currentItem.isPublished;
  formState.publishedAt = currentItem.publishedAt;
  modalOpen.value = true;
}

async function submitItem() {
  saving.value = true;
  try {
    const payload = {
      content: formState.content,
      isPublished: formState.isPublished,
      publishedAt: formState.publishedAt || null,
      title: formState.title,
      type: formState.type,
    };

    if (editingItem.value) {
      await updateCustomerApi(editingItem.value.id, payload);
      message.success('客户资料已更新');
    } else {
      await createCustomerApi(payload);
      message.success('客户资料已新增');
    }

    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeItem(item: CustomerItem | Record<string, any>) {
  const currentItem = item as CustomerItem;
  const deleted = await deleteCustomerApi(currentItem.id);
  if (deleted) {
    message.success('客户资料已删除');
  }
  await loadData();
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
            placeholder="客户名称/类型/备注"
          />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>
      <div class="toolbar">
        <h3>客户资料列表</h3>
        <Button v-if="canCreate" type="primary" @click="openCreateModal">新增</Button>
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
          <template v-if="column.dataIndex === 'action'">
            <Space>
              <Button v-if="canUpdate" type="link" size="small" @click="openEditModal(record)">编辑</Button>
              <Popconfirm title="确认删除这条数据？" @confirm="removeItem(record)">
                <Button v-if="canDelete" danger type="link" size="small">删除</Button>
              </Popconfirm>
            </Space>
          </template>
          <template v-if="column.dataIndex === 'isPublished'">
            {{ record.isPublished ? '启用' : '停用' }}
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
      <FormItem label="客户名称">
        <Input v-model:value="formState.title" allow-clear />
      </FormItem>
      <FormItem label="客户类型">
        <Input v-model:value="formState.type" allow-clear />
      </FormItem>
      <FormItem label="备注">
        <Input v-model:value="formState.content" allow-clear />
      </FormItem>
      <FormItem label="启用状态">
        <InputNumber
          v-model:value="formState.isPublished"
          class="form-control"
          :max="1"
          :min="0"
        />
      </FormItem>
      <FormItem label="跟进时间">
        <Input
          v-model:value="formState.publishedAt"
          allow-clear
          placeholder="如 2026-05-30T09:00:00+08:00，可留空"
        />
      </FormItem>
      </Form>
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
</style>
