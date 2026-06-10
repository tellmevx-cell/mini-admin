<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Spin,
  Switch,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';

import {
  buildProjectRuntimeServiceApi,
  createProjectRuntimeProjectApi,
  deleteProjectRuntimeProjectApi,
  getProjectRuntimeOverviewApi,
  getProjectRuntimeServiceBuildLogsApi,
  getProjectRuntimeServiceLogsApi,
  openProjectRuntimeServiceArtifactApi,
  restartProjectRuntimeServiceApi,
  startProjectRuntimeServiceApi,
  startProjectRuntimeWorkspaceApi,
  stopProjectRuntimeServiceApi,
  stopProjectRuntimeWorkspaceApi,
  updateProjectRuntimeProjectApi,
  type ProjectRuntimeLog,
  type ProjectRuntimeOverview,
  type ProjectRuntimeProject,
  type ProjectRuntimeService,
  type ProjectRuntimeWorkspace,
  type SaveProjectRuntimeProjectRequest,
  type SaveProjectRuntimeServiceRequest,
  type SaveProjectRuntimeWorkspaceRequest,
} from '#/api/system/project-runtime';

const { hasAccessByCodes } = useAccess();

const loading = ref(false);
const actionLoading = ref('');
const saving = ref(false);
const logLoading = ref(false);
const overview = ref<ProjectRuntimeOverview>();
const selectedProjectId = ref('');
const selectedWorkspaceId = ref('');
const selectedServiceId = ref('');
const projectModalOpen = ref(false);
const serviceModalOpen = ref(false);
const serviceLog = ref<ProjectRuntimeLog>();
const autoRefreshLogs = ref(true);
const logMode = ref<'build' | 'run'>('run');
const logViewRef = ref<HTMLElement>();
const logRefreshTimer = ref<ReturnType<typeof setInterval>>();
const serviceSaving = ref(false);
const editingServiceId = ref('');

const projectForm = reactive({
  code: '',
  description: '',
  isEnabled: true,
  name: '',
  order: 10,
  repositoryUrl: '',
  rootPath: '',
});

interface ServiceFormState {
  arguments: string;
  buildArguments: string;
  buildArtifactPath: string;
  buildCommand: string;
  buildLogFileName: string;
  buildLogPath: string;
  buildWorkingDirectory: string;
  command: string;
  healthUrl: string;
  isEnabled: boolean;
  logFileName: string;
  logPath: string;
  name: string;
  order: number;
  port?: number;
  serviceType: string;
  url: string;
  workingDirectory: string;
}

const serviceForm = reactive<ServiceFormState>({
  arguments: '',
  buildArguments: '',
  buildArtifactPath: '',
  buildCommand: '',
  buildLogFileName: '',
  buildLogPath: '',
  buildWorkingDirectory: '.',
  command: '',
  healthUrl: '',
  isEnabled: true,
  logFileName: '',
  logPath: '',
  name: '',
  order: 10,
  port: undefined,
  serviceType: 'Custom',
  url: '',
  workingDirectory: '.',
});

const serviceTypeOptions = [
  { label: '.NET API', value: 'Api' },
  { label: '.NET 通用', value: 'DotNet' },
  { label: 'Vue / Vben', value: 'Vue' },
  { label: 'React', value: 'React' },
  { label: 'uniapp', value: 'UniApp' },
  { label: '前端通用', value: 'Web' },
  { label: '自定义', value: 'Custom' },
];

const projects = computed(() => overview.value?.projects ?? []);
const selectedProject = computed<ProjectRuntimeProject | undefined>(() =>
  projects.value.find((item) => item.id === selectedProjectId.value),
);
const workspaces = computed(() => selectedProject.value?.workspaces ?? []);
const selectedWorkspace = computed<ProjectRuntimeWorkspace | undefined>(() =>
  workspaces.value.find((item) => item.id === selectedWorkspaceId.value),
);
const services = computed(() => selectedWorkspace.value?.services ?? []);
const selectedService = computed<ProjectRuntimeService | undefined>(() =>
  services.value.find((item) => item.id === selectedServiceId.value),
);
const canManage = computed(() =>
  hasAccessByCodes(['system:project-runtime:manage']),
);
const canViewLog = computed(() =>
  hasAccessByCodes(['system:project-runtime:log']),
);
const summaryCards = computed(() => {
  const summary = overview.value?.summary;
  return [
    { label: '项目', value: summary?.projectCount ?? 0 },
    { label: '工作区', value: summary?.workspaceCount ?? 0 },
    { label: '服务', value: summary?.serviceCount ?? 0 },
    { label: '运行中', value: summary?.runningServiceCount ?? 0 },
  ];
});
const logCommandText = computed(() => {
  const service = selectedService.value;
  if (!service) {
    return '-';
  }

  return logMode.value === 'build'
    ? `${service.buildCommand || '-'} ${service.buildArguments || ''}`.trim()
    : `${service.command} ${service.arguments}`.trim();
});
const logCheckedAt = computed(() =>
  logMode.value === 'build'
    ? selectedService.value?.buildState.checkedAt
    : selectedService.value?.state.checkedAt,
);
const logPlaceholder = computed(() =>
  logMode.value === 'build'
    ? '暂无打包日志。点击服务行里的“打包”后，这里会显示构建输出。'
    : '暂无运行日志。若服务是外部启动，请确认该服务配置了 logPath，例如 backend-dev.log。',
);
const logPathText = computed(() => {
  const service = selectedService.value;
  if (!service) {
    return '-';
  }

  return serviceLog.value?.logPath || (logMode.value === 'build' ? service.buildLogPath : service.logPath) || '-';
});
const shouldRefreshBuildState = computed(
  () =>
    logMode.value === 'build' &&
    selectedService.value?.buildState.status === 'Running',
);
const selectedLatestBuild = computed(() => selectedService.value?.latestBuild);
const selectedArtifact = computed(() => selectedService.value?.artifact);
const serviceModalTitle = computed(() =>
  editingServiceId.value ? '配置服务' : '新增服务',
);

