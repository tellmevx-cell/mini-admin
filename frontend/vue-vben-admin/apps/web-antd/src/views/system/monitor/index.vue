<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Progress, Spin, Tag, message } from 'ant-design-vue';

import {
  getSystemMonitorOverviewApi,
  type SystemMonitorDependency,
  type SystemMonitorOverview,
} from '#/api/system/monitor';

const loading = ref(false);
const overview = ref<SystemMonitorOverview>();

const summaryItems = computed(() => {
  const data = overview.value;
  if (!data) {
    return [];
  }

  const mysql = data.dependencies.find((item) => item.name === 'MySQL');
  const cache = data.dependencies.find((item) => item.name === 'Cache');

  return [
    {
      description: formatTime(data.api.timestamp),
      label: 'API 状态',
      status: data.api.status,
      value: data.api.status,
    },
    {
      description: dependencyDescription(mysql),
      label: 'MySQL',
      status: mysql?.status ?? 'Unknown',
      value: formatElapsed(mysql),
    },
    {
      description: dependencyDescription(cache),
      label: '缓存',
      status: cache?.status ?? 'Unknown',
      value: formatElapsed(cache),
    },
    {
      description: `${formatBytes(data.memory.usedPhysicalMemoryBytes)} / ${formatBytes(data.memory.totalPhysicalMemoryBytes)}`,
      label: '内存使用率',
      status:
        data.memory.physicalMemoryUsedPercent >= 85 ? 'Warning' : 'Healthy',
      value: `${data.memory.physicalMemoryUsedPercent}%`,
    },
  ];
});

const cpuPercent = computed(() =>
  clampPercent(overview.value?.cpu.processCpuPercent ?? 0),
);

const memoryPercent = computed(() =>
  clampPercent(overview.value?.memory.physicalMemoryUsedPercent ?? 0),
);

async function loadOverview() {
  loading.value = true;
  try {
    overview.value = await getSystemMonitorOverviewApi();
  } catch {
    message.error('系统监控数据加载失败');
  } finally {
    loading.value = false;
  }
}

function statusColor(status?: string) {
  if (status === 'Healthy') {
    return 'green';
  }
  if (status === 'Warning') {
    return 'orange';
  }
  if (status === 'Unhealthy') {
    return 'red';
  }
  return 'default';
}

function dependencyDescription(dependency?: SystemMonitorDependency) {
  return dependency?.description ?? '暂无数据';
}

function formatElapsed(dependency?: SystemMonitorDependency) {
  if (!dependency) {
    return '-';
  }

  return typeof dependency.elapsedMilliseconds === 'number'
    ? `${dependency.elapsedMilliseconds}ms`
    : dependency.status;
}

