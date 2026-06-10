<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Drawer,
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
  message,
} from 'ant-design-vue';

import {
  getScheduledJobListApi,
  getScheduledJobLogDetailsApi,
  getScheduledJobLogsApi,
  runScheduledJobApi,
  type ScheduledJobItem,
  type ScheduledJobLogDetailItem,
  type ScheduledJobLogItem,
  updateScheduledJobApi,
} from '#/api/system/scheduled-job';
import { markFileInvalidApi } from '#/api/system/file';

const { hasAccessByCodes } = useAccess();

interface JobFormState {
  description: string;
  intervalSeconds: number;
  isEnabled: boolean;
  name: string;
}

const loading = ref(false);
const saving = ref(false);
const runningJobId = ref('');
const jobs = ref<ScheduledJobItem[]>([]);
const total = ref(0);
const modalOpen = ref(false);
const editingJob = ref<ScheduledJobItem>();
const logDrawerOpen = ref(false);
const logLoading = ref(false);
const logJob = ref<ScheduledJobItem>();
const logs = ref<ScheduledJobLogItem[]>([]);
const logTotal = ref(0);
const detailDrawerOpen = ref(false);
const detailLoading = ref(false);
const detailLog = ref<ScheduledJobLogItem>();
const details = ref<ScheduledJobLogDetailItem[]>([]);
const detailTotal = ref(0);

const query = reactive({
  isEnabled: undefined as string | undefined,
  jobKey: '',
  name: '',
  page: 1,
  pageSize: 10,
});
const logQuery = reactive({
  page: 1,
  pageSize: 10,
});
const detailQuery = reactive({
  page: 1,
  pageSize: 10,
});
const formState = reactive<JobFormState>({
  description: '',
  intervalSeconds: 3600,
  isEnabled: true,
  name: '',
});

const columns = [
  { dataIndex: 'name', title: '任务名称', width: 180 },
  { dataIndex: 'jobKey', title: '任务标识', width: 190 },
  { dataIndex: 'intervalSeconds', title: '执行间隔', width: 120 },
  { dataIndex: 'state', title: '状态', width: 100 },
  { dataIndex: 'lastStatus', title: '最近结果', width: 120 },
  { dataIndex: 'lastRunAt', title: '最近执行', width: 180 },
  { dataIndex: 'nextRunAt', title: '下次执行', width: 180 },
  { dataIndex: 'action', title: '操作', width: 230 },
];
const logColumns = [
  { dataIndex: 'triggerType', title: '触发方式', width: 100 },
  { dataIndex: 'status', title: '状态', width: 100 },
  { dataIndex: 'message', title: '结果', width: 320 },
  { dataIndex: 'startedAt', title: '开始时间', width: 180 },
  { dataIndex: 'elapsedMilliseconds', title: '耗时', width: 100 },
  { dataIndex: 'action', title: '操作', width: 90 },
];
const detailColumns = [
  { dataIndex: 'targetName', title: '对象名称', width: 180 },
  { dataIndex: 'targetId', title: '对象 ID', width: 260 },
  { dataIndex: 'storageProvider', title: '存储', width: 90 },
  { dataIndex: 'storagePath', title: '存储路径', width: 260 },
  { dataIndex: 'status', title: '状态', width: 100 },
  { dataIndex: 'message', title: '原因', width: 220 },
  { dataIndex: 'action', title: '操作', width: 110 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));
const logPagination = computed<TablePaginationConfig>(() => ({
  current: logQuery.page,
  pageSize: logQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: logTotal.value,
}));
const detailPagination = computed<TablePaginationConfig>(() => ({
  current: detailQuery.page,
  pageSize: detailQuery.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: detailTotal.value,
}));

const enabledOptions = [
  { label: '启用', value: 'true' },
  { label: '停用', value: 'false' },
];

function parseBooleanSelectValue(value?: string) {
  if (value === 'true') {
    return true;
  }

  if (value === 'false') {
    return false;
  }

  return undefined;
}

const canUpdate = computed(() =>
  hasAccessByCodes(['system:scheduled-job:update']),
);
const canRun = computed(() => hasAccessByCodes(['system:scheduled-job:run']));
const canMarkFileInvalid = computed(() =>
  hasAccessByCodes(['system:file:mark-invalid']),
);

