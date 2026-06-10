<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';

import { useAccess } from '@vben/access';
import { Page } from '@vben/common-ui';

import {
  Button,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  message,
} from 'ant-design-vue';

import {
  createMenuApi,
  deleteMenuApi,
  getMenuListApi,
  type MenuManagementItem,
  updateMenuApi,
} from '#/api/system/menu';

const { hasAccessByCodes } = useAccess();

interface MenuFormState {
  affixTab: boolean;
  component: string;
  icon: string;
  isEnabled: boolean;
  isVisible: boolean;
  name: string;
  order: number;
  parentId?: string;
  path: string;
  permissionCode: string;
  redirect: string;
  title: string;
}

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const editingMenu = ref<MenuManagementItem>();
const menus = ref<MenuManagementItem[]>([]);
const formState = reactive<MenuFormState>({
  affixTab: false,
  component: '',
  icon: '',
  isEnabled: true,
  isVisible: true,
  name: '',
  order: 0,
  parentId: undefined,
  path: '',
  permissionCode: '',
  redirect: '',
  title: '',
});

const columns = [
  { dataIndex: 'title', title: '菜单名称', width: 220 },
  { dataIndex: 'name', title: '路由名称', width: 180 },
  { dataIndex: 'path', title: '路径', width: 220 },
  { dataIndex: 'permissionCode', title: '权限码' },
  { dataIndex: 'order', title: '排序', width: 90 },
  { dataIndex: 'state', title: '状态', width: 150 },
  { dataIndex: 'action', title: '操作', width: 220 },
];

const modalTitle = computed(() => (editingMenu.value ? '编辑菜单' : '新增菜单'));
const canCreate = computed(() => hasAccessByCodes(['system:menu:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:menu:update']));
const canDelete = computed(() => hasAccessByCodes(['system:menu:delete']));

const parentOptions = computed(() => {
  const options: { label: string; value: string }[] = [];

  function walk(items: MenuManagementItem[], level = 0) {
    for (const item of items) {
      if (item.id !== editingMenu.value?.id) {
        options.push({
          label: `${'  '.repeat(level)}${item.title}`,
          value: item.id,
        });
      }
      walk(item.children, level + 1);
    }
  }

  walk(menus.value);
  return options;
});

async function loadMenus() {
  loading.value = true;
  try {
    menus.value = await getMenuListApi();
  } finally {
    loading.value = false;
  }
}

function resetForm(parentId?: string) {
  editingMenu.value = undefined;
  formState.parentId = parentId;
  formState.name = '';
  formState.path = '';
  formState.component = '';
  formState.redirect = '';
  formState.title = '';
  formState.icon = '';
  formState.order = 0;
  formState.affixTab = false;
  formState.permissionCode = '';
  formState.isEnabled = true;
  formState.isVisible = true;
}

function openCreateModal(parentId?: string) {
  resetForm(parentId);
  modalOpen.value = true;
}

function openEditModal(menu: MenuManagementItem | Record<string, any>) {
  const currentMenu = menu as MenuManagementItem;
  editingMenu.value = currentMenu;
  formState.parentId = currentMenu.parentId ?? undefined;
  formState.name = currentMenu.name;
  formState.path = currentMenu.path;
  formState.component = currentMenu.component ?? '';
  formState.redirect = currentMenu.redirect ?? '';
  formState.title = currentMenu.title;
  formState.icon = currentMenu.icon ?? '';
  formState.order = currentMenu.order;
  formState.affixTab = currentMenu.affixTab;
  formState.permissionCode = currentMenu.permissionCode ?? '';
  formState.isEnabled = currentMenu.isEnabled;
  formState.isVisible = currentMenu.isVisible;
  modalOpen.value = true;
}

async function submitMenu() {
  if (!formState.name.trim() || !formState.path.trim() || !formState.title.trim()) {
    message.warning('请填写路由名称、路径和菜单名称');
    return;
  }

  const payload = {
    affixTab: formState.affixTab,
    component: formState.component || null,
    icon: formState.icon || null,
    isEnabled: formState.isEnabled,
    isVisible: formState.isVisible,
    name: formState.name,
    order: formState.order,
    parentId: formState.parentId || null,
    path: formState.path,
    permissionCode: formState.permissionCode || null,
    redirect: formState.redirect || null,
    title: formState.title,
  };

  saving.value = true;
  try {
    if (editingMenu.value) {
      await updateMenuApi(editingMenu.value.id, payload);
      message.success('菜单已更新');
    } else {
      await createMenuApi(payload);
      message.success('菜单已新增');
    }

    modalOpen.value = false;
    await loadMenus();
  } finally {
    saving.value = false;
  }
}

