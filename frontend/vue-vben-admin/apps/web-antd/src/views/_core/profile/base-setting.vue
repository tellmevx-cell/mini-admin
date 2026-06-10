<script setup lang="ts">
import type { UserInfo } from '@vben/types';

import { onMounted, ref } from 'vue';

import { Descriptions, DescriptionsItem, Spin, Tag } from 'ant-design-vue';

import { getUserInfoApi } from '#/api';

const loading = ref(false);
const userInfo = ref<UserInfo>();

onMounted(async () => {
  loading.value = true;
  try {
    userInfo.value = await getUserInfoApi();
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <Spin :spinning="loading">
    <Descriptions bordered :column="1" size="middle">
      <DescriptionsItem label="用户名">
        {{ userInfo?.username || '-' }}
      </DescriptionsItem>
      <DescriptionsItem label="姓名">
        {{ userInfo?.realName || '-' }}
      </DescriptionsItem>
      <DescriptionsItem label="所属部门">
        {{ userInfo?.departmentName || '-' }}
      </DescriptionsItem>
      <DescriptionsItem label="所属岗位">
        {{ userInfo?.positionName || '-' }}
      </DescriptionsItem>
      <DescriptionsItem label="角色">
        <Tag v-for="role in userInfo?.roles ?? []" :key="role" color="blue">
          {{ role }}
        </Tag>
        <span v-if="!userInfo?.roles?.length">-</span>
      </DescriptionsItem>
    </Descriptions>
  </Spin>
</template>
