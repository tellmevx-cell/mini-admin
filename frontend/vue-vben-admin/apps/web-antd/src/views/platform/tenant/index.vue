<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  DatePicker,
  Form,
  FormItem,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import {
  createTenantApi,
  disableTenantApi,
  enableTenantApi,
  getTenantInitializationTemplatesApi,
  getTenantListApi,
  type TenantItem,
  type TenantInitializationStatus,
  type TenantInitializationTemplate,
  type TenantStatus,
  updateTenantApi,
} from '#/api/platform/tenant';
import {
  getTenantPackageOptionsApi,
  type TenantPackageOption,
} from '#/api/platform/tenant-package';

interface TenantFormState {
  adminEmail: string;
  adminPassword: string;
  adminRealName: string;
  adminUserName: string;
  code: string;
  contactEmail: string;
  contactName: string;
  contactPhone: string;
  expireAt: Dayjs | null;
  initializationTemplateCode?: string;
  name: string;
  packageId?: string;
  remark: string;
}

const loading = ref(false);
const saving = ref(false);
const statusSavingId = ref('');
const modalOpen = ref(false);
const editingTenant = ref<TenantItem>();
const tenants = ref<TenantItem[]>([]);
const initializationTemplates = ref<TenantInitializationTemplate[]>([]);
const packageOptions = ref<TenantPackageOption[]>([]);
const total = ref(0);
const { hasAccessByCodes } = useAccess();

const query = reactive({
  code: '',
  name: '',
  page: 1,
  pageSize: 10,
  status: undefined as TenantStatus | undefined,
});

const formState = reactive<TenantFormState>({
  adminEmail: '',
  adminPassword: '',
  adminRealName: '',
  adminUserName: '',
  code: '',
  contactEmail: '',
  contactName: '',
  contactPhone: '',
  expireAt: null,
  initializationTemplateCode: undefined,
  name: '',
  packageId: undefined,
  remark: '',
});

const statusOptions: Array<{ label: string; value: TenantStatus }> = [
  { label: '待开通', value: 'Pending' },
  { label: '启用', value: 'Active' },
  { label: '停用', value: 'Disabled' },
  { label: '已过期', value: 'Expired' },
];

const columns = [
  { dataIndex: 'code', title: '租户编码', width: 160 },
  { dataIndex: 'name', title: '租户名称', width: 180 },
  { dataIndex: 'packageName', title: '套餐', width: 150 },
  { dataIndex: 'status', title: '状态', width: 110 },
  { dataIndex: 'initialization', title: '初始化', width: 190 },
  { dataIndex: 'contact', title: '联系人', width: 220 },
  { dataIndex: 'expireAt', title: '到期时间', width: 190 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 190 },
  { dataIndex: 'remark', title: '备注' },
  { dataIndex: 'action', title: '操作', width: 180 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));

const modalTitle = computed(() =>
  editingTenant.value ? '编辑租户' : '新增租户',
);
const canCreate = computed(() => hasAccessByCodes(['platform:tenant:create']));
const canUpdate = computed(() => hasAccessByCodes(['platform:tenant:update']));
const canEnable = computed(() => hasAccessByCodes(['platform:tenant:enable']));
const canDisable = computed(() => hasAccessByCodes(['platform:tenant:disable']));
const defaultExpireTime = dayjs().hour(23).minute(59).second(59);

