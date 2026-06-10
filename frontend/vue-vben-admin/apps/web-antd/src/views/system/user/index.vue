<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';

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
  message,
} from 'ant-design-vue';

import {
  getDepartmentListApi,
  type DepartmentItem,
} from '#/api/system/department';
import { getPositionListApi, type PositionItem } from '#/api/system/position';
import { getRoleListApi, type RoleListItem } from '#/api/system/role';
import {
  createUserApi,
  deleteUserApi,
  downloadUserImportErrorReportApi,
  downloadUserImportTemplateApi,
  exportUserApi,
  getUserListApi,
  importUserApi,
  previewImportUserApi,
  type UserImportResult,
  resetUserPasswordApi,
  type UserListItem,
  unlockUserLoginApi,
  updateUserApi,
} from '#/api/system/user';
import { useAuthStore } from '#/store';

const { hasAccessByCodes } = useAccess();
const authStore = useAuthStore();
const userStore = useUserStore();

interface UserFormState {
  departmentId?: string;
  email: string;
  isEnabled: boolean;
  password: string;
  positionId?: string;
  realName: string;
  roleIds: string[];
  userName: string;
}

const loading = ref(false);
const saving = ref(false);
const unlockingUserName = ref('');
const exportingUsers = ref(false);
const importingUsers = ref(false);
const previewingImport = ref(false);
const downloadingTemplate = ref(false);
const downloadingErrorReport = ref(false);
const modalOpen = ref(false);
const importPreviewModalOpen = ref(false);
const resetPasswordModalOpen = ref(false);
const resettingPassword = ref(false);
const importInputRef = ref<HTMLInputElement>();
const importPreviewFile = ref<File>();
const importPreviewResult = ref<UserImportResult>();
const editingUser = ref<UserListItem>();
const resetPasswordUser = ref<UserListItem>();
const users = ref<UserListItem[]>([]);
const total = ref(0);
const departments = ref<DepartmentItem[]>([]);
const positions = ref<PositionItem[]>([]);
const roles = ref<RoleListItem[]>([]);
const query = reactive({
  departmentId: undefined as string | undefined,
  page: 1,
  pageSize: 10,
  positionId: undefined as string | undefined,
  userName: '',
});
const formState = reactive<UserFormState>({
  departmentId: undefined,
  email: '',
  isEnabled: true,
  password: '',
  positionId: undefined,
  realName: '',
  roleIds: [],
  userName: '',
});
const resetPasswordForm = reactive({
  confirmPassword: '',
  newPassword: '',
});

const columns = [
  {
    dataIndex: 'userName',
    title: '用户名',
    width: 180,
  },
  {
    dataIndex: 'realName',
    title: '姓名',
    width: 180,
  },
  {
    dataIndex: 'email',
    title: '邮箱',
    width: 220,
  },
  {
    dataIndex: 'departmentName',
    title: '所属部门',
    width: 180,
  },
  {
    dataIndex: 'positionName',
    title: '所属岗位',
    width: 180,
  },
  {
    dataIndex: 'roles',
    title: '角色',
  },
  {
    dataIndex: 'status',
    title: '状态',
    width: 120,
  },
  {
    dataIndex: 'loginLockRemainingSeconds',
    title: '登录状态',
    width: 150,
  },
  {
    dataIndex: 'action',
    title: '操作',
    width: 300,
  },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  total: total.value,
}));

const modalTitle = computed(() => (editingUser.value ? '编辑用户' : '新增用户'));
const canCreate = computed(() => hasAccessByCodes(['system:user:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:user:update']));
const canDelete = computed(() => hasAccessByCodes(['system:user:delete']));
const canUnlock = computed(() => hasAccessByCodes(['system:user:unlock']));
const canResetPassword = computed(() =>
  hasAccessByCodes(['system:user:reset-password']),
);
const canImport = computed(() => hasAccessByCodes(['system:user:import']));
const canExport = computed(() => hasAccessByCodes(['system:user:export']));
const selectedDepartmentKeys = computed(() =>
  query.departmentId ? [query.departmentId] : [],
);
const selectedDepartmentName = computed(() =>
  findDepartmentName(departments.value, query.departmentId),
);

