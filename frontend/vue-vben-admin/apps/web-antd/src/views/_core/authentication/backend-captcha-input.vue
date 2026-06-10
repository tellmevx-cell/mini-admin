<script setup lang="ts">
const modelValue = defineModel<string>({ default: '' });

defineProps<{
  imageSrc?: string;
  loading?: boolean;
}>();

defineEmits<{
  refresh: [];
}>();
</script>

<template>
  <div class="backend-captcha">
    <input
      v-model="modelValue"
      autocomplete="off"
      class="backend-captcha-input"
      maxlength="8"
      placeholder="请输入验证码"
    />
    <button
      class="backend-captcha-image"
      :disabled="loading"
      type="button"
      @click="$emit('refresh')"
    >
      <span v-if="loading">加载中</span>
      <img v-else-if="imageSrc" alt="验证码" :src="imageSrc" />
      <span v-else>刷新</span>
    </button>
  </div>
</template>

<style scoped>
.backend-captcha {
  display: grid;
  width: 100%;
  grid-template-columns: minmax(0, 1fr) 120px;
  gap: 8px;
}

.backend-captcha-input {
  width: 100%;
  height: 40px;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--background));
  color: hsl(var(--foreground));
  outline: none;
  padding: 0 12px;
}

.backend-captcha-input:focus {
  border-color: hsl(var(--primary));
}

.backend-captcha-image {
  display: flex;
  height: 40px;
  align-items: center;
  justify-content: center;
  border: 1px solid hsl(var(--border));
  border-radius: 6px;
  background: hsl(var(--muted));
  color: hsl(var(--muted-foreground));
  cursor: pointer;
  overflow: hidden;
}

.backend-captcha-image:disabled {
  cursor: wait;
  opacity: 0.75;
}

.backend-captcha-image img {
  display: block;
  width: 120px;
  height: 40px;
}
</style>