async function loadTenants() {
  loading.value = true;
  try {
    const result = await getTenantListApi({
      code: query.code || undefined,
      name: query.name || undefined,
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
    });
    tenants.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

async function loadPackageOptions() {
  packageOptions.value = await getTenantPackageOptionsApi();
}

async function loadInitializationTemplates() {
  initializationTemplates.value = await getTenantInitializationTemplatesApi();
}

function handleSearch() {
  query.code = query.code.trim();
  query.name = query.name.trim();
  query.page = 1;
  void loadTenants();
}

function handleReset() {
  query.code = '';
  query.name = '';
  query.status = undefined;
  query.page = 1;
  void loadTenants();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadTenants();
}

function resetForm() {
  editingTenant.value = undefined;
  formState.code = '';
  formState.name = '';
  formState.contactName = '';
  formState.contactPhone = '';
  formState.contactEmail = '';
  formState.expireAt = null;
  formState.initializationTemplateCode =
    initializationTemplates.value.find((item) => item.isDefault)?.code ??
    initializationTemplates.value[0]?.code;
  formState.packageId = packageOptions.value.find((item) => item.isEnabled)?.id;
  formState.remark = '';
  formState.adminUserName = '';
  formState.adminRealName = '';
  formState.adminEmail = '';
  formState.adminPassword = '';
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(tenant: Record<string, any> | TenantItem) {
  const currentTenant = tenant as TenantItem;
  editingTenant.value = currentTenant;
  formState.code = currentTenant.code;
  formState.name = currentTenant.name;
  formState.contactName = currentTenant.contactName ?? '';
  formState.contactPhone = currentTenant.contactPhone ?? '';
  formState.contactEmail = currentTenant.contactEmail ?? '';
  formState.expireAt = currentTenant.expireAt ? dayjs(currentTenant.expireAt) : null;
  formState.initializationTemplateCode = currentTenant.initializationTemplateCode;
  formState.packageId = currentTenant.packageId ?? undefined;
  formState.remark = currentTenant.remark ?? '';
  formState.adminUserName = '';
  formState.adminRealName = '';
  formState.adminEmail = '';
  formState.adminPassword = '';
  modalOpen.value = true;
}

function buildPayload() {
  return {
    contactEmail: normalizeOptional(formState.contactEmail),
    contactName: normalizeOptional(formState.contactName),
    contactPhone: normalizeOptional(formState.contactPhone),
    expireAt: formState.expireAt ? formState.expireAt.toISOString() : null,
    name: formState.name.trim(),
    packageId: formState.packageId ?? null,
    remark: normalizeOptional(formState.remark),
  };
}

function normalizeExpireTime(value: Dayjs) {
  const current = formState.expireAt ?? defaultExpireTime;

  return value
    .hour(current.hour())
    .minute(current.minute())
    .second(current.second())
    .millisecond(0);
}

function setExpireYears(years: number) {
  formState.expireAt = dayjs()
    .add(years, 'year')
    .hour(23)
    .minute(59)
    .second(59)
    .millisecond(0);
}

function normalizeDatePickerValue(value?: Dayjs | null | string) {
  if (!value) {
    return null;
  }

  const nextValue = typeof value === 'string' ? dayjs(value) : value;
  return nextValue.isValid() ? nextValue : null;
}

function updateExpireAt(value?: Dayjs | null | string) {
  formState.expireAt = normalizeDatePickerValue(value);
}

function setExpireYear(value?: Dayjs | null | string) {
  const nextValue = normalizeDatePickerValue(value);
  if (!nextValue) {
    return;
  }

  formState.expireAt = nextValue
    .month(11)
    .date(31)
    .hour(23)
    .minute(59)
    .second(59)
    .millisecond(0);
}

function handleExpirePanelChange(value?: Dayjs | null | string) {
  const nextValue = normalizeDatePickerValue(value);
  if (nextValue) {
    formState.expireAt = normalizeExpireTime(nextValue);
  }
}

async function submitTenant() {
  const code = formState.code.trim().toLowerCase();
  const name = formState.name.trim();
  if (!name || (!editingTenant.value && !code)) {
    message.warning('请填写租户编码和租户名称');
    return;
  }

  if (
    !editingTenant.value &&
    (!formState.adminUserName.trim() ||
      !formState.adminRealName.trim() ||
      !formState.adminPassword.trim())
  ) {
    message.warning('请填写租户管理员账号、姓名和初始密码');
    return;
  }

  if (!editingTenant.value && !/^[a-z][a-z0-9-]*$/.test(code)) {
    message.warning('租户编码需以小写字母开头，仅支持小写字母、数字和短横线');
    return;
  }

  if (formState.expireAt && !formState.expireAt.isAfter(dayjs())) {
    message.warning('到期时间必须晚于当前时间');
    return;
  }

  saving.value = true;
  try {
    if (editingTenant.value) {
      await updateTenantApi(editingTenant.value.id, buildPayload());
      message.success('租户已更新');
    } else {
      await createTenantApi({
        ...buildPayload(),
        adminEmail: normalizeOptional(formState.adminEmail),
        adminPassword: formState.adminPassword.trim(),
        adminRealName: formState.adminRealName.trim(),
        adminUserName: formState.adminUserName.trim(),
        code,
        initializationTemplateCode: formState.initializationTemplateCode ?? null,
      });
      message.success('租户已新增');
    }

    modalOpen.value = false;
    await loadTenants();
  } finally {
    saving.value = false;
  }
}

async function switchTenantStatus(tenant: Record<string, any> | TenantItem, enable: boolean) {
  const currentTenant = tenant as TenantItem;
  statusSavingId.value = currentTenant.id;
  try {
    if (enable) {
      await enableTenantApi(currentTenant.id);
      message.success('租户已启用');
    } else {
      await disableTenantApi(currentTenant.id);
      message.success('租户已停用，租户用户会话已失效');
    }
    await loadTenants();
  } finally {
    statusSavingId.value = '';
  }
}

function normalizeOptional(value: string) {
  return value.trim() || null;
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function disablePastDate(current: Dayjs) {
  return current ? current.endOf('day').isBefore(dayjs()) : false;
}

function getStatusLabel(status: TenantStatus) {
  return statusOptions.find((item) => item.value === status)?.label ?? status;
}

function getStatusColor(status: TenantStatus) {
  if (status === 'Active') {
    return 'green';
  }
  if (status === 'Disabled') {
    return 'default';
  }
  if (status === 'Expired') {
    return 'red';
  }
  return 'gold';
}

function getInitializationStatusLabel(status: TenantInitializationStatus) {
  if (status === 'Success') {
    return '成功';
  }

  if (status === 'Failed') {
    return '失败';
  }

  return '初始化中';
}

function getInitializationStatusColor(status: TenantInitializationStatus) {
  if (status === 'Success') {
    return 'green';
  }

  if (status === 'Failed') {
    return 'red';
  }

  return 'gold';
}

function getTemplateName(code?: null | string) {
  return (
    initializationTemplates.value.find((item) => item.code === code)?.name ??
    code ??
    '-'
  );
}

onMounted(() => {
  void Promise.all([
    loadPackageOptions(),
    loadInitializationTemplates(),
    loadTenants(),
  ]);
});
</script>

<template>
  <Page auto-content-height>
    <div class="tenant-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">租户编码</span>
          <Input
            v-model:value="query.code"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">租户名称</span>
          <Input
            v-model:value="query.name"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">状态</span>
          <Select
            v-model:value="query.status"
            allow-clear
            class="query-select"
            :options="statusOptions"
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
          <h3>租户列表</h3>
          <Space>
            <Button @click="loadTenants">刷新</Button>
            <Button v-if="canCreate" type="primary" @click="openCreateModal">
              新增
            </Button>
          </Space>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="tenants"
          :loading="loading"
          :pagination="pagination"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'status'">
              <Tag :color="getStatusColor(record.status)">
                {{ getStatusLabel(record.status) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'packageName'">
              <Tag :color="record.packageName ? 'blue' : 'default'">
                {{ record.packageName || '未分配' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'initialization'">
              <div class="init-cell">
                <Tag :color="getInitializationStatusColor(record.initializationStatus)">
                  {{ getInitializationStatusLabel(record.initializationStatus) }}
                </Tag>
                <small>{{ getTemplateName(record.initializationTemplateCode) }}</small>
                <small v-if="record.initializedAt">
                  {{ formatTime(record.initializedAt) }}
                </small>
                <small v-if="record.initializationError" class="init-error">
                  {{ record.initializationError }}
                </small>
              </div>
            </template>
            <template v-if="column.dataIndex === 'contact'">
              <div class="contact-cell">
                <span>{{ record.contactName || '-' }}</span>
                <small>{{ record.contactPhone || record.contactEmail || '-' }}</small>
              </div>
            </template>
            <template v-if="column.dataIndex === 'expireAt'">
              {{ formatTime(record.expireAt) }}
            </template>
            <template v-if="column.dataIndex === 'updatedAt'">
              {{ formatTime(record.updatedAt) }}
            </template>
            <template v-if="column.dataIndex === 'remark'">
              <span class="remark-cell">{{ record.remark || '-' }}</span>
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
                  v-if="record.status === 'Active' && canDisable"
                  title="停用后该租户用户将无法继续访问，确认停用？"
                  @confirm="switchTenantStatus(record, false)"
                >
                  <Button
                    class="table-action danger"
                    :loading="statusSavingId === record.id"
                    size="small"
                  >
                    停用
                  </Button>
                </Popconfirm>
                <Popconfirm
                  v-if="record.status !== 'Active' && canEnable"
                  title="确认启用该租户？"
                  @confirm="switchTenantStatus(record, true)"
                >
                  <Button
                    class="table-action enable"
                    :loading="statusSavingId === record.id"
                    size="small"
                  >
                    启用
                  </Button>
                </Popconfirm>
                <span
                  v-if="
                    !canUpdate &&
                    !(record.status === 'Active' && canDisable) &&
                    !(record.status !== 'Active' && canEnable)
                  "
                >
                  -
                </span>
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
      @ok="submitTenant"
    >
      <Form layout="vertical">
        <h3 class="form-section-title">租户信息</h3>
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="租户编码" required>
            <Input
              v-model:value="formState.code"
              :disabled="Boolean(editingTenant)"
              placeholder="例如 demo-company"
            />
          </FormItem>
          <FormItem label="租户名称" required>
            <Input v-model:value="formState.name" placeholder="请输入租户名称" />
          </FormItem>
          <FormItem label="套餐">
            <Select
              v-model:value="formState.packageId"
              allow-clear
              :options="
                packageOptions.map((item) => ({
                  label: item.isEnabled ? item.name : `${item.name}(停用)`,
                  value: item.id,
                }))
              "
              placeholder="请选择套餐"
            />
          </FormItem>
          <FormItem v-if="!editingTenant" label="初始化模板">
            <Select
              v-model:value="formState.initializationTemplateCode"
              :options="
                initializationTemplates.map((item) => ({
                  label: item.name,
                  value: item.code,
                }))
              "
              placeholder="请选择初始化模板"
            />
          </FormItem>
          <FormItem label="联系人">
            <Input v-model:value="formState.contactName" placeholder="请输入联系人" />
          </FormItem>
          <FormItem label="联系电话">
            <Input v-model:value="formState.contactPhone" placeholder="请输入联系电话" />
          </FormItem>
          <FormItem label="联系邮箱">
            <Input v-model:value="formState.contactEmail" placeholder="请输入联系邮箱" />
          </FormItem>
          <FormItem label="到期时间">
            <DatePicker
              :value="formState.expireAt ?? undefined"
              class="w-full"
              :disabled-date="disablePastDate"
              format="YYYY-MM-DD HH:mm:ss"
              :show-time="{ defaultValue: defaultExpireTime }"
              placeholder="请选择"
              @panel-change="handleExpirePanelChange"
              @update:value="updateExpireAt"
            />
            <DatePicker
              :value="formState.expireAt ?? undefined"
              class="expire-year-picker"
              :disabled-date="disablePastDate"
              format="YYYY"
              picker="year"
              placeholder="按年份选择"
              @change="setExpireYear"
            />
            <Space class="expire-shortcuts" wrap>
              <Button size="small" @click="setExpireYears(1)">1年后</Button>
              <Button size="small" @click="setExpireYears(3)">3年后</Button>
              <Button size="small" @click="setExpireYears(5)">5年后</Button>
              <Button size="small" @click="formState.expireAt = null">
                不限制
              </Button>
            </Space>
          </FormItem>
        </div>
        <template v-if="!editingTenant">
          <h3 class="form-section-title">管理员信息</h3>
          <div class="grid grid-cols-2 gap-4">
            <FormItem label="管理员账号" required>
              <Input
                v-model:value="formState.adminUserName"
                placeholder="例如 tenant-admin"
              />
            </FormItem>
            <FormItem label="管理员姓名" required>
              <Input
                v-model:value="formState.adminRealName"
                placeholder="请输入管理员姓名"
              />
            </FormItem>
            <FormItem label="管理员邮箱">
              <Input
                v-model:value="formState.adminEmail"
                placeholder="请输入管理员邮箱"
              />
            </FormItem>
            <FormItem label="初始密码" required>
              <Input.Password
                v-model:value="formState.adminPassword"
                placeholder="请输入初始密码"
              />
            </FormItem>
          </div>
        </template>
        <FormItem label="备注">
          <Textarea
            v-model:value="formState.remark"
            :auto-size="{ minRows: 3, maxRows: 6 }"
            placeholder="请输入备注"
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.tenant-workspace {
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
  width: 180px;
}

.query-select {
  width: 140px;
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

.form-section-title {
  margin: 0 0 12px;
  font-size: 14px;
  font-weight: 600;
}

.form-section-title:not(:first-child) {
  margin-top: 6px;
}

.expire-shortcuts {
  margin-top: 8px;
}

.expire-year-picker {
  width: 160px;
  margin-top: 8px;
}

.contact-cell {
  display: flex;
  flex-direction: column;
  line-height: 1.5;
}

.init-cell {
  display: flex;
  flex-direction: column;
  gap: 3px;
  line-height: 1.4;
}

.contact-cell small {
  color: hsl(var(--muted-foreground));
}

.init-cell small {
  color: hsl(var(--muted-foreground));
}

.init-cell .init-error {
  color: #ff3868;
}

.remark-cell {
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

.table-action.enable {
  border-color: #4db56a;
  color: #2f944b;
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

@media (max-width: 1100px) {
  .query-bar {
    align-items: flex-start;
    flex-direction: column;
  }

  .query-input {
    width: 200px;
  }
}
</style>
