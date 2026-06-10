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
  Select,
  Space,
  Table,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';

import {
  acknowledgeAlertApi,
  getAlertListApi,
  type AlertItem,
} from '#/api/system/alert';

const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const acknowledging = ref(false);
const alerts = ref<AlertItem[]>([]);
const total = ref(0);
const modalOpen = ref(false);
const currentAlert = ref<AlertItem>();
const formState = reactive({
  remark: '',
});
const query = reactive({
  level: undefined as string | undefined,
  page: 1,
  pageSize: 10,
  status: undefined as string | undefined,
  type: undefined as string | undefined,
});

const canAcknowledge = computed(() =>
  hasAccessByCodes(['system:alert:acknowledge']),
);

const columns = [
  { dataIndex: 'level', title: '等级', width: 90 },
  { dataIndex: 'status', title: '状态', width: 110 },
  { dataIndex: 'title', title: '告警标题', width: 220 },
  { dataIndex: 'type', title: '类型', width: 170 },
  { dataIndex: 'source', title: '来源', width: 130 },
  { dataIndex: 'triggerCount', title: '次数', width: 80 },
  { dataIndex: 'lastTriggeredAt', title: '最近触发', width: 180 },
  { dataIndex: 'acknowledgedBy', title: '确认人', width: 120 },
  { dataIndex: 'action', title: '操作', width: 120 },
];

const levelOptions = [
  { label: '提示', value: 'Info' },
  { label: '警告', value: 'Warning' },
  { label: '严重', value: 'Critical' },
];

const statusOptions = [
  { label: '活跃', value: 'Active' },
  { label: '已确认', value: 'Acknowledged' },
  { label: '已恢复', value: 'Recovered' },
];

const typeOptions = [
  { label: '内存过高', value: 'MemoryHigh' },
  { label: '依赖异常', value: 'DependencyUnhealthy' },
  { label: '定时任务失败', value: 'ScheduledJobFailed' },
  { label: '失败操作日志', value: 'AuditFailureHigh' },
  { label: '异常文件', value: 'AbnormalFileDetected' },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));

async function loadAlerts() {
  loading.value = true;
  try {
    const result = await getAlertListApi({
      level: query.level,
      page: query.page,
      pageSize: query.pageSize,
      status: query.status,
      type: query.type,
    });
    alerts.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadAlerts();
}

function handleReset() {
  query.level = undefined;
  query.page = 1;
  query.status = undefined;
  query.type = undefined;
  void loadAlerts();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadAlerts();
}

function openAcknowledgeModal(alert: AlertItem | Record<string, any>) {
  const current = alert as AlertItem;
  currentAlert.value = current;
  formState.remark = current.acknowledgeRemark ?? '';
  modalOpen.value = true;
}

async function submitAcknowledge() {
  if (!currentAlert.value) {
    return;
  }

  acknowledging.value = true;
  try {
    await acknowledgeAlertApi(currentAlert.value.id, {
      remark: formState.remark.trim() || null,
    });
    message.success('告警已确认');
    modalOpen.value = false;
    await loadAlerts();
  } finally {
    acknowledging.value = false;
  }
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

function statusColor(status: string) {
  if (status === 'Recovered') {
    return 'green';
  }

  if (status === 'Acknowledged') {
    return 'blue';
  }

  return 'orange';
}

function levelText(level: string) {
  return levelOptions.find((item) => item.value === level)?.label ?? level;
}

function statusText(status: string) {
  return statusOptions.find((item) => item.value === status)?.label ?? status;
}

function typeText(type: string) {
  return typeOptions.find((item) => item.value === type)?.label ?? type;
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

onMounted(loadAlerts);
</script>

<template>
  <Page auto-content-height>
    <div class="alert-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">类型</span>
          <Select
            v-model:value="query.type"
            allow-clear
            class="query-select"
            :options="typeOptions"
            placeholder="全部"
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
            v-model:value="query.status"
            allow-clear
            class="query-select"
            :options="statusOptions"
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
          <h3>告警中心</h3>
          <Button @click="loadAlerts">刷新</Button>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="alerts"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 1220 }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'level'">
              <Tag :color="levelColor(record.level)">
                {{ levelText(record.level) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'status'">
              <Tag :color="statusColor(record.status)">
                {{ statusText(record.status) }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'title'">
              <div class="title-cell">
                <span>{{ record.title }}</span>
                <small>{{ record.content }}</small>
              </div>
            </template>
            <template v-if="column.dataIndex === 'type'">
              {{ typeText(record.type) }}
            </template>
            <template v-if="column.dataIndex === 'lastTriggeredAt'">
              {{ formatTime(record.lastTriggeredAt) }}
            </template>
            <template v-if="column.dataIndex === 'acknowledgedBy'">
              {{ record.acknowledgedBy || '-' }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Button
                v-if="canAcknowledge && record.status !== 'Recovered'"
                size="small"
                type="link"
                @click="openAcknowledgeModal(record)"
              >
                确认
              </Button>
              <span v-else>-</span>
            </template>
          </template>
        </Table>
      </div>

      <Modal
        v-model:open="modalOpen"
        title="确认告警"
        :confirm-loading="acknowledging"
        @ok="submitAcknowledge"
      >
        <Form layout="vertical">
          <FormItem label="告警标题">
            <Input :value="currentAlert?.title" disabled />
          </FormItem>
          <FormItem label="处理备注">
            <Textarea
              v-model:value="formState.remark"
              :maxlength="512"
              :rows="4"
              placeholder="请输入处理备注"
              show-count
            />
          </FormItem>
        </Form>
      </Modal>
    </div>
  </Page>
</template>

<style scoped>
.alert-workspace {
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

.query-select {
  width: 180px;
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

.title-cell {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 2px;
}

.title-cell small {
  max-width: 420px;
  overflow: hidden;
  color: hsl(var(--muted-foreground));
  text-overflow: ellipsis;
  white-space: nowrap;
}

:deep(.ant-table) {
  font-size: 13px;
}

@media (max-width: 900px) {
  .query-bar {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