async function loadOverview(keepSelection = true, silent = false) {
  if (!silent) {
    loading.value = true;
  }
  try {
    overview.value = await getProjectRuntimeOverviewApi();
    syncSelection(keepSelection);
  } finally {
    if (!silent) {
      loading.value = false;
    }
  }
}

function syncSelection(keepSelection: boolean) {
  const firstProject = projects.value[0];
  if (
    !keepSelection ||
    !projects.value.some((item) => item.id === selectedProjectId.value)
  ) {
    selectedProjectId.value = firstProject?.id ?? '';
  }

  const firstWorkspace = workspaces.value[0];
  if (
    !keepSelection ||
    !workspaces.value.some((item) => item.id === selectedWorkspaceId.value)
  ) {
    selectedWorkspaceId.value = firstWorkspace?.id ?? '';
  }

  const firstService = services.value[0];
  if (
    !keepSelection ||
    !services.value.some((item) => item.id === selectedServiceId.value)
  ) {
    selectedServiceId.value = firstService?.id ?? '';
  }
}

function selectProject(project: ProjectRuntimeProject) {
  selectedProjectId.value = project.id;
  selectedWorkspaceId.value = project.workspaces[0]?.id ?? '';
  selectedServiceId.value = project.workspaces[0]?.services[0]?.id ?? '';
  serviceLog.value = undefined;
  void loadServiceLog();
}

function selectWorkspace(workspace: ProjectRuntimeWorkspace) {
  selectedWorkspaceId.value = workspace.id;
  selectedServiceId.value = workspace.services[0]?.id ?? '';
  serviceLog.value = undefined;
  void loadServiceLog();
}

async function runServiceAction(
  service: ProjectRuntimeService,
  action: 'restart' | 'start' | 'stop',
) {
  actionLoading.value = `${action}:${service.id}`;
  try {
    const result =
      action === 'start'
        ? await startProjectRuntimeServiceApi(service.id)
        : action === 'stop'
          ? await stopProjectRuntimeServiceApi(service.id)
          : await restartProjectRuntimeServiceApi(service.id);
    message.success(result.message);
    await loadOverview();
    if (selectedServiceId.value === service.id && canViewLog.value) {
      await loadServiceLog(service.id);
    }
  } finally {
    actionLoading.value = '';
  }
}

async function runBuildAction(service: ProjectRuntimeService) {
  actionLoading.value = `build:${service.id}`;
  selectedServiceId.value = service.id;
  logMode.value = 'build';
  autoRefreshLogs.value = true;
  try {
    const result = await buildProjectRuntimeServiceApi(service.id);
    message.success(result.message);
    await loadOverview();
    await loadServiceLog(service.id);
  } finally {
    actionLoading.value = '';
  }
}

async function openArtifact(service: ProjectRuntimeService) {
  actionLoading.value = `artifact:${service.id}`;
  try {
    const result = await openProjectRuntimeServiceArtifactApi(service.id);
    if (result.status === 'Succeeded') {
      message.success(result.message);
    } else {
      message.warning(result.message);
    }
  } finally {
    actionLoading.value = '';
  }
}

async function runWorkspaceAction(
  workspace: ProjectRuntimeWorkspace,
  action: 'start' | 'stop',
) {
  actionLoading.value = `${action}:${workspace.id}`;
  try {
    const results =
      action === 'start'
        ? await startProjectRuntimeWorkspaceApi(workspace.id)
        : await stopProjectRuntimeWorkspaceApi(workspace.id);
    message.success(`已处理 ${results.length} 个服务`);
    await loadOverview();
  } finally {
    actionLoading.value = '';
  }
}

async function loadServiceLog(serviceId = selectedServiceId.value, silent = false) {
  if (!serviceId || !canViewLog.value) {
    return;
  }

  if (logLoading.value && silent) {
    return;
  }

  if (!silent) {
    logLoading.value = true;
  }
  try {
    serviceLog.value =
      logMode.value === 'build'
        ? await getProjectRuntimeServiceBuildLogsApi(serviceId, 400)
        : await getProjectRuntimeServiceLogsApi(serviceId, 400);
    if (autoRefreshLogs.value) {
      scrollLogToBottom();
    }
    if (shouldRefreshBuildState.value) {
      await loadOverview(true, true);
    }
  } finally {
    if (!silent) {
      logLoading.value = false;
    }
  }
}

function switchLogMode(mode: 'build' | 'run') {
  if (logMode.value === mode) {
    return;
  }

  logMode.value = mode;
  serviceLog.value = undefined;
  void loadServiceLog();
}

function scrollLogToBottom() {
  void nextTick(() => {
    const element = logViewRef.value;
    if (element) {
      element.scrollTop = element.scrollHeight;
    }
  });
}

function startLogRefreshTimer() {
  stopLogRefreshTimer();
  logRefreshTimer.value = setInterval(() => {
    if (autoRefreshLogs.value && selectedServiceId.value) {
      void loadServiceLog(selectedServiceId.value, true);
    }
  }, 2000);
}

function stopLogRefreshTimer() {
  if (logRefreshTimer.value) {
    clearInterval(logRefreshTimer.value);
    logRefreshTimer.value = undefined;
  }
}

function openProjectModal() {
  projectForm.name = '';
  projectForm.code = '';
  projectForm.rootPath = '';
  projectForm.repositoryUrl = '';
  projectForm.description = '';
  projectForm.isEnabled = true;
  projectForm.order = projects.value.length + 1;
  projectModalOpen.value = true;
}

