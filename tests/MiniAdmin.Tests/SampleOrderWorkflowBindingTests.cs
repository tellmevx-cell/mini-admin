using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.SampleOrders;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Application.SampleOrders;
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Application.Workflows;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Notifications;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class SampleOrderWorkflowBindingTests
{
    [Fact]
    public async Task Submit_And_Approve_Workflow_Updates_Sample_Order_Status()
    {
        await using var dbContext = CreateDbContext();
        var starterId = Guid.Parse("d1000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d1000000-0000-0000-0000-000000000002");
        var roleId = Guid.Parse("d2000000-0000-0000-0000-000000000001");
        await SeedWorkflowUsersAsync(dbContext, starterId, approverId, roleId);

        var workflowService = CreateWorkflowService(dbContext);
        var sampleOrderService = CreateSampleOrderService(dbContext, workflowService);
        var definition = await CreateRoleWorkflowDefinitionAsync(workflowService, roleId);
        var order = await sampleOrderService.CreateAsync(CreateOrderRequest());

        var submitted = await sampleOrderService.SubmitWorkflowAsync(
            Guid.Parse(order.Id),
            new SubmitSampleOrderWorkflowRequest(Guid.Parse(definition.Id), "提交采购审批"),
            new WorkflowUserContext(starterId, "starter"));

        Assert.NotNull(submitted);
        Assert.Equal(SampleOrder.PendingApprovalStatus, submitted.Status);
        Assert.NotNull(submitted.WorkflowInstanceId);

        await workflowService.ApproveAsync(
            Guid.Parse(submitted.WorkflowInstanceId!),
            new WorkflowActionRequest("同意"),
            new WorkflowUserContext(approverId, "approver"));

        var approvedOrder = await dbContext.Set<SampleOrder>().SingleAsync(x => x.Id == Guid.Parse(order.Id));
        Assert.Equal(SampleOrder.ApprovedStatus, approvedOrder.Status);
    }

    [Fact]
    public async Task Reject_Workflow_Updates_Sample_Order_Status()
    {
        await using var dbContext = CreateDbContext();
        var starterId = Guid.Parse("e1000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("e1000000-0000-0000-0000-000000000002");
        var roleId = Guid.Parse("e2000000-0000-0000-0000-000000000001");
        await SeedWorkflowUsersAsync(dbContext, starterId, approverId, roleId);

        var workflowService = CreateWorkflowService(dbContext);
        var sampleOrderService = CreateSampleOrderService(dbContext, workflowService);
        var definition = await CreateRoleWorkflowDefinitionAsync(workflowService, roleId);
        var order = await sampleOrderService.CreateAsync(CreateOrderRequest());
        var submitted = await sampleOrderService.SubmitWorkflowAsync(
            Guid.Parse(order.Id),
            new SubmitSampleOrderWorkflowRequest(Guid.Parse(definition.Id), "提交采购审批"),
            new WorkflowUserContext(starterId, "starter"));

        Assert.NotNull(submitted);
        await workflowService.RejectAsync(
            Guid.Parse(submitted.WorkflowInstanceId!),
            new WorkflowActionRequest("资料不完整"),
            new WorkflowUserContext(approverId, "approver"));

        var rejectedOrder = await dbContext.Set<SampleOrder>().SingleAsync(x => x.Id == Guid.Parse(order.Id));
        Assert.Equal(SampleOrder.RejectedStatus, rejectedOrder.Status);
    }

    [Fact]
    public async Task Withdraw_Workflow_Updates_Sample_Order_Status()
    {
        await using var dbContext = CreateDbContext();
        var starterId = Guid.Parse("f1000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f1000000-0000-0000-0000-000000000002");
        var roleId = Guid.Parse("f2000000-0000-0000-0000-000000000001");
        await SeedWorkflowUsersAsync(dbContext, starterId, approverId, roleId);

        var workflowService = CreateWorkflowService(dbContext);
        var sampleOrderService = CreateSampleOrderService(dbContext, workflowService);
        var definition = await CreateRoleWorkflowDefinitionAsync(workflowService, roleId);
        var order = await sampleOrderService.CreateAsync(CreateOrderRequest());
        var submitted = await sampleOrderService.SubmitWorkflowAsync(
            Guid.Parse(order.Id),
            new SubmitSampleOrderWorkflowRequest(Guid.Parse(definition.Id), "提交采购审批"),
            new WorkflowUserContext(starterId, "starter"));

        Assert.NotNull(submitted);
        await workflowService.WithdrawAsync(
            Guid.Parse(submitted.WorkflowInstanceId!),
            new WorkflowActionRequest("重新调整金额"),
            new WorkflowUserContext(starterId, "starter"));

        var withdrawnOrder = await dbContext.Set<SampleOrder>().SingleAsync(x => x.Id == Guid.Parse(order.Id));
        Assert.Equal(SampleOrder.WithdrawnStatus, withdrawnOrder.Status);
    }

    [Fact]
    public async Task Pending_And_Approved_Sample_Order_Cannot_Be_Edited_Or_Deleted()
    {
        await using var dbContext = CreateDbContext();
        var pendingOrderId = Guid.Parse("fa000000-0000-0000-0000-000000000001");
        var approvedOrderId = Guid.Parse("fa000000-0000-0000-0000-000000000002");
        dbContext.Set<SampleOrder>().AddRange(
            new SampleOrder
            {
                Id = pendingOrderId,
                OriginalName = "Pending",
                StoredName = "PO-001",
                ContentType = "Purchase",
                Size = 100,
                StorageProvider = "Manual",
                StoragePath = "pending",
                Status = SampleOrder.PendingApprovalStatus
            },
            new SampleOrder
            {
                Id = approvedOrderId,
                OriginalName = "Approved",
                StoredName = "PO-002",
                ContentType = "Purchase",
                Size = 200,
                StorageProvider = "Manual",
                StoragePath = "approved",
                Status = SampleOrder.ApprovedStatus
            });
        await dbContext.SaveChangesAsync();

        var sampleOrderService = CreateSampleOrderService(dbContext, CreateWorkflowService(dbContext));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sampleOrderService.UpdateAsync(pendingOrderId, CreateOrderRequest()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sampleOrderService.UpdateAsync(approvedOrderId, CreateOrderRequest()));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sampleOrderService.DeleteAsync(pendingOrderId));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sampleOrderService.DeleteAsync(approvedOrderId));
    }

    private static async Task SeedWorkflowUsersAsync(
        MiniAdminDbContext dbContext,
        Guid starterId,
        Guid approverId,
        Guid roleId)
    {
        dbContext.Users.AddRange(
            new User
            {
                Id = starterId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "approver",
                RealName = "Approver",
                PasswordHash = "hash"
            });
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "sample_order_approver",
            Name = "Sample Order Approver"
        });
        dbContext.UserRoles.Add(new UserRole
        {
            UserId = approverId,
            RoleId = roleId
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<WorkflowDefinitionDto> CreateRoleWorkflowDefinitionAsync(
        WorkflowAppService workflowService,
        Guid roleId)
    {
        var draftDefinition = await workflowService.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "sample_order_apply",
            "示例订单审批",
            "示例订单",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "Role",
                    null,
                    roleId,
                    1,
                    true)
            ]));
        return Assert.IsType<WorkflowDefinitionDto>(
            await workflowService.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id)));
    }

    private static SaveSampleOrderRequest CreateOrderRequest()
    {
        return new SaveSampleOrderRequest(
            "采购服务器",
            "PO-20260602-001",
            "采购申请",
            6800,
            "手工录入",
            "研发部申请采购测试服务器",
            SampleOrder.DraftStatus);
    }

    private static SampleOrderAppService CreateSampleOrderService(
        MiniAdminDbContext dbContext,
        WorkflowAppService workflowService)
    {
        return new SampleOrderAppService(
            new EfSampleOrderRepository(dbContext, new TestCurrentTenant()),
            workflowService);
    }

    private static WorkflowAppService CreateWorkflowService(MiniAdminDbContext dbContext)
    {
        var currentTenant = new TestCurrentTenant();
        var templateRepository = new EfNotificationTemplateRepository(
            dbContext,
            currentTenant,
            new TestPlatformCache());
        return new WorkflowAppService(
            new EfWorkflowRepository(
                dbContext,
                currentTenant,
                new ScribanNotificationTemplateRenderer(templateRepository),
                CreateNotificationDeliveryService(dbContext, currentTenant)),
            [new SampleOrderWorkflowStateHandler(dbContext, currentTenant)]);
    }

    private static NotificationDeliveryService CreateNotificationDeliveryService(
        MiniAdminDbContext dbContext,
        ICurrentTenant currentTenant)
    {
        return new NotificationDeliveryService(
            dbContext,
            new StubEmailNotificationSender(),
            Options.Create(new EmailNotificationOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "notice@example.com"
            }),
            new StubWebhookNotificationSender(),
            Options.Create(new WebhookNotificationOptions()),
            currentTenant);
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MiniAdminDbContext(options);
    }

    private sealed class TestCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;

        public string? TenantCode => null;

        public bool IsPlatform => true;

        public bool IsTenant => false;
    }

    private sealed class StubEmailNotificationSender : IEmailNotificationSender
    {
        public Task SendAsync(
            string to,
            string subject,
            string content,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubWebhookNotificationSender : IWebhookNotificationSender
    {
        public Task SendAsync(
            string endpointUrl,
            string payloadJson,
            string? secret,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
