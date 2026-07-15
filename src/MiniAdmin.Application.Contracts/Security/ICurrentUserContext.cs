namespace MiniAdmin.Application.Contracts.Security;

public interface ICurrentUserContext
{
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    string UserName { get; }

    Guid? TenantId { get; }

    string? TenantCode { get; }
}