function resetServiceForm() {
  serviceForm.name = '';
  serviceForm.serviceType = 'Custom';
  serviceForm.command = '';
  serviceForm.arguments = '';
  serviceForm.workingDirectory = '.';
  serviceForm.port = undefined;
  serviceForm.healthUrl = '';
  serviceForm.url = '';
  serviceForm.logFileName = '';
  serviceForm.logPath = '';
  serviceForm.buildCommand = '';
  serviceForm.buildArguments = '';
  serviceForm.buildWorkingDirectory = '.';
  serviceForm.buildLogFileName = '';
  serviceForm.buildLogPath = '';
  serviceForm.buildArtifactPath = '';
  serviceForm.isEnabled = true;
  serviceForm.order = services.value.length + 1;
}

function openCreateServiceModal() {
  if (!selectedWorkspace.value) {
    message.warning('请先选择工作区');
    return;
  }

  editingServiceId.value = '';
  resetServiceForm();
  serviceModalOpen.value = true;
}

function openEditServiceModal(service: ProjectRuntimeService) {
  editingServiceId.value = service.id;
  serviceForm.name = service.name;
  serviceForm.serviceType = service.serviceType || 'Custom';
  serviceForm.command = service.command;
  serviceForm.arguments = service.arguments;
  serviceForm.workingDirectory = service.workingDirectory || '.';
  serviceForm.port = service.port ?? undefined;
  serviceForm.healthUrl = service.healthUrl ?? '';
  serviceForm.url = service.url ?? '';
  serviceForm.logFileName = service.logFileName ?? '';
  serviceForm.logPath = service.logPath ?? '';
  serviceForm.buildCommand = service.buildCommand ?? '';
  serviceForm.buildArguments = service.buildArguments ?? '';
  serviceForm.buildWorkingDirectory = service.buildWorkingDirectory ?? '.';
  serviceForm.buildLogFileName = service.buildLogFileName ?? '';
  serviceForm.buildLogPath = service.buildLogPath ?? '';
  serviceForm.buildArtifactPath = service.buildArtifactPath ?? '';
  serviceForm.isEnabled = service.isEnabled;
  serviceForm.order = service.order;
  serviceModalOpen.value = true;
}

function applyServiceTemplate(value?: unknown) {
  const type = String(value ?? serviceForm.serviceType);
  serviceForm.serviceType = type;

  if (type === 'Api' || type === 'DotNet') {
    serviceForm.command = 'dotnet';
    serviceForm.arguments = type === 'Api' ? 'run' : 'run';
    serviceForm.workingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildCommand = 'dotnet';
    serviceForm.buildArguments = 'publish -c Release';
    serviceForm.buildWorkingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildArtifactPath = 'bin/Release';
  } else if (type === 'Vue') {
    serviceForm.command = 'pnpm';
    serviceForm.arguments = 'run dev:antd';
    serviceForm.workingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildCommand = 'pnpm';
    serviceForm.buildArguments = 'run build:antd';
    serviceForm.buildWorkingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildArtifactPath = 'dist';
  } else if (type === 'React' || type === 'Web') {
    serviceForm.command = 'pnpm';
    serviceForm.arguments = 'run dev';
    serviceForm.workingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildCommand = 'pnpm';
    serviceForm.buildArguments = 'run build';
    serviceForm.buildWorkingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildArtifactPath = 'dist';
  } else if (type === 'UniApp') {
    serviceForm.command = 'pnpm';
    serviceForm.arguments = 'run dev:h5';
    serviceForm.workingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildCommand = 'pnpm';
    serviceForm.buildArguments = 'run build:h5';
    serviceForm.buildWorkingDirectory = serviceForm.workingDirectory || '.';
    serviceForm.buildArtifactPath = 'dist';
  }

  const logPrefix = serviceForm.name.trim()
    ? serviceForm.name.trim().toLowerCase().replaceAll(' ', '-')
    : 'service';
  serviceForm.logFileName ||= `${logPrefix}.log`;
  serviceForm.buildLogFileName ||= `${logPrefix}-build.log`;
}

function normalizeOptionalText(value?: null | string) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : undefined;
}

function toSaveService(
  service: ProjectRuntimeService,
): SaveProjectRuntimeServiceRequest {
  return {
    arguments: service.arguments,
    buildArguments: normalizeOptionalText(service.buildArguments),
    buildArtifactPath: normalizeOptionalText(service.buildArtifactPath),
    buildCommand: normalizeOptionalText(service.buildCommand),
    buildLogFileName: normalizeOptionalText(service.buildLogFileName),
    buildLogPath: normalizeOptionalText(service.buildLogPath),
    buildWorkingDirectory: normalizeOptionalText(service.buildWorkingDirectory),
    command: service.command,
    healthUrl: normalizeOptionalText(service.healthUrl),
    id: service.id,
    isEnabled: service.isEnabled,
    logFileName: normalizeOptionalText(service.logFileName),
    logPath: normalizeOptionalText(service.logPath),
    name: service.name,
    order: service.order,
    port: service.port ?? undefined,
    serviceType: service.serviceType,
    url: normalizeOptionalText(service.url),
    workingDirectory: service.workingDirectory,
  };
}

function toSaveServiceFromForm(): SaveProjectRuntimeServiceRequest {
  return {
    arguments: serviceForm.arguments.trim(),
    buildArguments: normalizeOptionalText(serviceForm.buildArguments),
    buildArtifactPath: normalizeOptionalText(serviceForm.buildArtifactPath),
    buildCommand: normalizeOptionalText(serviceForm.buildCommand),
    buildLogFileName: normalizeOptionalText(serviceForm.buildLogFileName),
    buildLogPath: normalizeOptionalText(serviceForm.buildLogPath),
    buildWorkingDirectory: normalizeOptionalText(serviceForm.buildWorkingDirectory),
    command: serviceForm.command.trim(),
    healthUrl: normalizeOptionalText(serviceForm.healthUrl),
    id: editingServiceId.value || undefined,
    isEnabled: serviceForm.isEnabled,
    logFileName: normalizeOptionalText(serviceForm.logFileName),
    logPath: normalizeOptionalText(serviceForm.logPath),
    name: serviceForm.name.trim(),
    order: serviceForm.order,
    port: serviceForm.port ?? undefined,
    serviceType: serviceForm.serviceType,
    url: normalizeOptionalText(serviceForm.url),
    workingDirectory: serviceForm.workingDirectory.trim() || '.',
  };
}

