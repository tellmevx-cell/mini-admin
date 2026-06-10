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
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';

import {
  getAlertRuleListApi,
  type AlertRuleItem,
  type UpdateAlertRuleParams,
  updateAlertRuleApi,
} from '#/api/system/alert-rule';
import { getRoleListApi, type RoleListItem } from '#/api/system/role';
import { getUserListApi, type UserListItem } from '#/api/system/user';

interface AlertRuleFormState {
  emailEnabled: boolean;
  enabled: boolean;
  level: string;
  notifyEnabled: boolean;
  recipientRoleIds: string[];
  recipientUserIds: string[];
  remark: string;
  threshold: number;
  windowMinutes: number;
}

const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const editingRule = ref<AlertRuleItem>();
const rules = ref<AlertRuleItem[]>([]);
const roles = ref<RoleListItem[]>([]);
const recipientUsers = ref<UserListItem[]>([]);
const total = ref(0);
const query = reactive({
  enabled: undefined as string | undefined,
  keyword: '',
  level: undefined as string | undefined,
  page: 1,
  pageSize: 10,
});
const formState = reactive<AlertRuleFormState>({
  emailEnabled: false,
  enabled: true,
  level: 'Warning',
  notifyEnabled: true,
  recipientRoleIds: [],
  recipientUserIds: [],
  remark: '',
  threshold: 1,
  windowMinutes: 1440,
});

const canUpdate = computed(() =>
  hasAccessByCodes(['system:alert-rule:update']),
);

const columns = [
  { dataIndex: 'name', title: '规则名称', width: 180 },
  { dataIndex: 'code', title: '规则编码', width: 190 },
  { dataIndex: 'level', title: '等级', width: 90 },
  { dataIndex: 'threshold', title: '阈值', width: 100 },
  { dataIndex: 'windowMinutes', title: '统计窗口', width: 110 },
  { dataIndex: 'enabled', title: '状态', width: 100 },
  { dataIndex: 'notifyEnabled', title: '站内信', width: 100 },
  { dataIndex: 'emailEnabled', title: '邮件', width: 100 },
  { dataIndex: 'recipients', title: '接收人', width: 220 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 180 },
  { dataIndex: 'action', title: '操作', width: 100 },
];

