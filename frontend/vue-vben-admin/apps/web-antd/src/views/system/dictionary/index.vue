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
  Space,
  Switch,
  Table,
  Tag,
  message,
} from 'ant-design-vue';

import {
  createDictionaryItemApi,
  createDictionaryTypeApi,
  deleteDictionaryItemApi,
  deleteDictionaryTypeApi,
  getDictionaryListApi,
  type DictionaryItem,
  type DictionaryType,
  updateDictionaryItemApi,
  updateDictionaryTypeApi,
} from '#/api/system/dictionary';

interface TypeFormState {
  code: string;
  isEnabled: boolean;
  name: string;
  order: number;
}

interface ItemFormState {
  color: string;
  isEnabled: boolean;
  label: string;
  order: number;
  value: string;
}

const loading = ref(false);
const saving = ref(false);
const typeModalOpen = ref(false);
const itemModalOpen = ref(false);
const dictionaries = ref<DictionaryType[]>([]);
const selectedTypeId = ref<string>();
const typeNameKeyword = ref('');
const typeCodeKeyword = ref('');
const itemLabelKeyword = ref('');
const editingType = ref<DictionaryType>();
const editingItem = ref<DictionaryItem>();
const typeForm = reactive<TypeFormState>({
  code: '',
  isEnabled: true,
  name: '',
  order: 0,
});
const itemForm = reactive<ItemFormState>({
  color: '',
  isEnabled: true,
  label: '',
  order: 0,
  value: '',
});
const { hasAccessByCodes } = useAccess();

const typeColumns = [
  { dataIndex: 'name', title: '字典名称' },
  { dataIndex: 'code', title: '编码', width: 180 },
  { dataIndex: 'order', title: '排序', width: 80 },
  { dataIndex: 'state', title: '状态', width: 90 },
  { dataIndex: 'action', title: '操作', width: 150 },
];

const itemColumns = [
  { dataIndex: 'label', title: '选项名称' },
  { dataIndex: 'value', title: '选项值', width: 160 },
  { dataIndex: 'color', title: '颜色', width: 120 },
  { dataIndex: 'order', title: '排序', width: 80 },
  { dataIndex: 'state', title: '状态', width: 90 },
  { dataIndex: 'action', title: '操作', width: 150 },
];

const selectedType = computed(() =>
  dictionaries.value.find((item) => item.id === selectedTypeId.value),
);

const filteredDictionaryTypes = computed(() => {
  const name = typeNameKeyword.value.trim().toLowerCase();
  const code = typeCodeKeyword.value.trim().toLowerCase();

  return dictionaries.value.filter((item) => {
    const matchesName = !name || item.name.toLowerCase().includes(name);
    const matchesCode = !code || item.code.toLowerCase().includes(code);

    return matchesName && matchesCode;
  });
});

const filteredDictionaryItems = computed(() => {
  const keyword = itemLabelKeyword.value.trim().toLowerCase();
  const items = selectedType.value?.items ?? [];

  if (!keyword) {
    return items;
  }

  return items.filter((item) =>
    item.label.toLowerCase().includes(keyword) ||
    item.value.toLowerCase().includes(keyword),
  );
});

const typeModalTitle = computed(() => (editingType.value ? '编辑字典类型' : '新增字典类型'));
const itemModalTitle = computed(() => (editingItem.value ? '编辑字典项' : '新增字典项'));
const canCreate = computed(() => hasAccessByCodes(['system:dictionary:create']));
const canUpdate = computed(() => hasAccessByCodes(['system:dictionary:update']));
const canDelete = computed(() => hasAccessByCodes(['system:dictionary:delete']));

async function loadDictionaries() {
  loading.value = true;
  try {
    dictionaries.value = await getDictionaryListApi();
    selectedTypeId.value ??= dictionaries.value[0]?.id;
  } finally {
    loading.value = false;
  }
}