async function removeMenu(menu: MenuManagementItem | Record<string, any>) {
  const currentMenu = menu as MenuManagementItem;
  const deleted = await deleteMenuApi(currentMenu.id);
  if (deleted) {
    message.success('菜单已删除');
  } else {
    message.warning('该菜单有子菜单或已分配给角色，不能删除');
  }
  await loadMenus();
}

onMounted(loadMenus);
</script>

<template>
  <Page description="维护 Vben 动态路由需要的名称、路径、组件和权限码。" title="菜单管理">
    <div class="mb-4 flex items-center justify-between gap-3">
      <Space>
        <Button v-if="canCreate" type="primary" @click="openCreateModal()">
          新增根菜单
        </Button>
        <Button @click="loadMenus">刷新</Button>
      </Space>
    </div>

    <Table
      row-key="id"
      :columns="columns"
      :data-source="menus"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'state'">
          <Space>
            <Tag :color="record.isEnabled ? 'green' : 'default'">
              {{ record.isEnabled ? '启用' : '停用' }}
            </Tag>
            <Tag :color="record.isVisible ? 'blue' : 'default'">
              {{ record.isVisible ? '显示' : '隐藏' }}
            </Tag>
          </Space>
        </template>
        <template v-if="column.dataIndex === 'action'">
          <Space>
            <Button v-if="canCreate" type="link" @click="openCreateModal(record.id)">
              新增子级
            </Button>
            <Button v-if="canUpdate" type="link" @click="openEditModal(record)">
              编辑
            </Button>
            <Popconfirm
              v-if="canDelete"
              title="确认删除该菜单？"
              @confirm="removeMenu(record)"
            >
              <Button danger type="link">删除</Button>
            </Popconfirm>
            <span v-if="!canCreate && !canUpdate && !canDelete">-</span>
          </Space>
        </template>
      </template>
    </Table>

    <Modal
      v-model:open="modalOpen"
      :confirm-loading="saving"
      :title="modalTitle"
      width="760px"
      @ok="submitMenu"
    >
      <Form layout="vertical">
        <FormItem label="上级菜单">
          <Select
            v-model:value="formState.parentId"
            allow-clear
            :options="parentOptions"
            placeholder="不选择则为根菜单"
          />
        </FormItem>
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="路由名称" required>
            <Input v-model:value="formState.name" placeholder="例如 MenuManagement" />
          </FormItem>
          <FormItem label="菜单名称" required>
            <Input v-model:value="formState.title" placeholder="例如 菜单管理" />
          </FormItem>
          <FormItem label="路径" required>
            <Input v-model:value="formState.path" placeholder="例如 /system/menu" />
          </FormItem>
          <FormItem label="组件路径">
            <Input
              v-model:value="formState.component"
              placeholder="例如 /system/menu/index"
            />
          </FormItem>
          <FormItem label="重定向">
            <Input v-model:value="formState.redirect" placeholder="可选" />
          </FormItem>
          <FormItem label="图标">
            <Input v-model:value="formState.icon" placeholder="例如 lucide:menu" />
          </FormItem>
          <FormItem label="权限码">
            <Input
              v-model:value="formState.permissionCode"
              placeholder="例如 system:menu:query"
            />
          </FormItem>
          <FormItem label="排序">
            <InputNumber v-model:value="formState.order" class="w-full" />
          </FormItem>
        </div>
        <Space>
          <FormItem label="启用">
            <Switch v-model:checked="formState.isEnabled" />
          </FormItem>
          <FormItem label="显示">
            <Switch v-model:checked="formState.isVisible" />
          </FormItem>
          <FormItem label="固定标签">
            <Switch v-model:checked="formState.affixTab" />
          </FormItem>
        </Space>
      </Form>
    </Modal>
  </Page>
</template>
