<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Alert,
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
  message,
} from 'ant-design-vue';

import {
  type AbacPolicyItem,
  createAbacPolicyApi,
  deleteAbacPolicyApi,
  getAbacPoliciesApi,
  type SaveAbacPolicyParams,
  updateAbacPolicyApi,
} from '#/api/platform/kernel';

const defaultConditions = `{
  "attribute": "request.method",
  "operator": "equals",
  "value": "GET"
}`;

interface AbacPolicyFormState {
  action: string;
  conditionsJson: string;
  description: string;
  effect: 'Allow' | 'Deny';
  isEnabled: boolean;
  name: string;
  priority: number;
  resource: string;
  subjectId: string;
  subjectType: 'Any' | 'Application' | 'Role' | 'User';
  tenantId: null | string;
}

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const policies = ref<AbacPolicyItem[]>([]);
const editing = ref<AbacPolicyItem>();

const formState = reactive<AbacPolicyFormState>({
  action: 'query',
  conditionsJson: defaultConditions,
  description: '',
  effect: 'Allow',
  isEnabled: true,
  name: '',
  priority: 100,
  resource: '',
  subjectId: '',
  subjectType: 'Any',
  tenantId: null,
});

const columns: TableColumnsType = [
  { dataIndex: 'name', title: '策略名称', width: 220 },
  { dataIndex: 'subject', title: '主体', width: 180 },
  { dataIndex: 'resource', title: '资源 / 动作', width: 230 },
  { dataIndex: 'effect', title: '效果', width: 90 },
  { dataIndex: 'priority', title: '优先级', width: 90 },
  { dataIndex: 'conditionsJson', title: '生效条件', width: 320 },
  { dataIndex: 'isEnabled', title: '状态', width: 90 },
  { dataIndex: 'updatedAt', title: '更新时间', width: 180 },
  { dataIndex: 'action', fixed: 'right', title: '操作', width: 150 },
];

const canCreate = computed(() =>
  hasAccessByCodes(['platform:abac:create']),
);
const canUpdate = computed(() =>
  hasAccessByCodes(['platform:abac:update']),
);
const canDelete = computed(() =>
  hasAccessByCodes(['platform:abac:delete']),
);
const modalTitle = computed(() => (editing.value ? '编辑策略' : '新增策略'));

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function resetForm() {
  editing.value = undefined;
  Object.assign(formState, {
    action: 'query',
    conditionsJson: defaultConditions,
    description: '',
    effect: 'Allow',
    isEnabled: true,
    name: '',
    priority: 100,
    resource: '',
    subjectId: '',
    subjectType: 'Any',
    tenantId: null,
  });
}

function openCreate() {
  resetForm();
  modalOpen.value = true;
}

function openEdit(item: AbacPolicyItem | Record<string, any>) {
  const policy = item as AbacPolicyItem;
  editing.value = policy;
  Object.assign(formState, {
    action: policy.action,
    conditionsJson: policy.conditionsJson || '{}',
    description: policy.description ?? '',
    effect: policy.effect,
    isEnabled: policy.isEnabled,
    name: policy.name,
    priority: policy.priority,
    resource: policy.resource,
    subjectId: policy.subjectId ?? '',
    subjectType: policy.subjectType,
    tenantId: policy.tenantId ?? null,
  });
  modalOpen.value = true;
}

function validateForm() {
  if (!formState.name.trim() || !formState.resource.trim() || !formState.action.trim()) {
    message.warning('策略名称、资源和动作不能为空');
    return false;
  }
  if (formState.subjectType !== 'Any' && !formState.subjectId?.trim()) {
    message.warning('非 Any 主体必须填写主体标识');
    return false;
  }
  try {
    JSON.parse(formState.conditionsJson || '{}');
  } catch {
    message.warning('生效条件不是有效的 JSON');
    return false;
  }
  return true;
}

async function loadPolicies() {
  loading.value = true;
  try {
    policies.value = await getAbacPoliciesApi();
  } finally {
    loading.value = false;
  }
}

async function savePolicy() {
  if (!validateForm()) return;
  saving.value = true;
  try {
    const payload: SaveAbacPolicyParams = {
      ...formState,
      action: formState.action.trim(),
      conditionsJson: formState.conditionsJson?.trim() || '{}',
      description: formState.description?.trim() || null,
      name: formState.name.trim(),
      resource: formState.resource.trim(),
      subjectId:
        formState.subjectType === 'Any'
          ? null
          : formState.subjectId?.trim() || null,
    };
    if (editing.value) {
      await updateAbacPolicyApi(editing.value.id, payload);
      message.success('策略已更新');
    } else {
      await createAbacPolicyApi(payload);
      message.success('策略已创建');
    }
    modalOpen.value = false;
    await loadPolicies();
  } finally {
    saving.value = false;
  }
}

async function deletePolicy(id: string) {
  await deleteAbacPolicyApi(id);
  message.success('策略已删除');
  await loadPolicies();
}

onMounted(loadPolicies);
</script>