const levelOptions = [
  { label: '提示', value: 'Info' },
  { label: '警告', value: 'Warning' },
  { label: '严重', value: 'Critical' },
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

function parseBooleanSelectValue(value?: string) {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return undefined;
}

const roleOptions = computed(() =>
  roles.value.map((role) => ({
    label: `${role.name}（${role.code}）`,
    value: role.id,
  })),
);

const userOptions = computed(() =>
  recipientUsers.value.map((user) => ({
    label: `${user.realName}（${user.userName}）`,
    value: user.id,
  })),
);

async function loadRules() {
  loading.value = true;
  try {
    const result = await getAlertRuleListApi({
      enabled: parseBooleanSelectValue(query.enabled),
      keyword: query.keyword.trim() || undefined,
      level: query.level,
      page: query.page,
      pageSize: query.pageSize,
    });
    rules.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadRules();
}

function handleReset() {
  query.enabled = undefined;
  query.keyword = '';
  query.level = undefined;
  query.page = 1;
  void loadRules();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadRules();
}

async function loadRecipientOptions() {
  const [roleResult, userResult] = await Promise.all([
    getRoleListApi({ page: 1, pageSize: 100 }),
    getUserListApi({ page: 1, pageSize: 100 }),
  ]);
  roles.value = roleResult.items;
  recipientUsers.value = userResult.items;
}

async function openEditModal(rule: AlertRuleItem | Record<string, any>) {
  const currentRule = rule as AlertRuleItem;
  await loadRecipientOptions();
  editingRule.value = currentRule;
  formState.emailEnabled = currentRule.emailEnabled;
  formState.enabled = currentRule.enabled;
  formState.level = currentRule.level;
  formState.notifyEnabled = currentRule.notifyEnabled;
  formState.recipientRoleIds = getRecipientIds(currentRule, 'Role');
  formState.recipientUserIds = getRecipientIds(currentRule, 'User');
  formState.remark = currentRule.remark ?? '';
  formState.threshold = currentRule.threshold;
  formState.windowMinutes = currentRule.windowMinutes;
  modalOpen.value = true;
}

async function submitRule() {
  if (!editingRule.value) {
    return;
  }

  if (formState.threshold < 0 || formState.windowMinutes < 1) {
    message.warning('请填写有效的阈值和统计窗口');
    return;
  }

  saving.value = true;
  try {
    await updateAlertRuleApi(editingRule.value.id, {
      emailEnabled: formState.emailEnabled,
      enabled: formState.enabled,
      level: formState.level,
      notifyEnabled: formState.notifyEnabled,
      recipientRoleIds: formState.recipientRoleIds,
      recipientUserIds: formState.recipientUserIds,
      remark: formState.remark.trim() || null,
      threshold: formState.threshold,
      windowMinutes: formState.windowMinutes,
    });
    message.success('告警规则已更新');
    modalOpen.value = false;
    await loadRules();
  } finally {
    saving.value = false;
  }
}

async function toggleEnabled(rule: AlertRuleItem | Record<string, any>, enabled: boolean) {
  const currentRule = rule as AlertRuleItem;
  await updateAlertRuleApi(currentRule.id, createUpdatePayload(currentRule, { enabled }));
  message.success(enabled ? '规则已启用' : '规则已停用');
  await loadRules();
}

async function toggleNotify(rule: AlertRuleItem | Record<string, any>, notifyEnabled: boolean) {
  const currentRule = rule as AlertRuleItem;
  await updateAlertRuleApi(currentRule.id, createUpdatePayload(currentRule, { notifyEnabled }));
  message.success(notifyEnabled ? '站内信已开启' : '站内信已关闭');
  await loadRules();
}

async function toggleEmail(rule: AlertRuleItem | Record<string, any>, emailEnabled: boolean) {
  const currentRule = rule as AlertRuleItem;
  await updateAlertRuleApi(currentRule.id, createUpdatePayload(currentRule, { emailEnabled }));
  message.success(emailEnabled ? '邮件已开启' : '邮件已关闭');
  await loadRules();
}

function createUpdatePayload(
  rule: AlertRuleItem,
  overrides: Partial<UpdateAlertRuleParams>,
): UpdateAlertRuleParams {
  return {
    emailEnabled: rule.emailEnabled,
    enabled: rule.enabled,
    level: rule.level,
    notifyEnabled: rule.notifyEnabled,
    recipientRoleIds: getRecipientIds(rule, 'Role'),
    recipientUserIds: getRecipientIds(rule, 'User'),
    remark: rule.remark ?? null,
    threshold: rule.threshold,
    windowMinutes: rule.windowMinutes,
    ...overrides,
  };
}

function getRecipientIds(rule: AlertRuleItem, recipientType: 'Role' | 'User') {
  return rule.recipients
    .filter((recipient) => recipient.recipientType === recipientType)
    .map((recipient) => recipient.recipientId);
}

function formatRecipients(rule: AlertRuleItem | Record<string, any>) {
  const currentRule = rule as AlertRuleItem;
  return currentRule.recipients.length === 0
    ? ['admin']
    : currentRule.recipients.map((recipient) =>
        recipient.recipientType === 'Role'
          ? `角色:${recipient.recipientName}`
          : `用户:${recipient.recipientName}`,
      );
}

function levelColor(level: string) {
  if (level === 'Critical') {
    return 'red';
  }

  if (level === 'Warning') {
    return 'orange';
  }

  return 'blue';
}

function levelText(level: string) {
  return levelOptions.find((item) => item.value === level)?.label ?? level;
}

function formatWindow(minutes: number) {
  if (minutes >= 1440 && minutes % 1440 === 0) {
    return `${minutes / 1440} 天`;
  }

  if (minutes >= 60 && minutes % 60 === 0) {
    return `${minutes / 60} 小时`;
  }

  return `${minutes} 分钟`;
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

onMounted(loadRules);
</script>

<template>
  <Page auto-content-height>
    <div class="alert-rule-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">关键词</span>
          <Input
            v-model:value="query.keyword"
            allow-clear
            class="query-input"
            placeholder="规则名称/编码"
          />
          <span class="query-label">等级</span>
          <Select
            v-model:value="query.level"
            allow-clear
            class="query-select"
            :options="levelOptions"
            placeholder="全部"
          />
          <span class="query-label">状态</span>
          <Select
            v-model:value="query.enabled"
            allow-clear
            class="query-select"
            :options="enabledOptions"
            placeholder="全部"
          />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>

      <div class="table-shell">
        <div class="table-toolbar">
          <h3>告警规则</h3>
          <Button @click="loadRules">刷新</Button>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="rules"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 1480 }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'name'">
              <div class="rule-name-cell">
                <span>{{ record.name }}</span>
                <small>{{ record.description }}</small>
              </div>
            </template>
            <template v-if="column.dataIndex === 'code'">
              <Tag color="blue">{{ record.code }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'level'">
              <Tag :color="levelColor(record.level)">
                {{ levelText(record.level) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'threshold'">
              {{ record.operator }} {{ record.threshold }}
            </template>
            <template v-if="column.dataIndex === 'windowMinutes'">
              {{ formatWindow(record.windowMinutes) }}
            </template>
            <template v-if="column.dataIndex === 'enabled'">
              <Switch
                v-if="canUpdate"
                :checked="record.enabled"
                checked-children="启用"
                size="small"
                un-checked-children="停用"
                @change="(checked) => toggleEnabled(record, checked as boolean)"
              />
              <Tag v-else :color="record.enabled ? 'green' : 'default'">
                {{ record.enabled ? '启用' : '停用' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'notifyEnabled'">
              <Switch
                v-if="canUpdate"
                :checked="record.notifyEnabled"
                checked-children="开启"
                size="small"
                un-checked-children="关闭"
                @change="(checked) => toggleNotify(record, checked as boolean)"
              />
              <Tag v-else :color="record.notifyEnabled ? 'green' : 'default'">
                {{ record.notifyEnabled ? '开启' : '关闭' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'emailEnabled'">
              <Switch
                v-if="canUpdate"
                :checked="record.emailEnabled"
                checked-children="开启"
                size="small"
                un-checked-children="关闭"
                @change="(checked) => toggleEmail(record, checked as boolean)"
              />
              <Tag v-else :color="record.emailEnabled ? 'green' : 'default'">
                {{ record.emailEnabled ? '开启' : '关闭' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'recipients'">
              <div class="recipient-tags">
                <Tag
                  v-for="recipient in formatRecipients(record)"
                  :key="recipient"
                >
                  {{ recipient }}
                </Tag>
              </div>
            </template>
            <template v-if="column.dataIndex === 'updatedAt'">
              {{ formatTime(record.updatedAt) }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Button
                v-if="canUpdate"
                class="table-action edit"
                size="small"
                @click="openEditModal(record)"
              >
                编辑
              </Button>
              <span v-else>-</span>
            </template>
          </template>
        </Table>
      </div>
    </div>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      title="编辑告警规则"
      width="720px"
      @ok="submitRule"
    >
      <Form layout="vertical">
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="规则名称">
            <Input :value="editingRule?.name" disabled />
          </FormItem>
          <FormItem label="规则编码">
            <Input :value="editingRule?.code" disabled />
          </FormItem>
          <FormItem label="告警等级" required>
            <Select
              v-model:value="formState.level"
              :options="levelOptions"
            />
          </FormItem>
          <FormItem label="阈值" required>
            <InputNumber
              v-model:value="formState.threshold"
              class="w-full"
              :min="0"
              :precision="2"
            />
          </FormItem>
          <FormItem label="统计窗口" required>
            <InputNumber
              v-model:value="formState.windowMinutes"
              addon-after="分钟"
              class="w-full"
              :min="1"
            />
          </FormItem>
          <FormItem label="启用">
            <Switch v-model:checked="formState.enabled" />
          </FormItem>
          <FormItem label="站内信">
            <Switch v-model:checked="formState.notifyEnabled" />
          </FormItem>
          <FormItem label="邮件">
            <Switch v-model:checked="formState.emailEnabled" />
          </FormItem>
          <FormItem label="接收角色">
            <Select
              v-model:value="formState.recipientRoleIds"
              allow-clear
              mode="multiple"
              :options="roleOptions"
              placeholder="请选择接收角色"
            />
          </FormItem>
          <FormItem label="指定用户">
            <Select
              v-model:value="formState.recipientUserIds"
              allow-clear
              mode="multiple"
              :options="userOptions"
              placeholder="请选择指定用户"
            />
          </FormItem>
        </div>
        <FormItem label="备注">
          <Textarea
            v-model:value="formState.remark"
            :maxlength="512"
            :rows="3"
            placeholder="可选"
            show-count
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.alert-rule-workspace {
  display: flex;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 10px;
}

.query-bar,
.table-shell {
  border-radius: 4px;
  background: hsl(var(--background));
}

.query-bar {
  display: flex;
  min-height: 64px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
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

.rule-name-cell {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 2px;
}

.rule-name-cell small {
  max-width: 360px;
  overflow: hidden;
  color: hsl(var(--muted-foreground));
  text-overflow: ellipsis;
  white-space: nowrap;
}

.recipient-tags {
  display: flex;
  max-width: 220px;
  flex-wrap: wrap;
  gap: 4px;
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

  .query-select {
    width: 150px;
  }
}
</style>
