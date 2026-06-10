<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Empty,
  Form,
  FormItem,
  InputNumber,
  Spin,
  Table,
  Tag,
  message,
} from 'ant-design-vue';

import {
  getSecurityCenterOverviewApi,
  getSecurityEventListApi,
  getSecurityPolicyApi,
  type SecurityCenterOverview,
  type SecurityEvent,
  type SecurityPolicy,
  updateSecurityPolicyApi,
} from '#/api/system/security-center';

const loading = ref(false);
const eventLoading = ref(false);
const policyLoading = ref(false);
const policySaving = ref(false);
const overview = ref<SecurityCenterOverview>();
const events = ref<SecurityEvent[]>([]);
const eventTotal = ref(0);
const policyForm = ref<SecurityPolicy>({
  captchaExpireSeconds: 120,
  captchaRequiredFailures: 3,
  lockoutFailures: 5,
  lockoutMinutes: 10,
  onlineActiveTimeoutMinutes: 30,
  onlineTouchThrottleSeconds: 30,
  staleUserDays: 90,
});

const summaryItems = computed(() => {
  const data = overview.value;
  if (!data) {
    return [];
  }

  return [
    {
      label: '账号风险',
      meta: `${data.account.enabledUserCount}/${data.account.totalUserCount} 启用`,
      status: data.account.lockedUserCount > 0 ? 'Warning' : 'Healthy',
      value: data.account.lockedUserCount,
    },
    {
      label: '登录失败',
      meta: `${data.login.failedUserCount24h} 个用户 / ${data.login.failedIpCount24h} 个 IP`,
      status: data.login.failedLoginCount24h > 0 ? 'Warning' : 'Healthy',
      value: data.login.failedLoginCount24h,
    },
    {
      label: '权限变更',
      meta: '近 24 小时',
      status:
        data.permission.permissionChangeCount24h > 0 ? 'Warning' : 'Healthy',
      value: data.permission.permissionChangeCount24h,
    },
    {
      label: '在线会话',
      meta: '当前活跃用户',
      status: 'Healthy',
      value: data.session.onlineUserCount,
    },
  ];
});

const eventColumns = [
  {
    dataIndex: 'level',
    key: 'level',
    title: '级别',
    width: 90,
  },
  {
    dataIndex: 'eventType',
    key: 'eventType',
    title: '事件',
    width: 160,
  },
  {
    dataIndex: 'userName',
    key: 'userName',
    title: '用户',
    width: 140,
  },
  {
    dataIndex: 'title',
    key: 'title',
    title: '标题',
  },
  {
    dataIndex: 'ipAddress',
    key: 'ipAddress',
    title: 'IP',
    width: 150,
  },
  {
    dataIndex: 'createdAt',
    key: 'createdAt',
    title: '时间',
    width: 190,
  },
];

async function loadOverview() {
  loading.value = true;
  try {
    overview.value = await getSecurityCenterOverviewApi();
  } catch {
    message.error('安全中心数据加载失败');
  } finally {
    loading.value = false;
  }
}

async function loadEvents() {
  eventLoading.value = true;
  try {
    const page = await getSecurityEventListApi({ page: 1, pageSize: 8 });
    events.value = page.items;
    eventTotal.value = page.total;
  } catch {
    message.error('安全事件加载失败');
  } finally {
    eventLoading.value = false;
  }
}

async function loadPolicy() {
  policyLoading.value = true;
  try {
    policyForm.value = await getSecurityPolicyApi();
  } catch {
    message.error('安全策略加载失败');
  } finally {
    policyLoading.value = false;
  }
}

async function refreshAll() {
  await Promise.all([loadOverview(), loadEvents(), loadPolicy()]);
}

async function savePolicy() {
  policySaving.value = true;
  try {
    policyForm.value = await updateSecurityPolicyApi(policyForm.value);
    message.success('安全策略已保存');
    await loadOverview();
  } catch {
    message.error('安全策略保存失败');
  } finally {
    policySaving.value = false;
  }
}

function levelColor(level?: string) {
  if (level === 'Critical') {
    return 'red';
  }
  if (level === 'Warning') {
    return 'orange';
  }
  return 'blue';
}

function statusClass(status: string) {
  return status === 'Warning' ? 'status-warning' : 'status-healthy';
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-';
}

onMounted(refreshAll);
</script>

