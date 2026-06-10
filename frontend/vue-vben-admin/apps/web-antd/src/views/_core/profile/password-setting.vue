<script setup lang="ts">
import type { VbenFormSchema } from '#/adapter/form';

import { computed, ref } from 'vue';

import { ProfilePasswordSetting, z } from '@vben/common-ui';

import { message } from 'ant-design-vue';

import { changeCurrentPasswordApi } from '#/api';
import { useAuthStore } from '#/store';

const authStore = useAuthStore();
const submitting = ref(false);

const formSchema = computed((): VbenFormSchema[] => {
  return [
    {
      fieldName: 'oldPassword',
      label: '旧密码',
      component: 'VbenInputPassword',
      componentProps: {
        placeholder: '请输入旧密码',
      },
      rules: z.string({ required_error: '请输入旧密码' }).min(1, {
        message: '请输入旧密码',
      }),
    },
    {
      fieldName: 'newPassword',
      label: '新密码',
      component: 'VbenInputPassword',
      componentProps: {
        passwordStrength: true,
        placeholder: '请输入新密码',
      },
      rules: z.string({ required_error: '请输入新密码' }).min(6, {
        message: '新密码至少 6 位',
      }),
    },
    {
      fieldName: 'confirmPassword',
      label: '确认密码',
      component: 'VbenInputPassword',
      componentProps: {
        passwordStrength: true,
        placeholder: '请再次输入新密码',
      },
      dependencies: {
        rules(values) {
          const { newPassword } = values;
          return z
            .string({ required_error: '请再次输入新密码' })
            .min(1, { message: '请再次输入新密码' })
            .refine((value) => value === newPassword, {
              message: '两次输入的密码不一致',
            });
        },
        triggerFields: ['newPassword'],
      },
    },
  ];
});

async function handleSubmit(values: Record<string, string>) {
  submitting.value = true;
  try {
    await changeCurrentPasswordApi({
      confirmPassword: values.confirmPassword ?? '',
      newPassword: values.newPassword ?? '',
      oldPassword: values.oldPassword ?? '',
    });
    message.success('密码修改成功，请重新登录');
    await authStore.logout(false, false);
  } finally {
    submitting.value = false;
  }
}
</script>
<template>
  <ProfilePasswordSetting
    class="w-full max-w-xl"
    :form-schema="formSchema"
    :class="{ 'pointer-events-none opacity-70': submitting }"
    @submit="handleSubmit"
  />
</template>
