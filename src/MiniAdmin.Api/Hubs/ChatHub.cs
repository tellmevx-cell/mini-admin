using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MiniAdmin.Application.Contracts.Chat;
using MiniAdmin.Platform.AspNetCore.Authorization;
using MiniAdmin.Platform.Authorization;

namespace MiniAdmin.Api.Hubs;

public interface IChatHubClient
{
    Task MessageReceived(ChatMessageDto message);

    Task MessagesRead(ChatReadReceiptDto receipt);
}

[Authorize]
public sealed class ChatHub(
    IChatAppService chatAppService,
    IAuthorizationDecisionService authorizationDecisionService) : Hub<IChatHubClient>
{
    public async Task<ChatMessageDto> SendMessage(
        Guid receiverId,
        string content,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(
            "message:chat:send",
            "send",
            cancellationToken);
        return await chatAppService.SendAsync(
            new SendChatMessageRequest(receiverId, content),
            cancellationToken);
    }

    public async Task<ChatReadReceiptDto> MarkRead(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthorizedAsync(
            "message:chat:read",
            "read",
            cancellationToken);
        return await chatAppService.MarkReadAsync(conversationId, cancellationToken);
    }

    private async Task EnsureAuthorizedAsync(
        string permission,
        string action,
        CancellationToken cancellationToken)
    {
        var httpContext = Context.GetHttpContext()
            ?? throw new HubException("请求上下文不可用。");
        var decision = await authorizationDecisionService.AuthorizeAsync(
            new AuthorizationRequest(
                Context.User!,
                permission,
                "message.chat",
                action,
                AuthorizationRequestAttributeFactory.Create(httpContext)),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw new HubException("没有执行该聊天操作的权限。");
        }
    }
}
