<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';

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
  Table,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';

import {
  createSampleOrderApi,
  deleteSampleOrderApi,
  getSampleOrderListApi,
  type SampleOrderItem,
  submitSampleOrderWorkflowApi,
  updateSampleOrderApi,
  withdrawSampleOrderWorkflowApi,
} from '#/api/business/sample-order';
import {
  getWorkflowDefinitionOptionsApi,
  type WorkflowDefinitionOption,
} from '#/api/workflow/center';

const DRAFT_STATUS = 'Draft';
const PENDING_STATUS = 'PendingApproval';
const APPROVED_STATUS = 'Approved';
const REJECTED_STATUS = 'Rejected';
const WITHDRAWN_STATUS = 'Withdrawn';

interface SampleOrderFormState {
  contentType: string;
  originalName: string;
  size: number;
  storagePath: string;
  storageProvider: string;
  storedName: string;
}

const router = useRouter();
const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const saving = ref(false);
const submittingWorkflow = ref(false);
const modalOpen = ref(false);
const submitWorkflowModalOpen = ref(false);
const editingItem = ref<SampleOrderItem>();
const workflowTarget = ref<SampleOrderItem>();
const items = ref<SampleOrderItem[]>([]);
const total = ref(0);
const workflowDefinitions = ref<WorkflowDefinitionOption[]>([]);

const query = reactive({
  contentType: '',
  keyword: '',
  originalName: '',
  page: 1,
  pageSize: 10,
  size: 0,
  status: '',
  storagePath: '',
  storageProvider: '',
  storedName: '',
});
const formState = reactive<SampleOrderFormState>({
  contentType: '',
  originalName: '',
  size: 0,
  storagePath: '',
  storageProvider: '',
  storedName: '',
});
const workflowForm = reactive({
  comment: '',
  definitionId: '',
});

const statusOptions = [
  { label: '草稿', value: DRAFT_STATUS },
  { label: '审批中', value: PENDING_STATUS },
  { label: '已通过', value: APPROVED_STATUS },
  { label: '已驳回', value: REJECTED_STATUS },
  { label: '已撤回', value: WITHDRAWN_STATUS },
];

const columns = [
  { dataIndex: 'originalName', title: '订单名称', width: 180 },
  { dataIndex: 'storedName', title: '订单编号', width: 180 },
  { dataIndex: 'contentType', title: '订单类型', width: 130 },
  { dataIndex: 'size', title: '金额', width: 110 },
  { dataIndex: 'storageProvider', title: '来源', width: 120 },
  { dataIndex: 'storagePath', title: '备注', width: 240 },
  { dataIndex: 'status', title: '审批状态', width: 120 },
  { dataIndex: 'createdAt', title: '创建时间', width: 180 },
  { dataIndex: 'action', fixed: 'right' as const, title: '操作', width: 290 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));
const modalTitle = computed(() =>
  editingItem.value ? '编辑示例订单' : '新增示例订单',
);
const workflowOptions = computed(() =>
  workflowDefinitions.value.map((item) => ({
    label: `${item.name}（${item.code}）`,
    value: item.id,
  })),
);
const canCreate = computed(() =>
  hasAccessByCodes(['business:sample-order:create']),
);
const canUpdate = computed(() =>
  hasAccessByCodes(['business:sample-order:update']),
);
const canDelete = computed(() =>
  hasAccessByCodes(['business:sample-order:delete']),
);
const canSubmitWorkflow = computed(() =>
  hasAccessByCodes(['business:sample-order:submit-workflow']),
);
const canWithdrawWorkflow = computed(() =>
  hasAccessByCodes(['business:sample-order:withdraw-workflow']),
);

function statusMeta(status: string) {
  const map: Record<string, { color: string; label: string }> = {
    [APPROVED_STATUS]: { color: 'green', label: '已通过' },
    [DRAFT_STATUS]: { color: 'default', label: '草稿' },
    [PENDING_STATUS]: { color: 'blue', label: '审批中' },
    [REJECTED_STATUS]: { color: 'red', label: '已驳回' },
    [WITHDRAWN_STATUS]: { color: 'orange', label: '已撤回' },
  };

  return map[status] ?? { color: 'default', label: status || '草稿' };
}

function canEditRecord(record: Record<string, any> | SampleOrderItem) {
  const currentRecord = record as SampleOrderItem;
  return ![PENDING_STATUS, APPROVED_STATUS].includes(currentRecord.status);
}

function canSubmitRecord(record: Record<string, any> | SampleOrderItem) {
  const currentRecord = record as SampleOrderItem;
  return [DRAFT_STATUS, REJECTED_STATUS, WITHDRAWN_STATUS].includes(
    currentRecord.status || DRAFT_STATUS,
  );
}

