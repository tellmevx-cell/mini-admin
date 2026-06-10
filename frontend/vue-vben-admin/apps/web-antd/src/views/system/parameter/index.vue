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
  createSystemParameterApi,
  deleteSystemParameterApi,
  getSystemParameterListApi,
  type SystemParameterItem,
  updateSystemParameterApi,
} from '#/api/system/parameter';
import { useBrandingStore } from '#/store';

interface ParameterFormState {
  group: string;
  isEnabled: boolean;
  key: string;
  name: string;
  order: number;
  remark: string;
  value: string;
}

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const editingParameter = ref<SystemParameterItem>();
const parameters = ref<SystemParameterItem[]>([]);
const total = ref(0);
const brandingStore = useBrandingStore();
const query = reactive({
  group: '',
  key: '',
  name: '',
  page: 1,
  pageSize: 10,
});
const formState = reactive<ParameterFormState>({
  group: 'system',
  isEnabled: true,
  key: '',
  name: '',
  order: 0,
  remark: '',
  value: '',
});
const { hasAccessByCodes } = useAccess();

const columns = [
  { dataIndex: 'name', title: '参数名称', width: 180 },
  { dataIndex: 'key', title: '参数键名', width: 220 },
  { dataIndex: 'value', title: '参数键值' },
  { dataIndex: 'group', title: '参数分组', width: 130 },
  { dataIndex: 'order', title: '排序', width: 90 },
  { dataIndex: 'state', title: '状态', width: 100 },
  { dataIndex: 'remark', title: '备注', width: 220 },
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
  editingParameter.value ? '编辑参数' : '新增参数',
);
const canCreate = computed(() => hasAccessByCodes(['system:parameter:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:parameter:update']));
const canDelete = computed(() => hasAccessByCodes(['system:parameter:delete']));

function isBrandingParameterKey(key?: string) {
  return key?.startsWith('app.brand.') || key?.startsWith('app.watermark.');
}

async function refreshBrandingIfNeeded(...keys: Array<string | undefined>) {
  if (keys.some((key) => isBrandingParameterKey(key))) {
    await brandingStore.loadBranding();
  }
}

async function loadParameters() {
  loading.value = true;
  try {
    const result = await getSystemParameterListApi({
      group: query.group || undefined,
      key: query.key || undefined,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    parameters.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadParameters();
}

function handleReset() {
  query.group = '';
  query.key = '';
  query.name = '';
  query.page = 1;
  void loadParameters();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadParameters();
}

function resetForm() {
  editingParameter.value = undefined;
  formState.key = '';
  formState.name = '';
  formState.value = '';
  formState.group = 'system';
  formState.remark = '';
  formState.order = 0;
  formState.isEnabled = true;
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(parameter: Record<string, any> | SystemParameterItem) {
  const currentParameter = parameter as SystemParameterItem;
  editingParameter.value = currentParameter;
  formState.key = currentParameter.key;
  formState.name = currentParameter.name;
  formState.value = currentParameter.value;
  formState.group = currentParameter.group;
  formState.remark = currentParameter.remark ?? '';
  formState.order = currentParameter.order;
  formState.isEnabled = currentParameter.isEnabled;
  modalOpen.value = true;
}

async function submitParameter() {
  if (
    !formState.key.trim() ||
    !formState.name.trim() ||
    !formState.group.trim()
  ) {
    message.warning('请填写参数键名、参数名称和参数分组');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      group: formState.group,
      isEnabled: formState.isEnabled,
      key: formState.key,
      name: formState.name,
      order: formState.order,
      remark: formState.remark || null,
      value: formState.value,
    };

    if (editingParameter.value) {
      await updateSystemParameterApi(editingParameter.value.id, payload);
      await refreshBrandingIfNeeded(editingParameter.value.key, payload.key);
      message.success('参数已更新');
    } else {
      await createSystemParameterApi(payload);
      await refreshBrandingIfNeeded(payload.key);
      message.success('参数已新增');
    }

    modalOpen.value = false;
    await loadParameters();
  } finally {
    saving.value = false;
  }
}

async function removeParameter(parameter: Record<string, any> | SystemParameterItem) {
  const currentParameter = parameter as SystemParameterItem;
  const deleted = await deleteSystemParameterApi(currentParameter.id);
  if (deleted) {
    await refreshBrandingIfNeeded(currentParameter.key);
    message.success('参数已删除');
  }
  await loadParameters();
}

onMounted(loadParameters);
</script>

<template>
  <Page auto-content-height>
    <div class="parameter-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">参数名称</span>
          <Input
            v-model:value="query.name"
            allow-clear
            class="query-input"
            placeholder="请输入"
          />
          <span class="query-label">参数键名</span>
          <Input
            v-model:value="query.key"
            allow-clear
            class="query-input"
            placeholder="请输入"
          />
          <span class="query-label">参数分组</span>
          <Input
            v-model:value="query.group"
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
          <h3>参数列表</h3>
          <Space>
            <Button @click="loadParameters">刷新缓存</Button>
            <Button>导出</Button>
            <Button v-if="canDelete" disabled>删除</Button>
            <Button v-if="canCreate" type="primary" @click="openCreateModal">新增</Button>
          </Space>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="parameters"
          :loading="loading"
          :pagination="pagination"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'value'">
              <span class="value-cell">{{ record.value }}</span>
            </template>
            <template v-if="column.dataIndex === 'group'">
              <Tag color="blue">{{ record.group }}</Tag>
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
                  title="确认删除该参数？"
                  @confirm="removeParameter(record)"
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
      width="720px"
      @ok="submitParameter"
    >
      <Form layout="vertical">
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="参数键名" required>
            <Input v-model:value="formState.key" placeholder="例如 site_name" />
          </FormItem>
          <FormItem label="参数名称" required>
            <Input v-model:value="formState.name" placeholder="例如 站点名称" />
          </FormItem>
          <FormItem label="参数分组" required>
            <Input v-model:value="formState.group" placeholder="例如 system" />
          </FormItem>
          <FormItem label="排序">
            <InputNumber v-model:value="formState.order" class="w-full" />
          </FormItem>
        </div>
        <FormItem label="参数键值">
          <Input v-model:value="formState.value" placeholder="请输入参数值" />
        </FormItem>
        <FormItem label="备注">
          <Input v-model:value="formState.remark" placeholder="可选" />
        </FormItem>
        <FormItem label="启用">
          <Switch v-model:checked="formState.isEnabled" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.parameter-workspace {
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

.value-cell {
  display: inline-block;
  max-width: 320px;
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

@media (max-width: 1200px) {
  .query-bar {
    align-items: flex-start;
    flex-direction: column;
  }

  .query-input {
    width: 200px;
  }
}
</style>
