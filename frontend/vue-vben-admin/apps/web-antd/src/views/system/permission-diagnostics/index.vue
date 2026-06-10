<script setup lang="ts">
import type {
  PermissionDiagnosticsMenu,
  PermissionDiagnosticsResult,
  PermissionDiagnosticsRole,
} from '#/api/system/permission-diagnostics';

import { computed, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Empty,
  Input,
  message,
  Space,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  getPermissionDiagnosticsApi,
  refreshPermissionDiagnosticsCacheApi,
} from '#/api/system/permission-diagnostics';

const { hasAccessByCodes } = useAccess();
const TypographyText = Typography.Text;

const loading = ref(false);
const refreshLoading = ref(false);
const userName = ref('admin');
const diagnostics = ref<PermissionDiagnosticsResult>();

const canRefreshCache = computed(() =>
  hasAccessByCodes(['system:permission-diagnostics:refresh-cache']),
);
const menuColumns = [
  { dataIndex: 'title', title: '菜单', width: 180 },
  { dataIndex: 'path', title: '路径', width: 220 },
  { dataIndex: 'permissionCode', title: '权限码', width: 260 },
  { dataIndex: 'isVisible', title: '显示', width: 90 },
];
const roleColumns = [
  { dataIndex: 'name', title: '角色', width: 180 },
  { dataIndex: 'dataScope', title: '数据权限', width: 140 },
  { dataIndex: 'menuCount', title: '角色菜单', width: 110 },
  { dataIndex: 'visibleMenuCount', title: '可见菜单', width: 110 },
  { dataIndex: 'buttonPermissionCount', title: '按钮权限', width: 110 },
  { dataIndex: 'isEnabled', title: '状态', width: 90 },
];

async function loadDiagnostics() {
  const name = userName.value.trim();
  if (!name) {
    message.warning('请输入用户名');
    return;
  }

  loading.value = true;
  try {
    diagnostics.value = await getPermissionDiagnosticsApi(name);
  } catch {
    diagnostics.value = undefined;
    message.error('权限诊断查询失败');
  } finally {
    loading.value = false;
  }
}

async function refreshCache() {
  if (!diagnostics.value) {
    return;
  }

  refreshLoading.value = true;
  try {
    await refreshPermissionDiagnosticsCacheApi(diagnostics.value.user.userName);
    message.success('权限缓存已刷新');
    await loadDiagnostics();
  } catch {
    message.error('权限缓存刷新失败');
  } finally {
    refreshLoading.value = false;
  }
}

function getDataScopeColor(level?: string) {
  if (level === 'All') {
    return 'green';
  }

  if (level === 'Self') {
    return 'orange';
  }

  return level === 'None' ? 'red' : 'blue';
}

function getDataScopeLabel(level?: string) {
  if (level === 'All') {
    return '全部';
  }

  if (level === 'DepartmentAndChildren') {
    return '本部门及子部门';
  }

  if (level === 'Department') {
    return '本部门';
  }

  if (level === 'CustomDepartments') {
    return '自定义部门';
  }

  if (level === 'Mixed') {
    return '组合范围';
  }

  if (level === 'Self') {
    return '仅本人';
  }

  return '无权限';
}

function getRoleDataScopeLabel(dataScope: string) {
  if (dataScope === 'all') {
    return '全部';
  }

  if (dataScope === 'department-and-children') {
    return '本部门及子部门';
  }

  if (dataScope === 'department') {
    return '本部门';
  }

  if (dataScope === 'custom') {
    return '自定义部门';
  }

  if (dataScope === 'self') {
    return '仅本人';
  }

  return dataScope;
}

function getWarningAlertType(level?: string) {
  if (level === 'error') {
    return 'error';
  }

  if (level === 'warning') {
    return 'warning';
  }

  return 'info';
}

function getTenantLabel() {
  if (!diagnostics.value?.tenant.isTenant) {
    return '平台用户';
  }

  return diagnostics.value.tenant.tenantCode
    ? `${diagnostics.value.tenant.tenantName || '-'} / ${diagnostics.value.tenant.tenantCode}`
    : diagnostics.value.tenant.tenantName || '-';
}