<template>
  <Page
    description="在 RBAC 权限之上按用户、角色、应用与请求属性实施精细化允许或拒绝策略。显式拒绝始终优先。"
    title="访问控制策略"
  >
    <div class="policy-shell">
      <Alert
        banner
        message="ABAC 会在已有 RBAC 权限通过后继续判定。建议先用 Allow 小范围验证，再配置高优先级 Deny。"
        show-icon
        type="info"
      />

      <div class="toolbar">
        <div>
          <strong>策略列表</strong>
          <span class="toolbar-hint">共 {{ policies.length }} 条</span>
        </div>
        <Space>
          <Button @click="loadPolicies">刷新</Button>
          <Button v-if="canCreate" type="primary" @click="openCreate">
            新增策略
          </Button>
        </Space>
      </div>

      <Table
        :columns="columns"
        :data-source="policies"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1450 }"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'subject'">
            <Tag color="blue">{{ record.subjectType }}</Tag>
            <div v-if="record.subjectId" class="muted ellipsis">
              {{ record.subjectId }}
            </div>
          </template>
          <template v-else-if="column.dataIndex === 'resource'">
            <div class="mono">{{ record.resource }}</div>
            <Tag>{{ record.action }}</Tag>
          </template>
          <template v-else-if="column.dataIndex === 'effect'">
            <Tag :color="record.effect === 'Deny' ? 'red' : 'green'">
              {{ record.effect }}
            </Tag>
          </template>
          <template v-else-if="column.dataIndex === 'conditionsJson'">
            <code class="condition-preview">{{ record.conditionsJson }}</code>
          </template>
          <template v-else-if="column.dataIndex === 'isEnabled'">
            <Tag :color="record.isEnabled ? 'success' : 'default'">
              {{ record.isEnabled ? '启用' : '停用' }}
            </Tag>
          </template>
          <template v-else-if="column.dataIndex === 'updatedAt'">
            {{ formatTime(record.updatedAt) }}
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <Space>
              <Button v-if="canUpdate" size="small" type="link" @click="openEdit(record)">
                编辑
              </Button>
              <Popconfirm
                v-if="canDelete"
                title="确认删除这条策略？"
                @confirm="deletePolicy(record.id)"
              >
                <Button danger size="small" type="link">删除</Button>
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
      :width="760"
      @ok="savePolicy"
    >
      <Form layout="vertical">
        <div class="form-grid">
          <FormItem label="策略名称" required>
            <Input v-model:value="formState.name" :maxlength="128" />
          </FormItem>
          <FormItem label="策略效果" required>
            <Select
              v-model:value="formState.effect"
              :options="[
                { label: 'Allow（允许）', value: 'Allow' },
                { label: 'Deny（拒绝，优先）', value: 'Deny' },
              ]"
            />
          </FormItem>
          <FormItem label="主体类型" required>
            <Select
              v-model:value="formState.subjectType"
              :options="[
                { label: 'Any（所有主体）', value: 'Any' },
                { label: 'User（用户 ID）', value: 'User' },
                { label: 'Role（角色编码）', value: 'Role' },
                { label: 'Application（客户端 ID）', value: 'Application' },
              ]"
            />
          </FormItem>
          <FormItem label="主体标识" :required="formState.subjectType !== 'Any'">
            <Input
              v-model:value="formState.subjectId"
              :disabled="formState.subjectType === 'Any'"
              placeholder="用户 ID、角色编码或客户端 ID"
            />
          </FormItem>
          <FormItem label="资源" required>
            <Input v-model:value="formState.resource" placeholder="例如 platform.cache" />
          </FormItem>
          <FormItem label="动作" required>
            <Input v-model:value="formState.action" placeholder="例如 query / clear" />
          </FormItem>
          <FormItem label="优先级">
            <InputNumber v-model:value="formState.priority" :min="0" class="full-width" />
          </FormItem>
          <FormItem label="启用策略">
            <Switch v-model:checked="formState.isEnabled" />
          </FormItem>
        </div>
        <FormItem label="生效条件 JSON">
          <Textarea
            v-model:value="formState.conditionsJson"
            :auto-size="{ minRows: 7, maxRows: 14 }"
            class="mono"
          />
          <div class="field-tip">
            支持 all / any / not，以及 equals、notEquals、contains、in、greaterThan、lessThan、ipInCidr。
          </div>
        </FormItem>
        <FormItem label="说明">
          <Textarea v-model:value="formState.description" :rows="2" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.policy-shell {
  display: grid;
  gap: 16px;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 18px;
  border: 1px solid hsl(var(--border));
  border-radius: 12px;
  background: hsl(var(--card));
}

.toolbar-hint,
.muted,
.field-tip {
  margin-left: 10px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 18px;
}

.full-width {
  width: 100%;
}

.mono,
.condition-preview {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
}

.condition-preview {
  display: block;
  max-width: 300px;
  overflow: hidden;
  color: hsl(var(--muted-foreground));
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ellipsis {
  max-width: 160px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 720px) {
  .form-grid {
    grid-template-columns: 1fr;
  }

  .toolbar {
    align-items: flex-start;
    gap: 12px;
  }
}
</style>
