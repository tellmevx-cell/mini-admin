<script lang="ts" setup>
import type {
  WorkbenchProjectItem,
  WorkbenchQuickNavItem,
  WorkbenchTodoItem,
  WorkbenchTrendItem,
} from '@vben/common-ui';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { useAccess } from '@vben/access';
import {
  AnalysisChartCard,
  WorkbenchHeader,
  WorkbenchProject,
  WorkbenchQuickNav,
  WorkbenchTodo,
  WorkbenchTrends,
} from '@vben/common-ui';
import { preferences } from '@vben/preferences';
import { useUserStore } from '@vben/stores';
import { openWindow } from '@vben/utils';

import { Button, Progress, Skeleton, Tag } from 'ant-design-vue';

import {
  getCurrentTenantResourceUsageApi,
  type TenantQuotaStatus,
  type TenantResourceMetric,
  type TenantResourceUsage,
} from '#/api/tenant/resource-usage';

import AnalyticsVisitsSource from '../analytics/analytics-visits-source.vue';

const userStore = useUserStore();
const { hasAccessByCodes } = useAccess();
const quotaLoading = ref(true);
const resourceUsage = ref<null | TenantResourceUsage>(null);
const quotaMetrics = computed(() =>
  resourceUsage.value
    ? [resourceUsage.value.users, resourceUsage.value.storage]
    : [],
);
const canManageUsers = computed(() =>
  hasAccessByCodes(['system:user:query']),
);
const canManageFiles = computed(() =>
  hasAccessByCodes(['system:file:query']),
);

// 这是一个示例数据，实际项目中需要根据实际情况进行调整
// url 也可以是内部路由，在 navTo 方法中识别处理，进行内部跳转
// 例如：url: /dashboard/workspace
const projectItems: WorkbenchProjectItem[] = [
  {
    color: '',
    content: '不要等待机会，而要创造机会。',
    date: '2021-04-01',
    group: '开源组',
    icon: 'carbon:logo-github',
    title: 'Github',
    url: 'https://github.com',
  },
  {
    color: '#3fb27f',
    content: '现在的你决定将来的你。',
    date: '2021-04-01',
    group: '算法组',
    icon: 'ion:logo-vue',
    title: 'Vue',
    url: 'https://vuejs.org',
  },
  {
    color: '#e18525',
    content: '没有什么才能比努力更重要。',
    date: '2021-04-01',
    group: '上班摸鱼',
    icon: 'ion:logo-html5',
    title: 'Html5',
    url: 'https://developer.mozilla.org/zh-CN/docs/Web/HTML',
  },
  {
    color: '#bf0c2c',
    content: '热情和欲望可以突破一切难关。',
    date: '2021-04-01',
    group: 'UI',
    icon: 'ion:logo-angular',
    title: 'Angular',
    url: 'https://angular.io',
  },
  {
    color: '#00d8ff',
    content: '健康的身体是实现目标的基石。',
    date: '2021-04-01',
    group: '技术牛',
    icon: 'bx:bxl-react',
    title: 'React',
    url: 'https://reactjs.org',
  },
  {
    color: '#EBD94E',
    content: '路是走出来的，而不是空想出来的。',
    date: '2021-04-01',
    group: '架构组',
    icon: 'ion:logo-javascript',
    title: 'Js',
    url: 'https://developer.mozilla.org/zh-CN/docs/Web/JavaScript',
  },
];

