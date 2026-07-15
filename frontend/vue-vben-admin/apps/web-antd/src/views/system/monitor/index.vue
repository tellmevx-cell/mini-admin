<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Button, Progress, Spin, Tag, message } from 'ant-design-vue';

import {
  getSystemMonitorOverviewApi,
  type SystemMonitorDependency,
  type SystemMonitorOverview,
} from '#/api/system/monitor';
import { $t } from '#/locales';

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
      label: $t('page.monitor.apiStatus'),
      status: data.api.status,
      value: data.api.status,
    },
    {
      description: dependencyDescription(mysql),
      label: $t('page.monitor.mysql'),
      status: mysql?.status ?? 'Unknown',
      value: formatElapsed(mysql),
    },
    {
      description: dependencyDescription(cache),
      label: $t('page.monitor.cache'),
      status: cache?.status ?? 'Unknown',
      value: formatElapsed(cache),
    },
    {
      description: `${formatBytes(data.memory.usedPhysicalMemoryBytes)} / ${formatBytes(data.memory.totalPhysicalMemoryBytes)}`,
      label: $t('page.monitor.memoryUsage'),
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
    message.error($t('page.monitor.loadFailed'));
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
  return dependency?.description ?? $t('page.monitor.noData');
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
    parts.push(`${days} ${$t('page.monitor.day')}`);
  }
  if (hours > 0) {
    parts.push(`${hours} ${$t('page.monitor.hour')}`);
  }
  parts.push(`${minutes} ${$t('page.monitor.minute')}`);

  return parts.join(' ');
}

function formatBitRate(bitsPerSecond: number) {
  if (bitsPerSecond >= 1_000_000_000) {
    return `${(bitsPerSecond / 1_000_000_000).toFixed(2)} Gbps`;
  }
  if (bitsPerSecond >= 1_000_000) {
    return `${(bitsPerSecond / 1_000_000).toFixed(2)} Mbps`;
  }
  if (bitsPerSecond >= 1_000) {
    return `${(bitsPerSecond / 1_000).toFixed(2)} Kbps`;
  }
  return `${bitsPerSecond} bps`;
}

function displayList(values?: string[]) {
  return values?.length ? values.join(' / ') : $t('page.monitor.notDetected');
}

onMounted(loadOverview);
</script>

