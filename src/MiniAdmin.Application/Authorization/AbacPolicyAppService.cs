using MiniAdmin.Application.Contracts.Authorization;
using MiniAdmin.Platform.Authorization;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Application.Authorization;

[DynamicApi("platform/abac-policies", Name = "AbacPolicies", Tag = "访问控制")]
public sealed class AbacPolicyAppService(IAbacPolicyRepository repository)
{
    [DynamicGet(
        Permission = "platform:abac:query",
        Resource = "platform.abac-policy",
        Action = "query",
        OperationId = "GetAbacPolicies",
        Summary = "查询 ABAC 策略")]
    public Task<IReadOnlyList<AbacPolicyDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        return repository.GetListAsync(cancellationToken);
    }

    [DynamicPost(
        Permission = "platform:abac:create",
        Resource = "platform.abac-policy",
        Action = "create",
        OperationId = "CreateAbacPolicy",
        Summary = "创建 ABAC 策略")]
    public Task<AbacPolicyDto> CreateAsync(
        SaveAbacPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        return repository.CreateAsync(request, cancellationToken);
    }

    [DynamicPut(
        "{id:guid}",
        Permission = "platform:abac:update",
        Resource = "platform.abac-policy",
        Action = "update",
        OperationId = "UpdateAbacPolicy",
        Summary = "更新 ABAC 策略")]
    public Task<AbacPolicyDto?> UpdateAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid id,
        SaveAbacPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        return repository.UpdateAsync(id, request, cancellationToken);
    }

    [DynamicDelete(
        "{id:guid}",
        Permission = "platform:abac:delete",
        Resource = "platform.abac-policy",
        Action = "delete",
        OperationId = "DeleteAbacPolicy",
        Summary = "删除 ABAC 策略")]
    public Task<bool> DeleteAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid id,
        CancellationToken cancellationToken = default)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }

    private static void Validate(SaveAbacPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            string.IsNullOrWhiteSpace(request.Resource) ||
            string.IsNullOrWhiteSpace(request.Action))
        {
            throw new InvalidOperationException("策略名称、资源和动作不能为空。");
        }

        if (request.SubjectType is not ("Any" or "User" or "Role" or "Application"))
        {
            throw new InvalidOperationException("主体类型只支持 Any、User、Role 或 Application。");
        }

        if (request.SubjectType != "Any" && string.IsNullOrWhiteSpace(request.SubjectId))
        {
            throw new InvalidOperationException("用户或角色策略必须指定主体标识。");
        }

        if (request.Effect is not ("Allow" or "Deny"))
        {
            throw new InvalidOperationException("策略效果只支持 Allow 或 Deny。");
        }

        AbacConditionEvaluator.Validate(request.ConditionsJson);
    }
}