// 同样，这里的 url 也可以使用以 http 开头的外部链接
const quickNavItems: WorkbenchQuickNavItem[] = [
  {
    color: '#1fdaca',
    icon: 'ion:home-outline',
    title: '首页',
    url: '/',
  },
  {
    color: '#bf0c2c',
    icon: 'ion:grid-outline',
    title: '仪表盘',
    url: '/dashboard',
  },
  {
    color: '#e18525',
    icon: 'ion:layers-outline',
    title: '组件',
    url: '/demos/features/icons',
  },
  {
    color: '#3fb27f',
    icon: 'ion:settings-outline',
    title: '系统管理',
    url: '/demos/features/login-expired', // 这里的 URL 是示例，实际项目中需要根据实际情况进行调整
  },
  {
    color: '#4daf1bc9',
    icon: 'ion:key-outline',
    title: '权限管理',
    url: '/demos/access/page-control',
  },
  {
    color: '#00d8ff',
    icon: 'ion:bar-chart-outline',
    title: '图表',
    url: '/analytics',
  },
];

const todoItems = ref<WorkbenchTodoItem[]>([
  {
    completed: false,
    content: `审查最近提交到Git仓库的前端代码，确保代码质量和规范。`,
    date: '2024-07-30 11:00:00',
    title: '审查前端代码提交',
  },
  {
    completed: true,
    content: `检查并优化系统性能，降低CPU使用率。`,
    date: '2024-07-30 11:00:00',
    title: '系统性能优化',
  },
  {
    completed: false,
    content: `进行系统安全检查，确保没有安全漏洞或未授权的访问。 `,
    date: '2024-07-30 11:00:00',
    title: '安全检查',
  },
  {
    completed: false,
    content: `更新项目中的所有npm依赖包，确保使用最新版本。`,
    date: '2024-07-30 11:00:00',
    title: '更新项目依赖',
  },
  {
    completed: false,
    content: `修复用户报告的页面UI显示问题，确保在不同浏览器中显示一致。 `,
    date: '2024-07-30 11:00:00',
    title: '修复UI显示问题',
  },
]);
const trendItems: WorkbenchTrendItem[] = [
  {
    avatar: 'svg:avatar-1',
    content: `在 <a>开源组</a> 创建了项目 <a>Vue</a>`,
    date: '刚刚',
    title: '威廉',
  },
  {
    avatar: 'svg:avatar-2',
    content: `关注了 <a>威廉</a> `,
    date: '1个小时前',
    title: '艾文',
  },
  {
    avatar: 'svg:avatar-3',
    content: `发布了 <a>个人动态</a> `,
    date: '1天前',
    title: '克里斯',
  },
  {
    avatar: 'svg:avatar-4',
    content: `发表文章 <a>如何编写一个Vite插件</a> `,
    date: '2天前',
    title: 'Vben',
  },
  {
    avatar: 'svg:avatar-1',
    content: `回复了 <a>杰克</a> 的问题 <a>如何进行项目优化？</a>`,
    date: '3天前',
    title: '皮特',
  },
  {
    avatar: 'svg:avatar-2',
    content: `关闭了问题 <a>如何运行项目</a> `,
    date: '1周前',
    title: '杰克',
  },
  {
    avatar: 'svg:avatar-3',
    content: `发布了 <a>个人动态</a> `,
    date: '1周前',
    title: '威廉',
  },
  {
    avatar: 'svg:avatar-4',
    content: `推送了代码到 <a>Github</a>`,
    date: '2021-04-01 20:00',
    title: '威廉',
  },
  {
    avatar: 'svg:avatar-4',
    content: `发表文章 <a>如何编写使用 Admin Vben</a> `,
    date: '2021-03-01 20:00',
    title: 'Vben',
  },
];

const router = useRouter();

// 这是一个示例方法，实际项目中需要根据实际情况进行调整
// This is a sample method, adjust according to the actual project requirements
function navTo(nav: WorkbenchProjectItem | WorkbenchQuickNavItem) {
  if (nav.url?.startsWith('http')) {
    openWindow(nav.url);
    return;
  }
  if (nav.url?.startsWith('/')) {
    router.push(nav.url).catch((error) => {
      console.error('Navigation failed:', error);
    });
  } else {
    console.warn(`Unknown URL for navigation item: ${nav.title} -> ${nav.url}`);
  }
}

