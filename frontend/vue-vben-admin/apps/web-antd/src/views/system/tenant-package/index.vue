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
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Textarea,
  Tree,
  message,
} from 'ant-design-vue';

import { getMenuTreeApi, type MenuTreeNode } from '#/api/system/menu';
import {
  createTenantPackageApi,
  disableTenantPackageApi,
  enableTenantPackageApi,
  getTenantPackageListApi,
  getTenantPackageMenusApi,
  type TenantPackageItem,
  updateTenantPackageApi,
  updateTenantPackageMenusApi,
} from '#/api/platform/tenant-package';

interface PackageFormState {
  isEnabled: boolean;
  maxStorageMb: number;
  maxUsers: number;
  name: string;
  remark: string;
}

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const saving = ref(false);
const statusSavingId = ref('');
const permissionLoading = ref(false);
const permissionSaving = ref(false);
const modalOpen = ref(false);
const permissionModalOpen = ref(false);
const editingPackage = ref<TenantPackageItem>();
const assigningPackage = ref<TenantPackageItem>();
const packages = ref<TenantPackageItem[]>([]);
const menuTree = ref<MenuTreeNode[]>([]);
const checkedMenuIds = ref<string[]>([]);
const total = ref(0);

const query = reactive({
  isEnabled: undefined as string | undefined,
  name: '',
  page: 1,
  pageSize: 10,
});

const formState = reactive<PackageFormState>({
  isEnabled: true,
  maxStorageMb: 1024,
  maxUsers: 100,
  name: '',
  remark: '',
});

const columns = [
  { dataIndex: 'name', title: '套餐名称', width: 180 },
  { dataIndex: 'quota', title: '配额', width: 220 },
  { dataIndex: 'menuCount', title: '菜单权限', width: 120 },
  { dataIndex: 'isEnabled', title: '状态', width: 100 },
  { dataIndex: 'remark', title: '备注' },
  { dataIndex: 'action', title: '操作', width: 260 },
];

const enabledOptions = [
  { label: '启用', value: 'true' },
  { label: '停用', value: 'false' },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));

const permissionTreeData = computed(() => menuTree.value as any[]);

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
  editingPackage.value ? '编辑套餐' : '新增套餐',
);
const canCreate = computed(() => hasAccessByCodes(['platform:tenant:create']));
const canUpdate = computed(() => hasAccessByCodes(['platform:tenant:update']));
const canEnable = computed(() => hasAccessByCodes(['platform:tenant:enable']));
const canDisable = computed(() => hasAccessByCodes(['platform:tenant:disable']));

function getParentMenuIds(items: MenuTreeNode[]) {
  const parentIds = new Set<string>();

  function walk(nodes: MenuTreeNode[]) {
    for (const node of nodes) {
      if (node.children.length > 0) {
        parentIds.add(node.id);
        walk(node.children);
      }
    }
  }

  walk(items);
  return parentIds;
}

function filterLeafMenuIds(menuIds: string[], menus: MenuTreeNode[]) {
  const parentIds = getParentMenuIds(menus);
  return menuIds.filter((menuId) => !parentIds.has(menuId));
}

