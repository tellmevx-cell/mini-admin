<script setup lang="ts">
import type { TableColumnsType } from 'ant-design-vue';

import { computed, onMounted, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Input,
  Modal,
  Popconfirm,
  Space,
  Table,
  Tag,
  message,
} from 'ant-design-vue';

import {
  getPlatformCacheEntriesApi,
  invalidatePlatformCacheTagsApi,
  type PlatformCacheEntry,
  removePlatformCacheEntryApi,
} from '#/api/platform/kernel';

const { hasAccessByCodes } = useAccess();
const loading = ref(false);
const clearing = ref(false);
const category = ref('');
const tagInput = ref('');
const entries = ref<PlatformCacheEntry[]>([]);

const columns: TableColumnsType = [
  { dataIndex: 'category', title: '分类', width: 140 },
  { dataIndex: 'logicalKey', title: '逻辑键', width: 280 },
  { dataIndex: 'tenantId', title: '租户', width: 210 },
  { dataIndex: 'tags', title: '标签', width: 300 },
  { dataIndex: 'lastAccessedAt', title: '最后访问', width: 180 },
  { dataIndex: 'expiresAt', title: '过期时间', width: 180 },
  { dataIndex: 'physicalKey', title: '物理键', width: 360 },
  { dataIndex: 'action', fixed: 'right', title: '操作', width: 100 },
];

const canClear = computed(() =>
  hasAccessByCodes(['platform:cache:clear']),
);

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '永不过期';
}

async function loadEntries() {
  loading.value = true;
  try {
    entries.value = await getPlatformCacheEntriesApi(category.value.trim());
  } finally {
    loading.value = false;
  }
}

async function invalidateTags() {
  const tags = tagInput.value
    .split(/[,，\s]+/)
    .map((item) => item.trim())
    .filter(Boolean);
  if (tags.length === 0) {
    message.warning('请输入至少一个缓存标签');
    return;
  }

  Modal.confirm({
    content: `将精准失效标签：${tags.join('、')}`,
    okText: '确认失效',
    title: '失效缓存标签',
    async onOk() {
      clearing.value = true;
      try {
        const result = await invalidatePlatformCacheTagsApi(tags);
        message.success(result.message);
        tagInput.value = '';
        await loadEntries();
      } finally {
        clearing.value = false;
      }
    },
  });
}

async function removeEntry(item: PlatformCacheEntry | Record<string, any>) {
  const entry = item as PlatformCacheEntry;
  const result = await removePlatformCacheEntryApi(
    entry.category,
    entry.logicalKey,
  );
  message.success(result.message);
  await loadEntries();
}

onMounted(loadEntries);
</script>

<template>
  <Page
    description="查看当前节点登记的授权、菜单、配置与字典缓存，并按标签或逻辑键精准失效。"
    title="缓存管理"
  >
    <div class="cache-shell">
      <Alert
        message="清理操作通过版本门控即时生效，不扫描 Redis 全量 Key，也不会影响其他租户的同名缓存。"
        show-icon
        type="success"
      />

      <section class="action-panel">
        <div class="action-block">
          <span class="action-label">缓存分类</span>
          <Input
            v-model:value="category"
            allow-clear
            placeholder="留空查询全部，例如 authorization"
            @press-enter="loadEntries"
          />
          <Button type="primary" @click="loadEntries">查询</Button>
        </div>
        <div v-if="canClear" class="action-block danger-zone">
          <span class="action-label">标签失效</span>
          <Input
            v-model:value="tagInput"
            placeholder="多个标签使用逗号分隔"
            @press-enter="invalidateTags"
          />
          <Button danger :loading="clearing" @click="invalidateTags">
            精准失效
          </Button>
        </div>
      </section>

      <div class="table-heading">
        <div>
          <strong>已登记缓存键</strong>
          <span>{{ entries.length }} 条</span>
        </div>
        <Button @click="loadEntries">刷新</Button>
      </div>

      <Table
        :columns="columns"
        :data-source="entries"
        :loading="loading"
        :pagination="false"
        :scroll="{ x: 1750 }"
        row-key="physicalKey"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.dataIndex === 'category'">
            <Tag color="geekblue">{{ record.category }}</Tag>
          </template>
          <template v-else-if="column.dataIndex === 'tenantId'">
            <span class="mono muted">{{ record.tenantId || '全局' }}</span>
          </template>
          <template v-else-if="column.dataIndex === 'tags'">
            <Space :size="[4, 4]" wrap>
              <Tag v-for="tag in record.tags" :key="tag">{{ tag }}</Tag>
            </Space>
          </template>
          <template v-else-if="column.dataIndex === 'lastAccessedAt'">
            {{ formatTime(record.lastAccessedAt) }}
          </template>
          <template v-else-if="column.dataIndex === 'expiresAt'">
            {{ formatTime(record.expiresAt) }}
          </template>
          <template v-else-if="column.dataIndex === 'logicalKey'">
            <span class="mono">{{ record.logicalKey }}</span>
          </template>
          <template v-else-if="column.dataIndex === 'physicalKey'">
            <span class="mono physical-key">{{ record.physicalKey }}</span>
          </template>
          <template v-else-if="column.dataIndex === 'action'">
            <Popconfirm
              v-if="canClear"
              title="确认失效这个逻辑键？"
              @confirm="removeEntry(record)"
            >
              <Button danger size="small" type="link">清理</Button>
            </Popconfirm>
          </template>
        </template>
      </Table>
    </div>
  </Page>
</template>

<style scoped>
.cache-shell {
  display: grid;
  gap: 16px;
}

.action-panel {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}

.action-block {
  display: grid;
  grid-template-columns: 88px minmax(160px, 1fr) auto;
  gap: 10px;
  align-items: center;
  padding: 18px;
  border: 1px solid hsl(var(--border));
  border-radius: 12px;
  background: hsl(var(--card));
}

.danger-zone {
  border-color: color-mix(in srgb, #ef4444 25%, hsl(var(--border)));
}

.action-label {
  color: hsl(var(--muted-foreground));
  font-size: 13px;
  font-weight: 600;
}

.table-heading {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 2px;
}

.table-heading span {
  margin-left: 10px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.mono {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 12px;
}

.muted,
.physical-key {
  color: hsl(var(--muted-foreground));
}

.physical-key {
  display: inline-block;
  max-width: 340px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 900px) {
  .action-panel {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 600px) {
  .action-block {
    grid-template-columns: 1fr;
  }
}
</style>