async function loadResourceUsage() {
  quotaLoading.value = true;
  try {
    resourceUsage.value = await getCurrentTenantResourceUsageApi();
  } finally {
    quotaLoading.value = false;
  }
}

function quotaLabel(status: TenantQuotaStatus) {
  return {
    Exhausted: '配额已耗尽',
    Normal: '用量正常',
    Unlimited: '无限制',
    Warning: '接近上限',
  }[status];
}

function quotaColor(status: TenantQuotaStatus) {
  return {
    Exhausted: '#ef4444',
    Normal: '#16a34a',
    Unlimited: '#2563eb',
    Warning: '#d97706',
  }[status];
}

function quotaTagColor(status: TenantQuotaStatus) {
  return {
    Exhausted: 'red',
    Normal: 'green',
    Unlimited: 'blue',
    Warning: 'gold',
  }[status];
}

function progressPercent(metric: TenantResourceMetric) {
  return metric.limitValue > 0
    ? Math.min(Math.round(metric.usagePercent), 100)
    : 100;
}

function formatResourceValue(metric: TenantResourceMetric, value: number) {
  if (metric.resourceType === 'Users') {
    return `${value} 个`;
  }

  if (value >= 1024 * 1024 * 1024) {
    return `${(value / (1024 * 1024 * 1024)).toFixed(2)} GB`;
  }

  return `${(value / (1024 * 1024)).toFixed(2)} MB`;
}

function formatCheckedAt(value: string) {
  return new Date(value).toLocaleString();
}

function canManage(metric: TenantResourceMetric) {
  return metric.resourceType === 'Users'
    ? canManageUsers.value
    : canManageFiles.value;
}

function openResourceManagement(metric: TenantResourceMetric) {
  void router.push(metric.managementPath);
}

onMounted(() => {
  void loadResourceUsage();
});
</script>

<template>
  <div class="p-5">
    <WorkbenchHeader
      :avatar="userStore.userInfo?.avatar || preferences.app.defaultAvatar"
    >
      <template #title>
        早安, {{ userStore.userInfo?.realName }}, 开始您一天的工作吧！
      </template>
      <template #description> 今日晴，20℃ - 32℃！ </template>
    </WorkbenchHeader>

    <section
      v-if="quotaLoading || resourceUsage"
      class="quota-overview mt-5"
    >
      <Skeleton v-if="quotaLoading" active :paragraph="{ rows: 3 }" />
      <template v-else-if="resourceUsage">
        <header class="quota-overview__header">
          <div>
            <div class="quota-overview__eyebrow">租户资源用量</div>
            <h2>{{ resourceUsage.tenantName }}</h2>
            <p>
              当前套餐：{{ resourceUsage.packageName || '未分配套餐' }} ·
              更新时间：{{ formatCheckedAt(resourceUsage.checkedAt) }}
            </p>
          </div>
          <div class="quota-overview__summary">
            <span>整体状态</span>
            <Tag :color="quotaTagColor(resourceUsage.overallStatus)">
              {{ quotaLabel(resourceUsage.overallStatus) }}
            </Tag>
          </div>
        </header>

        <div class="quota-grid">
          <article
            v-for="metric in quotaMetrics"
            :key="metric.resourceType"
            class="quota-card"
            :class="`quota-card--${metric.status.toLowerCase()}`"
          >
            <div class="quota-card__topline">
              <div>
                <span class="quota-card__name">{{ metric.displayName }}</span>
                <strong>
                  {{ formatResourceValue(metric, metric.usedValue) }}
                </strong>
              </div>
              <Tag :color="quotaTagColor(metric.status)">
                {{ quotaLabel(metric.status) }}
              </Tag>
            </div>
            <Progress
              :percent="progressPercent(metric)"
              :show-info="false"
              :stroke-color="quotaColor(metric.status)"
              :trail-color="'rgba(148, 163, 184, 0.18)'"
            />
            <div class="quota-card__footer">
              <div>
                <span>配额上限</span>
                <b>
                  {{
                    metric.limitValue > 0
                      ? formatResourceValue(metric, metric.limitValue)
                      : '无限制'
                  }}
                </b>
              </div>
              <div v-if="metric.limitValue > 0">
                <span>使用比例</span>
                <b>{{ metric.usagePercent.toFixed(2) }}%</b>
              </div>
              <Button
                v-if="canManage(metric)"
                size="small"
                @click="openResourceManagement(metric)"
              >
                去管理
              </Button>
            </div>
          </article>
        </div>
      </template>
    </section>

    <div class="mt-5 flex flex-col lg:flex-row">
      <div class="mr-4 w-full lg:w-3/5">
        <WorkbenchProject :items="projectItems" title="项目" @click="navTo" />
        <WorkbenchTrends :items="trendItems" class="mt-5" title="最新动态" />
      </div>
      <div class="w-full lg:w-2/5">
        <WorkbenchQuickNav
          :items="quickNavItems"
          class="mt-5 lg:mt-0"
          title="快捷导航"
          @click="navTo"
        />
        <WorkbenchTodo :items="todoItems" class="mt-5" title="待办事项" />
        <AnalysisChartCard class="mt-5" title="访问来源">
          <AnalyticsVisitsSource />
        </AnalysisChartCard>
      </div>
    </div>
  </div>
