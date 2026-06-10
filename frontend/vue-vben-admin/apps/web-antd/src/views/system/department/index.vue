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
  createDepartmentApi,
  deleteDepartmentApi,
  getDepartmentListApi,
  type DepartmentItem,
  updateDepartmentApi,
} from '#/api/system/department';

const { hasAccessByCodes } = useAccess();

interface DepartmentFormState {
  code: string;
  isEnabled: boolean;
  leader: string;
  name: string;
  order: number;
  parentId?: string;
  phone: string;
}

const loading = ref(false);
const saving = ref(false);
const modalOpen = ref(false);
const editingDepartment = ref<DepartmentItem>();
const departments = ref<DepartmentItem[]>([]);
const formState = reactive<DepartmentFormState>({
  code: '',
  isEnabled: true,
  leader: '',
  name: '',
  order: 0,
  parentId: undefined,
  phone: '',
});

const columns = [
  { dataIndex: 'name', title: '部门名称', width: 220 },
  { dataIndex: 'code', title: '部门编码', width: 160 },
  { dataIndex: 'leader', title: '负责人', width: 140 },
  { dataIndex: 'phone', title: '联系电话', width: 160 },
  { dataIndex: 'order', title: '排序', width: 90 },
  { dataIndex: 'state', title: '状态', width: 100 },
  { dataIndex: 'action', title: '操作', width: 220 },
];

const modalTitle = computed(() =>
  editingDepartment.value ? '编辑部门' : '新增部门',
);
const canCreate = computed(() => hasAccessByCodes(['system:department:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:department:update']));
const canDelete = computed(() => hasAccessByCodes(['system:department:delete']));

const parentOptions = computed(() => {
  const options: { label: string; value: string }[] = [];
  const blockedIds = new Set<string>();

  function collectBlocked(item: DepartmentItem) {
    blockedIds.add(item.id);
    for (const child of item.children) {
      collectBlocked(child);
    }
  }

  if (editingDepartment.value) {
    collectBlocked(editingDepartment.value);
  }

  function walk(items: DepartmentItem[], level = 0) {
    for (const item of items) {
      if (!blockedIds.has(item.id)) {
        options.push({
          label: `${'  '.repeat(level)}${item.name}`,
          value: item.id,
        });
      }
      walk(item.children, level + 1);
    }
  }

  walk(departments.value);
  return options;
});

async function loadDepartments() {
  loading.value = true;
  try {
    departments.value = await getDepartmentListApi();
  } finally {
    loading.value = false;
  }
}

function resetForm(parentId?: string) {
  editingDepartment.value = undefined;
  formState.parentId = parentId;
  formState.code = '';
  formState.name = '';
  formState.leader = '';
  formState.phone = '';
  formState.order = 0;
  formState.isEnabled = true;
}

function openCreateModal(parentId?: string) {
  resetForm(parentId);
  modalOpen.value = true;
}

function openEditModal(department: DepartmentItem | Record<string, any>) {
  const currentDepartment = department as DepartmentItem;
  editingDepartment.value = currentDepartment;
  formState.parentId = currentDepartment.parentId ?? undefined;
  formState.code = currentDepartment.code;
  formState.name = currentDepartment.name;
  formState.leader = currentDepartment.leader ?? '';
  formState.phone = currentDepartment.phone ?? '';
  formState.order = currentDepartment.order;
  formState.isEnabled = currentDepartment.isEnabled;
  modalOpen.value = true;
}

async function submitDepartment() {
  if (!formState.code.trim() || !formState.name.trim()) {
    message.warning('请填写部门编码和部门名称');
    return;
  }

  const payload = {
    code: formState.code,
    isEnabled: formState.isEnabled,
    leader: formState.leader || null,
    name: formState.name,
    order: formState.order,
    parentId: formState.parentId || null,
    phone: formState.phone || null,
  };

  saving.value = true;
  try {
    if (editingDepartment.value) {
      await updateDepartmentApi(editingDepartment.value.id, payload);
      message.success('部门已更新');
    } else {
      await createDepartmentApi(payload);
      message.success('部门已新增');
    }

    modalOpen.value = false;
    await loadDepartments();
  } finally {
    saving.value = false;
  }
}

async function removeDepartment(department: DepartmentItem | Record<string, any>) {
  const currentDepartment = department as DepartmentItem;
  const deleted = await deleteDepartmentApi(currentDepartment.id);
  if (deleted) {
    message.success('部门已删除');
  } else {
    message.warning('该部门有子部门，不能删除');
  }
  await loadDepartments();
}

onMounted(loadDepartments);
</script>

<template>
  <Page description="维护组织架构的上下级关系。" title="部门管理">
    <div class="mb-4 flex items-center justify-between gap-3">
      <Space>
        <Button v-if="canCreate" type="primary" @click="openCreateModal()">
          新增根部门
        </Button>
        <Button @click="loadDepartments">刷新</Button>
      </Space>
    </div>

    <Table
      row-key="id"
      :columns="columns"
      :data-source="departments"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.dataIndex === 'state'">
          <Tag :color="record.isEnabled ? 'green' : 'default'">
            {{ record.isEnabled ? '启用' : '停用' }}
          </Tag>
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
              title="确认删除该部门？"
              @confirm="removeDepartment(record)"
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
      @ok="submitDepartment"
    >
      <Form layout="vertical">
        <FormItem label="上级部门">
          <Select
            v-model:value="formState.parentId"
            allow-clear
            :options="parentOptions"
            placeholder="不选择则为根部门"
          />
        </FormItem>
        <div class="grid grid-cols-2 gap-4">
          <FormItem label="部门编码" required>
            <Input v-model:value="formState.code" placeholder="例如 rd" />
          </FormItem>
          <FormItem label="部门名称" required>
            <Input v-model:value="formState.name" placeholder="例如 研发部" />
          </FormItem>
          <FormItem label="负责人">
            <Input v-model:value="formState.leader" placeholder="可选" />
          </FormItem>
          <FormItem label="联系电话">
            <Input v-model:value="formState.phone" placeholder="可选" />
          </FormItem>
          <FormItem label="排序">
            <InputNumber v-model:value="formState.order" class="w-full" />
          </FormItem>
          <FormItem label="启用">
            <Switch v-model:checked="formState.isEnabled" />
          </FormItem>
        </div>
      </Form>
    </Modal>
  </Page>
</template>
