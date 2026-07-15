<script setup lang="ts">
import type { Dayjs } from 'dayjs';
import type { TableColumnsType } from 'ant-design-vue';

import { computed, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  DatePicker,
  Descriptions,
  DescriptionsItem,
  Divider,
  Form,
  FormItem,
  Input,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tabs,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import {
  createMyOpenApiCredentialApi,
  createOpenPlatformApplicationApi,
  deleteOpenPlatformApplicationApi,
  getMyOpenApiCredentialsApi,
  getOpenPlatformApplicationsApi,
  type OpenApiCredentialItem,
  type OpenPlatformApplicationItem,
  revokeMyOpenApiCredentialApi,
  rotateOpenPlatformApplicationSecretApi,
} from '#/api/open-platform';
import { getPlatformPagesApi } from '#/api/platform/kernel';

interface ApplicationFormState {
  allowClientCredentials: boolean;
  apiPermissions: string[];
  clientType: 'Confidential' | 'Public';
  displayName: string;
  postLogoutRedirectUris: string;
  redirectUris: string;
  scopes: string[];
}

interface CredentialFormState {
  expiresAt?: Dayjs;
  name: string;
  permissions: string[];
}

const { hasAccessByCodes } = useAccess();
const activeTab = ref('applications');
const loadingApplications = ref(false);
const loadingCredentials = ref(false);
const saving = ref(false);
const applicationModalOpen = ref(false);
const credentialModalOpen = ref(false);
const secretModalOpen = ref(false);
const secretTitle = ref('密钥已生成');
const secretIdentity = ref('');
const secretValue = ref('');
const applications = ref<OpenPlatformApplicationItem[]>([]);
const credentials = ref<OpenApiCredentialItem[]>([]);
const permissionOptions = ref<Array<{ label: string; value: string }>>([]);

const applicationForm = reactive<ApplicationFormState>({
  allowClientCredentials: true,
  apiPermissions: [],
  clientType: 'Confidential',
  displayName: '',
  postLogoutRedirectUris: '',
  redirectUris: 'https://localhost:3000/callback',
  scopes: ['openid', 'profile', 'email', 'roles', 'offline_access', 'miniadmin_api'],
});

const credentialForm = reactive<CredentialFormState>({
  expiresAt: dayjs().add(1, 'year'),
  name: '',
  permissions: [],
});

const applicationColumns: TableColumnsType = [
  { dataIndex: 'displayName', title: '应用', width: 220 },
  { dataIndex: 'clientId', title: 'Client ID', width: 300 },
  { dataIndex: 'clientType', title: '客户端类型', width: 130 },
  { dataIndex: 'redirectUris', title: '回调地址', width: 320 },
  { dataIndex: 'scopes', title: '授权范围', width: 280 },
  { dataIndex: 'apiPermissions', title: 'API 权限', width: 320 },
  { dataIndex: 'createdAt', title: '创建时间', width: 180 },
  { dataIndex: 'action', fixed: 'right', title: '操作', width: 190 },
];

const credentialColumns: TableColumnsType = [
  { dataIndex: 'name', title: '凭证名称', width: 220 },
  { dataIndex: 'appKey', title: 'AppKey', width: 320 },
  { dataIndex: 'permissions', title: '权限范围', width: 380 },
  { dataIndex: 'isEnabled', title: '状态', width: 90 },
  { dataIndex: 'expiresAt', title: '到期时间', width: 180 },
  { dataIndex: 'lastUsedAt', title: '最后调用', width: 180 },
  { dataIndex: 'action', fixed: 'right', title: '操作', width: 110 },
];

const canCreateApplication = computed(() =>
  hasAccessByCodes(['open-platform:application:create']),
);
const canRotateApplication = computed(() =>
  hasAccessByCodes(['open-platform:application:rotate-secret']),
);
const canDeleteApplication = computed(() =>
  hasAccessByCodes(['open-platform:application:delete']),
);
const canManageCredential = computed(() =>
  hasAccessByCodes(['open-platform:credential:manage']),
);

watch(
  () => applicationForm.clientType,
  (value) => {
    if (value === 'Public') applicationForm.allowClientCredentials = false;
  },
);

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function splitLines(value: string) {
  return value
    .split(/[\r\n,，]+/)
    .map((item) => item.trim())
    .filter(Boolean);
}

async function loadPermissionOptions() {
  const pages = await getPlatformPagesApi();
  permissionOptions.value = pages
    .flatMap((page) =>
      page.permissions.map((permission) => ({
        label: `${permission.title.zhCn} (${permission.code})`,
        value: permission.code,
      })),
    )
    .sort((left, right) => left.value.localeCompare(right.value));
}

async function loadApplications() {
  loadingApplications.value = true;
  try {
    applications.value = await getOpenPlatformApplicationsApi();
  } finally {
    loadingApplications.value = false;
  }
}

async function loadCredentials() {
  loadingCredentials.value = true;
  try {
    credentials.value = await getMyOpenApiCredentialsApi();
  } finally {
    loadingCredentials.value = false;
  }
}

function openApplicationModal() {
  Object.assign(applicationForm, {
    allowClientCredentials: true,
    apiPermissions: [],
    clientType: 'Confidential',
    displayName: '',
    postLogoutRedirectUris: '',
    redirectUris: 'https://localhost:3000/callback',
    scopes: ['openid', 'profile', 'email', 'roles', 'offline_access', 'miniadmin_api'],
  });
  applicationModalOpen.value = true;
}

function openCredentialModal() {
  Object.assign(credentialForm, {
    expiresAt: dayjs().add(1, 'year'),
    name: '',
    permissions: [],
  });
  credentialModalOpen.value = true;
}

function showSecret(title: string, identity: string, secret: string) {
  secretTitle.value = title;
  secretIdentity.value = identity;
  secretValue.value = secret;
  secretModalOpen.value = true;
}

async function copySecret() {
  await navigator.clipboard.writeText(secretValue.value);
  message.success('密钥已复制');
}

async function createApplication() {
  if (!applicationForm.displayName.trim()) {
    message.warning('请输入应用名称');
    return;
  }
  const redirectUris = splitLines(applicationForm.redirectUris);
  if (redirectUris.length === 0) {
    message.warning('请至少填写一个登录回调地址');
    return;
  }

  saving.value = true;
  try {
    const result = await createOpenPlatformApplicationApi({
      allowClientCredentials: applicationForm.allowClientCredentials,
      apiPermissions: applicationForm.apiPermissions,
      clientType: applicationForm.clientType,
      displayName: applicationForm.displayName.trim(),
      postLogoutRedirectUris: splitLines(applicationForm.postLogoutRedirectUris),
      redirectUris,
      scopes: applicationForm.scopes,
    });
    applicationModalOpen.value = false;
    showSecret(
      '客户端密钥仅展示一次',
      result.application.clientId,
      result.clientSecret,
    );
    await loadApplications();
  } finally {
    saving.value = false;
  }
}

async function rotateSecret(item: OpenPlatformApplicationItem | Record<string, any>) {
  const application = item as OpenPlatformApplicationItem;
  const secret = await rotateOpenPlatformApplicationSecretApi(application.id);
  showSecret('客户端密钥已轮换', application.clientId, secret);
}

async function deleteApplication(id: string) {
  await deleteOpenPlatformApplicationApi(id);
  message.success('应用及其令牌已撤销');
  await loadApplications();
}

async function createCredential() {
  if (!credentialForm.name.trim()) {
    message.warning('请输入凭证名称');
    return;
  }
  if (credentialForm.permissions.length === 0) {
    message.warning('请至少选择一个 API 权限');
    return;
  }

  saving.value = true;
  try {
    const result = await createMyOpenApiCredentialApi({
      expiresAt: credentialForm.expiresAt?.toISOString() ?? null,
      name: credentialForm.name.trim(),
      permissions: credentialForm.permissions,
    });
    credentialModalOpen.value = false;
    showSecret('AppSecret 仅展示一次', result.credential.appKey, result.appSecret);
    await loadCredentials();
  } finally {
    saving.value = false;
  }
}

async function revokeCredential(id: string) {
  await revokeMyOpenApiCredentialApi(id);
  message.success('凭证已撤销');
  await loadCredentials();
}

onMounted(async () => {
  await Promise.all([
    loadApplications(),
    loadCredentials(),
    loadPermissionOptions(),
  ]);
});
</script>

<template>
  <Page
    description="统一管理 OAuth2/OIDC 第三方客户端与个人 AppKey/AppSecret，所有授权范围都来自 PageRegistry 权限目录。"
    title="开放平台"
  >
    <div class="open-platform-shell">
      <section class="protocol-card">
        <div>
          <span class="eyebrow">OIDC DISCOVERY</span>
          <strong>/.well-known/openid-configuration</strong>
        </div>
        <Divider type="vertical" />
        <div><span>授权端点</span><code>/connect/authorize</code></div>
        <div><span>令牌端点</span><code>/connect/token</code></div>
        <div><span>用户信息</span><code>/connect/userinfo</code></div>
      </section>

      <Tabs v-model:active-key="activeTab" class="content-card">
        <Tabs.TabPane key="applications" tab="第三方应用">
          <div class="tab-toolbar">
            <Alert
              message="支持授权码 + PKCE、刷新令牌和客户端凭证模式。Public 客户端不会签发 ClientSecret。"
              show-icon
              type="info"
            />
            <Space>
              <Button @click="loadApplications">刷新</Button>
              <Button
                v-if="canCreateApplication"
                type="primary"
                @click="openApplicationModal"
              >
                注册应用
              </Button>
            </Space>
          </div>

          <Table
            :columns="applicationColumns"
            :data-source="applications"
            :loading="loadingApplications"
            :pagination="false"
            :scroll="{ x: 1800 }"
            row-key="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'displayName'">
                <strong>{{ record.displayName }}</strong>
                <div class="muted">{{ record.allowsClientCredentials ? '允许客户端凭证' : '用户委托授权' }}</div>
              </template>
              <template v-else-if="column.dataIndex === 'clientId'">
                <code>{{ record.clientId }}</code>
              </template>
              <template v-else-if="column.dataIndex === 'clientType'">
                <Tag :color="record.clientType === 'Confidential' ? 'geekblue' : 'cyan'">
                  {{ record.clientType }}
                </Tag>
              </template>
              <template v-else-if="column.dataIndex === 'redirectUris'">
                <div v-for="uri in record.redirectUris" :key="uri" class="ellipsis" :title="uri">
                  {{ uri }}
                </div>
              </template>
              <template v-else-if="column.dataIndex === 'scopes'">
                <Space :size="[4, 4]" wrap>
                  <Tag v-for="scope in record.scopes" :key="scope">{{ scope }}</Tag>
                </Space>
              </template>
              <template v-else-if="column.dataIndex === 'apiPermissions'">
                <Space :size="[4, 4]" wrap>
                  <Tag v-for="permission in record.apiPermissions" :key="permission" color="blue">
                    {{ permission }}
                  </Tag>
                  <span v-if="record.apiPermissions.length === 0" class="muted">无业务权限</span>
                </Space>
              </template>
              <template v-else-if="column.dataIndex === 'createdAt'">
                {{ formatTime(record.createdAt) }}
              </template>
              <template v-else-if="column.dataIndex === 'action'">
                <Space>
                  <Popconfirm
                    v-if="canRotateApplication && record.clientType === 'Confidential'"
                    title="旧密钥会立即失效，确认轮换？"
                    @confirm="rotateSecret(record)"
                  >
                    <Button size="small" type="link">轮换密钥</Button>
                  </Popconfirm>
                  <Popconfirm
                    v-if="canDeleteApplication"
                    title="将同时撤销该应用的全部令牌，确认删除？"
                    @confirm="deleteApplication(record.id)"
                  >
                    <Button danger size="small" type="link">删除</Button>
                  </Popconfirm>
                </Space>
              </template>
            </template>
          </Table>
        </Tabs.TabPane>

        <Tabs.TabPane key="credentials" tab="我的 OpenAPI 凭证">
          <div class="tab-toolbar">
            <Alert
              message="签名调用使用 AppKey、Unix 时间戳、Nonce 和 HMAC-SHA256；Nonce 只能使用一次。"
              show-icon
              type="warning"
            />
            <Space>
              <Button @click="loadCredentials">刷新</Button>
              <Button
                v-if="canManageCredential"
                type="primary"
                @click="openCredentialModal"
              >
                创建凭证
              </Button>
            </Space>
          </div>

          <Table
            :columns="credentialColumns"
            :data-source="credentials"
            :loading="loadingCredentials"
            :pagination="false"
            :scroll="{ x: 1450 }"
            row-key="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'appKey'">
                <code>{{ record.appKey }}</code>
              </template>
              <template v-else-if="column.dataIndex === 'permissions'">
                <Space :size="[4, 4]" wrap>
                  <Tag v-for="permission in record.permissions" :key="permission" color="blue">
                    {{ permission }}
                  </Tag>
                </Space>
              </template>
              <template v-else-if="column.dataIndex === 'isEnabled'">
                <Tag :color="record.isEnabled ? 'success' : 'default'">
                  {{ record.isEnabled ? '有效' : '已撤销' }}
                </Tag>
              </template>
              <template v-else-if="column.dataIndex === 'expiresAt'">
                {{ formatTime(record.expiresAt) }}
              </template>
              <template v-else-if="column.dataIndex === 'lastUsedAt'">
                {{ formatTime(record.lastUsedAt) }}
              </template>
              <template v-else-if="column.dataIndex === 'action'">
                <Popconfirm
                  v-if="canManageCredential && record.isEnabled"
                  title="确认撤销这个凭证？"
                  @confirm="revokeCredential(record.id)"
                >
                  <Button danger size="small" type="link">撤销</Button>
                </Popconfirm>
              </template>
            </template>
          </Table>
        </Tabs.TabPane>
      </Tabs>
    </div>

    <Modal
      v-model:open="applicationModalOpen"
      :confirm-loading="saving"
      title="注册第三方应用"
      :width="760"
      @ok="createApplication"
    >
      <Form layout="vertical">
        <div class="form-grid">
          <FormItem label="应用名称" required>
            <Input v-model:value="applicationForm.displayName" :maxlength="128" />
          </FormItem>
          <FormItem label="客户端类型" required>
            <Select
              v-model:value="applicationForm.clientType"
              :options="[
                { label: 'Confidential（服务端应用）', value: 'Confidential' },
                { label: 'Public（SPA / 移动端）', value: 'Public' },
              ]"
            />
          </FormItem>
        </div>
        <FormItem label="登录回调地址" required>
          <Textarea
            v-model:value="applicationForm.redirectUris"
            :rows="3"
            placeholder="每行一个 HTTPS 地址；localhost 可使用 HTTP"
          />
        </FormItem>
        <FormItem label="退出回调地址">
          <Textarea
            v-model:value="applicationForm.postLogoutRedirectUris"
            :rows="2"
            placeholder="每行一个地址"
          />
        </FormItem>
        <FormItem label="OIDC 授权范围">
          <Select
            v-model:value="applicationForm.scopes"
            mode="multiple"
            :options="[
              'openid',
              'profile',
              'email',
              'roles',
              'offline_access',
              'miniadmin_api',
            ].map((value) => ({ label: value, value }))"
          />
        </FormItem>
        <FormItem label="API 权限">
          <Select
            v-model:value="applicationForm.apiPermissions"
            mode="multiple"
            option-filter-prop="label"
            :options="permissionOptions"
            placeholder="按最小权限原则选择"
          />
        </FormItem>
        <FormItem v-if="applicationForm.clientType === 'Confidential'" label="客户端凭证模式">
          <Switch v-model:checked="applicationForm.allowClientCredentials" />
          <span class="switch-tip">允许应用在没有用户参与时调用已授权 API</span>
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="credentialModalOpen"
      :confirm-loading="saving"
      title="创建个人 OpenAPI 凭证"
      :width="680"
      @ok="createCredential"
    >
      <Form layout="vertical">
        <FormItem label="凭证名称" required>
          <Input v-model:value="credentialForm.name" :maxlength="128" />
        </FormItem>
        <FormItem label="到期时间">
          <DatePicker
            v-model:value="credentialForm.expiresAt"
            class="full-width"
            show-time
          />
        </FormItem>
        <FormItem label="API 权限" required>
          <Select
            v-model:value="credentialForm.permissions"
            mode="multiple"
            option-filter-prop="label"
            :options="permissionOptions"
            placeholder="凭证权限不会超过当前用户权限"
          />
        </FormItem>
      </Form>
    </Modal>

    <Modal v-model:open="secretModalOpen" :footer="null" :title="secretTitle" width="640px">
      <Alert
        message="请立即保存，关闭后无法再次查看。仓库和日志中都不要记录真实密钥。"
        show-icon
        type="warning"
      />
      <Descriptions bordered :column="1" class="secret-panel" size="small">
        <DescriptionsItem label="标识">
          <code>{{ secretIdentity }}</code>
        </DescriptionsItem>
        <DescriptionsItem label="Secret">
          <div class="secret-row">
            <code>{{ secretValue }}</code>
            <Button size="small" @click="copySecret">复制</Button>
          </div>
        </DescriptionsItem>
      </Descriptions>
    </Modal>
  </Page>