const visibleMenus = computed<PermissionDiagnosticsMenu[]>(() =>
  diagnostics.value?.menuItems.filter((item) => item.isVisible) ?? [],
);
const buttonPermissions = computed<PermissionDiagnosticsMenu[]>(() =>
  diagnostics.value?.menuItems.filter((item) => !item.isVisible) ?? [],
);
const roleRows = computed<PermissionDiagnosticsRole[]>(
  () => diagnostics.value?.roles ?? [],
);
</script>

<template>
  <Page auto-content-height>
    <div class="diagnostics-workspace">
      <div class="query-bar">
        <Space wrap>
          <span class="query-label">用户</span>
          <Input
            v-model:value="userName"
            allow-clear
            class="query-input"
            placeholder="请输入用户名"
            @press-enter="loadDiagnostics"
          />
          <Button type="primary" :loading="loading" @click="loadDiagnostics">
            查询
          </Button>
          <Button
            v-if="canRefreshCache && diagnostics"
            :loading="refreshLoading"
            @click="refreshCache"
          >
            刷新缓存
          </Button>
        </Space>
      </div>

      <template v-if="diagnostics">
        <section class="panel chain-panel">
          <div class="section-title">
            <h3>权限链路</h3>
            <Tag :color="diagnostics.warnings.length === 0 ? 'green' : 'orange'">
              {{ diagnostics.warnings.length === 0 ? '链路正常' : `${diagnostics.warnings.length} 项提示` }}
            </Tag>
          </div>
          <div class="chain-flow">
            <div class="chain-step">
              <span>角色菜单</span>
              <strong>{{ diagnostics.effective.roleMenuCount }}</strong>
            </div>
            <div class="chain-arrow">→</div>
            <div class="chain-step">
              <span>套餐范围</span>
              <strong>
                {{
                  diagnostics.tenant.isPackageLimited
                    ? diagnostics.effective.packageMenuCount
                    : '不限'
                }}
              </strong>
            </div>
            <div class="chain-arrow">→</div>
            <div class="chain-step highlight">
              <span>最终菜单</span>
              <strong>{{ diagnostics.effective.finalMenuCount }}</strong>
            </div>
            <div class="chain-step">
              <span>按钮权限</span>
              <strong>{{ diagnostics.effective.buttonPermissionCount }}</strong>
            </div>
            <div class="chain-step">
              <span>权限码</span>
              <strong>{{ diagnostics.effective.permissionCodeCount }}</strong>
            </div>
          </div>
        </section>

        <div class="diagnosis-alerts">
          <Alert
            v-if="diagnostics.warnings.length === 0"
            message="当前权限链路未发现异常"
            show-icon
            type="success"
          />
          <template v-else>
            <Alert
              v-for="warning in diagnostics.warnings"
              :key="warning.code"
              :description="warning.suggestion"
              :message="warning.message"
              show-icon
              :type="getWarningAlertType(warning.level)"
            />
          </template>
        </div>

        <div class="summary-grid">
          <section class="panel">
            <h3>用户</h3>
            <div class="kv-grid">
              <span>账号</span>
              <strong>{{ diagnostics.user.userName }}</strong>
              <span>姓名</span>
              <strong>{{ diagnostics.user.realName }}</strong>
              <span>部门</span>
              <strong>{{ diagnostics.user.departmentName || '-' }}</strong>
              <span>岗位</span>
              <strong>{{ diagnostics.user.positionName || '-' }}</strong>
              <span>状态</span>
              <strong>
                <Tag :color="diagnostics.user.isEnabled ? 'green' : 'red'">
                  {{ diagnostics.user.isEnabled ? '启用' : '禁用' }}
                </Tag>
              </strong>
            </div>
          </section>

          <section class="panel">
            <h3>租户套餐</h3>
            <div class="kv-grid">
              <span>归属</span>
              <strong>{{ getTenantLabel() }}</strong>
              <span>套餐</span>
              <strong>{{ diagnostics.tenant.packageName || '-' }}</strong>
              <span>限制</span>
              <strong>
                <Tag :color="diagnostics.tenant.isPackageLimited ? 'blue' : 'default'">
                  {{ diagnostics.tenant.isPackageLimited ? '启用套餐' : '不限制' }}
                </Tag>
              </strong>
              <span>菜单数</span>
              <strong>{{ diagnostics.tenant.packageMenuCount }}</strong>
            </div>
          </section>

          <section class="panel">
            <h3>有效权限</h3>
            <div class="kv-grid">
              <span>最终菜单</span>
              <strong>{{ diagnostics.effective.finalMenuCount }}</strong>
              <span>可见菜单</span>
              <strong>{{ diagnostics.effective.visibleMenuCount }}</strong>
              <span>按钮</span>
              <strong>{{ diagnostics.effective.buttonPermissionCount }}</strong>
              <span>权限码</span>
              <strong>{{ diagnostics.effective.permissionCodeCount }}</strong>
            </div>
          </section>

          <section class="panel">
            <h3>数据权限</h3>
            <div class="scope-line">
              <Tag :color="getDataScopeColor(diagnostics.dataScope.level)">
                {{ getDataScopeLabel(diagnostics.dataScope.level) }}
              </Tag>
              <span>{{ diagnostics.dataScope.description }}</span>
            </div>
            <div class="scope-names">
              <template v-if="diagnostics.dataScope.departmentNames.length > 0">
                <Tag
                  v-for="departmentName in diagnostics.dataScope.departmentNames"
                  :key="departmentName"
                  color="blue"
                >
                  {{ departmentName }}
                </Tag>
              </template>
              <span v-else>未限定部门</span>
            </div>
            <div class="department-list">
              <TypographyText
                v-for="departmentId in diagnostics.dataScope.departmentIds"
                :key="departmentId"
                code
              >
                {{ departmentId }}
              </TypographyText>
              <span v-if="diagnostics.dataScope.departmentIds.length === 0">-</span>
            </div>
          </section>
        </div>

        <section class="panel cache-panel">
          <h3>缓存</h3>
          <div class="kv-grid cache-grid">
            <span>权限</span>
            <TypographyText code>{{ diagnostics.cache.permissionCodesKey }}</TypographyText>
            <span>菜单</span>
            <TypographyText code>{{ diagnostics.cache.menusKey }}</TypographyText>
            <span>令牌戳</span>
            <TypographyText code>{{ diagnostics.cache.securityStampKey }}</TypographyText>
          </div>
        </section>

        <section class="panel">
          <div class="section-title">
            <h3>角色</h3>
            <span>{{ diagnostics.roles.length }} 个</span>
          </div>
          <Table
            row-key="id"
            bordered
            size="small"
            :columns="roleColumns"
            :data-source="roleRows"
            :pagination="false"
            :scroll="{ x: 760 }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'name'">
                <strong>{{ record.name }}</strong>
                <TypographyText class="role-code" code>
                  {{ record.code }}
                </TypographyText>
                <div
                  v-if="record.customDepartmentNames && record.customDepartmentNames.length > 0"
                  class="role-scope-tags"
                >
                  <Tag
                    v-for="departmentName in record.customDepartmentNames"
                    :key="`${record.id}-${departmentName}`"
                    color="blue"
                  >
                    {{ departmentName }}
                  </Tag>
                </div>
              </template>
              <template v-if="column.dataIndex === 'dataScope'">
                {{ getRoleDataScopeLabel(record.dataScope) }}
              </template>
              <template v-if="column.dataIndex === 'isEnabled'">
                <Tag :color="record.isEnabled ? 'green' : 'default'">
                  {{ record.isEnabled ? '启用' : '停用' }}
                </Tag>
              </template>
            </template>
          </Table>
        </section>

        <section class="panel">
          <div class="section-title">
            <h3>权限码</h3>
            <span>{{ diagnostics.permissionCodes.length }} 个</span>
          </div>
          <div class="permission-code-list">
            <TypographyText
              v-for="code in diagnostics.permissionCodes"
              :key="code"
              code
            >
              {{ code }}
            </TypographyText>
          </div>
        </section>

        <section class="panel">
          <div class="section-title">
            <h3>菜单</h3>
            <span>{{ visibleMenus.length }} 个</span>
          </div>
          <Table
            row-key="id"
            bordered
            size="small"
            :columns="menuColumns"
            :data-source="visibleMenus"
            :pagination="false"
            :scroll="{ x: 760 }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'permissionCode'">
                <TypographyText v-if="record.permissionCode" code>
                  {{ record.permissionCode }}
                </TypographyText>
                <span v-else>-</span>
              </template>
              <template v-if="column.dataIndex === 'isVisible'">
                <Tag color="green">显示</Tag>
              </template>
            </template>
          </Table>
        </section>

        <section class="panel">
          <div class="section-title">
            <h3>按钮权限</h3>
            <span>{{ buttonPermissions.length }} 个</span>
          </div>
          <div class="permission-code-list">
            <TypographyText
              v-for="item in buttonPermissions"
              :key="item.id"
              code
            >
              {{ item.permissionCode || item.path }}
            </TypographyText>
          </div>
        </section>
      </template>

      <Empty v-else class="empty-state" description="暂无诊断结果" />
    </div>
  </Page>
