<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { useAccessStore } from '@vben/stores';

import {
  Avatar,
  Badge,
  Button,
  Empty,
  Input,
  Skeleton,
  Tag,
  Textarea,
  message,
} from 'ant-design-vue';
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr';

import {
  type ChatContact,
  type ChatConversation,
  type ChatMessage,
  getChatContactsApi,
  getChatConversationsApi,
  getChatMessagesApi,
  markChatConversationReadApi,
  sendChatMessageApi,
} from '#/api/message/chat';

const accessStore = useAccessStore();
const conversations = ref<ChatConversation[]>([]);
const contacts = ref<ChatContact[]>([]);
const messages = ref<ChatMessage[]>([]);
const activeConversationId = ref('');
const activeContact = ref<ChatContact>();
const contactKeyword = ref('');
const draft = ref('');
const loading = ref(false);
const messageLoading = ref(false);
const sending = ref(false);
const connectionStatus = ref<'connected' | 'connecting' | 'offline'>('connecting');
const messageListRef = ref<HTMLElement>();
let connection: HubConnection | undefined;

function initials(name: string) {
  return name.trim().slice(0, 1).toUpperCase();
}

function formatConversationTime(value?: null | string) {
  if (!value) return '';
  const date = new Date(value);
  const today = new Date();
  if (date.toDateString() === today.toDateString()) {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleDateString();
}

function formatMessageTime(value: string) {
  return new Date(value).toLocaleString([], {
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    month: '2-digit',
  });
}

function appendMessage(item: ChatMessage) {
  if (messages.value.some((messageItem) => messageItem.id === item.id)) return;
  messages.value.push(item);
  messages.value.sort(
    (left, right) =>
      new Date(left.createdAt).getTime() - new Date(right.createdAt).getTime(),
  );
  void scrollToBottom();
}

async function scrollToBottom() {
  await nextTick();
  const element = messageListRef.value;
  if (element) element.scrollTop = element.scrollHeight;
}

async function loadConversations() {
  conversations.value = await getChatConversationsApi();
}

async function loadContacts() {
  contacts.value = await getChatContactsApi(contactKeyword.value.trim());
}

async function loadInitialData() {
  loading.value = true;
  try {
    await Promise.all([loadConversations(), loadContacts()]);
    const first = conversations.value[0];
    if (first && !activeContact.value) await selectConversation(first);
  } finally {
    loading.value = false;
  }
}

async function markRead(conversationId: string) {
  await markChatConversationReadApi(conversationId);
  const conversation = conversations.value.find(
    (item) => item.id === conversationId,
  );
  if (conversation) conversation.unreadCount = 0;
}

async function selectConversation(item: ChatConversation) {
  activeConversationId.value = item.id;
  activeContact.value = item.otherUser;
  messageLoading.value = true;
  try {
    messages.value = await getChatMessagesApi(item.id);
    await scrollToBottom();
    if (item.unreadCount > 0) await markRead(item.id);
  } finally {
    messageLoading.value = false;
  }
}

function selectContact(item: ChatContact) {
  const existing = conversations.value.find(
    (conversation) => conversation.otherUser.id === item.id,
  );
  if (existing) {
    void selectConversation(existing);
    return;
  }
  activeConversationId.value = '';
  activeContact.value = item;
  messages.value = [];
  void scrollToBottom();
}

async function sendMessage() {
  const content = draft.value.trim();
  if (!content || !activeContact.value || sending.value) return;

  sending.value = true;
  try {
    const result = await sendChatMessageApi(activeContact.value.id, content);
    draft.value = '';
    activeConversationId.value = result.conversationId;
    appendMessage(result);
    await loadConversations();
  } finally {
    sending.value = false;
  }
}

function resolveHubUrl() {
  const apiUrl = String(import.meta.env.VITE_GLOB_API_URL || '/api').replace(/\/$/, '');
  return apiUrl.startsWith('http')
    ? `${apiUrl}/hubs/chat`
    : `${window.location.origin}${apiUrl}/hubs/chat`;
}

async function connectSignalR() {
  if (!accessStore.accessToken) {
    connectionStatus.value = 'offline';
    return;
  }

  connection = new HubConnectionBuilder()
    .withUrl(resolveHubUrl(), {
      accessTokenFactory: () => accessStore.accessToken ?? '',
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000])
    .configureLogging(LogLevel.Warning)
    .build();

  connection.on('MessageReceived', async (item: ChatMessage) => {
    const selectedUserId = activeContact.value?.id;
    const belongsToActiveContact =
      selectedUserId === item.senderId || selectedUserId === item.receiverId;
    if (belongsToActiveContact) {
      activeConversationId.value = item.conversationId;
      appendMessage(item);
      if (item.senderId === selectedUserId) await markRead(item.conversationId);
    }
    await loadConversations();
  });

  connection.on('MessagesRead', (receipt: { conversationId: string; readAt: string }) => {
    if (receipt.conversationId !== activeConversationId.value) return;
    messages.value = messages.value.map((item) =>
      item.readAt || item.senderId === activeContact.value?.id
        ? item
        : { ...item, readAt: receipt.readAt },
    );
  });

  connection.onreconnecting(() => {
    connectionStatus.value = 'connecting';
  });
  connection.onreconnected(() => {
    connectionStatus.value = 'connected';
    void loadConversations();
  });
  connection.onclose(() => {
    connectionStatus.value = 'offline';
  });

  try {
    await connection.start();
    connectionStatus.value = 'connected';
  } catch {
    connectionStatus.value = 'offline';
    message.warning('实时连接暂不可用，历史消息和发送功能仍可使用');
  }
}

onMounted(async () => {
  await loadInitialData();
  await connectSignalR();
});

onBeforeUnmount(async () => {
  if (connection && connection.state !== HubConnectionState.Disconnected) {
    await connection.stop();
  }
});
</script>

<template>
  <Page
    description="租户内点对点沟通，消息历史持久化，并通过 SignalR 实时推送消息与已读回执。"
    title="在线聊天"
  >
    <div class="chat-shell">
      <aside class="conversation-panel">
        <div class="panel-heading">
          <div>
            <strong>消息</strong>
            <Tag
              :color="
                connectionStatus === 'connected'
                  ? 'success'
                  : connectionStatus === 'connecting'
                    ? 'processing'
                    : 'default'
              "
            >
              {{
                connectionStatus === 'connected'
                  ? '实时在线'
                  : connectionStatus === 'connecting'
                    ? '连接中'
                    : '轮询模式'
              }}
            </Tag>
          </div>
          <Button size="small" type="text" @click="loadInitialData">刷新</Button>
        </div>

        <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />
        <template v-else>
          <div v-if="conversations.length" class="conversation-list">
            <button
              v-for="item in conversations"
              :key="item.id"
              :class="['conversation-item', { active: activeConversationId === item.id }]"
              type="button"
              @click="selectConversation(item)"
            >
              <Badge :count="item.unreadCount" :offset="[-2, 4]">
                <Avatar class="contact-avatar">{{ initials(item.otherUser.realName) }}</Avatar>
              </Badge>
              <span class="conversation-copy">
                <span class="conversation-title">
                  <strong>{{ item.otherUser.realName }}</strong>
                  <time>{{ formatConversationTime(item.updatedAt) }}</time>
                </span>
                <span class="conversation-preview">
                  {{ item.lastMessage?.content || '开始新的对话' }}
                </span>
              </span>
            </button>
          </div>

          <div class="contact-section">
            <span class="section-label">发起新对话</span>
            <Input.Search
              v-model:value="contactKeyword"
              allow-clear
              placeholder="搜索姓名或账号"
              @search="loadContacts"
            />
            <div class="contact-list">
              <button
                v-for="item in contacts"
                :key="item.id"
                :class="['contact-item', { active: activeContact?.id === item.id }]"
                type="button"
                @click="selectContact(item)"
              >
                <Avatar size="small">{{ initials(item.realName) }}</Avatar>
                <span>
                  <strong>{{ item.realName }}</strong>
                  <small>@{{ item.userName }}</small>
                </span>
              </button>
            </div>
          </div>
        </template>
      </aside>

      <main class="message-panel">
        <template v-if="activeContact">
          <header class="message-heading">
            <Avatar class="contact-avatar">{{ initials(activeContact.realName) }}</Avatar>
            <div>
              <strong>{{ activeContact.realName }}</strong>
              <span>@{{ activeContact.userName }}</span>
            </div>
          </header>

          <div ref="messageListRef" class="message-list">
            <Skeleton v-if="messageLoading" active :paragraph="{ rows: 5 }" />
            <Empty
              v-else-if="messages.length === 0"
              description="还没有消息，发一句问候吧"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />
            <div
              v-for="item in messages"
              v-else
              :key="item.id"
              :class="['message-row', { mine: item.senderId !== activeContact.id }]"
            >
              <div class="message-bubble">
                <p>{{ item.content }}</p>
                <span>
                  {{ formatMessageTime(item.createdAt) }}
                  <template v-if="item.senderId !== activeContact.id">
                    · {{ item.readAt ? '已读' : '未读' }}
                  </template>
                </span>
              </div>
            </div>
          </div>

          <footer class="composer">
            <Textarea
              v-model:value="draft"
              :auto-size="{ minRows: 2, maxRows: 5 }"
              :maxlength="2000"
              placeholder="输入消息，Enter 发送，Shift + Enter 换行"
              @keydown.enter.exact.prevent="sendMessage"
            />
            <div class="composer-footer">
              <span>{{ draft.length }}/2000</span>
              <Button
                :disabled="!draft.trim()"
                :loading="sending"
                type="primary"
                @click="sendMessage"
              >
                发送
              </Button>
            </div>
          </footer>
        </template>
        <Empty v-else class="empty-chat" description="选择联系人开始对话" />
      </main>
    </div>
  </Page>
</template>

<style scoped>
.chat-shell {
  display: grid;
  grid-template-columns: minmax(260px, 320px) minmax(0, 1fr);
  height: calc(100vh - 220px);
  min-height: 580px;
  overflow: hidden;
  border: 1px solid hsl(var(--border));
  border-radius: 16px;
  background: hsl(var(--card));
}

.conversation-panel {
  display: flex;
  min-width: 0;
  overflow: hidden;
  border-right: 1px solid hsl(var(--border));
  flex-direction: column;
}

.panel-heading,
.message-heading {
  display: flex;
  align-items: center;
  min-height: 68px;
  padding: 14px 18px;
  border-bottom: 1px solid hsl(var(--border));
}

.panel-heading {
  justify-content: space-between;
}

.panel-heading > div {
  display: flex;
  align-items: center;
  gap: 10px;
}

.conversation-list,
.contact-list {
  display: grid;
  gap: 3px;
}

.conversation-list {
  max-height: 44%;
  padding: 8px;
  overflow-y: auto;
}

.conversation-item,
.contact-item {
  display: flex;
  width: 100%;
  gap: 12px;
  align-items: center;
  padding: 11px;
  color: inherit;
  text-align: left;
  cursor: pointer;
  border: 0;
  border-radius: 11px;
  background: transparent;
}

.conversation-item:hover,
.contact-item:hover,
.conversation-item.active,
.contact-item.active {
  background: hsl(var(--accent));
}

.conversation-copy,
.contact-item span {
  display: grid;
  min-width: 0;
  flex: 1;
}

.conversation-title {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.conversation-title time,
.conversation-preview,
.contact-item small,
.message-heading span,
.composer-footer span {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.conversation-preview {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.contact-section {
  display: grid;
  min-height: 0;
  gap: 10px;
  padding: 14px;
  overflow: hidden;
  border-top: 1px solid hsl(var(--border));
}

.section-label {
  color: hsl(var(--muted-foreground));
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.08em;
}

.contact-list {
  overflow-y: auto;
}

.message-panel {
  display: flex;
  min-width: 0;
  background:
    radial-gradient(circle at 100% 0%, color-mix(in srgb, #0ea5e9 8%, transparent), transparent 34%),
    hsl(var(--background));
  flex-direction: column;
}

.message-heading {
  gap: 12px;
  background: hsl(var(--card));
}

.message-heading div {
  display: grid;
}

.contact-avatar {
  color: #075985;
  background: #e0f2fe;
}

.message-list {
  display: flex;
  min-height: 0;
  padding: 24px;
  overflow-y: auto;
  flex: 1;
  flex-direction: column;
  gap: 12px;
}

.message-row {
  display: flex;
  justify-content: flex-start;
}

.message-row.mine {
  justify-content: flex-end;
}

.message-bubble {
  max-width: min(72%, 680px);
  padding: 10px 13px 8px;
  border: 1px solid hsl(var(--border));
  border-radius: 5px 15px 15px;
  background: hsl(var(--card));
  box-shadow: 0 4px 16px rgb(15 23 42 / 5%);
}

.mine .message-bubble {
  color: white;
  border-color: #0284c7;
  border-radius: 15px 5px 15px 15px;
  background: #0284c7;
}

.message-bubble p {
  margin: 0;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.message-bubble span {
  display: block;
  margin-top: 5px;
  color: hsl(var(--muted-foreground));
  font-size: 10px;
  text-align: right;
}

.mine .message-bubble span {
  color: rgb(255 255 255 / 72%);
}

.composer {
  display: grid;
  gap: 8px;
  padding: 14px 18px;
  border-top: 1px solid hsl(var(--border));
  background: hsl(var(--card));
}

.composer-footer {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 12px;
}

.empty-chat {
  display: grid;
  flex: 1;
  place-content: center;
}

@media (max-width: 800px) {
  .chat-shell {
    grid-template-columns: 220px minmax(0, 1fr);
  }
}

@media (max-width: 620px) {
  .chat-shell {
    grid-template-columns: 1fr;
    height: auto;
  }

  .conversation-panel {
    max-height: 360px;
    border-right: 0;
    border-bottom: 1px solid hsl(var(--border));
  }

  .message-panel {
    min-height: 540px;
  }
}
</style>