function openCreateTypeModal() {
  editingType.value = undefined;
  typeForm.code = '';
  typeForm.name = '';
  typeForm.order = 0;
  typeForm.isEnabled = true;
  typeModalOpen.value = true;
}

function openEditTypeModal(type: DictionaryType | Record<string, any>) {
  const currentType = type as DictionaryType;
  editingType.value = currentType;
  typeForm.code = currentType.code;
  typeForm.name = currentType.name;
  typeForm.order = currentType.order;
  typeForm.isEnabled = currentType.isEnabled;
  typeModalOpen.value = true;
}

function openCreateItemModal() {
  if (!selectedType.value) {
    message.warning('请先选择字典类型');
    return;
  }

  editingItem.value = undefined;
  itemForm.label = '';
  itemForm.value = '';
  itemForm.color = '';
  itemForm.order = 0;
  itemForm.isEnabled = true;
  itemModalOpen.value = true;
}

function openEditItemModal(item: DictionaryItem | Record<string, any>) {
  const currentItem = item as DictionaryItem;
  editingItem.value = currentItem;
  itemForm.label = currentItem.label;
  itemForm.value = currentItem.value;
  itemForm.color = currentItem.color ?? '';
  itemForm.order = currentItem.order;
  itemForm.isEnabled = currentItem.isEnabled;
  itemModalOpen.value = true;
}

async function submitType() {
  if (!typeForm.code.trim() || !typeForm.name.trim()) {
    message.warning('请填写字典编码和名称');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      code: typeForm.code,
      isEnabled: typeForm.isEnabled,
      name: typeForm.name,
      order: typeForm.order,
    };

    if (editingType.value) {
      await updateDictionaryTypeApi(editingType.value.id, payload);
      message.success('字典类型已更新');
    } else {
      const created = await createDictionaryTypeApi(payload);
      selectedTypeId.value = created.id;
      message.success('字典类型已新增');
    }

    typeModalOpen.value = false;
    await loadDictionaries();
  } finally {
    saving.value = false;
  }
}

async function submitItem() {
  if (!selectedType.value || !itemForm.label.trim() || !itemForm.value.trim()) {
    message.warning('请填写选项名称和选项值');
    return;
  }

  saving.value = true;
  try {
    const payload = {
      color: itemForm.color || null,
      isEnabled: itemForm.isEnabled,
      label: itemForm.label,
      order: itemForm.order,
      typeId: selectedType.value.id,
      value: itemForm.value,
    };

    if (editingItem.value) {
      await updateDictionaryItemApi(editingItem.value.id, payload);
      message.success('字典项已更新');
    } else {
      await createDictionaryItemApi(payload);
      message.success('字典项已新增');
    }

    itemModalOpen.value = false;
    await loadDictionaries();
  } finally {
    saving.value = false;
  }
}

async function removeType(type: DictionaryType | Record<string, any>) {
  const currentType = type as DictionaryType;
  const deleted = await deleteDictionaryTypeApi(currentType.id);
  if (deleted) {
    message.success('字典类型已删除');
    selectedTypeId.value = undefined;
  } else {
    message.warning('该字典类型下有字典项，不能删除');
  }
  await loadDictionaries();
}

async function removeItem(item: DictionaryItem | Record<string, any>) {
  const currentItem = item as DictionaryItem;
  const deleted = await deleteDictionaryItemApi(currentItem.id);
  if (deleted) {
    message.success('字典项已删除');
  }
  await loadDictionaries();
}

function resetTypeSearch() {
  typeNameKeyword.value = '';
  typeCodeKeyword.value = '';
}

function resetItemSearch() {
  itemLabelKeyword.value = '';
}

function selectDictionaryType(record: DictionaryType) {
  selectedTypeId.value = record.id;
  itemLabelKeyword.value = '';
}

function createTypeRow(record: DictionaryType) {
  return {
    onClick: () => selectDictionaryType(record),
  };
}

