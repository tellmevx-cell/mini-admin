<script setup lang="ts">
import { computed } from 'vue';

import { Page } from '@vben/common-ui';

import { Table, Tag } from 'ant-design-vue';

interface ModuleRow {
  key: string;
  name: string;
  remark: string;
  status: '待实现' | '规划中' | '已接入';
}

const props = defineProps<{
  description: string;
  rows: readonly ModuleRow[];
  title: string;
}>();

const tableRows = computed(() => [...props.rows]);

const columns = [
  {
    dataIndex: 'name',
    title: '能力点',
    width: 220,
  },
  {
    dataIndex: 'remark',
    title: '说明',
  },
  {
    dataIndex: 'status',
    title: '状态',
    width: 120,
  },
];
</script>

<template>
  <Page :description="description" :title="title">
    <Table
      row-key="key"
      :columns="columns"
      :data-source="tableRows"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'status'">
          <Tag :color="record.status === '已接入' ? 'green' : 'blue'">
            {{ record.status }}
          </Tag>
        </template>
      </template>
    </Table>
  </Page>
</template>