function formatBytes(value: number) {
  if (value >= 1024 * 1024 * 1024) {
    return `${(value / 1024 / 1024 / 1024).toFixed(2)} GB`;
  }
  if (value >= 1024 * 1024) {
    return `${(value / 1024 / 1024).toFixed(2)} MB`;
  }
  if (value >= 1024) {
    return `${(value / 1024).toFixed(2)} KB`;
  }
  return `${value} B`;
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function clampPercent(value: number) {
  return Math.max(0, Math.min(100, Number(value.toFixed(2))));
}

function progressStatus(percent: number) {
  return percent >= 85 ? 'exception' : 'normal';
}

function formatUptime(seconds: number) {
  const days = Math.floor(seconds / 86_400);
  const hours = Math.floor((seconds % 86_400) / 3600);
  const minutes = Math.floor((seconds % 3600) / 60);
  const parts = [];

  if (days > 0) {
    parts.push(`${days} 天`);
  }
  if (hours > 0) {
    parts.push(`${hours} 小时`);
  }
  parts.push(`${minutes} 分钟`);

  return parts.join(' ');
}

onMounted(loadOverview);
</script>

<template>
  <Page auto-content-height>
    <div class="monitor-workspace">
      <div class="monitor-header">
        <div>
          <h2>系统监控</h2>
          <p>查看应用运行状态、服务器资源和关键依赖健康情况</p>
        </div>
        <Button :loading="loading" @click="loadOverview">刷新</Button>
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
                <span
                  class="summary-dot"
                  :class="`status-${item.status.toLowerCase()}`"
                ></span>
                {{ item.value }}
              </div>
              <div class="summary-desc">{{ item.description }}</div>
            </section>
          </div>

          <div class="metric-grid">
            <section class="info-panel resource-panel">
              <div class="panel-title">
                <h3>CPU</h3>
                <span>进程视角</span>
              </div>
              <div class="resource-meter">
                <Progress
                  type="circle"
                  :percent="cpuPercent"
                  :size="108"
                  :status="progressStatus(cpuPercent)"
                />
                <div class="resource-copy">
                  <div class="resource-value">{{ cpuPercent }}%</div>
                  <div class="resource-caption">进程平均 CPU</div>
                </div>
              </div>
              <table class="info-table">
                <tbody>
                  <tr>
                    <th>核心数</th>
                    <td>{{ overview.cpu.processorCount }}</td>
                  </tr>
                  <tr>
                    <th>线程数</th>
                    <td>{{ overview.cpu.threadCount }}</td>
                  </tr>
                </tbody>
              </table>
            </section>

            <section class="info-panel resource-panel">
              <div class="panel-title">
                <h3>内存</h3>
                <span>系统视角</span>
              </div>
              <div class="resource-meter">
                <Progress
                  type="circle"
                  :percent="memoryPercent"
                  :size="108"
                  :status="progressStatus(memoryPercent)"
                />
                <div class="resource-copy">
                  <div class="resource-value">{{ memoryPercent }}%</div>
                  <div class="resource-caption">
                    {{ formatBytes(overview.memory.usedPhysicalMemoryBytes) }}
                    已用
                  </div>
                </div>
              </div>
              <div class="memory-split">
                <div>
                  <span>总内存</span>
                  <strong>
                    {{ formatBytes(overview.memory.totalPhysicalMemoryBytes) }}
                  </strong>
                </div>
                <div>
                  <span>可用内存</span>
                  <strong>
                    {{
                      formatBytes(overview.memory.availablePhysicalMemoryBytes)
                    }}
                  </strong>
                </div>
                <div>
                  <span>已用内存</span>
                  <strong>
                    {{ formatBytes(overview.memory.usedPhysicalMemoryBytes) }}
                  </strong>
                </div>
              </div>
              <table class="info-table">
                <tbody>
                  <tr>
                    <th>进程工作集</th>
                    <td>{{ formatBytes(overview.memory.workingSetBytes) }}</td>
                  </tr>
                  <tr>
                    <th>托管堆</th>
                    <td>{{ formatBytes(overview.memory.managedHeapBytes) }}</td>
                  </tr>
                  <tr>
                    <th>GC 堆大小</th>
                    <td>{{ formatBytes(overview.memory.gcTotalMemoryBytes) }}</td>
                  </tr>
                  <tr>
                    <th>GC 次数</th>
                    <td>
                      Gen0 {{ overview.memory.gen0Collections }} / Gen1
                      {{ overview.memory.gen1Collections }} / Gen2
                      {{ overview.memory.gen2Collections }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </section>
          </div>

          <section class="info-panel">
            <div class="panel-title">
              <h3>应用与服务器</h3>
            </div>
            <div class="detail-grid">
              <div class="detail-item">
                <span>应用环境</span>
                <strong>{{ overview.application.environment }}</strong>
              </div>
              <div class="detail-item">
                <span>运行时</span>
                <strong>{{ overview.application.runtimeVersion }}</strong>
              </div>
              <div class="detail-item">
                <span>服务器</span>
                <strong>{{ overview.server.machineName }}</strong>
              </div>
              <div class="detail-item">
                <span>操作系统</span>
                <strong>{{ overview.server.operatingSystem }}</strong>
              </div>
              <div class="detail-item">
                <span>系统架构</span>
                <strong>{{ overview.server.architecture }}</strong>
              </div>
              <div class="detail-item">
                <span>运行时长</span>
                <strong>{{ formatUptime(overview.application.uptimeSeconds) }}</strong>
              </div>
              <div class="detail-item">
                <span>启动时间</span>
                <strong>{{ formatTime(overview.application.startedAt) }}</strong>
              </div>
              <div class="detail-item wide-item">
                <span>内容根目录</span>
                <strong>{{ overview.application.contentRootPath }}</strong>
              </div>
            </div>
          </section>

          <div class="bottom-grid">
            <section class="info-panel">
              <div class="panel-title">
                <h3>依赖健康检查</h3>
              </div>
              <div class="dependency-list">
                <div
                  v-for="dependency in overview.dependencies"
                  :key="dependency.name"
                  class="dependency-row"
                >
                  <div class="dependency-main">
                    <strong>{{ dependency.name }}</strong>
                    <span>{{ dependency.description }}</span>
                  </div>
                  <div class="dependency-meta">
                    <Tag :color="statusColor(dependency.status)">
                      {{ dependency.status }}
                    </Tag>
                    <span>{{ formatElapsed(dependency) }}</span>
                  </div>
                </div>
              </div>
            </section>

            <section class="info-panel">
              <div class="panel-title">
                <h3>最近状态</h3>
                <span>近 24 小时</span>
              </div>
              <table class="info-table">
                <tbody>
                  <tr>
                    <th>任务失败数</th>
                    <td>{{ overview.recent.failedScheduledJobCount }}</td>
                  </tr>
                  <tr>
                    <th>异常操作日志</th>
                    <td>{{ overview.recent.failedAuditLogCount }}</td>
                  </tr>
                  <tr>
                    <th>在线用户</th>
                    <td>{{ overview.recent.onlineUserCount }}</td>
                  </tr>
                  <tr>
                    <th>异常文件</th>
                    <td>{{ overview.recent.abnormalFileCount }}</td>
                  </tr>
                </tbody>
              </table>
            </section>
          </div>
        </template>
      </Spin>
    </div>
  </Page>
</template>

<style scoped>
.monitor-workspace {
  display: flex;
  width: 100%;
  max-width: 100%;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 12px;
  overflow-x: hidden;
}

.monitor-header,
.summary-panel,
.info-panel {
  border-radius: 6px;
  background: hsl(var(--background));
}

.monitor-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 14px 16px;
}

.monitor-header h2 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.monitor-header p {
  margin: 4px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 13px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}

.summary-panel {
  min-width: 0;
  min-height: 106px;
  padding: 14px;
}

.summary-label,
.summary-desc,
.panel-title span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.summary-value {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 8px;
  color: hsl(var(--foreground));
  font-size: 20px;
  font-weight: 650;
  line-height: 1.2;
  word-break: break-word;
}

.summary-desc {
  margin-top: 6px;
  display: -webkit-box;
  min-height: 34px;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
  line-height: 1.45;
  overflow-wrap: anywhere;
}

.summary-dot {
  width: 8px;
  height: 8px;
  flex: none;
  border-radius: 50%;
  background: #94a3b8;
}

.status-healthy {
  background: #16a34a;
}

.status-warning {
  background: #f59e0b;
}

.status-unhealthy {
  background: #dc2626;
}

.metric-grid,
.bottom-grid {
  display: grid;
  gap: 12px;
}

.metric-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.bottom-grid {
  grid-template-columns: minmax(0, 1.2fr) minmax(280px, 360px);
}

.info-panel {
  min-width: 0;
  overflow: hidden;
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

.info-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  table-layout: fixed;
}

.info-table th,
.info-table td {
  border-bottom: 1px solid hsl(var(--border));
  padding: 10px 14px;
  text-align: left;
  vertical-align: middle;
}

.info-table tr:last-child th,
.info-table tr:last-child td {
  border-bottom: 0;
}

.info-table th {
  color: hsl(var(--muted-foreground));
  font-weight: 500;
}

.info-table th {
  width: 36%;
}

.info-table td,
.dependency-main span,
.detail-item strong {
  overflow-wrap: anywhere;
}

.resource-panel {
  display: flex;
  flex-direction: column;
}

.resource-meter {
  display: flex;
  align-items: center;
  gap: 18px;
  padding: 16px 18px 12px;
}

.resource-copy {
  min-width: 0;
}

.resource-value {
  color: hsl(var(--foreground));
  font-size: 26px;
  font-weight: 700;
  line-height: 1.1;
}

.resource-caption {
  margin-top: 6px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.memory-split,
.detail-grid {
  display: grid;
  gap: 10px;
  padding: 0 14px 14px;
}

.memory-split {
  grid-template-columns: repeat(3, minmax(0, 1fr));
}

.memory-split div,
.detail-item {
  min-width: 0;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  padding: 10px 12px;
}

.memory-split span,
.detail-item span {
  display: block;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.memory-split strong,
.detail-item strong {
  display: block;
  margin-top: 4px;
  color: hsl(var(--foreground));
  font-size: 13px;
  font-weight: 600;
}

.detail-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
  padding-top: 14px;
}

.wide-item {
  grid-column: span 2;
}

.dependency-list {
  display: flex;
  flex-direction: column;
}

.dependency-row {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 16px;
  align-items: center;
  border-bottom: 1px solid hsl(var(--border));
  padding: 12px 14px;
}

.dependency-row:last-child {
  border-bottom: 0;
}

.dependency-main {
  min-width: 0;
}

.dependency-main strong {
  display: block;
  color: hsl(var(--foreground));
  font-size: 13px;
  font-weight: 600;
}

.dependency-main span,
.dependency-meta span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.dependency-main span {
  display: block;
  margin-top: 4px;
}

.dependency-meta {
  display: flex;
  align-items: center;
  gap: 8px;
  white-space: nowrap;
}

@media (max-width: 1200px) {
  .summary-grid,
  .metric-grid,
  .bottom-grid {
    grid-template-columns: 1fr;
  }

  .detail-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 700px) {
  .monitor-header {
    align-items: flex-start;
    flex-direction: column;
  }

  .resource-meter,
  .dependency-row {
    align-items: flex-start;
  }

  .resource-meter {
    flex-direction: column;
  }

  .dependency-row {
    grid-template-columns: 1fr;
  }

  .memory-split,
  .detail-grid {
    grid-template-columns: 1fr;
  }

  .wide-item {
    grid-column: auto;
  }

  .dependency-meta {
    justify-content: space-between;
  }
}
</style>
