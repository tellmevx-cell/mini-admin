<script setup lang="ts">
import SystemPlaceholderPage from '../_components/system-placeholder-page.vue';

const rows = [
  { key: 'tenant', name: '租户列表', remark: '管理每个租户的基本信息、状态和套餐。', status: '规划中' },
  { key: 'isolation', name: '数据隔离', remark: '后续进入多租户数据隔离和租户上下文。', status: '待实现' },
] as const;
</script>

<template>
  <SystemPlaceholderPage
    title="租户管理"
    description="平台级后台管理多个租户的入口。"
    :rows="rows"
  />
</template>
