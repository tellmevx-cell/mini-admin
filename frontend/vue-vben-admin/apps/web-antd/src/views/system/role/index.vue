<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import { useAccessStore } from '@vben/stores';

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
  Tree,
  TreeSelect,
  message,
} from 'ant-design-vue';

import {
  getDepartmentListApi,
  type DepartmentItem,
} from '#/api/system/department';
import { getMenuTreeApi, type MenuTreeNode } from '#/api/system/menu';
import {
  createRoleApi,
  deleteRoleApi,
  getRoleListApi,
  getRoleMenusApi,
  type RoleListItem,
  updateRoleMenusApi,
  updateRoleApi,
} from '#/api/system/role';
import { getAccessCodesApi } from '#/api';

const { hasAccessByCodes } = useAccess();
const accessStore = useAccessStore();

interface RoleFormState {
  code: string;
  customDepartmentIds: string[];
  dataScope: string;
  isEnabled: boolean;
  name: string;
}

interface DepartmentTreeSelectNode {
  children: DepartmentTreeSelectNode[];
  key: string;
  title: string;
  value: string;
}

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const permissionModalOpen = ref(false);
const permissionSaving = ref(false);
const permissionLoading = ref(false);
const editingRole = ref<RoleListItem>();
const assigningRole = ref<RoleListItem>();
const roles = ref<RoleListItem[]>([]);
const menuTree = ref<MenuTreeNode[]>([]);
const departments = ref<DepartmentItem[]>([]);
const checkedMenuIds = ref<string[]>([]);
const total = ref(0);
const query = reactive({
  code: '',
  name: '',
  page: 1,
  pageSize: 10,
});
const formState = reactive<RoleFormState>({
  code: '',
  customDepartmentIds: [],
  dataScope: 'all',
  isEnabled: true,
  name: '',
});

const dataScopeOptions = [
  { label: '全部数据', value: 'all' },
  { label: '本部门及下级', value: 'department-and-children' },
  { label: '本部门', value: 'department' },
  { label: '自定义部门', value: 'custom' },
  { label: '仅本人', value: 'self' },
];

const columns = [
  {
    dataIndex: 'code',
    title: '角色编码',
    width: 220,
  },
  {
    dataIndex: 'name',
    title: '角色名称',
  },
  {
    dataIndex: 'dataScope',
    title: '数据范围',
    width: 150,
  },
  {
    dataIndex: 'status',
    title: '状态',
    width: 120,
  },
  {
    dataIndex: 'action',
    title: '操作',
    width: 180,
  },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  total: total.value,
}));

const modalTitle = computed(() => (editingRole.value ? '编辑角色' : '新增角色'));
const canCreate = computed(() => hasAccessByCodes(['system:role:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:role:update']));
const canDelete = computed(() => hasAccessByCodes(['system:role:delete']));
const canAssign = computed(() => hasAccessByCodes(['system:role:assign']));
const departmentTreeData = computed(() => mapDepartmentTree(departments.value));
const menuTreeData = computed(() => menuTree.value as any[]);

function getDataScopeLabel(dataScope: string) {
  return dataScopeOptions.find((item) => item.value === dataScope)?.label ?? dataScope;
}

function mapDepartmentTree(items: DepartmentItem[]): DepartmentTreeSelectNode[] {
  return items.map((item) => ({
    children: mapDepartmentTree(item.children),
    key: item.id,
    title: item.name,
    value: item.id,
  }));
}

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