onMounted(loadDictionaries);
</script>

<template>
  <Page auto-content-height>
    <div class="dictionary-workspace">
      <section class="dictionary-panel">
        <div class="query-bar">
          <Space wrap>
            <span class="query-label">字典名称</span>
            <Input
              v-model:value="typeNameKeyword"
              allow-clear
              class="query-input"
              placeholder="请输入"
            />
            <span class="query-label">字典类型</span>
            <Input
              v-model:value="typeCodeKeyword"
              allow-clear
              class="query-input"
              placeholder="请输入"
            />
          </Space>
          <Space>
            <Button @click="resetTypeSearch">重置</Button>
            <Button type="primary">搜索</Button>
            <Button type="link">收起^</Button>
          </Space>
        </div>

        <div class="table-shell">
          <div class="table-toolbar">
            <h3>字典类型列表</h3>
            <Space>
              <Button @click="loadDictionaries">刷新缓存</Button>
              <Button>导出</Button>
              <Button v-if="canDelete" disabled>删除</Button>
              <Button v-if="canCreate" type="primary" @click="openCreateTypeModal">新增</Button>
            </Space>
          </div>

          <Table
            row-key="id"
            bordered
            size="small"
            :columns="typeColumns"
            :data-source="filteredDictionaryTypes"
            :loading="loading"
            :pagination="{
              pageSize: 10,
              showSizeChanger: true,
              showTotal: (total) => `共 ${total} 条记录`,
            }"
            :custom-row="createTypeRow"
            :row-class-name="(record) => record.id === selectedTypeId ? 'selected-row' : ''"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'state'">
                <Tag :color="record.isEnabled ? 'green' : 'default'">
                  {{ record.isEnabled ? '正常' : '停用' }}
                </Tag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <Space>
                  <Button
                    v-if="canUpdate"
                    class="table-action edit"
                    size="small"
                    @click.stop="openEditTypeModal(record)"
                  >
                    编辑
                  </Button>
                  <Popconfirm
                    v-if="canDelete"
                    title="确认删除该字典类型？"
                    @confirm="removeType(record)"
                  >
                    <Button class="table-action danger" size="small" @click.stop>删除</Button>
                  </Popconfirm>
                  <span v-if="!canUpdate && !canDelete">-</span>
                </Space>
              </template>
            </template>
          </Table>
        </div>
      </section>

      <section class="dictionary-panel">
        <div class="query-bar">
          <Space wrap>
            <span class="query-label">字典标签</span>
            <Input
              v-model:value="itemLabelKeyword"
              allow-clear
              class="query-input"
              placeholder="请输入"
            />
          </Space>
          <Space>
            <Button @click="resetItemSearch">重置</Button>
            <Button type="primary">搜索</Button>
            <Button type="link">收起^</Button>
          </Space>
        </div>

        <div class="table-shell">
          <div class="table-toolbar">
            <h3>字典数据列表{{ selectedType ? ` - ${selectedType.name}` : '' }}</h3>
            <Space>
              <Button>导出</Button>
              <Button v-if="canDelete" disabled>删除</Button>
              <Button
                v-if="canCreate"
                type="primary"
                :disabled="!selectedType"
                @click="openCreateItemModal"
              >
                新增
              </Button>
              <Button @click="loadDictionaries">刷新</Button>
            </Space>
          </div>

          <Table
            row-key="id"
            bordered
            size="small"
            :columns="itemColumns"
            :data-source="filteredDictionaryItems"
            :loading="loading"
            :pagination="{
              pageSize: 10,
              showSizeChanger: true,
              showTotal: (total) => `共 ${total} 条记录`,
            }"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.dataIndex === 'color'">
                <Tag v-if="record.color" :color="record.color">{{ record.label }}</Tag>
                <span v-else>-</span>
              </template>
              <template v-if="column.dataIndex === 'state'">
                <Tag :color="record.isEnabled ? 'green' : 'default'">
                  {{ record.isEnabled ? '正常' : '停用' }}
                </Tag>
              </template>
              <template v-if="column.dataIndex === 'action'">
                <Space>
                  <Button
                    v-if="canUpdate"
                    class="table-action edit"
                    size="small"
                    @click="openEditItemModal(record)"
                  >
                    编辑
                  </Button>
                  <Popconfirm
                    v-if="canDelete"
                    title="确认删除该字典项？"
                    @confirm="removeItem(record)"
                  >
                    <Button class="table-action danger" size="small">删除</Button>
                  </Popconfirm>
                  <span v-if="!canUpdate && !canDelete">-</span>
                </Space>
              </template>
            </template>
          </Table>
        </div>
      </section>
    </div>

    <Modal
      v-model:open="typeModalOpen"
      :confirm-loading="saving"
      :title="typeModalTitle"
      @ok="submitType"
    >
      <Form layout="vertical">
        <FormItem label="字典编码" required>
          <Input v-model:value="typeForm.code" placeholder="例如 user_status" />
        </FormItem>
        <FormItem label="字典名称" required>
          <Input v-model:value="typeForm.name" placeholder="例如 用户状态" />
        </FormItem>
        <FormItem label="排序">
          <InputNumber v-model:value="typeForm.order" class="w-full" />
        </FormItem>
        <FormItem label="启用">
          <Switch v-model:checked="typeForm.isEnabled" />
        </FormItem>
      </Form>
    </Modal>

    <Modal
      v-model:open="itemModalOpen"
      :confirm-loading="saving"
      :title="itemModalTitle"
      @ok="submitItem"
    >
      <Form layout="vertical">
        <FormItem label="选项名称" required>
          <Input v-model:value="itemForm.label" placeholder="例如 启用" />
        </FormItem>
        <FormItem label="选项值" required>
          <Input v-model:value="itemForm.value" placeholder="例如 1" />
        </FormItem>
        <FormItem label="颜色">
          <Input v-model:value="itemForm.color" placeholder="例如 green、blue、default" />
        </FormItem>
        <FormItem label="排序">
          <InputNumber v-model:value="itemForm.order" class="w-full" />
        </FormItem>
        <FormItem label="启用">
          <Switch v-model:checked="itemForm.isEnabled" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.dictionary-workspace {
  display: grid;
  grid-template-columns: minmax(520px, 1fr) minmax(520px, 1fr);
  gap: 12px;
  min-height: calc(100vh - 150px);
}