<template>
  <Page auto-content-height>
    <div class="security-workspace">
      <div class="security-header">
        <div>
          <h2>安全中心</h2>
          <p>集中查看账号、登录、权限和会话风险</p>
        </div>
        <Button :loading="loading || eventLoading" @click="refreshAll">
          刷新
        </Button>
      </div>

      <Spin :spinning="loading && !overview">
        <template v-if="overview">
          <div class="summary-grid">
            <section
              v-for="item in summaryItems"
              :key="item.label"
              class="summary-panel"
            >
              <div class="summary-label">{{ item.label }}</div>
              <div class="summary-value">
                <span class="summary-dot" :class="statusClass(item.status)">
                </span>
                {{ item.value }}
              </div>
              <div class="summary-meta">{{ item.meta }}</div>
            </section>
          </div>

          <div class="content-grid">
            <section class="info-panel">
              <div class="panel-title">
                <h3>安全事件</h3>
                <span>{{ eventTotal }} 条</span>
              </div>
              <Table
                :columns="eventColumns"
                :data-source="events"
                :loading="eventLoading"
                :pagination="false"
                row-key="id"
                size="small"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'level'">
                    <Tag :color="levelColor(record.level)">
                      {{ record.level }}
                    </Tag>
                  </template>
                  <template v-else-if="column.key === 'userName'">
                    {{ record.userName || '-' }}
                  </template>
                  <template v-else-if="column.key === 'ipAddress'">
                    {{ record.ipAddress || '-' }}
                  </template>
                  <template v-else-if="column.key === 'createdAt'">
                    {{ formatTime(record.createdAt) }}
                  </template>
                </template>
              </Table>
            </section>

            <div class="side-stack">
              <section class="info-panel">
                <div class="panel-title">
                  <h3>账号安全</h3>
                  <span>当前状态</span>
                </div>
                <div class="metric-list">
                  <div>
                    <span>停用用户</span>
                    <strong>{{ overview.account.disabledUserCount }}</strong>
                  </div>
                  <div>
                    <span>锁定用户</span>
                    <strong>{{ overview.account.lockedUserCount }}</strong>
                  </div>
                  <div>
                    <span>长期未登录</span>
                    <strong>{{ overview.account.staleUserCount }}</strong>
                  </div>
                </div>
              </section>

              <section class="info-panel">
                <div class="panel-title">
                  <h3>安全策略</h3>
                  <Button
                    size="small"
                    type="primary"
                    :loading="policySaving"
                    @click="savePolicy"
                  >
                    保存
                  </Button>
                </div>
                <Spin :spinning="policyLoading">
                  <Form class="policy-form" layout="vertical">
                    <FormItem label="验证码触发失败次数">
                      <InputNumber
                        v-model:value="policyForm.captchaRequiredFailures"
                        :min="1"
                        :max="10"
                        class="w-full"
                      />
                    </FormItem>
                    <FormItem label="账号锁定失败次数">
                      <InputNumber
                        v-model:value="policyForm.lockoutFailures"
                        :min="1"
                        :max="20"
                        class="w-full"
                      />
                    </FormItem>
                    <FormItem label="账号锁定分钟数">
                      <InputNumber
                        v-model:value="policyForm.lockoutMinutes"
                        :min="1"
                        :max="1440"
                        class="w-full"
                      />
                    </FormItem>
                    <FormItem label="验证码有效秒数">
                      <InputNumber
                        v-model:value="policyForm.captchaExpireSeconds"
                        :min="30"
                        :max="600"
                        class="w-full"
                      />
                    </FormItem>
                    <FormItem label="在线活跃分钟数">
                      <InputNumber
                        v-model:value="policyForm.onlineActiveTimeoutMinutes"
                        :min="1"
                        :max="1440"
                        class="w-full"
                      />
                    </FormItem>
                    <FormItem label="在线心跳写入秒数">
                      <InputNumber
                        v-model:value="policyForm.onlineTouchThrottleSeconds"
                        :min="5"
                        :max="600"
                        class="w-full"
                      />
                    </FormItem>
                    <FormItem label="长期未登录天数">
                      <InputNumber
                        v-model:value="policyForm.staleUserDays"
                        :min="1"
                        :max="3650"
                        class="w-full"
                      />
                    </FormItem>
                  </Form>
                </Spin>
              </section>
            </div>
          </div>

          <div class="bottom-grid">
            <section class="info-panel">
              <div class="panel-title">
                <h3>高风险事件</h3>
                <span>最近记录</span>
              </div>
              <div
                v-if="overview.permission.recentHighRiskEvents.length > 0"
                class="event-list"
              >
                <div
                  v-for="event in overview.permission.recentHighRiskEvents"
                  :key="event.id"
                  class="event-row"
                >
                  <Tag :color="levelColor(event.level)">{{ event.level }}</Tag>
                  <div>
                    <strong>{{ event.title }}</strong>
                    <span>{{ event.description }}</span>
                  </div>
                  <time>{{ formatTime(event.createdAt) }}</time>
                </div>
              </div>
              <Empty v-else description="暂无高风险事件" />
            </section>

            <section class="info-panel">
              <div class="panel-title">
                <h3>强制下线</h3>
                <span>最近记录</span>
              </div>
              <div
                v-if="overview.session.recentForceLogoutEvents.length > 0"
                class="event-list"
              >
                <div
                  v-for="event in overview.session.recentForceLogoutEvents"
                  :key="event.id"
                  class="event-row"
                >
                  <Tag color="orange">ForceLogout</Tag>
                  <div>
                    <strong>{{ event.userName || event.relatedEntityId }}</strong>
                    <span>{{ event.description }}</span>
                  </div>
                  <time>{{ formatTime(event.createdAt) }}</time>
                </div>
              </div>
              <Empty v-else description="暂无强制下线记录" />
            </section>
          </div>
        </template>
      </Spin>
    </div>
  </Page>