async function loadJobs() {
  loading.value = true;
  try {
    const result = await getScheduledJobListApi({
      isEnabled: parseBooleanSelectValue(query.isEnabled),
      jobKey: query.jobKey.trim() || undefined,
      name: query.name.trim() || undefined,
      page: query.page,
      pageSize: query.pageSize,
    });
    jobs.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadJobs();
}

function handleReset() {
  query.isEnabled = undefined;
  query.jobKey = '';
  query.name = '';
  query.page = 1;
  void loadJobs();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadJobs();
}

function openEditModal(job: Record<string, any> | ScheduledJobItem) {
  const currentJob = job as ScheduledJobItem;
  editingJob.value = currentJob;
  formState.name = currentJob.name;
  formState.description = currentJob.description ?? '';
  formState.intervalSeconds = currentJob.intervalSeconds;
  formState.isEnabled = currentJob.isEnabled;
  modalOpen.value = true;
}

async function submitJob() {
  if (!editingJob.value) {
    return;
  }

  if (!formState.name.trim()) {
    message.warning('请填写任务名称');
    return;
  }

  saving.value = true;
  try {
    await updateScheduledJobApi(editingJob.value.id, {
      description: formState.description.trim() || null,
      intervalSeconds: Math.max(formState.intervalSeconds, 60),
      isEnabled: formState.isEnabled,
      name: formState.name.trim(),
    });
    message.success('任务已更新');
    modalOpen.value = false;
    await loadJobs();
  } finally {
    saving.value = false;
  }
}

async function toggleJob(job: Record<string, any> | ScheduledJobItem, checked: boolean) {
  const currentJob = job as ScheduledJobItem;
  await updateScheduledJobApi(currentJob.id, {
    description: currentJob.description ?? null,
    intervalSeconds: currentJob.intervalSeconds,
    isEnabled: checked,
    name: currentJob.name,
  });
  message.success(checked ? '任务已启用' : '任务已停用');
  await loadJobs();
}

async function runJob(job: Record<string, any> | ScheduledJobItem) {
  const currentJob = job as ScheduledJobItem;
  runningJobId.value = currentJob.id;
  try {
    const result = await runScheduledJobApi(currentJob.id);
    if (result.status === 'Success') {
      message.success(result.message);
    } else if (result.status === 'Warning') {
      message.warning(result.message);
    } else {
      message.error(result.message);
    }
    await loadJobs();
    if (logDrawerOpen.value && logJob.value?.id === currentJob.id) {
      await loadLogs();
    }
  } finally {
    runningJobId.value = '';
  }
}

async function openLogDrawer(job: Record<string, any> | ScheduledJobItem) {
  logJob.value = job as ScheduledJobItem;
  logQuery.page = 1;
  logDrawerOpen.value = true;
  await loadLogs();
}

async function loadLogs() {
  if (!logJob.value) {
    return;
  }

  logLoading.value = true;
  try {
    const result = await getScheduledJobLogsApi(logJob.value.id, {
      page: logQuery.page,
      pageSize: logQuery.pageSize,
    });
    logs.value = result.items;
    logTotal.value = result.total;
  } finally {
    logLoading.value = false;
  }
}

function handleLogTableChange(nextPagination: TablePaginationConfig) {
  logQuery.page = nextPagination.current ?? 1;
  logQuery.pageSize = nextPagination.pageSize ?? 10;
  void loadLogs();
}

async function openDetailDrawer(log: Record<string, any> | ScheduledJobLogItem) {
  detailLog.value = log as ScheduledJobLogItem;
  detailQuery.page = 1;
  detailDrawerOpen.value = true;
  await loadDetails();
}

async function loadDetails() {
  if (!detailLog.value) {
    return;
  }

  detailLoading.value = true;
  try {
    const result = await getScheduledJobLogDetailsApi(detailLog.value.id, {
      page: detailQuery.page,
      pageSize: detailQuery.pageSize,
    });
    details.value = result.items;
    detailTotal.value = result.total;
  } finally {
    detailLoading.value = false;
  }
}

function handleDetailTableChange(nextPagination: TablePaginationConfig) {
  detailQuery.page = nextPagination.current ?? 1;
  detailQuery.pageSize = nextPagination.pageSize ?? 10;
  void loadDetails();
}

async function markDetailFileInvalid(detail: Record<string, any> | ScheduledJobLogDetailItem) {
  const currentDetail = detail as ScheduledJobLogDetailItem;
  if (!currentDetail.targetId) {
    return;
  }

  await markFileInvalidApi(currentDetail.targetId);
  message.success('文件已标记无效');
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function formatInterval(seconds: number) {
  if (seconds >= 86_400 && seconds % 86_400 === 0) {
    return `${seconds / 86_400} 天`;
  }

  if (seconds >= 3600 && seconds % 3600 === 0) {
    return `${seconds / 3600} 小时`;
  }

  if (seconds >= 60 && seconds % 60 === 0) {
    return `${seconds / 60} 分钟`;
  }

  return `${seconds} 秒`;
}

function statusColor(status: string) {
  if (status === 'Success') {
    return 'green';
  }

  if (status === 'Failed') {
    return 'red';
  }

  if (status === 'Warning') {
    return 'orange';
  }

  return 'default';
}

onMounted(loadJobs);
</script>

<template>
  <Page auto-content-height>
    <div class="job-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">任务名称</span>
          <Input
            v-model:value="query.name"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">任务标识</span>
          <Input
            v-model:value="query.jobKey"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">状态</span>
          <Select
            v-model:value="query.isEnabled"
            allow-clear
            class="query-select"
            placeholder="全部"
            :options="enabledOptions"
          />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>

      <div class="table-shell">
        <div class="table-toolbar">
          <h3>定时任务</h3>
          <Button @click="loadJobs">刷新</Button>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="jobs"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 1320 }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'name'">
              <div class="name-cell">
                <span>{{ record.name }}</span>
                <small>{{ record.description || '-' }}</small>
              </div>
            </template>
            <template v-if="column.dataIndex === 'jobKey'">
              <Tag color="blue">{{ record.jobKey }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'intervalSeconds'">
              {{ formatInterval(record.intervalSeconds) }}
            </template>
            <template v-if="column.dataIndex === 'state'">
              <Switch
                :checked="record.isEnabled"
                :disabled="!canUpdate"
                checked-children="启"
                un-checked-children="停"
                @change="(checked) => toggleJob(record, checked as boolean)"
              />
            </template>
            <template v-if="column.dataIndex === 'lastStatus'">
              <Tag :color="statusColor(record.lastStatus)">
                {{ record.lastStatus }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'lastRunAt'">
              {{ formatTime(record.lastRunAt) }}
            </template>
            <template v-if="column.dataIndex === 'nextRunAt'">
              {{ formatTime(record.nextRunAt) }}
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Space>
                <Button size="small" type="link" @click="openLogDrawer(record)">
                  日志
                </Button>
                <Button
                  v-if="canUpdate"
                  size="small"
                  type="link"
                  @click="openEditModal(record)"
                >
                  编辑
                </Button>
                <Popconfirm
                  v-if="canRun"
                  title="确认立即执行该任务？"
                  @confirm="runJob(record)"
                >
                  <Button
                    size="small"
                    type="link"
                    :loading="runningJobId === record.id"
                  >
                    执行
                  </Button>
                </Popconfirm>
              </Space>
            </template>
          </template>
        </Table>
      </div>

      <Modal
        v-model:open="modalOpen"
        title="编辑任务"
        :confirm-loading="saving"
        @ok="submitJob"
      >
        <Form layout="vertical">
          <FormItem label="任务名称" required>
            <Input v-model:value="formState.name" />
          </FormItem>
          <FormItem label="任务描述">
            <Input v-model:value="formState.description" />
          </FormItem>
          <FormItem label="执行间隔（秒）" required>
            <InputNumber
              v-model:value="formState.intervalSeconds"
              class="full-input"
              :min="60"
              :step="60"
            />
          </FormItem>
          <FormItem label="启用状态">
            <Switch v-model:checked="formState.isEnabled" />
          </FormItem>
        </Form>
      </Modal>

      <Drawer
        v-model:open="logDrawerOpen"
        width="760"
        :title="`${logJob?.name ?? '任务'} 执行日志`"
      >
        <Table
          row-key="id"
          bordered
          size="small"
          :columns="logColumns"
          :data-source="logs"
          :loading="logLoading"
          :pagination="logPagination"
          :scroll="{ x: 820 }"
          @change="handleLogTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'triggerType'">
              <Tag>{{ record.triggerType === 'Manual' ? '手动' : '自动' }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'status'">
              <Tag :color="statusColor(record.status)">{{ record.status }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'startedAt'">
              {{ formatTime(record.startedAt) }}
            </template>
            <template v-if="column.dataIndex === 'elapsedMilliseconds'">
              {{ record.elapsedMilliseconds }} ms
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Button size="small" type="link" @click="openDetailDrawer(record)">
                详情
              </Button>
            </template>
          </template>
        </Table>
      </Drawer>

      <Drawer
        v-model:open="detailDrawerOpen"
        width="900"
        :title="`${detailLog?.jobName ?? '任务'} 执行详情`"
      >
        <Table
          row-key="id"
          bordered
          size="small"
          :columns="detailColumns"
          :data-source="details"
          :loading="detailLoading"
          :pagination="detailPagination"
          :scroll="{ x: 1110 }"
          @change="handleDetailTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'targetName'">
              {{ record.targetName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'targetId'">
              {{ record.targetId || '-' }}
            </template>
            <template v-if="column.dataIndex === 'storageProvider'">
              <Tag>{{ record.storageProvider || '-' }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'storagePath'">
              {{ record.storagePath || '-' }}
            </template>
            <template v-if="column.dataIndex === 'status'">
              <Tag :color="statusColor(record.status)">{{ record.status }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Popconfirm
                v-if="
                  canMarkFileInvalid &&
                  record.targetType === 'ManagedFile' &&
                  record.targetId
                "
                title="确认将该文件标记为无效？"
                @confirm="markDetailFileInvalid(record)"
              >
                <Button size="small" type="link">标记无效</Button>
              </Popconfirm>
              <span v-else>-</span>
            </template>
          </template>
        </Table>
      </Drawer>
    </div>
  </Page>
</template>

<style scoped>
.job-workspace {
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

.query-input,
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

.name-cell {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.name-cell small {
  color: hsl(var(--muted-foreground));
}

.full-input {
  width: 100%;
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