.dictionary-panel {
  display: flex;
  min-width: 0;
  flex-direction: column;
  gap: 10px;
}

.query-bar {
  display: flex;
  min-height: 64px;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px 12px;
}

.query-label {
  font-weight: 600;
  white-space: nowrap;
}

.query-input {
  width: 260px;
}

.table-shell {
  min-height: 0;
  flex: 1;
  border-radius: 4px;
  background: hsl(var(--background));
  padding: 10px 10px 0;
}

.table-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding-bottom: 10px;
}

.table-toolbar h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
}

.table-action {
  height: 24px;
  padding: 0 8px;
  border-radius: 4px;
  background: transparent;
}

.table-action.edit {
  border-color: #f5b94b;
  color: #d89000;
}

.table-action.danger {
  border-color: #ff6b93;
  color: #ff3868;
}

:deep(.selected-row) > td {
  background: hsl(var(--accent));
}

:deep(.ant-table-tbody > tr) {
  cursor: pointer;
}

:deep(.ant-table-wrapper) {
  height: calc(100% - 42px);
}

:deep(.ant-table) {
  font-size: 13px;
}

:deep(.ant-table-thead > tr > th) {
  font-weight: 600;
}

:deep(.ant-pagination) {
  margin: 10px 0;
}

@media (max-width: 1200px) {
  .dictionary-workspace {
    grid-template-columns: 1fr;
  }
}
</style>