function canWithdrawRecord(record: Record<string, any> | SampleOrderItem) {
  const currentRecord = record as SampleOrderItem;
  return currentRecord.status === PENDING_STATUS && Boolean(currentRecord.workflowInstanceId);
}

async function loadWorkflowDefinitions() {
  if (workflowDefinitions.value.length > 0) {
    return;
  }

  workflowDefinitions.value = await getWorkflowDefinitionOptionsApi();
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getSampleOrderListApi({
      contentType: query.contentType || undefined,
      keyword: query.keyword || undefined,
      originalName: query.originalName || undefined,
      page: query.page,
      pageSize: query.pageSize,
      size: query.size || undefined,
      status: query.status || undefined,
      storagePath: query.storagePath || undefined,
      storageProvider: query.storageProvider || undefined,
      storedName: query.storedName || undefined,
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
  query.contentType = '';
  query.keyword = '';
  query.originalName = '';
  query.page = 1;
  query.size = 0;
  query.status = '';
  query.storagePath = '';
  query.storageProvider = '';
  query.storedName = '';
  void loadData();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadData();
}

function resetForm() {
  editingItem.value = undefined;
  formState.contentType = '';
  formState.originalName = '';
  formState.size = 0;
  formState.storagePath = '';
  formState.storageProvider = '';
  formState.storedName = '';
}

function openCreateModal() {
  resetForm();
  modalOpen.value = true;
}

function openEditModal(item: Record<string, any> | SampleOrderItem) {
  const currentItem = item as SampleOrderItem;
  if (!canEditRecord(currentItem)) {
    message.warning('审批中或已通过的订单不能编辑');
    return;
  }

  editingItem.value = currentItem;
  formState.contentType = currentItem.contentType;
  formState.originalName = currentItem.originalName;
  formState.size = currentItem.size;
  formState.storagePath = currentItem.storagePath;
  formState.storageProvider = currentItem.storageProvider;
  formState.storedName = currentItem.storedName;
  modalOpen.value = true;
}

async function submitItem() {
  saving.value = true;
  try {
    const payload = {
      contentType: formState.contentType,
      originalName: formState.originalName,
      size: formState.size,
      status: editingItem.value?.status || DRAFT_STATUS,
      storagePath: formState.storagePath,
      storageProvider: formState.storageProvider,
      storedName: formState.storedName,
    };

    if (editingItem.value) {
      await updateSampleOrderApi(editingItem.value.id, payload);
      message.success('示例订单已更新');
    } else {
      await createSampleOrderApi(payload);
      message.success('示例订单已新增');
    }

    modalOpen.value = false;
    await loadData();
  } finally {
    saving.value = false;
  }
}

async function removeItem(item: Record<string, any> | SampleOrderItem) {
  const currentItem = item as SampleOrderItem;
  if (!canEditRecord(currentItem)) {
    message.warning('审批中或已通过的订单不能删除');
    return;
  }

  const deleted = await deleteSampleOrderApi(currentItem.id);
  if (deleted) {
    message.success('示例订单已删除');
  }
  await loadData();
}

async function openSubmitWorkflowModal(item: Record<string, any> | SampleOrderItem) {
  const currentItem = item as SampleOrderItem;
  if (!canSubmitRecord(currentItem)) {
    message.warning('当前状态不能提交审批');
    return;
  }

  await loadWorkflowDefinitions();
  if (workflowDefinitions.value.length === 0) {
    message.warning('请先在审批中心创建并启用流程定义');
    return;
  }

  workflowTarget.value = currentItem;
  workflowForm.definitionId = workflowDefinitions.value[0]?.id ?? '';
  workflowForm.comment = '';
  submitWorkflowModalOpen.value = true;
}

async function submitWorkflow() {
  if (!workflowTarget.value) {
    return;
  }

  if (!workflowForm.definitionId) {
    message.warning('请选择流程定义');
    return;
  }

  submittingWorkflow.value = true;
  try {
    await submitSampleOrderWorkflowApi(workflowTarget.value.id, {
      comment: workflowForm.comment || undefined,
      definitionId: workflowForm.definitionId,
    });
    message.success('示例订单已提交审批');
    submitWorkflowModalOpen.value = false;
    await loadData();
  } finally {
    submittingWorkflow.value = false;
  }
}

async function withdrawWorkflow(item: Record<string, any> | SampleOrderItem) {
  const currentItem = item as SampleOrderItem;
  if (!canWithdrawRecord(currentItem)) {
    message.warning('当前状态不能撤回审批');
    return;
  }

  await withdrawSampleOrderWorkflowApi(currentItem.id, {
    comment: '业务页面撤回',
  });
  message.success('示例订单已撤回');
  await loadData();
}

function openWorkflowCenter() {
  router.push('/workflow/center');
}

onMounted(() => {
  void loadData();
  void loadWorkflowDefinitions();
});
</script>

<template>
  <Page auto-content-height>
    <div class="sample-order-page">
      <div class="query-bar">
        <div class="query-fields">
          <Input
            v-model:value="query.keyword"
            allow-clear
            class="query-input keyword"
            placeholder="关键词：订单名称/编号/状态"
          />
          <Input
            v-model:value="query.originalName"
            allow-clear
            class="query-input"
            placeholder="订单名称"
          />
          <Input
            v-model:value="query.storedName"
            allow-clear
            class="query-input"
            placeholder="订单编号"
          />
          <Select
            v-model:value="query.status"
            allow-clear
            class="query-input"
            :options="statusOptions"
            placeholder="审批状态"
          />
        </div>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>

      <div class="toolbar">
        <div>
          <h3>示例订单</h3>
          <p>业务单据提交后进入审批中心，审批结果会自动回写状态。</p>
        </div>
        <Button v-if="canCreate" type="primary" @click="openCreateModal">
          新增订单
        </Button>
      </div>

      <Table
        row-key="id"
        size="small"
        bordered
        :columns="columns"
        :data-source="items"
        :loading="loading"
        :pagination="pagination"
        :scroll="{ x: 1350 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'status'">
            <Tag :color="statusMeta(record.status).color">
              {{ statusMeta(record.status).label }}
            </Tag>
          </template>
          <template v-if="column.dataIndex === 'size'">
            {{ Number(record.size || 0).toLocaleString() }}
          </template>
          <template v-if="column.dataIndex === 'action'">
            <Space>
              <Button
                v-if="canSubmitWorkflow && canSubmitRecord(record)"
                size="small"
                type="link"
                @click="openSubmitWorkflowModal(record)"
              >
                提交审批
              </Button>
              <Popconfirm title="确认撤回这条审批？" @confirm="withdrawWorkflow(record)">
                <Button
                  v-if="canWithdrawWorkflow && canWithdrawRecord(record)"
                  size="small"
                  type="link"
                >
                  撤回
                </Button>
              </Popconfirm>
              <Button
                v-if="record.workflowInstanceId"
                size="small"
                type="link"
                @click="openWorkflowCenter"
              >
                查看流程
              </Button>
              <Button
                v-if="canUpdate && canEditRecord(record)"
                size="small"
                type="link"
                @click="openEditModal(record)"
              >
                编辑
              </Button>
              <Popconfirm title="确认删除这条数据？" @confirm="removeItem(record)">
                <Button
                  v-if="canDelete && canEditRecord(record)"
                  danger
                  size="small"
                  type="link"
                >
                  删除
                </Button>
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
      @ok="submitItem"
    >
      <Form :model="formState" layout="vertical">
        <FormItem label="订单名称">
          <Input v-model:value="formState.originalName" allow-clear />
        </FormItem>
        <FormItem label="订单编号">
          <Input v-model:value="formState.storedName" allow-clear />
        </FormItem>
        <FormItem label="订单类型">
          <Input v-model:value="formState.contentType" allow-clear />
        </FormItem>
        <FormItem label="金额">
          <InputNumber v-model:value="formState.size" class="form-control" />
        </FormItem>
        <FormItem label="来源">
          <Input v-model:value="formState.storageProvider" allow-clear />
        </FormItem>
        <FormItem label="备注">
          <Textarea
            v-model:value="formState.storagePath"
            :auto-size="{ minRows: 3, maxRows: 5 }"
            allow-clear
          />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="submitWorkflowModalOpen"
      :confirm-loading="submittingWorkflow"
      title="提交审批"
      @ok="submitWorkflow"
    >
      <Form :model="workflowForm" layout="vertical">
        <FormItem label="流程定义">
          <Select
            v-model:value="workflowForm.definitionId"
            :options="workflowOptions"
            placeholder="请选择流程定义"
          />
        </FormItem>
        <FormItem label="提交说明">
          <Textarea
            v-model:value="workflowForm.comment"
            :auto-size="{ minRows: 3, maxRows: 5 }"
            allow-clear
            placeholder="可以填写本次提交审批的说明"
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.sample-order-page {
  min-height: calc(100vh - 150px);
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 12px;
}

.query-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 12px;
}

.query-fields {
  display: flex;
  flex: 1;
  flex-wrap: wrap;
  gap: 8px;
}

.query-input {
  width: 180px;
}

.query-input.keyword {
  width: 260px;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 6px 0 12px;
}

.toolbar h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.toolbar p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.form-control {
  width: 100%;
}
</style>