function toSaveWorkspace(
  workspace: ProjectRuntimeWorkspace,
  serviceList?: SaveProjectRuntimeServiceRequest[],
): SaveProjectRuntimeWorkspaceRequest {
  return {
    branchName: normalizeOptionalText(workspace.branchName),
    id: workspace.id,
    isDefault: workspace.isDefault,
    isEnabled: workspace.isEnabled,
    name: workspace.name,
    order: workspace.order,
    path: workspace.path,
    profileName: normalizeOptionalText(workspace.profileName),
    services: serviceList ?? workspace.services.map(toSaveService),
  };
}

function buildProjectSaveRequest(
  project: ProjectRuntimeProject,
  workspaceId: string,
  serviceList: SaveProjectRuntimeServiceRequest[],
): SaveProjectRuntimeProjectRequest {
  return {
    code: project.code,
    description: normalizeOptionalText(project.description),
    isEnabled: project.isEnabled,
    name: project.name,
    order: project.order,
    repositoryUrl: normalizeOptionalText(project.repositoryUrl),
    rootPath: project.rootPath,
    workspaces: project.workspaces.map((workspace) =>
      toSaveWorkspace(
        workspace,
        workspace.id === workspaceId ? serviceList : undefined,
      ),
    ),
  };
}

async function saveServiceConfig() {
  const project = selectedProject.value;
  const workspace = selectedWorkspace.value;
  if (!project || !workspace) {
    message.warning('请先选择项目和工作区');
    return;
  }

  if (!serviceForm.name.trim()) {
    message.warning('请填写服务名称');
    return;
  }

  serviceSaving.value = true;
  try {
    const currentServices = workspace.services.map(toSaveService);
    const nextService = toSaveServiceFromForm();
    const nextServices = editingServiceId.value
      ? currentServices.map((service) =>
          service.id === editingServiceId.value ? nextService : service,
        )
      : [...currentServices, nextService];
    const updatedProject = await updateProjectRuntimeProjectApi(
      project.id,
      buildProjectSaveRequest(project, workspace.id, nextServices),
    );
    const updatedWorkspace = updatedProject.workspaces.find(
      (item) => item.id === workspace.id,
    );
    const savedService = editingServiceId.value
      ? updatedWorkspace?.services.find((item) => item.id === editingServiceId.value)
      : updatedWorkspace?.services.find(
          (item) =>
            item.name === nextService.name &&
            item.order === nextService.order &&
            item.serviceType === nextService.serviceType,
        );

    message.success(editingServiceId.value ? '服务配置已保存' : '服务已新增');
    serviceModalOpen.value = false;
    await loadOverview(true);
    selectedServiceId.value = savedService?.id ?? selectedServiceId.value;
    await loadServiceLog();
  } finally {
    serviceSaving.value = false;
  }
}

async function removeServiceConfig(service: ProjectRuntimeService) {
  const project = selectedProject.value;
  const workspace = selectedWorkspace.value;
  if (!project || !workspace) {
    return;
  }

  const nextServices = workspace.services
    .filter((item) => item.id !== service.id)
    .map(toSaveService);
  await updateProjectRuntimeProjectApi(
    project.id,
    buildProjectSaveRequest(project, workspace.id, nextServices),
  );
  message.success('服务配置已删除');
  await loadOverview(true);
  if (selectedServiceId.value === service.id) {
    selectedServiceId.value = selectedWorkspace.value?.services[0]?.id ?? '';
  }
  await loadServiceLog();
}

async function saveProject() {
  saving.value = true;
  try {
    const project = await createProjectRuntimeProjectApi({
      code: projectForm.code,
      description: projectForm.description || undefined,
      isEnabled: projectForm.isEnabled,
      name: projectForm.name,
      order: projectForm.order,
      repositoryUrl: projectForm.repositoryUrl || undefined,
      rootPath: projectForm.rootPath,
    });
    message.success('项目已登记');
    projectModalOpen.value = false;
    await loadOverview(false);
    selectedProjectId.value = project.id;
    syncSelection(true);
  } finally {
    saving.value = false;
  }
}

async function removeProject(project: ProjectRuntimeProject) {
  const deleted = await deleteProjectRuntimeProjectApi(project.id);
  if (deleted) {
    message.success('项目已删除');
    await loadOverview(false);
  }
}

function statusColor(status?: string) {
  if (status === 'Running') {
    return 'green';
  }
  if (status === 'External') {
    return 'blue';
  }
  if (status === 'Failed') {
    return 'red';
  }
  if (status === 'Disabled') {
    return 'default';
  }
  return 'orange';
}

function statusText(status?: string) {
  const map: Record<string, string> = {
    Disabled: '未启用',
    External: '外部运行',
    Failed: '失败',
    Running: '运行中',
    Stopped: '已停止',
  };
  return status ? (map[status] ?? status) : '-';
}

function buildStatusColor(status?: string) {
  if (status === 'Running') {
    return 'processing';
  }
  if (status === 'Succeeded') {
    return 'green';
  }
  if (status === 'Failed') {
    return 'red';
  }
  if (status === 'NotConfigured') {
    return 'default';
  }
  return 'blue';
}

function buildStatusText(status?: string) {
  const map: Record<string, string> = {
    Failed: '打包失败',
    Idle: '可打包',
    NotConfigured: '未配置打包',
    Running: '打包中',
    Succeeded: '打包成功',
  };
  return status ? (map[status] ?? status) : '-';
}