async function loadPackages() {
  loading.value = true;
  try {
    const result = await getTenantPackageListApi({
      isEnabled: parseBooleanSelectValue(query.isEnabled),
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    packages.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.name = query.name.trim();
  query.page = 1;
  void loadPackages();
}

function handleReset() {
  query.name = '';
  query.isEnabled = undefined;
  query.page = 1;
  void loadPackages();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadPackages();
}

function resetForm() {
  editingPackage.value = undefined;
  formState.name = '';
  formState.maxUsers = 100;
  formState.maxStorageMb = 1024;
  formState.isEnabled = true;
  formState.remark = '';
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(item: Record<string, any> | TenantPackageItem) {
  const currentItem = item as TenantPackageItem;
  editingPackage.value = currentItem;
  formState.name = currentItem.name;
  formState.maxUsers = currentItem.maxUsers;
  formState.maxStorageMb = currentItem.maxStorageMb;
  formState.isEnabled = currentItem.isEnabled;
  formState.remark = currentItem.remark ?? '';
  modalOpen.value = true;
}

async function submitPackage() {
  if (!formState.name.trim()) {
    message.warning('请填写套餐名称');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      isEnabled: formState.isEnabled,
      maxStorageMb: formState.maxStorageMb,
      maxUsers: formState.maxUsers,
      name: formState.name.trim(),
      remark: formState.remark.trim() || null,
    };
    if (editingPackage.value) {
      await updateTenantPackageApi(editingPackage.value.id, payload);
      message.success('套餐已更新');
    } else {
      await createTenantPackageApi(payload);
      message.success('套餐已新增');
    }

    modalOpen.value = false;
    await loadPackages();
  } finally {
    saving.value = false;
  }
}

async function switchPackageStatus(item: Record<string, any> | TenantPackageItem, enabled: boolean) {
  const currentItem = item as TenantPackageItem;
  statusSavingId.value = currentItem.id;
  try {
    if (enabled) {
      await enableTenantPackageApi(currentItem.id);
      message.success('套餐已启用');
    } else {
      await disableTenantPackageApi(currentItem.id);
      message.success('套餐已停用');
    }
    await loadPackages();
  } finally {
    statusSavingId.value = '';
  }
}

async function openPermissionModal(item: Record<string, any> | TenantPackageItem) {
  const currentItem = item as TenantPackageItem;
  assigningPackage.value = currentItem;
  permissionModalOpen.value = true;
  permissionLoading.value = true;
  try {
    const [menus, packageMenuIds] = await Promise.all([
      getMenuTreeApi(),
      getTenantPackageMenusApi(currentItem.id),
    ]);
    menuTree.value = menus;
    checkedMenuIds.value = filterLeafMenuIds(packageMenuIds, menus);
  } finally {
    permissionLoading.value = false;
  }
}

async function submitPermissions() {
  if (!assigningPackage.value) {
    return;
  }

  permissionSaving.value = true;
  try {
    checkedMenuIds.value = await updateTenantPackageMenusApi(
      assigningPackage.value.id,
      checkedMenuIds.value,
    );
    message.success('套餐权限已保存，超出套餐的租户角色权限已自动清理');
    permissionModalOpen.value = false;
    await loadPackages();
  } finally {
    permissionSaving.value = false;
  }
}

onMounted(loadPackages);
</script>

<template>
  <Page description="维护租户可使用的菜单和按钮权限范围。" title="租户套餐">
    <div class="package-workspace">
      <div class="query-bar">
        <Space wrap>
          <Input
            v-model:value="query.name"
            allow-clear
            class="query-input"
            placeholder="套餐名称"
          />
          <Select
            v-model:value="query.isEnabled"
            allow-clear
            class="query-select"
            :options="enabledOptions"
            placeholder="状态"
          />
          <Button type="primary" @click="handleSearch">查询</Button>
          <Button @click="handleReset">重置</Button>
        </Space>
        <Button v-if="canCreate" type="primary" @click="openCreateModal">
          新增套餐
        </Button>
      </div>

      <div class="table-shell">
        <Table
          row-key="id"
          :columns="columns"
          :data-source="packages"
          :loading="loading"
          :pagination="pagination"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'quota'">
              <Space>
                <Tag color="blue">
                  {{ record.maxUsers === 0 ? '用户不限' : `${record.maxUsers} 用户` }}
                </Tag>
                <Tag color="cyan">
                  {{ record.maxStorageMb === 0 ? '存储不限' : `${record.maxStorageMb} MB` }}
                </Tag>
              </Space>
            </template>
            <template v-if="column.dataIndex === 'menuCount'">
              <Tag color="purple">{{ record.menuCount }} 项</Tag>
            </template>
            <template v-if="column.dataIndex === 'isEnabled'">
              <Tag :color="record.isEnabled ? 'green' : 'default'">
                {{ record.isEnabled ? '启用' : '停用' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'remark'">
              <span class="remark-cell">{{ record.remark || '-' }}</span>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Space>
                <Button v-if="canUpdate" size="small" @click="openEditModal(record)">
                  编辑
                </Button>
                <Button
                  v-if="canUpdate"
                  size="small"
                  @click="openPermissionModal(record)"
                >
                  分配权限
                </Button>
                <Popconfirm
                  v-if="record.isEnabled && canDisable"
                  title="确认停用该套餐？"
                  @confirm="switchPackageStatus(record, false)"
                >
                  <Button
                    danger
                    :loading="statusSavingId === record.id"
                    size="small"
                  >
                    停用
                  </Button>
                </Popconfirm>
                <Popconfirm
                  v-if="!record.isEnabled && canEnable"
                  title="确认启用该套餐？"
                  @confirm="switchPackageStatus(record, true)"
                >
                  <Button
                    :loading="statusSavingId === record.id"
                    size="small"
                    type="primary"
                  >
                    启用
                  </Button>
                </Popconfirm>
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
      @ok="submitPackage"
    >
      <Form layout="vertical">
        <FormItem label="套餐名称" required>
          <Input v-model:value="formState.name" placeholder="例如 企业版" />
        </FormItem>
        <div class="grid grid-cols-2 gap-4">
          <FormItem extra="填写 0 表示不限制用户数量" label="最大用户数">
            <InputNumber
              v-model:value="formState.maxUsers"
              class="w-full"
              :min="0"
            />
          </FormItem>
          <FormItem extra="填写 0 表示不限制存储容量" label="存储容量(MB)">
            <InputNumber
              v-model:value="formState.maxStorageMb"
              class="w-full"
              :min="0"
            />
          </FormItem>
        </div>
        <FormItem label="启用状态">
          <Switch v-model:checked="formState.isEnabled" />
        </FormItem>
        <FormItem label="备注">
          <Textarea
            v-model:value="formState.remark"
            :auto-size="{ minRows: 3, maxRows: 5 }"
            placeholder="请输入备注"
          />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="permissionModalOpen"
      :confirm-loading="permissionSaving"
      :title="`分配套餐权限${assigningPackage ? ` - ${assigningPackage.name}` : ''}`"
      width="620px"
      @ok="submitPermissions"
    >
      <Tree
        v-model:checked-keys="checkedMenuIds"
        checkable
        :field-names="{ children: 'children', key: 'id', title: 'title' }"
        :loading="permissionLoading"
        :tree-data="permissionTreeData"
      />
    </Modal>
  </Page>
</template>

<style scoped>
.package-workspace {
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

.query-input {
  width: 180px;
}

.query-select {
  width: 120px;
}

.table-shell {
  min-height: 0;
  flex: 1;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px 10px 0;
}

.remark-cell {
  display: inline-block;
  max-width: 360px;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: bottom;
  white-space: nowrap;
}

:deep(.ant-table) {
  font-size: 13px;
}
</style>