async function loadRoles() {
  loading.value = true;
  try {
    const result = await getRoleListApi({
      code: query.code || undefined,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    roles.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

async function loadDepartments() {
  departments.value = await getDepartmentListApi();
}

function handleSearch() {
  query.page = 1;
  void loadRoles();
}

function handleReset() {
  query.code = '';
  query.name = '';
  query.page = 1;
  void loadRoles();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadRoles();
}

function openCreateModal() {
  editingRole.value = undefined;
  formState.code = '';
  formState.customDepartmentIds = [];
  formState.dataScope = 'all';
  formState.name = '';
  formState.isEnabled = true;
  modalOpen.value = true;
}

function openEditModal(role: Record<string, any> | RoleListItem) {
  const currentRole = role as RoleListItem;
  editingRole.value = currentRole;
  formState.code = currentRole.code;
  formState.customDepartmentIds = [...(currentRole.customDepartmentIds ?? [])];
  formState.dataScope = currentRole.dataScope || 'all';
  formState.name = currentRole.name;
  formState.isEnabled = currentRole.status === 1;
  modalOpen.value = true;
}

async function openPermissionModal(role: Record<string, any> | RoleListItem) {
  const currentRole = role as RoleListItem;
  assigningRole.value = currentRole;
  permissionModalOpen.value = true;
  permissionLoading.value = true;
  try {
    const [menus, roleMenuIds] = await Promise.all([
      getMenuTreeApi(),
      getRoleMenusApi(currentRole.id),
    ]);
    menuTree.value = menus;
    checkedMenuIds.value = filterLeafMenuIds(roleMenuIds, menus);
  } finally {
    permissionLoading.value = false;
  }
}

async function submitRole() {
  if (!formState.code.trim() || !formState.name.trim()) {
    message.warning('请填写角色编码和角色名称');
    return;
  }

  if (
    formState.dataScope === 'custom' &&
    formState.customDepartmentIds.length === 0
  ) {
    message.warning('自定义数据范围至少选择一个部门');
    return;
  }

  saving.value = true;
  try {
    if (editingRole.value) {
      await updateRoleApi(editingRole.value.id, {
        customDepartmentIds:
          formState.dataScope === 'custom'
            ? [...formState.customDepartmentIds]
            : undefined,
        dataScope: formState.dataScope,
        isEnabled: formState.isEnabled,
        name: formState.name,
      });
      message.success('角色已更新');
    } else {
      await createRoleApi({
        code: formState.code,
        customDepartmentIds:
          formState.dataScope === 'custom'
            ? [...formState.customDepartmentIds]
            : undefined,
        dataScope: formState.dataScope,
        isEnabled: formState.isEnabled,
        name: formState.name,
      });
      message.success('角色已新增');
    }

    modalOpen.value = false;
    await loadRoles();
  } finally {
    saving.value = false;
  }
}

async function removeRole(role: Record<string, any> | RoleListItem) {
  const currentRole = role as RoleListItem;
  const deleted = await deleteRoleApi(currentRole.id);
  if (deleted) {
    message.success('角色已删除');
  } else {
    message.warning('该角色正在使用，不能删除');
  }
  await loadRoles();
}

async function submitPermissions() {
  if (!assigningRole.value) {
    return;
  }

  permissionSaving.value = true;
  try {
    checkedMenuIds.value = await updateRoleMenusApi(
      assigningRole.value.id,
      checkedMenuIds.value,
    );
    accessStore.setAccessCodes(await getAccessCodesApi());
    message.success('权限已保存');
    permissionModalOpen.value = false;
  } finally {
    permissionSaving.value = false;
  }
}

onMounted(async () => {
  await Promise.all([loadRoles(), loadDepartments()]);
});
</script>

<template>
  <Page description="维护角色编码、名称和启停状态。" title="角色管理">
    <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
      <Space wrap>
        <Input v-model:value="query.code" allow-clear placeholder="角色编码" />
        <Input v-model:value="query.name" allow-clear placeholder="角色名称" />
        <Button type="primary" @click="handleSearch">查询</Button>
        <Button @click="handleReset">重置</Button>
      </Space>
      <Button v-if="canCreate" type="primary" @click="openCreateModal">
        新增角色
      </Button>
    </div>

    <Table
      row-key="id"
      :columns="columns"
      :data-source="roles"
      :loading="loading"
      :pagination="pagination"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'status'">
          <Tag :color="record.status === 1 ? 'green' : 'default'">
            {{ record.status === 1 ? '启用' : '停用' }}
          </Tag>
        </template>
        <template v-if="column.dataIndex === 'dataScope'">
          <Tag color="blue">{{ getDataScopeLabel(record.dataScope) }}</Tag>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <Space>
            <Button v-if="canUpdate" type="link" @click="openEditModal(record)">
              编辑
            </Button>
            <Button v-if="canAssign" type="link" @click="openPermissionModal(record)">
              分配权限
            </Button>
            <Popconfirm
              v-if="canDelete && record.code !== 'admin'"
              title="确认删除该角色？"
              @confirm="removeRole(record)"
            >
              <Button danger type="link">删除</Button>
            </Popconfirm>
            <span
              v-if="
                !canUpdate &&
                !canAssign &&
                (!canDelete || record.code === 'admin')
              "
            >
              -
            </span>
          </Space>
        </template>
      </template>
    </Table>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="modalTitle"
      @ok="submitRole"
    >
      <Form layout="vertical">
        <FormItem label="角色编码" required>
          <Input
            v-model:value="formState.code"
            :disabled="Boolean(editingRole)"
            placeholder="例如 manager"
          />
        </FormItem>
        <FormItem label="角色名称" required>
          <Input v-model:value="formState.name" placeholder="例如 运营管理员" />
        </FormItem>
        <FormItem label="数据范围" required>
          <Select
            v-model:value="formState.dataScope"
            :options="dataScopeOptions"
            placeholder="请选择数据范围"
          />
        </FormItem>
        <FormItem
          v-if="formState.dataScope === 'custom'"
          label="可见部门"
          required
        >
          <TreeSelect
            v-model:value="formState.customDepartmentIds"
            allow-clear
            multiple
            tree-checkable
            :tree-data="departmentTreeData"
            placeholder="请选择可见部门"
          />
        </FormItem>
        <FormItem label="启用状态">
          <Switch v-model:checked="formState.isEnabled" />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="permissionModalOpen"
      :confirm-loading="permissionSaving"
      :title="`分配权限${assigningRole ? ` - ${assigningRole.name}` : ''}`"
      @ok="submitPermissions"
    >
      <Tree
        v-model:checked-keys="checkedMenuIds"
        checkable
        :field-names="{ children: 'children', key: 'id', title: 'title' }"
        :loading="permissionLoading"
        :tree-data="menuTreeData"
      />
    </Modal>
  </Page>
</template>
