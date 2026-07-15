using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.Chat;

public sealed class ChatPageDefinitionProvider : IPageDefinitionProvider
{
    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition(
            Key: "message.chat",
            ParentKey: null,
            Path: "/message/chat",
            Component: "/message/chat/index",
            Redirect: null,
            Icon: "lucide:messages-square",
            Order: 320,
            I18nKey: "page.message.chat.title",
            Title: new LocalizedText("在线聊天", "Online Chat"),
            IsVisible: true,
            Permissions:
            [
                new PermissionDefinition(
                    "message:chat:query",
                    "message.chat",
                    "query",
                    "permission.message.chat.query",
                    new LocalizedText("查看聊天", "View chat")),
                new PermissionDefinition(
                    "message:chat:send",
                    "message.chat",
                    "send",
                    "permission.message.chat.send",
                    new LocalizedText("发送聊天消息", "Send chat messages")),
                new PermissionDefinition(
                    "message:chat:read",
                    "message.chat",
                    "read",
                    "permission.message.chat.read",
                    new LocalizedText("更新聊天已读状态", "Update chat read state"))
            ]);
    }
}
