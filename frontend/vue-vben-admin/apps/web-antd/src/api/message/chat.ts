import { requestClient } from '#/api/request';

const rawResponse = { responseReturn: 'body' as const };

export interface ChatContact {
  id: string;
  realName: string;
  userName: string;
}

export interface ChatMessage {
  content: string;
  conversationId: string;
  createdAt: string;
  id: string;
  readAt?: null | string;
  receiverId: string;
  receiverName: string;
  senderId: string;
  senderName: string;
}

export interface ChatConversation {
  id: string;
  lastMessage?: ChatMessage | null;
  otherUser: ChatContact;
  unreadCount: number;
  updatedAt: string;
}

export interface ChatReadReceipt {
  conversationId: string;
  readAt: string;
  readCount: number;
  readerId: string;
}

export async function getChatContactsApi(keyword?: string) {
  return requestClient.get<ChatContact[]>('/message/chat/contacts', {
    ...rawResponse,
    params: { keyword: keyword || undefined, take: 100 },
  });
}

export async function getChatConversationsApi() {
  return requestClient.get<ChatConversation[]>('/message/chat/conversations', {
    ...rawResponse,
    params: { take: 100 },
  });
}

export async function getChatMessagesApi(conversationId: string) {
  return requestClient.get<ChatMessage[]>(
    `/message/chat/conversations/${conversationId}/messages`,
    {
      ...rawResponse,
      params: { take: 100 },
    },
  );
}

export async function sendChatMessageApi(receiverId: string, content: string) {
  return requestClient.post<ChatMessage>(
    '/message/chat/messages',
    { content, receiverId },
    rawResponse,
  );
}

export async function markChatConversationReadApi(conversationId: string) {
  return requestClient.post<ChatReadReceipt>(
    `/message/chat/conversations/${conversationId}/read`,
    undefined,
    rawResponse,
  );
}