</template>

<style scoped>
.security-workspace {
  display: flex;
  width: 100%;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 12px;
  overflow-x: hidden;
}

.security-header,
.summary-panel,
.info-panel {
  border-radius: 6px;
  background: hsl(var(--background));
}

.security-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
}

.security-header h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.security-header p,
.summary-label,
.summary-meta,
.panel-title span,
.event-row span,
.event-row time,
.metric-list span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.security-header p {
  margin: 4px 0 0;
}

.summary-grid,
.content-grid,
.bottom-grid {
  display: grid;
  gap: 12px;
}

.summary-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.content-grid {
  grid-template-columns: minmax(0, 1fr) minmax(280px, 360px);
}

.bottom-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.summary-panel {
  min-height: 106px;
  padding: 14px;
}

.summary-value {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
  color: hsl(var(--foreground));
  font-size: 24px;
  font-weight: 700;
  line-height: 1.1;
}

.summary-meta {
  margin-top: 8px;
}

.summary-dot {
  width: 8px;
  height: 8px;
  flex: none;
  border-radius: 50%;
}

.status-healthy {
  background: #16a34a;
}

.status-warning {
  background: #f59e0b;
}

.info-panel {
  min-width: 0;
  overflow: hidden;
}

.side-stack {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 12px;
}

.panel-title {
  display: flex;
  min-height: 46px;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border-bottom: 1px solid hsl(var(--border));
  padding: 0 14px;
}

.panel-title h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.metric-list {
  display: grid;
  gap: 10px;
  padding: 14px;
}

.metric-list div {
  display: flex;
  min-height: 54px;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  padding: 0 12px;
}

.metric-list strong {
  color: hsl(var(--foreground));
  font-size: 20px;
  font-weight: 700;
}

.policy-form {
  display: grid;
  grid-template-columns: 1fr;
  gap: 2px;
  padding: 14px;
}

.policy-form :deep(.ant-form-item) {
  margin-bottom: 8px;
}

.policy-form :deep(.ant-form-item-label > label) {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.event-list {
  display: flex;
  flex-direction: column;
}

.event-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  gap: 10px;
  align-items: center;
  border-bottom: 1px solid hsl(var(--border));
  padding: 12px 14px;
}

.event-row:last-child {
  border-bottom: 0;
}

.event-row div {
  min-width: 0;
}

.event-row strong,
.event-row span {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.event-row strong {
  color: hsl(var(--foreground));
  font-size: 13px;
  font-weight: 600;
}

.event-row span {
  margin-top: 4px;
}

.event-row time {
  white-space: nowrap;
}

@media (max-width: 1200px) {
  .summary-grid,
  .content-grid,
  .bottom-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 700px) {
  .security-header,
  .event-row {
    align-items: flex-start;
  }

  .security-header {
    flex-direction: column;
  }

  .event-row {
    grid-template-columns: 1fr;
  }
}
</style>