</template>

<style scoped>
.diagnostics-workspace {
  display: flex;
  min-height: calc(100vh - 150px);
  flex-direction: column;
  gap: 10px;
}

.query-bar,
.panel {
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 12px;
}

.query-label {
  font-weight: 600;
  white-space: nowrap;
}

.query-input {
  width: 260px;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 10px;
}

.panel {
  min-width: 0;
}

.panel h3 {
  margin: 0 0 10px;
  font-size: 15px;
  font-weight: 600;
}

.kv-grid {
  display: grid;
  grid-template-columns: 72px minmax(0, 1fr);
  gap: 8px 10px;
  line-height: 1.7;
}

.kv-grid span,
.section-title span,
.scope-line span {
  color: hsl(var(--muted-foreground));
}

.kv-grid strong,
.cache-panel :deep(.ant-typography) {
  min-width: 0;
  overflow-wrap: anywhere;
  font-weight: 500;
}

.scope-line {
  display: flex;
  align-items: center;
  gap: 8px;
}

.scope-names {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.chain-panel {
  padding: 14px;
}

.chain-flow {
  display: grid;
  grid-template-columns: minmax(120px, 1fr) 24px minmax(120px, 1fr) 24px minmax(120px, 1fr) minmax(120px, 1fr) minmax(120px, 1fr);
  gap: 8px;
  align-items: stretch;
}

.chain-step {
  display: flex;
  min-height: 64px;
  flex-direction: column;
  justify-content: center;
  border: 1px solid hsl(var(--border));
  border-radius: 4px;
  padding: 10px 12px;
}

.chain-step span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.chain-step strong {
  margin-top: 4px;
  font-size: 22px;
  line-height: 1.1;
}

.chain-step.highlight {
  border-color: hsl(var(--primary) / 35%);
  background: hsl(var(--primary) / 6%);
}

.chain-arrow {
  display: flex;
  align-items: center;
  justify-content: center;
  color: hsl(var(--muted-foreground));
  font-size: 18px;
}

.diagnosis-alerts {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.department-list,
.permission-code-list {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-top: 10px;
}

.section-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.cache-grid {
  grid-template-columns: 72px minmax(0, 1fr);
}

.role-code {
  display: block;
  margin-top: 4px;
}

.role-scope-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-top: 8px;
}

.empty-state {
  flex: 1;
  justify-content: center;
  border-radius: 4px;
  background: hsl(var(--background));
}

:deep(.ant-table) {
  font-size: 13px;
}

:deep(.ant-typography) {
  max-width: 100%;
  overflow-wrap: anywhere;
  white-space: normal;
}

@media (max-width: 1200px) {
  .summary-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .chain-flow {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .chain-arrow {
    display: none;
  }
}

@media (max-width: 720px) {
  .query-input {
    width: 100%;
  }

  .summary-grid,
  .chain-flow {
    grid-template-columns: 1fr;
  }
}
</style>