function serviceTypeText(type: string) {
  const map: Record<string, string> = {
    Api: '后端',
    Custom: '自定义',
    Web: '前端',
  };
  return map[type] ?? type;
}

function formatTime(value?: null | string) {
  return value ? new Date(value).toLocaleString() : '-';
}

function formatDuration(value?: null | number) {
  if (!value) {
    return '-';
  }

  if (value < 1000) {
    return `${value} ms`;
  }

  return `${(value / 1000).toFixed(1)} s`;
}

function formatBytes(value?: null | number) {
  if (value === null || value === undefined) {
    return '-';
  }

  if (value < 1024) {
    return `${value} B`;
  }

  if (value < 1024 * 1024) {
    return `${(value / 1024).toFixed(1)} KB`;
  }

  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function artifactTypeText(type?: string) {
  const map: Record<string, string> = {
    Directory: '目录',
    File: '文件',
    Missing: '未生成',
  };
  return type ? (map[type] ?? type) : '-';
}

function openUrl(url?: null | string) {
  if (url) {
    window.open(url, '_blank', 'noopener,noreferrer');
  }
}

watch(selectedServiceId, (serviceId) => {
  serviceLog.value = undefined;
  if (serviceId) {
    void loadServiceLog(serviceId);
  }
});

onMounted(async () => {
  await loadOverview(false);
  await loadServiceLog();
  startLogRefreshTimer();
});

onBeforeUnmount(stopLogRefreshTimer);
</script>

<template>
  <Page auto-content-height>
    <div class="runtime-page">
      <div class="runtime-header">
        <div>
          <h2>项目运行管理</h2>
          <p>集中启动、观察和诊断本地项目服务</p>
        </div>
        <Space>
          <Button :loading="loading" @click="loadOverview()">刷新</Button>
          <Button v-if="canManage" type="primary" @click="openProjectModal">
            新增项目
          </Button>
        </Space>
      </div>

      <Spin :spinning="loading && !overview">
        <div class="runtime-summary">
          <div
            v-for="item in summaryCards"
            :key="item.label"
            class="summary-pill"
          >
            <span>{{ item.label }}</span>
            <strong>{{ item.value }}</strong>
          </div>
        </div>

        <div class="runtime-workbench">
          <aside class="runtime-sidebar">
            <section class="sidebar-section">
              <div class="panel-heading">
                <h3>项目</h3>
                <span>{{ projects.length }} 个</span>
              </div>
              <Empty v-if="projects.length === 0" description="暂无项目" />
              <button
                v-for="project in projects"
                :key="project.id"
                class="project-item"
                :class="{ active: project.id === selectedProjectId }"
                type="button"
                @click="selectProject(project)"
              >
                <div>
                  <strong>{{ project.name }}</strong>
                  <span>{{ project.code }}</span>
                </div>
                <Tag :color="project.isEnabled ? 'green' : 'default'">
                  {{ project.isEnabled ? '启用' : '停用' }}
                </Tag>
              </button>
            </section>

            <section class="sidebar-section">
              <div class="panel-heading">
                <h3>工作区</h3>
                <span>{{ workspaces.length }} 个</span>
              </div>
              <div class="workspace-list">
                <button
                  v-for="workspace in workspaces"
                  :key="workspace.id"
                  class="workspace-item"
                  :class="{ active: workspace.id === selectedWorkspaceId }"
                  type="button"
                  @click="selectWorkspace(workspace)"
                >
                  <div>
                    <strong>{{ workspace.name }}</strong>
                    <span>{{ workspace.branchName || '未识别分支' }}</span>
                  </div>
                  <Tag v-if="workspace.isDefault" color="blue">默认</Tag>
                </button>
              </div>
            </section>
          </aside>

          <main class="runtime-main">
            <template v-if="selectedProject && selectedWorkspace">
              <section class="runtime-context">
                <div>
                  <div class="context-title">
                    <h3>{{ selectedProject.name }}</h3>
                    <Tag :color="selectedProject.isEnabled ? 'green' : 'default'">
                      {{ selectedProject.isEnabled ? '启用' : '停用' }}
                    </Tag>
                  </div>
                  <p>{{ selectedWorkspace.path }}</p>
                </div>
                <Space>
                  <Button
                    v-if="selectedProject.repositoryUrl"
                    @click="openUrl(selectedProject.repositoryUrl)"
                  >
                    仓库
                  </Button>
                  <Button
                    v-if="canManage"
                    :loading="actionLoading === `start:${selectedWorkspace.id}`"
                    type="primary"
                    @click="runWorkspaceAction(selectedWorkspace, 'start')"
                  >
                    启动全部
                  </Button>
                  <Button
                    v-if="canManage"
                    :loading="actionLoading === `stop:${selectedWorkspace.id}`"
                    danger
                    @click="runWorkspaceAction(selectedWorkspace, 'stop')"
                  >
                    停止全部
                  </Button>
                  <Popconfirm
                    v-if="canManage"
                    title="确认删除该项目配置？运行中的服务会被停止。"
                    @confirm="removeProject(selectedProject)"
                  >
                    <Button danger>删除配置</Button>
                  </Popconfirm>
                </Space>
              </section>

              <section class="service-panel">
                <div class="panel-heading">
                  <div>
                    <h3>服务</h3>
                    <span>点击服务行切换右侧日志源</span>
                  </div>
                  <Button
                    v-if="canManage"
                    size="small"
                    type="primary"
                    @click="openCreateServiceModal"
                  >
                    新增服务
                  </Button>
                </div>

                <div class="service-list">
                  <Empty v-if="services.length === 0" description="暂无服务" />
                  <button
                    v-for="service in services"
                    :key="service.id"
                    class="service-row"
                    :class="{ active: service.id === selectedServiceId }"
                    type="button"
                    @click="selectedServiceId = service.id"
                  >
                    <div class="service-main">
                      <div class="service-title">
                        <h4>{{ service.name }}</h4>
                        <Tag :color="statusColor(service.state.status)">
                          {{ statusText(service.state.status) }}
                        </Tag>
                        <Tag :color="buildStatusColor(service.buildState.status)">
                          {{ buildStatusText(service.buildState.status) }}
                        </Tag>
                      </div>
                      <p>{{ service.state.message }}</p>
                    </div>
                    <div class="service-facts">
                      <span>{{ serviceTypeText(service.serviceType) }}</span>
                      <span>端口 {{ service.port || '-' }}</span>
                      <span>PID {{ service.state.processId || '-' }}</span>
                      <span>{{ service.state.healthOk ? '健康正常' : '健康未通过' }}</span>
                      <span>最近打包 {{ buildStatusText(service.latestBuild?.status) }}</span>
                      <span>产物 {{ service.artifact.exists ? '已生成' : '未生成' }}</span>
                    </div>
                    <div class="service-actions" @click.stop>
                      <Button
                        v-if="canManage"
                        size="small"
                        :loading="actionLoading === `start:${service.id}`"
                        @click="runServiceAction(service, 'start')"
                      >
                        启动
                      </Button>
                      <Button
                        v-if="canManage"
                        size="small"
                        :loading="actionLoading === `restart:${service.id}`"
                        @click="runServiceAction(service, 'restart')"
                      >
                        重启
                      </Button>
                      <Button
                        v-if="canManage"
                        danger
                        size="small"
                        :loading="actionLoading === `stop:${service.id}`"
                        @click="runServiceAction(service, 'stop')"
                      >
                        停止
                      </Button>
                      <Button
                        v-if="canManage"
                        size="small"
                        :disabled="!service.buildCommand"
                        :loading="actionLoading === `build:${service.id}`"
                        @click="runBuildAction(service)"
                      >
                        打包
                      </Button>
                      <Button
                        v-if="service.url"
                        size="small"
                        @click="openUrl(service.url)"
                      >
                        打开
                      </Button>
                      <Button
                        v-if="canManage"
                        size="small"
                        :disabled="!service.artifact.exists"
                        :loading="actionLoading === `artifact:${service.id}`"
                        @click="openArtifact(service)"
                      >
                        产物
                      </Button>
                      <Button
                        v-if="canManage"
                        size="small"
                        @click="openEditServiceModal(service)"
                      >
                        配置
                      </Button>
                      <Popconfirm
                        v-if="canManage"
                        title="确认删除该服务配置？运行中的进程不会在这里强制停止。"
                        @confirm="removeServiceConfig(service)"
                      >
                        <Button danger size="small">删除</Button>
                      </Popconfirm>
                    </div>
                  </button>
                </div>
              </section>

              <section class="log-console">
                <div class="log-toolbar">
                  <div>
                    <h3>实时输出</h3>
                    <span>{{ selectedService?.name || '未选择服务' }}</span>
                  </div>
                  <Space v-if="selectedService && canViewLog" size="small">
                    <div class="log-mode-switch">
                      <button
                        :class="{ active: logMode === 'run' }"
                        type="button"
                        @click="switchLogMode('run')"
                      >
                        运行日志
                      </button>
                      <button
                        :class="{ active: logMode === 'build' }"
                        type="button"
                        @click="switchLogMode('build')"
                      >
                        打包日志
                      </button>
                    </div>
                    <span class="log-refresh-label">实时输出</span>
                    <Switch v-model:checked="autoRefreshLogs" size="small" />
                    <Button :loading="logLoading" size="small" @click="loadServiceLog()">
                      刷新
                    </Button>
                  </Space>
                </div>
                <div v-if="selectedService" class="log-meta">
                  <div>
                    <span>命令</span>
                    <strong>{{ logCommandText }}</strong>
                  </div>
                  <div>
                    <span>日志文件</span>
                    <strong>{{ logPathText }}</strong>
                  </div>
                  <div>
                    <span>检查时间</span>
                    <strong>{{ formatTime(logCheckedAt) }}</strong>
                  </div>
                </div>
                <div v-if="selectedService" class="build-insight">
                  <div>
                    <span>最近构建</span>
                    <strong>
                      {{ buildStatusText(selectedLatestBuild?.status) }}
                      <template v-if="selectedLatestBuild?.exitCode !== null && selectedLatestBuild?.exitCode !== undefined">
                        / ExitCode {{ selectedLatestBuild.exitCode }}
                      </template>
                    </strong>
                    <small>
                      {{ formatTime(selectedLatestBuild?.endedAt || selectedLatestBuild?.startedAt) }}
                      · {{ formatDuration(selectedLatestBuild?.durationMilliseconds) }}
                    </small>
                  </div>
                  <div>
                    <span>构建产物</span>
                    <strong>
                      {{ artifactTypeText(selectedArtifact?.type) }}
                      · {{ selectedArtifact?.exists ? formatBytes(selectedArtifact.sizeBytes) : '未生成' }}
                    </strong>
                    <small>{{ selectedArtifact?.path || selectedService.buildArtifactPath || '-' }}</small>
                  </div>
                  <Space>
                    <Button
                      size="small"
                      :disabled="!selectedArtifact?.exists"
                      :loading="actionLoading === `artifact:${selectedService.id}`"
                      @click="openArtifact(selectedService)"
                    >
                      打开产物
                    </Button>
                    <Button size="small" @click="loadOverview(true, true)">
                      刷新状态
                    </Button>
                  </Space>
                </div>
                <Spin :spinning="logLoading">
                  <pre ref="logViewRef" class="log-view">{{
                    serviceLog?.lines.join('\n') || logPlaceholder
                  }}</pre>
                </Spin>
              </section>
            </template>
            <Empty v-else description="请选择项目和工作区" />
          </main>
        </div>
      </Spin>

      <Modal
        v-model:open="projectModalOpen"
        title="新增本地项目"
        :confirm-loading="saving"
        @ok="saveProject"
      >
        <Form layout="vertical">
          <FormItem label="项目名称" required>
            <Input v-model:value="projectForm.name" placeholder="例如 MiniAdmin" />
          </FormItem>
          <FormItem label="项目编码" required>
            <Input v-model:value="projectForm.code" placeholder="例如 mini-admin" />
          </FormItem>
          <FormItem label="本地目录" required>
            <Input
              v-model:value="projectForm.rootPath"
              placeholder="例如 C:\monica\code\mini-admin"
            />
          </FormItem>
          <FormItem label="仓库地址">
            <Input v-model:value="projectForm.repositoryUrl" />
          </FormItem>
          <FormItem label="说明">
            <Textarea v-model:value="projectForm.description" :rows="3" />
          </FormItem>
          <div class="form-row">
            <FormItem label="排序">
              <InputNumber v-model:value="projectForm.order" :min="0" />
            </FormItem>
            <FormItem label="启用">
              <Switch v-model:checked="projectForm.isEnabled" />
            </FormItem>
          </div>
        </Form>
      </Modal>

      <Modal
        v-model:open="serviceModalOpen"
        :title="serviceModalTitle"
        :confirm-loading="serviceSaving"
        wrap-class-name="project-runtime-service-modal"
        width="860px"
        @ok="saveServiceConfig"
      >
        <Form class="service-config-form" layout="vertical">
          <section class="form-section">
            <div class="form-section-title">
              <h4>基础信息</h4>
              <span>决定服务在运行面板中的展示和排序</span>
            </div>
            <div class="form-grid">
              <FormItem label="服务名称" required>
                <Input v-model:value="serviceForm.name" placeholder="例如 MiniAdmin API" />
              </FormItem>
              <FormItem label="服务类型">
                <Select
                  v-model:value="serviceForm.serviceType"
                  :options="serviceTypeOptions"
                  @change="applyServiceTemplate"
                />
              </FormItem>
              <FormItem label="排序">
                <InputNumber v-model:value="serviceForm.order" :min="0" />
              </FormItem>
              <FormItem label="启用">
                <Switch v-model:checked="serviceForm.isEnabled" />
              </FormItem>
            </div>
          </section>

          <section class="form-section">
            <div class="form-section-title">
              <h4>运行配置</h4>
              <span>用于启动服务、健康检查和打开访问地址</span>
            </div>
            <div class="form-grid">
              <FormItem label="启动命令">
                <Input v-model:value="serviceForm.command" placeholder="dotnet / pnpm / npm" />
              </FormItem>
              <FormItem label="启动参数">
                <Input v-model:value="serviceForm.arguments" placeholder="run --project ... / run dev" />
              </FormItem>
              <FormItem label="工作目录">
                <Input v-model:value="serviceForm.workingDirectory" placeholder="相对工作区，例如 ." />
              </FormItem>
              <FormItem label="端口">
                <InputNumber v-model:value="serviceForm.port" :min="1" :max="65535" />
              </FormItem>
              <FormItem label="健康检查地址">
                <Input v-model:value="serviceForm.healthUrl" placeholder="http://localhost:5320/health" />
              </FormItem>
              <FormItem label="访问地址">
                <Input v-model:value="serviceForm.url" placeholder="http://localhost:5666/" />
              </FormItem>
              <FormItem label="日志文件名">
                <Input v-model:value="serviceForm.logFileName" placeholder="service.log" />
              </FormItem>
              <FormItem label="日志路径">
                <Input v-model:value="serviceForm.logPath" placeholder="可选，例如 backend-dev.log" />
              </FormItem>
            </div>
          </section>

          <section class="form-section">
            <div class="form-section-title">
              <h4>打包配置</h4>
              <span>用于构建、查看构建日志和定位产物</span>
            </div>
            <div class="form-grid">
              <FormItem label="打包命令">
                <Input v-model:value="serviceForm.buildCommand" placeholder="dotnet / pnpm" />
              </FormItem>
              <FormItem label="打包参数">
                <Input v-model:value="serviceForm.buildArguments" placeholder="publish -c Release / run build" />
              </FormItem>
              <FormItem label="打包工作目录">
                <Input v-model:value="serviceForm.buildWorkingDirectory" placeholder="默认跟随运行工作目录" />
              </FormItem>
              <FormItem label="打包日志文件名">
                <Input v-model:value="serviceForm.buildLogFileName" placeholder="service-build.log" />
              </FormItem>
              <FormItem label="打包日志路径">
                <Input v-model:value="serviceForm.buildLogPath" placeholder="可选，例如 frontend-build.log" />
              </FormItem>
              <FormItem label="产物路径">
                <Input v-model:value="serviceForm.buildArtifactPath" placeholder="dist / bin/Release / artifacts/publish/..." />
              </FormItem>
            </div>
          </section>
        </Form>
      </Modal>
    </div>
  </Page>
</template>

<style scoped>
.runtime-page {
  display: flex;
  min-height: 100%;
  flex-direction: column;
  gap: 12px;
}

.runtime-header,
.runtime-context,
.panel-heading,
.log-toolbar,
.context-title,
.service-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.runtime-header h2,
.runtime-context h3,
.panel-heading h3,
.log-toolbar h3,
.service-title h4 {
  margin: 0;
  color: #172033;
}

.runtime-header p,
.runtime-context p,
.panel-heading span,
.log-toolbar span,
.project-item span,
.workspace-item span {
  margin: 4px 0 0;
  color: #697386;
  font-size: 13px;
}

.runtime-summary {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.summary-pill {
  display: inline-flex;
  align-items: baseline;
  gap: 8px;
  border: 1px solid #e6eaf0;
  border-radius: 8px;
  background: #fff;
  padding: 8px 12px;
}

.summary-pill span {
  color: #697386;
  font-size: 12px;
}

.summary-pill strong {
  color: #172033;
  font-size: 18px;
}

.runtime-workbench {
  display: grid;
  min-height: 720px;
  gap: 12px;
  grid-template-columns: 280px minmax(0, 1fr);
}

.runtime-sidebar,
.runtime-main,
.runtime-context,
.service-panel,
.log-console {
  border: 1px solid #e6eaf0;
  border-radius: 8px;
  background: #fff;
}

.runtime-sidebar {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 16px;
  padding: 14px;
}

.sidebar-section {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.project-item,
.workspace-item {
  display: flex;
  width: 100%;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  border: 1px solid #e6eaf0;
  border-radius: 8px;
  background: #f8fafc;
  cursor: pointer;
  padding: 10px;
  text-align: left;
  transition: all 0.18s ease;
}

.project-item strong,
.workspace-item strong {
  display: block;
  color: #172033;
  font-size: 14px;
}

.project-item.active,
.workspace-item.active,
.service-row.active {
  border-color: #2f6fed;
  background: #f2f6ff;
}

.workspace-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.runtime-main {
  display: grid;
  min-width: 0;
  gap: 12px;
  grid-template-rows: auto auto minmax(0, 1fr);
  min-height: 0;
  padding: 14px;
}

.runtime-context,
.service-panel,
.log-console {
  min-width: 0;
  padding: 14px;
}

.runtime-context p {
  max-width: 760px;
  overflow-wrap: anywhere;
}

.service-panel {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.service-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.service-row {
  display: grid;
  align-items: center;
  gap: 14px;
  grid-template-columns: minmax(220px, 1fr) 340px auto;
  border: 1px solid #e6eaf0;
  border-radius: 8px;
  background: #fff;
  cursor: pointer;
  padding: 12px;
  text-align: left;
}

.service-main p {
  margin: 6px 0 0;
  color: #4b5565;
  font-size: 13px;
  line-height: 1.45;
}

.service-facts {
  display: grid;
  gap: 6px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  color: #566176;
  font-size: 12px;
}

.service-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 8px;
}

.log-console {
  display: flex;
  height: clamp(380px, 46vh, 560px);
  min-height: 0;
  flex-direction: column;
  gap: 10px;
  overflow: hidden;
}

.log-console :deep(.ant-spin-nested-loading),
.log-console :deep(.ant-spin-container) {
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
}

.log-meta {
  display: grid;
  gap: 8px;
  grid-template-columns: minmax(260px, 1.4fr) minmax(220px, 1fr) 180px;
  border: 1px solid #edf0f5;
  border-radius: 8px;
  background: #f8fafc;
  padding: 10px 12px;
}

.log-meta span {
  display: block;
  color: #697386;
  font-size: 12px;
}

.log-meta strong {
  display: block;
  margin-top: 3px;
  color: #172033;
  font-size: 12px;
  font-weight: 500;
  overflow-wrap: anywhere;
}

.build-insight {
  display: grid;
  align-items: center;
  gap: 8px;
  grid-template-columns: minmax(220px, 1fr) minmax(260px, 1.4fr) auto;
  border: 1px solid #e6edf8;
  border-radius: 8px;
  background: #f7fbff;
  padding: 10px 12px;
}

.build-insight span,
.build-insight small {
  display: block;
  color: #697386;
  font-size: 12px;
}

.build-insight strong {
  display: block;
  margin-top: 3px;
  color: #172033;
  font-size: 12px;
  font-weight: 600;
  overflow-wrap: anywhere;
}

.build-insight small {
  margin-top: 3px;
  overflow-wrap: anywhere;
}

.log-refresh-label {
  color: #697386;
  font-size: 12px;
}

.log-mode-switch {
  display: inline-flex;
  overflow: hidden;
  border: 1px solid #d8dee8;
  border-radius: 6px;
  background: #f7f9fc;
}

.log-mode-switch button {
  border: 0;
  background: transparent;
  color: #566176;
  cursor: pointer;
  font-size: 12px;
  line-height: 24px;
  padding: 0 10px;
}

.log-mode-switch button.active {
  background: #fff;
  color: #1f5fcc;
  font-weight: 600;
  box-shadow: 0 0 0 1px #dbe7ff inset;
}

.log-view {
  min-height: 0;
  flex: 1;
  overflow: auto;
  border-radius: 8px;
  background: #111827;
  color: #d1d5db;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.6;
  margin: 0;
  padding: 14px;
  white-space: pre-wrap;
}

.form-row {
  display: grid;
  gap: 12px;
  grid-template-columns: 1fr 1fr;
}

.service-config-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.form-section {
  border: 1px solid #edf0f5;
  border-radius: 8px;
  background: #fbfcfe;
  padding: 12px;
}

.form-section-title {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 10px;
}

.form-section-title h4 {
  margin: 0;
  color: #172033;
  font-size: 14px;
}

.form-section-title span {
  color: #697386;
  font-size: 12px;
}

.form-grid {
  display: grid;
  gap: 0 12px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.form-grid :deep(.ant-input-number) {
  width: 100%;
}

:global(.project-runtime-service-modal .ant-modal-body) {
  max-height: calc(100vh - 220px);
  overflow-y: auto;
}

@media (max-width: 1280px) {
  .runtime-workbench {
    grid-template-columns: 1fr;
  }

  .runtime-sidebar {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }

  .service-row,
  .log-meta,
  .build-insight {
    grid-template-columns: 1fr;
  }

  .service-actions {
    justify-content: flex-start;
  }
}

@media (max-width: 900px) {
  .runtime-header,
  .runtime-context,
  .log-toolbar {
    align-items: flex-start;
    flex-direction: column;
  }

  .runtime-sidebar {
    grid-template-columns: 1fr;
  }

  .form-grid {
    grid-template-columns: 1fr;
  }

  .form-section-title {
    align-items: flex-start;
    flex-direction: column;
    gap: 4px;
  }
}
</style>
