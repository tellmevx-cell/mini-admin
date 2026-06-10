<script setup lang="ts">
import type { TablePaginationConfig } from 'ant-design-vue';

import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';
import { useUserStore } from '@vben/stores';

import { Button, Input, message, Popconfirm, Space, Table, Tag } from 'ant-design-vue';

import {
  forceLogoutOnlineSessionApi,
  forceLogoutOnlineUserApi,
  getOnlineUserListApi,
  type OnlineUserItem,
} from '#/api/system/online-user';
import { useAuthStore } from '#/store';

const { hasAccessByCodes } = useAccess();
const authStore = useAuthStore();
const userStore = useUserStore();

const loading = ref(false);
const users = ref<OnlineUserItem[]>([]);
const total = ref(0);
const query = reactive({
  page: 1,
  pageSize: 10,
  userName: '',
});

const columns = [
  { dataIndex: 'userName', title: '账号', width: 130 },
  { dataIndex: 'realName', title: '姓名', width: 130 },
  { dataIndex: 'ipAddress', title: 'IP', width: 150 },
  { dataIndex: 'deviceName', title: '设备', width: 120 },
  { dataIndex: 'browserName', title: '浏览器', width: 150 },
  { dataIndex: 'loginAt', title: '登录时间', width: 180 },
  { dataIndex: 'lastActiveAt', title: '最近活跃', width: 180 },
  { dataIndex: 'userAgent', title: 'User-Agent', width: 360 },
  { dataIndex: 'action', title: '操作', width: 170 },
];

const pagination = computed<TablePaginationConfig>(() => ({
  current: query.page,
  pageSize: query.pageSize,
  showSizeChanger: true,
  showTotal: (count) => `共 ${count} 条记录`,
  total: total.value,
}));
const canForceLogout = computed(() =>
  hasAccessByCodes(['system:online-user:force-logout']),
);

async function loadUsers() {
  loading.value = true;
  try {
    const result = await getOnlineUserListApi({
      page: query.page,
      pageSize: query.pageSize,
      userName: query.userName.trim() || undefined,
    });
    users.value = result.items;
    total.value = result.total;
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  query.page = 1;
  void loadUsers();
}

function handleReset() {
  query.page = 1;
  query.userName = '';
  void loadUsers();
}

function handleTableChange(nextPagination: TablePaginationConfig) {
  query.page = nextPagination.current ?? 1;
  query.pageSize = nextPagination.pageSize ?? 10;
  void loadUsers();
}

async function forceLogout(user: OnlineUserItem | Record<string, any>) {
  const currentUser = user as OnlineUserItem;
  const isCurrentUser = currentUser.userId === userStore.userInfo?.userId;

  await forceLogoutOnlineUserApi(currentUser.userId);
  if (isCurrentUser) {
    message.success('当前账号已强制下线，请重新登录');
    await authStore.logout(false, false);
    return;
  }

  message.success(`${currentUser.userName} 已强制下线`);
  await loadUsers();
}

async function forceLogoutSession(user: OnlineUserItem | Record<string, any>) {
  const currentUser = user as OnlineUserItem;
  const isCurrentSession = currentUser.sessionId === authStore.getCurrentSessionId();

  await forceLogoutOnlineSessionApi(currentUser.sessionId);
  if (isCurrentSession) {
    message.success('当前会话已强制下线，请重新登录');
    await authStore.logout(false, false);
    return;
  }

  message.success(`${currentUser.userName} 的该会话已强制下线`);
  await loadUsers();
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-';
}

onMounted(loadUsers);
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
        </Space>
        <Space>
          <Button @click="handleReset">重置</Button>
          <Button type="primary" @click="handleSearch">搜索</Button>
        </Space>
      </div>

      <div class="table-shell">
        <div class="table-toolbar">
          <h3>在线用户</h3>
          <Button @click="loadUsers">刷新</Button>
        </div>

        <Table
          row-key="sessionId"
          bordered
          size="small"
          :columns="columns"
          :data-source="users"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 1480 }"
          @change="handleTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.dataIndex === 'userName'">
              <Tag color="blue">{{ record.userName }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'ipAddress'">
              {{ record.ipAddress || '-' }}
            </template>
            <template v-if="column.dataIndex === 'deviceName'">
              <Tag>{{ record.deviceName || '未知设备' }}</Tag>
            </template>
            <template v-if="column.dataIndex === 'browserName'">
              {{ record.browserName || '-' }}
            </template>
            <template v-if="column.dataIndex === 'loginAt'">
              {{ formatTime(record.loginAt) }}
            </template>
            <template v-if="column.dataIndex === 'lastActiveAt'">
              {{ formatTime(record.lastActiveAt) }}
            </template>
            <template v-if="column.dataIndex === 'userAgent'">
              <span class="agent-cell">{{ record.userAgent || '-' }}</span>
            </template>
            <template v-if="column.dataIndex === 'action'">
              <Space v-if="canForceLogout" :size="4">
                <Popconfirm
                  title="确认强制该会话下线？"
                  @confirm="forceLogoutSession(record)"
                >
                  <Button danger size="small" type="link">下线会话</Button>
                </Popconfirm>
                <Popconfirm
                  title="确认强制该用户全部会话下线？"
                  @confirm="forceLogout(record)"
                >
                  <Button danger size="small" type="link">下线用户</Button>
                </Popconfirm>
              </Space>
              <span v-else>-</span>
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

.query-input {
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
}
</style>
