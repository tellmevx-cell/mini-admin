<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';
import type { Dayjs } from 'dayjs';

import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, DatePicker, Input, Select, Space, Table, Tag } from 'ant-design-vue';

import {
  getLoginLogListApi,
  type LoginLogItem,
  type LoginLogListParams,
} from '#/api/system/login-log';

const RangePicker = DatePicker.RangePicker;

const loading = ref(false);
const logs = ref<LoginLogItem[]>([]);
const total = ref(0);
const createdAtRange = ref<[Dayjs, Dayjs]>();
const query = reactive({
  isSuccess: undefined as string | undefined,
  page: 1,
  pageSize: 10,
  userName: '',
});

const successOptions = [
  { label: '成功', value: 'true' },
  { label: '失败', value: 'false' },
];

const columns = [
  { dataIndex: 'createdAt', title: '登录时间', width: 180 },
  { dataIndex: 'userName', title: '账号', width: 130 },
  { dataIndex: 'realName', title: '姓名', width: 130 },
  { dataIndex: 'isSuccess', title: '结果', width: 90 },
  { dataIndex: 'message', title: '消息', width: 120 },
  { dataIndex: 'ipAddress', title: 'IP', width: 150 },
  { dataIndex: 'userAgent', title: '浏览器', width: 420 },
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

async function loadLogs() {
  loading.value = true;
  try {
    const result = await getLoginLogListApi(buildQueryParams());
    logs.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function buildQueryParams(): LoginLogListParams {
  return {
    endCreatedAt: createdAtRange.value?.[1]?.toISOString(),
    isSuccess: parseBooleanSelectValue(query.isSuccess),
    page: query.page,
    pageSize: query.pageSize,
    startCreatedAt: createdAtRange.value?.[0]?.toISOString(),
    userName: query.userName.trim() || undefined,
  };
}

function handleSearch() {
  query.page = 1;
  void loadLogs();
}

function handleReset() {
  query.isSuccess = undefined;
  query.page = 1;
  query.userName = '';
  createdAtRange.value = undefined;
  void loadLogs();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadLogs();
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-';
}

onMounted(loadLogs);
</script>

<template>
  <Page auto-content-height>
    <div class="page-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">账号</span>
          <Input
            v-model:value="query.userName"
            allow-clear
            class="query-input"
            placeholder="请输入"
            @press-enter="handleSearch"
          />
          <span class="query-label">结果</span>
          <Select
            v-model:value="query.isSuccess"
            allow-clear
            class="query-select"
            :options="successOptions"
            placeholder="请选择"
          />
          <span class="query-label">时间</span>
          <RangePicker v-model:value="createdAtRange" class="query-range" show-time />
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>

      <div class="table-shell">
        <div class="table-toolbar">
          <h3>登录日志</h3>
          <Button @click="loadLogs">刷新</Button>
        </div>

        <Table
          row-key="id"
          bordered
          size="small"
          :columns="columns"
          :data-source="logs"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 1220 }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'createdAt'">
              {{ formatTime(record.createdAt) }}
            </template>
            <template v-if="column.dataIndex === 'realName'">
              {{ record.realName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'isSuccess'">
              <Tag :color="record.isSuccess ? 'green' : 'red'">
                {{ record.isSuccess ? '成功' : '失败' }}
              </Tag>
            </template>
            <template v-if="column.dataIndex === 'ipAddress'">
              {{ record.ipAddress || '-' }}
            </template>
            <template v-if="column.dataIndex === 'userAgent'">
              <span class="agent-cell">{{ record.userAgent || '-' }}</span>
            </template>
          </template>
        </Table>
      </div>
    </div>
  </Page>
</template>

<style scoped>
.page-workspace {
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
  width: 160px;
}

.query-range {
  width: 360px;
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

.agent-cell {
  display: inline-block;
  max-width: 400px;
  overflow: hidden;
  text-overflow: ellipsis;
  vertical-align: bottom;
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

  .query-range {
    width: 100%;
  }
}
</style>