</template>

<style scoped>
.open-platform-shell {
  display: grid;
  gap: 16px;
}

.protocol-card {
  display: flex;
  align-items: center;
  gap: 24px;
  padding: 18px 22px;
  overflow-x: auto;
  border: 1px solid hsl(var(--border));
  border-radius: 14px;
  background:
    linear-gradient(120deg, color-mix(in srgb, #0ea5e9 10%, transparent), transparent 50%),
    hsl(var(--card));
}

.protocol-card > div {
  display: grid;
  gap: 4px;
  min-width: max-content;
}

.protocol-card span,
.eyebrow,
.muted,
.switch-tip {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.eyebrow {
  letter-spacing: 0.12em;
}

.content-card {
  padding: 6px 18px 18px;
  border: 1px solid hsl(var(--border));
  border-radius: 14px;
  background: hsl(var(--card));
}

.tab-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 18px;
  margin-bottom: 16px;
}

.tab-toolbar :deep(.ant-alert) {
  flex: 1;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0 18px;
}

.full-width {
  width: 100%;
}

.switch-tip {
  margin-left: 12px;
}

.ellipsis {
  max-width: 300px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

code {
  font-family: 'JetBrains Mono', 'Cascadia Code', monospace;
  font-size: 12px;
}

.secret-panel {
  margin-top: 16px;
}

.secret-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.secret-row code {
  overflow-wrap: anywhere;
}

@media (max-width: 800px) {
  .tab-toolbar {
    align-items: stretch;
    flex-direction: column;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }
}
</style>