</template>

<style scoped>
.quota-overview {
  position: relative;
  overflow: hidden;
  padding: 22px;
  border: 1px solid hsl(var(--border));
  border-radius: 14px;
  background:
    radial-gradient(circle at 92% -20%, rgb(37 99 235 / 16%), transparent 34%),
    linear-gradient(135deg, hsl(var(--card)), hsl(var(--card)) 72%, rgb(15 118 110 / 5%));
  box-shadow: 0 12px 36px rgb(15 23 42 / 6%);
}

.quota-overview__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  margin-bottom: 18px;
}

.quota-overview__eyebrow {
  margin-bottom: 5px;
  color: #2563eb;
  font-size: 12px;
  font-weight: 700;
  letter-spacing: 0.14em;
}

.quota-overview h2 {
  margin: 0;
  font-size: 22px;
  font-weight: 720;
}

.quota-overview p {
  margin: 5px 0 0;
  color: hsl(var(--muted-foreground));
  font-size: 13px;
}

.quota-overview__summary {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border: 1px solid hsl(var(--border));
  border-radius: 10px;
  background: hsl(var(--background) / 70%);
  font-size: 12px;
}

.quota-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 14px;
}

.quota-card {
  padding: 17px 18px;
  border: 1px solid hsl(var(--border));
  border-left: 4px solid #16a34a;
  border-radius: 11px;
  background: hsl(var(--background) / 78%);
}

.quota-card--warning {
  border-left-color: #d97706;
}

.quota-card--exhausted {
  border-left-color: #ef4444;
}

.quota-card--unlimited {
  border-left-color: #2563eb;
}

.quota-card__topline,
.quota-card__footer {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.quota-card__topline {
  margin-bottom: 13px;
}

.quota-card__topline > div {
  display: grid;
  gap: 3px;
}

.quota-card__name,
.quota-card__footer span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.quota-card__topline strong {
  font-size: 20px;
  font-variant-numeric: tabular-nums;
}

.quota-card__footer {
  align-items: flex-end;
  margin-top: 9px;
}

.quota-card__footer > div {
  display: grid;
  gap: 1px;
}

.quota-card__footer b {
  font-size: 12px;
  font-variant-numeric: tabular-nums;
}

@media (max-width: 768px) {
  .quota-overview {
    padding: 16px;
  }

  .quota-overview__header {
    flex-direction: column;
  }

  .quota-grid {
    grid-template-columns: 1fr;
  }
}
</style>