<template>
  <Page auto-content-height>
    <div class="monitor-workspace">
      <div class="monitor-header">
        <div>
          <h2>{{ $t('page.monitor.title') }}</h2>
          <p>{{ $t('page.monitor.subtitle') }}</p>
        </div>
        <Button :loading="loading" @click="loadOverview">
          {{ $t('page.monitor.refresh') }}
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
                <h3>{{ $t('page.monitor.cpu') }}</h3>
                <span>{{ $t('page.monitor.processView') }}</span>
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
                  <div class="resource-caption">
                    {{ $t('page.monitor.averageProcessCpu') }}
                  </div>
                </div>
              </div>
              <table class="info-table">
                <tbody>
                  <tr>
                    <th>{{ $t('page.monitor.cores') }}</th>
                    <td>{{ overview.cpu.processorCount }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.threads') }}</th>
                    <td>{{ overview.cpu.threadCount }}</td>
                  </tr>
                </tbody>
              </table>
            </section>

            <section class="info-panel resource-panel">
              <div class="panel-title">
                <h3>{{ $t('page.monitor.memory') }}</h3>
                <span>{{ $t('page.monitor.systemView') }}</span>
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
                    {{ $t('page.monitor.used') }}
                  </div>
                </div>
              </div>
              <div class="memory-split">
                <div>
                  <span>{{ $t('page.monitor.totalMemory') }}</span>
                  <strong>
                    {{ formatBytes(overview.memory.totalPhysicalMemoryBytes) }}
                  </strong>
                </div>
                <div>
                  <span>{{ $t('page.monitor.availableMemory') }}</span>
                  <strong>
                    {{
                      formatBytes(overview.memory.availablePhysicalMemoryBytes)
                    }}
                  </strong>
                </div>
                <div>
                  <span>{{ $t('page.monitor.usedMemory') }}</span>
                  <strong>
                    {{ formatBytes(overview.memory.usedPhysicalMemoryBytes) }}
                  </strong>
                </div>
              </div>
              <table class="info-table">
                <tbody>
                  <tr>
                    <th>{{ $t('page.monitor.workingSet') }}</th>
                    <td>{{ formatBytes(overview.memory.workingSetBytes) }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.managedHeap') }}</th>
                    <td>{{ formatBytes(overview.memory.managedHeapBytes) }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.gcHeap') }}</th>
                    <td>{{ formatBytes(overview.memory.gcTotalMemoryBytes) }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.gcCollections') }}</th>
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
              <h3>{{ $t('page.monitor.applicationServer') }}</h3>
            </div>
            <div class="detail-grid">
              <div class="detail-item">
                <span>{{ $t('page.monitor.environment') }}</span>
                <strong>{{ overview.application.environment }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.runtime') }}</span>
                <strong>{{ overview.application.runtimeVersion }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.server') }}</span>
                <strong>{{ overview.server.machineName }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.operatingSystem') }}</span>
                <strong>{{ overview.server.operatingSystem }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.architecture') }}</span>
                <strong>{{ overview.server.architecture }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.uptime') }}</span>
                <strong>{{ formatUptime(overview.application.uptimeSeconds) }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.process') }}</span>
                <strong>
                  PID {{ overview.application.processId }} /
                  {{ overview.application.processArchitecture }}
                </strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.gcMode') }}</span>
                <strong>
                  {{ overview.application.serverGarbageCollection ? 'Server' : 'Workstation' }}
                  / {{ overview.application.garbageCollectionLatencyMode }}
                </strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.startedAt') }}</span>
                <strong>{{ formatTime(overview.application.startedAt) }}</strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.device') }}</span>
                <strong>
                  {{ overview.hardware.manufacturer }} /
                  {{ overview.hardware.model }}
                </strong>
              </div>
              <div class="detail-item">
                <span>{{ $t('page.monitor.motherboard') }}</span>
                <strong>
                  {{ overview.hardware.motherboardManufacturer }} /
                  {{ overview.hardware.motherboardModel }}
                </strong>
              </div>
              <div class="detail-item wide-item">
                <span>{{ $t('page.monitor.cpuModel') }}</span>
                <strong>{{ overview.hardware.cpuModel }}</strong>
              </div>
              <div class="detail-item wide-item">
                <span>{{ $t('page.monitor.gpu') }}</span>
                <strong>{{ displayList(overview.hardware.gpus) }}</strong>
              </div>
              <div class="detail-item wide-item">
                <span>{{ $t('page.monitor.contentRoot') }}</span>
                <strong>{{ overview.application.contentRootPath }}</strong>
              </div>
            </div>
          </section>

          <div class="telemetry-grid">
            <section class="info-panel">
              <div class="panel-title">
                <h3>{{ $t('page.monitor.disk') }}</h3>
                <span>
                  {{ overview.disks.length }} {{ $t('page.monitor.volumes') }}
                </span>
              </div>
              <div v-if="overview.disks.length" class="disk-list">
                <div
                  v-for="disk in overview.disks"
                  :key="`${disk.name}-${disk.rootPath}`"
                  class="disk-row"
                >
                  <div class="disk-heading">
                    <div>
                      <strong>{{ disk.name }}</strong>
                      <span>
                        {{ disk.driveType }} ·
                        {{ disk.fileSystem || $t('page.monitor.unknownFormat') }}
                      </span>
                    </div>
                    <span v-if="disk.isReady">
                      {{ formatBytes(disk.usedBytes) }} /
                      {{ formatBytes(disk.totalBytes) }}
                    </span>
                    <Tag v-else>{{ $t('page.monitor.notReady') }}</Tag>
                  </div>
                  <Progress
                    v-if="disk.isReady"
                    :percent="clampPercent(disk.usedPercent)"
                    :show-info="false"
                    :status="progressStatus(disk.usedPercent)"
                    size="small"
                  />
                </div>
              </div>
              <div v-else class="empty-copy">
                {{ $t('page.monitor.noDisks') }}
              </div>
            </section>

            <section class="info-panel">
              <div class="panel-title">
                <h3>{{ $t('page.monitor.network') }}</h3>
                <span>
                  {{ overview.networks.length }}
                  {{ $t('page.monitor.interfaces') }}
                </span>
              </div>
              <div v-if="overview.networks.length" class="network-list">
                <div
                  v-for="network in overview.networks"
                  :key="network.name"
                  class="network-row"
                >
                  <div class="network-heading">
                    <div>
                      <strong>{{ network.name }}</strong>
                      <span>{{ network.interfaceType }}</span>
                    </div>
                    <Tag :color="network.status === 'Up' ? 'green' : 'default'">
                      {{ network.status }}
                    </Tag>
                  </div>
                  <div class="network-address">
                    {{ displayList(network.addresses) }}
                  </div>
                  <div class="network-stats">
                    <span>
                      {{ $t('page.monitor.speed') }}
                      {{ formatBitRate(network.speedBitsPerSecond) }}
                    </span>
                    <span>
                      {{ $t('page.monitor.received') }}
                      {{ formatBytes(network.bytesReceived) }}
                    </span>
                    <span>
                      {{ $t('page.monitor.sent') }}
                      {{ formatBytes(network.bytesSent) }}
                    </span>
                  </div>
                </div>
              </div>
              <div v-else class="empty-copy">
                {{ $t('page.monitor.noNetworks') }}
              </div>
            </section>
          </div>

          <div class="bottom-grid">
            <section class="info-panel">
              <div class="panel-title">
                <h3>{{ $t('page.monitor.dependencies') }}</h3>
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
                <h3>{{ $t('page.monitor.recent') }}</h3>
                <span>{{ $t('page.monitor.last24Hours') }}</span>
              </div>
              <table class="info-table">
                <tbody>
                  <tr>
                    <th>{{ $t('page.monitor.failedJobs') }}</th>
                    <td>{{ overview.recent.failedScheduledJobCount }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.failedAudits') }}</th>
                    <td>{{ overview.recent.failedAuditLogCount }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.onlineUsers') }}</th>
                    <td>{{ overview.recent.onlineUserCount }}</td>
                  </tr>
                  <tr>
                    <th>{{ $t('page.monitor.abnormalFiles') }}</th>
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
.bottom-grid,
.telemetry-grid {
  display: grid;
  gap: 12px;
}

.metric-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.bottom-grid {
  grid-template-columns: minmax(0, 1.2fr) minmax(280px, 360px);
}

.telemetry-grid {
  grid-template-columns: repeat(2, minmax(0, 1fr));
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

.disk-list,
.network-list {
  display: flex;
  flex-direction: column;
}

.disk-row,
.network-row {
  border-bottom: 1px solid hsl(var(--border));
  padding: 12px 14px;
}

.disk-row:last-child,
.network-row:last-child {
  border-bottom: 0;
}

.disk-heading,
.network-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.disk-heading > div,
.network-heading > div {
  min-width: 0;
}

.disk-heading strong,
.network-heading strong {
  display: block;
  font-size: 13px;
}

.disk-heading span,
.network-heading span,
.network-address,
.network-stats,
.empty-copy {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.disk-row :deep(.ant-progress) {
  margin-top: 8px;
}

.network-address {
  margin-top: 8px;
  overflow-wrap: anywhere;
}

.network-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 16px;
  margin-top: 6px;
}

.empty-copy {
  padding: 24px 14px;
  text-align: center;
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
  .bottom-grid,
  .telemetry-grid {
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