const departmentOptions = computed(() => {
  const options: { label: string; value: string }[] = [];

  function walk(items: DepartmentItem[], level = 0) {
    for (const item of items) {
      options.push({
        label: `${'  '.repeat(level)}${item.name}`,
        value: item.id,
      });
      walk(item.children, level + 1);
    }
  }

  walk(departments.value);
  return options;
});

const roleOptions = computed(() =>
  roles.value.map((role) => ({
    label: role.name,
    value: role.id,
  })),
);

const departmentTreeData = computed(() => departments.value as any[]);

const positionOptions = computed(() =>
  positions.value.map((position) => ({
    label: position.name,
    value: position.id,
  })),
);

function findDepartmentName(
  items: DepartmentItem[],
  departmentId?: string,
): string | undefined {
  if (!departmentId) {
    return undefined;
  }

  for (const item of items) {
    if (item.id === departmentId) {
      return item.name;
    }

    const childName = findDepartmentName(item.children, departmentId);
    if (childName) {
      return childName;
    }
  }

  return undefined;
}

async function loadUsers() {
  loading.value = true;
  try {
    const result = await getUserListApi({
      page: query.page,
      pageSize: query.pageSize,
      departmentId: query.departmentId,
      positionId: query.positionId,
      userName: query.userName || undefined,
    });
    users.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

async function loadFormOptions() {
  const [, roleResult] = await Promise.all([
    loadOrganizationOptions(),
    getRoleListApi({ page: 1, pageSize: 100 }),
  ]);
  roles.value = roleResult.items;
}

async function loadOrganizationOptions() {
  const [departmentTree, positionResult] = await Promise.all([
    getDepartmentListApi(),
    getPositionListApi({ page: 1, pageSize: 100 }),
  ]);
  departments.value = departmentTree;
  positions.value = positionResult.items;
}

function resetForm() {
  editingUser.value = undefined;
  formState.userName = '';
  formState.realName = '';
  formState.email = '';
  formState.password = '';
  formState.departmentId = undefined;
  formState.positionId = undefined;
  formState.roleIds = [];
  formState.isEnabled = true;
}

function hasRoleChanged(user: UserListItem) {
  const nextRoleCodes = roles.value
    .filter((role) => formState.roleIds.includes(role.id))
    .map((role) => role.code)
    .sort();
  const currentRoleCodes = [...user.roles].sort();

  return nextRoleCodes.join('|') !== currentRoleCodes.join('|');
}

function shouldRequireReloginAfterUpdate(user: UserListItem) {
  const currentUserName = userStore.userInfo?.username;
  if (!currentUserName || user.userName !== currentUserName) {
    return false;
  }

  return hasRoleChanged(user) || !formState.isEnabled;
}

function isLoginLocked(user: Record<string, any> | UserListItem) {
  const currentUser = user as UserListItem;
  return (currentUser.loginLockRemainingSeconds ?? 0) > 0;
}

function formatLoginLockRemaining(user: Record<string, any> | UserListItem) {
  const currentUser = user as UserListItem;
  return `${Math.ceil((currentUser.loginLockRemainingSeconds ?? 0) / 60)}分钟`;
}

function canResetPasswordForUser(user: Record<string, any> | UserListItem) {
  const currentUser = user as UserListItem;
  return (
    canResetPassword.value && currentUser.userName !== userStore.userInfo?.username
  );
}

async function openCreateModal() {
  resetForm();
  await loadFormOptions();
  modalOpen.value = true;
}

async function openEditModal(user: Record<string, any> | UserListItem) {
  const currentUser = user as UserListItem;
  await loadFormOptions();
  editingUser.value = currentUser;
  formState.userName = currentUser.userName;
  formState.realName = currentUser.realName;
  formState.email = currentUser.email ?? '';
  formState.password = '';
  formState.departmentId = currentUser.departmentId ?? undefined;
  formState.positionId = currentUser.positionId ?? undefined;
  formState.roleIds = roles.value
    .filter((role) => currentUser.roles.includes(role.code))
    .map((role) => role.id);
  formState.isEnabled = currentUser.status === 1;
  modalOpen.value = true;
}

async function submitUser() {
  if (!formState.userName.trim() || !formState.realName.trim()) {
    message.warning('请填写用户名和姓名');
    return;
  }

  if (!editingUser.value && !formState.password.trim()) {
    message.warning('新增用户需要填写初始密码');
    return;
  }

  saving.value = true;
  try {
    if (editingUser.value) {
      const shouldRelogin = shouldRequireReloginAfterUpdate(editingUser.value);
      await updateUserApi(editingUser.value.id, {
        departmentId: formState.departmentId || null,
        email: formState.email.trim() || null,
        isEnabled: formState.isEnabled,
        password: formState.password || null,
        positionId: formState.positionId || null,
        realName: formState.realName,
        roleIds: formState.roleIds,
      });
      if (shouldRelogin) {
        message.warning('当前账户权限已变更，请重新登录');
        modalOpen.value = false;
        await authStore.logout(false, false);
        return;
      }
      message.success('用户已更新');
    } else {
      await createUserApi({
        departmentId: formState.departmentId || null,
        email: formState.email.trim() || null,
        isEnabled: formState.isEnabled,
        password: formState.password,
        positionId: formState.positionId || null,
        realName: formState.realName,
        roleIds: formState.roleIds,
        userName: formState.userName,
      });
      message.success('用户已新增');
    }

    modalOpen.value = false;
    await loadUsers();
  } finally {
    saving.value = false;
  }
}

async function removeUser(user: Record<string, any> | UserListItem) {
  const currentUser = user as UserListItem;
  try {
    await deleteUserApi(currentUser.id);
    message.success('用户已删除');
  } finally {
    await loadUsers();
  }
}

async function unlockLogin(user: Record<string, any> | UserListItem) {
  const currentUser = user as UserListItem;
  unlockingUserName.value = currentUser.userName;
  try {
    await unlockUserLoginApi(currentUser.userName);
    message.success(`${currentUser.userName} 登录锁定已解除`);
    await loadUsers();
  } finally {
    unlockingUserName.value = '';
  }
}

function openResetPasswordModal(user: Record<string, any> | UserListItem) {
  resetPasswordUser.value = user as UserListItem;
  resetPasswordForm.newPassword = '';
  resetPasswordForm.confirmPassword = '';
  resetPasswordModalOpen.value = true;
}

async function submitResetPassword() {
  if (!resetPasswordUser.value) {
    return;
  }

  if (!resetPasswordForm.newPassword.trim()) {
    message.warning('请输入新密码');
    return;
  }

  if (resetPasswordForm.newPassword !== resetPasswordForm.confirmPassword) {
    message.warning('两次输入的密码不一致');
    return;
  }

  resettingPassword.value = true;
  try {
    await resetUserPasswordApi(resetPasswordUser.value.id, {
      confirmPassword: resetPasswordForm.confirmPassword,
      newPassword: resetPasswordForm.newPassword,
    });
    message.success(`${resetPasswordUser.value.userName} 密码已重置`);
    resetPasswordModalOpen.value = false;
  } finally {
    resettingPassword.value = false;
  }
}

async function exportUsers() {
  exportingUsers.value = true;
  try {
    const blob = await exportUserApi({
      departmentId: query.departmentId,
      page: query.page,
      pageSize: query.pageSize,
      positionId: query.positionId,
      userName: query.userName || undefined,
    });
    downloadBlob(blob, 'mini-admin-users.xlsx');
    message.success('用户已导出');
  } finally {
    exportingUsers.value = false;
  }
}

async function downloadImportTemplate() {
  downloadingTemplate.value = true;
  try {
    const blob = await downloadUserImportTemplateApi();
    downloadBlob(blob, 'mini-admin-user-import-template.xlsx');
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
    importPreviewResult.value = await previewImportUserApi(file);
    importPreviewModalOpen.value = true;
  } finally {
    previewingImport.value = false;
  }
}

async function confirmImportUsers() {
  if (!importPreviewFile.value || !importPreviewResult.value) {
    return;
  }

  if (importPreviewResult.value.errors.length > 0) {
    message.warning('请先修正失败行后再导入');
    return;
  }

  importingUsers.value = true;
  try {
    const result = await importUserApi(importPreviewFile.value);
    message.success(`导入成功 ${result.createdCount} 个用户`);
    importPreviewModalOpen.value = false;
    importPreviewFile.value = undefined;
    importPreviewResult.value = undefined;
    await loadUsers();
  } finally {
    importingUsers.value = false;
  }
}

async function downloadImportErrorReport() {
  if (!importPreviewFile.value) {
    return;
  }

  downloadingErrorReport.value = true;
  try {
    const blob = await downloadUserImportErrorReportApi(importPreviewFile.value);
    downloadBlob(blob, 'mini-admin-user-import-errors.xlsx');
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

function handleSearch() {
  query.userName = query.userName.trim();
  query.page = 1;
  void loadUsers();
}

function handleReset() {
  query.userName = '';
  query.positionId = undefined;
  query.page = 1;
  void loadUsers();
}

function handleResetAll() {
  query.userName = '';
  query.departmentId = undefined;
  query.positionId = undefined;
  query.page = 1;
  void loadUsers();
}

function handleDepartmentSelect(selectedKeys: Array<number | string>) {
  query.departmentId = selectedKeys[0]?.toString();
  query.page = 1;
  void loadUsers();
}

function handleAllDepartments() {
  query.departmentId = undefined;
  query.page = 1;
  void loadUsers();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadUsers();
}

onMounted(() => {
  void loadOrganizationOptions();
  void loadUsers();
});
</script>

<template>
  <Page>
    <div class="user-workspace">
      <aside class="department-panel">
        <div class="department-header">
          <h3>部门组织</h3>
          <Button size="small" type="link" @click="handleAllDepartments">
            全部部门
          </Button>
        </div>
        <Tree
          block-node
          :field-names="{ children: 'children', key: 'id', title: 'name' }"
          :selected-keys="selectedDepartmentKeys"
          :tree-data="departmentTreeData"
          @select="handleDepartmentSelect"
        />
      </aside>

      <section class="user-panel">
        <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
          <Space wrap>
            <Input
              v-model:value="query.userName"
              class="w-56"
              allow-clear
              placeholder="搜索用户名"
              @press-enter="handleSearch"
            />
            <Select
              v-model:value="query.positionId"
              allow-clear
              class="w-52"
              :options="positionOptions"
              placeholder="所属岗位"
            />
            <Tag v-if="selectedDepartmentName" color="blue">
              当前部门：{{ selectedDepartmentName }}
            </Tag>
            <Button @click="handleReset">重置筛选</Button>
            <Button @click="handleResetAll">全部重置</Button>
            <Button type="primary" @click="handleSearch">搜索</Button>
          </Space>
          <Space wrap>
            <Button
              v-if="canExport"
              :loading="exportingUsers"
              @click="exportUsers"
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
            <Button v-if="canCreate" type="primary" @click="openCreateModal">
              新增用户
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
          :columns="columns"
          :data-source="users"
          :loading="loading"
          :pagination="pagination"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'departmentName'">
              {{ record.departmentName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'email'">
              {{ record.email || '-' }}
            </template>
            <template v-if="column.dataIndex === 'positionName'">
              {{ record.positionName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'roles'">
              <Tag v-for="role in record.roles" :key="role" color="blue">
                {{ role }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'status'">
              <Tag :color="record.status === 1 ? 'green' : 'default'">
                {{ record.status === 1 ? '启用' : '停用' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'loginLockRemainingSeconds'">
              <Tag :color="isLoginLocked(record) ? 'red' : 'green'">
                {{
                  isLoginLocked(record)
                    ? `已锁定 ${formatLoginLockRemaining(record)}`
                    : '正常'
                }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Space>
                <Button v-if="canUpdate" type="link" @click="openEditModal(record)">
                  编辑
                </Button>
                <Button
                  v-if="canUnlock && isLoginLocked(record)"
                  type="link"
                  :loading="unlockingUserName === record.userName"
                  @click="unlockLogin(record)"
                >
                  解锁
                </Button>
                <Button
                  v-if="canResetPasswordForUser(record)"
                  type="link"
                  @click="openResetPasswordModal(record)"
                >
                  重置密码
                </Button>
                <Popconfirm
                  v-if="canDelete && record.userName !== 'admin'"
                  title="确认删除该用户？"
                  @confirm="removeUser(record)"
                >
                  <Button danger type="link">删除</Button>
                </Popconfirm>
                <span
                  v-if="
                    !canUpdate &&
                    (!canUnlock || !isLoginLocked(record)) &&
                    !canResetPasswordForUser(record) &&
                    (!canDelete || record.userName === 'admin')
                  "
                >
                  -
                </span>
              </Space>
            </template>
          </template>
        </Table>
      </section>
    </div>

    <Modal
      v-model:open="importPreviewModalOpen"
      :confirm-loading="importingUsers"
      :ok-button-props="{
        disabled: Boolean(importPreviewResult?.errors.length),
      }"
      title="导入预检"
      @ok="confirmImportUsers"
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
            { dataIndex: 'userName', title: '用户名', width: 160 },
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

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="modalTitle"
      @ok="submitUser"
    >
      <Form layout="vertical">
        <FormItem label="用户名" required>
          <Input
            v-model:value="formState.userName"
            :disabled="Boolean(editingUser)"
            placeholder="例如 zhangsan"
          />
        </FormItem>
        <FormItem label="姓名" required>
          <Input v-model:value="formState.realName" placeholder="例如 张三" />
        </FormItem>
        <FormItem label="邮箱">
          <Input
            v-model:value="formState.email"
            placeholder="用于邮件通知"
            type="email"
          />
        </FormItem>
        <FormItem :label="editingUser ? '重置密码' : '初始密码'" :required="!editingUser">
          <Input
            v-model:value="formState.password"
            autocomplete="new-password"
            :placeholder="editingUser ? '不填写则不修改' : '例如 123456'"
            type="password"
          />
        </FormItem>
        <FormItem label="所属部门">
          <Select
            v-model:value="formState.departmentId"
            allow-clear
            :options="departmentOptions"
            placeholder="请选择部门"
          />
        </FormItem>
        <FormItem label="所属岗位">
          <Select
            v-model:value="formState.positionId"
            allow-clear
            :options="positionOptions"
            placeholder="请选择岗位"
          />
        </FormItem>
        <FormItem label="角色">
          <Select
            v-model:value="formState.roleIds"
            mode="multiple"
            :options="roleOptions"
            placeholder="请选择角色"
          />
        </FormItem>
        <FormItem label="启用状态">
          <Switch v-model:checked="formState.isEnabled" />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="resetPasswordModalOpen"
      :confirm-loading="resettingPassword"
      title="重置密码"
      @ok="submitResetPassword"
    >
      <Form layout="vertical">
        <FormItem label="用户">
          <Input :value="resetPasswordUser?.userName" disabled />
        </FormItem>
        <FormItem label="新密码" required>
          <Input
            v-model:value="resetPasswordForm.newPassword"
            autocomplete="new-password"
            placeholder="至少 6 位，包含字母和数字"
            type="password"
          />
        </FormItem>
        <FormItem label="确认密码" required>
          <Input
            v-model:value="resetPasswordForm.confirmPassword"
            autocomplete="new-password"
            placeholder="请再次输入新密码"
            type="password"
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.user-workspace {
  display: grid;
  grid-template-columns: 260px minmax(0, 1fr);
  gap: 12px;
  min-height: calc(100vh - 150px);
}

.department-panel,
.user-panel {
  min-width: 0;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 12px;
}

.department-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 8px;
}

.department-header h3 {
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

@media (max-width: 1000px) {
  .user-workspace {
    grid-template-columns: 1fr;
  }
}
</style>
