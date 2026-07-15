using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Application.Workflows;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Notifications;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class WorkflowAppServiceTests
{
    [Fact]
    public async Task Starts_Instance_And_Approves_Role_Node()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1000000-0000-0000-0000-000000000002");
        var roleId = Guid.Parse("a2000000-0000-0000-0000-000000000001");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
            Code = "wf_approver",
            Name = "Workflow Approver"
        });
        dbContext.UserRoles.Add(new UserRole
        {
            UserId = approverId,
            RoleId = roleId
        });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowTask",
            "模板待办：{instanceTitle}",
            "模板待办 {definitionName} {nodeName} {approverUserName}");
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowApprove",
            "模板通过：{instanceTitle}",
            "模板通过 {operatorUserName} {comment} {businessKey}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_apply",
            "Leave Apply",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "Role",
                    null,
                    roleId,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-001",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));
        var todoTasks = await service.GetTodoTasksAsync(new WorkflowUserContext(approverId, "approver"));

        var task = Assert.Single(todoTasks);
        Assert.Equal(instance.Id, task.InstanceId);
        Assert.Equal("Pending", task.Status);
        var approverNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == approverId &&
                x.SourceType == "WorkflowTask"));
        Assert.Equal("模板待办：Annual leave", approverNotification.Title);
        Assert.Equal("模板待办 Leave Apply Manager Approve approver", approverNotification.Message);
        Assert.Equal($"/workflow/center?workflowInstanceId={instance.Id}&workflowTaskId={task.Id}", approverNotification.Link);

        var approved = await service.ApproveAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("ok"),
            new WorkflowUserContext(approverId, "approver"));

        Assert.NotNull(approved);
        Assert.Equal("Approved", approved.Status);
        Assert.Null(approved.CurrentNodeId);
        Assert.Contains(approved.ActionLogs, log => log.Action == "Approve");
        var initiatorNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == initiatorId &&
                x.SourceType == "WorkflowApprove"));
        Assert.Equal("模板通过：Annual leave", initiatorNotification.Title);
        Assert.Equal("模板通过 approver ok LEAVE-001", initiatorNotification.Message);
        Assert.Equal($"/workflow/center?workflowInstanceId={instance.Id}", initiatorNotification.Link);
    }

    [Fact]
    public async Task Workflow_Task_Notification_Policy_Can_Disable_InApp_Message()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1050000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1050000-0000-0000-0000-000000000002");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "approver");
        dbContext.NotificationPolicies.Add(new NotificationPolicy
        {
            Id = Guid.Parse("a1050000-0000-0000-0000-000000000101"),
            EventCode = "WorkflowTask",
            EventName = "审批待办",
            Category = "Workflow",
            RecipientStrategy = "WorkflowDefault",
            EnableInApp = false,
            EnableEmail = false,
            EnableWebhook = false,
            IsEnabled = true,
            Remark = "测试关闭待办站内信"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var instance = await StartUserWorkflowAsync(
            service,
            "policy_task_disabled",
            "Policy task disabled",
            initiatorId,
            "starter",
            approverId);

        Assert.NotEmpty(instance.Tasks);
        Assert.Empty(dbContext.UserNotifications.Where(x =>
            x.UserId == approverId &&
            x.SourceType == "WorkflowTask"));
    }

    [Fact]
    public async Task Workflow_Task_Email_Policy_Can_Create_Email_Without_InApp_Message()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1060000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1060000-0000-0000-0000-000000000002");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "approver", "approver@example.com");
        dbContext.NotificationPolicies.Add(new NotificationPolicy
        {
            Id = Guid.Parse("a1060000-0000-0000-0000-000000000101"),
            EventCode = "WorkflowTask",
            EventName = "审批待办",
            Category = "Workflow",
            RecipientStrategy = "WorkflowDefault",
            EnableInApp = false,
            EnableEmail = true,
            EnableWebhook = false,
            IsEnabled = true,
            Remark = "测试关闭站内信但开启邮件"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var instance = await StartUserWorkflowAsync(
            service,
            "policy_task_email_enabled",
            "Policy task email enabled",
            initiatorId,
            "starter",
            approverId);

        var task = Assert.Single(instance.Tasks);
        Assert.Empty(dbContext.UserNotifications.Where(x =>
            x.UserId == approverId &&
            x.SourceType == "WorkflowTask"));
        var delivery = Assert.Single(dbContext.NotificationDeliveries.Where(x =>
            x.UserId == approverId &&
            x.Channel == "Email" &&
            x.SourceType == "WorkflowTask"));
        Assert.Equal(task.Id, delivery.SourceId);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal("approver@example.com", delivery.RecipientAddress);
    }

    [Fact]
    public async Task Workflow_Task_Notification_Subscription_Can_Override_User_Channels()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1061000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1061000-0000-0000-0000-000000000002");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "approver", "approver@example.com");
        dbContext.NotificationPolicies.Add(new NotificationPolicy
        {
            Id = Guid.Parse("a1061000-0000-0000-0000-000000000101"),
            EventCode = "WorkflowTask",
            EventName = "审批待办",
            Category = "Workflow",
            RecipientStrategy = "WorkflowDefault",
            EnableInApp = true,
            EnableEmail = true,
            EnableWebhook = false,
            IsEnabled = true,
            Remark = "测试全局开启站内信与邮件"
        });
        dbContext.NotificationSubscriptions.Add(new NotificationSubscription
        {
            Id = Guid.Parse("a1061000-0000-0000-0000-000000000201"),
            UserId = approverId,
            EventCode = "WorkflowTask",
            EnableInApp = false,
            EnableEmail = true,
            EnableWebhook = false,
            IsEnabled = true
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var instance = await StartUserWorkflowAsync(
            service,
            "subscription_task_email_only",
            "Subscription task email only",
            initiatorId,
            "starter",
            approverId);

        var task = Assert.Single(instance.Tasks);
        Assert.Empty(dbContext.UserNotifications.Where(x =>
            x.UserId == approverId &&
            x.SourceType == "WorkflowTask"));
        var delivery = Assert.Single(dbContext.NotificationDeliveries.Where(x =>
            x.UserId == approverId &&
            x.Channel == "Email" &&
            x.SourceType == "WorkflowTask"));
        Assert.Equal(task.Id, delivery.SourceId);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal("approver@example.com", delivery.RecipientAddress);
    }

    [Fact]
    public async Task Workflow_Task_Webhook_Policy_Can_Create_Webhook_Without_InApp_Message()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1070000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1070000-0000-0000-0000-000000000002");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "approver");
        dbContext.NotificationPolicies.Add(new NotificationPolicy
        {
            Id = Guid.Parse("a1070000-0000-0000-0000-000000000101"),
            EventCode = "WorkflowTask",
            EventName = "审批待办",
            Category = "Workflow",
            RecipientStrategy = "WorkflowDefault",
            EnableInApp = false,
            EnableEmail = false,
            EnableWebhook = true,
            IsEnabled = true,
            Remark = "测试关闭站内信但开启 Webhook"
        });
        await dbContext.SaveChangesAsync();

        var webhookSender = new StubWebhookNotificationSender();
        var service = CreateService(
            dbContext,
            webhookSender,
            webhookEnabled: true,
            webhookEndpointUrl: "https://hooks.example.com/mini-admin");
        var instance = await StartUserWorkflowAsync(
            service,
            "policy_task_webhook_enabled",
            "Policy task webhook enabled",
            initiatorId,
            "starter",
            approverId);

        var task = Assert.Single(instance.Tasks);
        Assert.Empty(dbContext.UserNotifications.Where(x =>
            x.UserId == approverId &&
            x.SourceType == "WorkflowTask"));
        var delivery = Assert.Single(dbContext.NotificationDeliveries.Where(x =>
            x.UserId == approverId &&
            x.Channel == "Webhook" &&
            x.SourceType == "WorkflowTask"));
        var request = Assert.Single(webhookSender.Requests);
        Assert.Equal(task.Id, delivery.SourceId);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal("https://hooks.example.com/mini-admin", delivery.RecipientAddress);
        Assert.Contains(task.Id, request.PayloadJson);
        Assert.Contains("Policy task webhook enabled", request.PayloadJson);
    }

    [Fact]
    public async Task Transfers_Pending_Task_To_Another_User()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1100000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1100000-0000-0000-0000-000000000002");
        var targetApproverId = Guid.Parse("a1100000-0000-0000-0000-000000000003");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
            },
            new User
            {
                Id = targetApproverId,
                UserName = "target",
                RealName = "Target",
                PasswordHash = "hash"
            });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowTransfer",
            "模板转办：{instanceTitle}",
            "模板转办 {operatorUserName} {targetUserName} {nodeName} {comment}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "transfer_apply",
            "Transfer Apply",
            "Transfer",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Transfer test",
                "TRANSFER-001",
                """{"amount":100}"""),
            new WorkflowUserContext(initiatorId, "starter"));
        var sourceTodo = Assert.Single(await service.GetTodoTasksAsync(new WorkflowUserContext(approverId, "approver")));

        var transferred = await service.TransferTaskAsync(
            Guid.Parse(sourceTodo.Id),
            new WorkflowTransferTaskRequest(targetApproverId, "临时请你处理"),
            new WorkflowUserContext(approverId, "approver"));

        Assert.NotNull(transferred);
        Assert.Equal(targetApproverId.ToString(), transferred.ApproverUserId);
        Assert.Empty(await service.GetTodoTasksAsync(new WorkflowUserContext(approverId, "approver")));
        var targetTodo = Assert.Single(await service.GetTodoTasksAsync(new WorkflowUserContext(targetApproverId, "target")));
        Assert.Equal(sourceTodo.Id, targetTodo.Id);
        var targetNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == targetApproverId &&
                x.SourceType == "WorkflowTransfer"));
        Assert.Equal("模板转办：Transfer test", targetNotification.Title);
        Assert.Equal("模板转办 approver target Manager Approve 临时请你处理", targetNotification.Message);

        var detail = await service.GetInstanceAsync(
            Guid.Parse(targetTodo.InstanceId),
            new WorkflowUserContext(targetApproverId, "target"));
        Assert.NotNull(detail);
        Assert.Contains(detail.ActionLogs, log =>
            log.Action == "Transfer" &&
            log.OperatorUserName == "approver" &&
            log.Comment?.Contains("target", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task Initiator_Can_Remind_Pending_Task_And_Creates_Notification()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1200000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1200000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowRemind",
            "模板催办：{instanceTitle}",
            "模板催办 {operatorUserName} {nodeName} {comment}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "remind_apply",
            "Remind Apply",
            "Remind",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Remind test",
                "REMIND-001",
                """{"amount":100}"""),
            new WorkflowUserContext(initiatorId, "starter"));
        var approverTodo = Assert.Single(await service.GetTodoTasksAsync(new WorkflowUserContext(approverId, "manager")));

        var reminded = await service.RemindTaskAsync(
            Guid.Parse(approverTodo.Id),
            new WorkflowRemindTaskRequest("请今天处理一下"),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.NotNull(reminded);
        Assert.Equal("Pending", reminded.Status);
        var remindNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == approverId &&
                x.SourceType == "WorkflowRemind"));
        Assert.Equal("模板催办：Remind test", remindNotification.Title);
        Assert.Equal("模板催办 starter Manager Approve 请今天处理一下", remindNotification.Message);

        var detail = await service.GetInstanceAsync(
            Guid.Parse(approverTodo.InstanceId),
            new WorkflowUserContext(initiatorId, "starter"));
        Assert.NotNull(detail);
        Assert.Contains(detail.ActionLogs, log =>
            log.Action == "Remind" &&
            log.OperatorUserName == "starter" &&
            log.Comment == "请今天处理一下");
    }

    [Fact]
    public async Task Starts_Instance_By_Node_Order_When_Designer_Graph_Has_No_Edges()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("a1300000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("a1300000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "edge_missing_apply",
            "Edge Missing Apply",
            "Leave",
            null,
            """
            {
              "nodes": [
                { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 260, "y": 140 },
                { "id": "end", "type": "end", "label": "结束", "x": 480, "y": 140 }
              ],
              "edges": []
            }
            """,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Edge missing leave",
                "EDGE-MISSING-001",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.Equal("Pending", instance.Status);
        Assert.Equal("主管审批", instance.CurrentNodeName);
        var todo = Assert.Single(await service.GetTodoTasksAsync(new WorkflowUserContext(approverId, "manager")));
        Assert.Equal(instance.Id, todo.InstanceId);
    }

    [Fact]
    public async Task Rejects_Definition_When_Start_Node_Has_No_Outgoing_Edge()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("a1400000-0000-0000-0000-000000000001");
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
                "broken_start_apply",
                "Broken Start Apply",
                "Leave",
                null,
                """
                {
                  "nodes": [
                    { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                    { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 260, "y": 140 },
                    { "id": "end", "type": "end", "label": "结束", "x": 480, "y": 140 }
                  ],
                  "edges": [
                    { "id": "edge-manager-end", "source": "approve-manager", "target": "end" }
                  ]
                }
                """,
                true,
                [
                    new SaveWorkflowNodeRequest(
                        "approve-manager",
                        "主管审批",
                        "User",
                        approverId,
                        null,
                        1,
                        true)
                ])));

        Assert.Contains("开始节点", exception.Message);
        Assert.Contains("出口", exception.Message);
    }

    [Fact]
    public async Task Rejects_Definition_When_Condition_Node_Has_No_Default_Branch()
    {
        await using var dbContext = CreateDbContext();
        var managerId = Guid.Parse("a1500000-0000-0000-0000-000000000001");
        var directorId = Guid.Parse("a1500000-0000-0000-0000-000000000002");
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
                "condition_without_default",
                "Condition Without Default",
                "Leave",
                null,
                """
                {
                  "nodes": [
                    { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                    { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 260, "y": 140 },
                    { "id": "condition-days", "type": "condition", "label": "请假天数", "x": 480, "y": 140 },
                    { "id": "approve-director", "type": "approve", "label": "总监审批", "x": 700, "y": 60 },
                    { "id": "end", "type": "end", "label": "结束", "x": 940, "y": 140 }
                  ],
                  "edges": [
                    { "id": "edge-start-manager", "source": "start", "target": "approve-manager" },
                    { "id": "edge-manager-condition", "source": "approve-manager", "target": "condition-days" },
                    { "id": "edge-long-director", "source": "condition-days", "target": "approve-director", "conditionField": "days", "conditionOperator": "GreaterThan", "conditionValue": "3" },
                    { "id": "edge-director-end", "source": "approve-director", "target": "end" }
                  ]
                }
                """,
                true,
                [
                    new SaveWorkflowNodeRequest(
                        "approve-manager",
                        "主管审批",
                        "User",
                        managerId,
                        null,
                        1,
                        true),
                    new SaveWorkflowNodeRequest(
                        "approve-director",
                        "总监审批",
                        "User",
                        directorId,
                        null,
                        2,
                        true)
                ])));

        Assert.Contains("条件节点", exception.Message);
        Assert.Contains("默认分支", exception.Message);
    }

    [Fact]
    public async Task Rejects_Definition_When_Condition_Branch_Has_No_Rule()
    {
        await using var dbContext = CreateDbContext();
        var managerId = Guid.Parse("a1600000-0000-0000-0000-000000000001");
        var directorId = Guid.Parse("a1600000-0000-0000-0000-000000000002");
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
                "condition_without_rule",
                "Condition Without Rule",
                "Leave",
                null,
                """
                {
                  "nodes": [
                    { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                    { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 260, "y": 140 },
                    { "id": "condition-days", "type": "condition", "label": "请假天数", "x": 480, "y": 140 },
                    { "id": "approve-director", "type": "approve", "label": "总监审批", "x": 700, "y": 60 },
                    { "id": "end", "type": "end", "label": "结束", "x": 940, "y": 140 }
                  ],
                  "edges": [
                    { "id": "edge-start-manager", "source": "start", "target": "approve-manager" },
                    { "id": "edge-manager-condition", "source": "approve-manager", "target": "condition-days" },
                    { "id": "edge-long-director", "source": "condition-days", "target": "approve-director" },
                    { "id": "edge-default-end", "source": "condition-days", "target": "end", "isDefault": true },
                    { "id": "edge-director-end", "source": "approve-director", "target": "end" }
                  ]
                }
                """,
                true,
                [
                    new SaveWorkflowNodeRequest(
                        "approve-manager",
                        "主管审批",
                        "User",
                        managerId,
                        null,
                        1,
                        true),
                    new SaveWorkflowNodeRequest(
                        "approve-director",
                        "总监审批",
                        "User",
                        directorId,
                        null,
                        2,
                        true)
                ])));

        Assert.Contains("条件分支", exception.Message);
        Assert.Contains("判断规则", exception.Message);
    }

    [Fact]
    public async Task Rejects_Definition_When_Enabled_Node_Is_Not_Reachable_In_Designer_Graph()
    {
        await using var dbContext = CreateDbContext();
        var managerId = Guid.Parse("a1700000-0000-0000-0000-000000000001");
        var directorId = Guid.Parse("a1700000-0000-0000-0000-000000000002");
        var service = CreateService(dbContext);

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
                "unreachable_node_apply",
                "Unreachable Node Apply",
                "Leave",
                null,
                """
                {
                  "nodes": [
                    { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                    { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 260, "y": 140 },
                    { "id": "approve-director", "type": "approve", "label": "总监审批", "x": 520, "y": 60 },
                    { "id": "end", "type": "end", "label": "结束", "x": 760, "y": 140 }
                  ],
                  "edges": [
                    { "id": "edge-start-manager", "source": "start", "target": "approve-manager" },
                    { "id": "edge-manager-end", "source": "approve-manager", "target": "end" }
                  ]
                }
                """,
                true,
                [
                    new SaveWorkflowNodeRequest(
                        "approve-manager",
                        "主管审批",
                        "User",
                        managerId,
                        null,
                        1,
                        true),
                    new SaveWorkflowNodeRequest(
                        "approve-director",
                        "总监审批",
                        "User",
                        directorId,
                        null,
                        2,
                        true)
                ])));

        Assert.Contains("总监审批", exception.Message);
        Assert.Contains("不可达", exception.Message);
    }

    [Fact]
    public async Task Routes_To_Branch_By_Condition_Edge()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b1000000-0000-0000-0000-000000000001");
        var managerId = Guid.Parse("b1000000-0000-0000-0000-000000000002");
        var directorId = Guid.Parse("b1000000-0000-0000-0000-000000000003");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = managerId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            },
            new User
            {
                Id = directorId,
                UserName = "director",
                RealName = "Director",
                PasswordHash = "hash"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_condition",
            "Leave Condition",
            "Leave",
            null,
            """
            {
              "nodes": [
                { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 260, "y": 140 },
                { "id": "condition-days", "type": "condition", "label": "请假天数", "x": 480, "y": 140 },
                { "id": "approve-director", "type": "approve", "label": "总监审批", "x": 700, "y": 60 },
                { "id": "end", "type": "end", "label": "结束", "x": 940, "y": 140 }
              ],
              "edges": [
                { "id": "edge-start-manager", "source": "start", "target": "approve-manager" },
                { "id": "edge-manager-condition", "source": "approve-manager", "target": "condition-days" },
                { "id": "edge-long-director", "source": "condition-days", "target": "approve-director", "conditionField": "days", "conditionOperator": "GreaterThan", "conditionValue": "3" },
                { "id": "edge-default-end", "source": "condition-days", "target": "end", "isDefault": true }
              ]
            }
            """,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    managerId,
                    null,
                    1,
                    true),
                new SaveWorkflowNodeRequest(
                    "approve-director",
                    "总监审批",
                    "User",
                    directorId,
                    null,
                    2,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-002",
                """{"days":5}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.Equal("主管审批", instance.CurrentNodeName);

        var afterManager = await service.ApproveAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("ok"),
            new WorkflowUserContext(managerId, "manager"));

        Assert.NotNull(afterManager);
        Assert.Equal("Pending", afterManager.Status);
        Assert.Equal("总监审批", afterManager.CurrentNodeName);
        var directorTodo = await service.GetTodoTasksAsync(new WorkflowUserContext(directorId, "director"));
        Assert.Single(directorTodo);

        var shortLeave = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Short leave",
                "LEAVE-003",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var afterShortLeaveManager = await service.ApproveAsync(
            Guid.Parse(shortLeave.Id),
            new WorkflowActionRequest("ok"),
            new WorkflowUserContext(managerId, "manager"));

        Assert.NotNull(afterShortLeaveManager);
        Assert.Equal("Approved", afterShortLeaveManager.Status);
        Assert.Null(afterShortLeaveManager.CurrentNodeName);
    }

    [Fact]
    public async Task Any_Approval_Mode_Closes_Sibling_Tasks_After_First_Approval()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b2000000-0000-0000-0000-000000000001");
        var firstApproverId = Guid.Parse("b2000000-0000-0000-0000-000000000002");
        var secondApproverId = Guid.Parse("b2000000-0000-0000-0000-000000000003");
        var roleId = Guid.Parse("b2000000-0000-0000-0000-000000000004");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = firstApproverId,
                UserName = "first",
                RealName = "First",
                PasswordHash = "hash"
            },
            new User
            {
                Id = secondApproverId,
                UserName = "second",
                RealName = "Second",
                PasswordHash = "hash"
            });
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "approval_team",
            Name = "Approval Team"
        });
        dbContext.UserRoles.AddRange(
            new UserRole { UserId = firstApproverId, RoleId = roleId },
            new UserRole { UserId = secondApproverId, RoleId = roleId });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "any_mode_apply",
            "Any Mode Apply",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-team",
                    "团队审批",
                    "Role",
                    null,
                    roleId,
                    1,
                    true,
                    "approve",
                    "Any")
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Any approval",
                "ANY-001",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.Equal("Pending", instance.Status);
        Assert.Equal(2, dbContext.WorkflowTasks.Count(x => x.InstanceId == Guid.Parse(instance.Id) && x.Status == "Pending"));

        var approved = await service.ApproveAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("first ok"),
            new WorkflowUserContext(firstApproverId, "first"));

        Assert.NotNull(approved);
        Assert.Equal("Approved", approved.Status);
        Assert.Equal(1, dbContext.WorkflowTasks.Count(x => x.InstanceId == Guid.Parse(instance.Id) && x.Status == "Approved"));
        Assert.Equal(1, dbContext.WorkflowTasks.Count(x => x.InstanceId == Guid.Parse(instance.Id) && x.Status == "Closed"));
    }

    [Fact]
    public async Task All_Approval_Mode_Waits_For_All_Approvers_Before_Moving_Forward()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b2100000-0000-0000-0000-000000000001");
        var firstApproverId = Guid.Parse("b2100000-0000-0000-0000-000000000002");
        var secondApproverId = Guid.Parse("b2100000-0000-0000-0000-000000000003");
        var roleId = Guid.Parse("b2100000-0000-0000-0000-000000000004");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = firstApproverId,
                UserName = "first",
                RealName = "First",
                PasswordHash = "hash"
            },
            new User
            {
                Id = secondApproverId,
                UserName = "second",
                RealName = "Second",
                PasswordHash = "hash"
            });
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "approval_committee",
            Name = "Approval Committee"
        });
        dbContext.UserRoles.AddRange(
            new UserRole { UserId = firstApproverId, RoleId = roleId },
            new UserRole { UserId = secondApproverId, RoleId = roleId });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "all_mode_apply",
            "All Mode Apply",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-team",
                    "委员会会签",
                    "Role",
                    null,
                    roleId,
                    1,
                    true,
                    "approve",
                    "All")
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "All approval",
                "ALL-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var afterFirstApproval = await service.ApproveAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("first ok"),
            new WorkflowUserContext(firstApproverId, "first"));

        Assert.NotNull(afterFirstApproval);
        Assert.Equal("Pending", afterFirstApproval.Status);
        Assert.Equal("委员会会签", afterFirstApproval.CurrentNodeName);
        Assert.Single(await service.GetTodoTasksAsync(new WorkflowUserContext(secondApproverId, "second")));
        Assert.Empty(await service.GetTodoTasksAsync(new WorkflowUserContext(firstApproverId, "first")));

        var afterSecondApproval = await service.ApproveAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("second ok"),
            new WorkflowUserContext(secondApproverId, "second"));

        Assert.NotNull(afterSecondApproval);
        Assert.Equal("Approved", afterSecondApproval.Status);
        Assert.Null(afterSecondApproval.CurrentNodeName);
        Assert.Equal(2, dbContext.WorkflowTasks.Count(x => x.InstanceId == Guid.Parse(instance.Id) && x.Status == "Approved"));
    }

    [Fact]
    public async Task All_Approval_Mode_Rejects_Instance_When_Any_Approver_Rejects()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b2200000-0000-0000-0000-000000000001");
        var firstApproverId = Guid.Parse("b2200000-0000-0000-0000-000000000002");
        var secondApproverId = Guid.Parse("b2200000-0000-0000-0000-000000000003");
        var roleId = Guid.Parse("b2200000-0000-0000-0000-000000000004");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = firstApproverId,
                UserName = "first",
                RealName = "First",
                PasswordHash = "hash"
            },
            new User
            {
                Id = secondApproverId,
                UserName = "second",
                RealName = "Second",
                PasswordHash = "hash"
            });
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "approval_board",
            Name = "Approval Board"
        });
        dbContext.UserRoles.AddRange(
            new UserRole { UserId = firstApproverId, RoleId = roleId },
            new UserRole { UserId = secondApproverId, RoleId = roleId });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "all_mode_reject_apply",
            "All Mode Reject Apply",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-team",
                    "委员会会签",
                    "Role",
                    null,
                    roleId,
                    1,
                    true,
                    "approve",
                    "All")
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "All reject",
                "ALL-REJECT-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var rejected = await service.RejectAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("no"),
            new WorkflowUserContext(firstApproverId, "first"));

        Assert.NotNull(rejected);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal(1, dbContext.WorkflowTasks.Count(x => x.InstanceId == Guid.Parse(instance.Id) && x.Status == "Rejected"));
        Assert.Equal(1, dbContext.WorkflowTasks.Count(x => x.InstanceId == Guid.Parse(instance.Id) && x.Status == "Closed"));
    }

    [Fact]
    public async Task Cc_Node_Writes_Cc_Log_And_Continues_To_Next_Approve_Node()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b1100000-0000-0000-0000-000000000001");
        var ccUserId = Guid.Parse("b1100000-0000-0000-0000-000000000002");
        var approverId = Guid.Parse("b1100000-0000-0000-0000-000000000003");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = ccUserId,
                UserName = "hr",
                RealName = "HR",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowCc",
            "模板抄送：{instanceTitle}",
            "模板抄送 {operatorUserName} {nodeName} {businessKey}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_with_cc",
            "Leave With Cc",
            "Leave",
            null,
            """
            {
              "nodes": [
                { "id": "start", "type": "start", "label": "开始", "x": 60, "y": 140 },
                { "id": "cc-hr", "type": "cc", "label": "抄送人事", "x": 260, "y": 140 },
                { "id": "approve-manager", "type": "approve", "label": "主管审批", "x": 480, "y": 140 },
                { "id": "end", "type": "end", "label": "结束", "x": 700, "y": 140 }
              ],
              "edges": [
                { "id": "edge-start-cc", "source": "start", "target": "cc-hr" },
                { "id": "edge-cc-manager", "source": "cc-hr", "target": "approve-manager" },
                { "id": "edge-manager-end", "source": "approve-manager", "target": "end" }
              ]
            }
            """,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "cc-hr",
                    "抄送人事",
                    "User",
                    ccUserId,
                    null,
                    1,
                    true,
                    "cc"),
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    approverId,
                    null,
                    2,
                    true,
                    "approve")
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-CC-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.Equal("Pending", instance.Status);
        Assert.Equal("主管审批", instance.CurrentNodeName);
        Assert.Contains(instance.ActionLogs, log =>
            log.Action == "Cc" &&
            log.OperatorUserId == ccUserId.ToString() &&
            log.NodeName == "抄送人事");
        Assert.Empty(await service.GetTodoTasksAsync(new WorkflowUserContext(ccUserId, "hr")));
        Assert.Single(await service.GetTodoTasksAsync(new WorkflowUserContext(approverId, "manager")));
        var ccNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == ccUserId &&
                x.SourceType == "WorkflowCc"));
        Assert.Equal("模板抄送：Annual leave", ccNotification.Title);
        Assert.Equal("模板抄送 starter 抄送人事 LEAVE-CC-001", ccNotification.Message);
        var ccRecord = Assert.Single(dbContext.WorkflowCcRecords.Where(x =>
            x.InstanceId == Guid.Parse(instance.Id) &&
            x.RecipientUserId == ccUserId));
        Assert.Equal(ccRecord.Id.ToString(), ccNotification.SourceId);
        Assert.Equal(
            $"/workflow/center?workflowInstanceId={instance.Id}&workflowCcId={ccRecord.Id}",
            ccNotification.Link);

        var ccInstances = await service.GetCcInstancesAsync(
            new WorkflowInstanceListQuery(),
            new WorkflowUserContext(ccUserId, "hr"));

        var ccInstance = Assert.Single(ccInstances.Items);
        Assert.Equal(instance.Id, ccInstance.Id);
    }

    [Fact]
    public async Task Cc_Record_Can_Be_Tracked_And_Marked_As_Read()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b1200000-0000-0000-0000-000000000001");
        var ccUserId = Guid.Parse("b1200000-0000-0000-0000-000000000002");
        var approverId = Guid.Parse("b1200000-0000-0000-0000-000000000003");
        var outsiderId = Guid.Parse("b1200000-0000-0000-0000-000000000004");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = ccUserId,
                UserName = "hr",
                RealName = "HR",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            },
            new User
            {
                Id = outsiderId,
                UserName = "outsider",
                RealName = "Outsider",
                PasswordHash = "hash"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_cc_read_tracking",
            "Leave Cc Read Tracking",
            "Leave",
            null,
            """
            {
              "nodes": [
                { "id": "start", "type": "start", "label": "开始" },
                { "id": "cc-hr", "type": "cc", "label": "抄送人事" },
                { "id": "approve-manager", "type": "approve", "label": "主管审批" },
                { "id": "end", "type": "end", "label": "结束" }
              ],
              "edges": [
                { "id": "edge-start-cc", "source": "start", "target": "cc-hr" },
                { "id": "edge-cc-manager", "source": "cc-hr", "target": "approve-manager" },
                { "id": "edge-manager-end", "source": "approve-manager", "target": "end" }
              ]
            }
            """,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "cc-hr",
                    "抄送人事",
                    "User",
                    ccUserId,
                    null,
                    1,
                    true,
                    "cc"),
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    approverId,
                    null,
                    2,
                    true,
                    "approve")
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave read tracking",
                "LEAVE-CC-READ-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var unreadRecords = await service.GetCcRecordsAsync(
            new WorkflowCcListQuery(ReadStatus: "unread"),
            new WorkflowUserContext(ccUserId, "hr"));

        var unread = Assert.Single(unreadRecords.Items);
        Assert.Equal(instance.Id, unread.InstanceId);
        Assert.Equal("Annual leave read tracking", unread.InstanceTitle);
        Assert.Equal("抄送人事", unread.NodeName);
        Assert.False(unread.IsRead);
        Assert.Null(unread.ReadAt);

        var denied = await service.MarkCcRecordAsReadAsync(
            Guid.Parse(unread.Id),
            new WorkflowUserContext(outsiderId, "outsider"));
        Assert.Null(denied);

        var marked = await service.MarkCcRecordAsReadAsync(
            Guid.Parse(unread.Id),
            new WorkflowUserContext(ccUserId, "hr"));

        Assert.NotNull(marked);
        Assert.True(marked.IsRead);
        Assert.NotNull(marked.ReadAt);
        Assert.Equal("Read", marked.ReadStatus);

        var stillUnread = await service.GetCcRecordsAsync(
            new WorkflowCcListQuery(ReadStatus: "unread"),
            new WorkflowUserContext(ccUserId, "hr"));
        var readRecords = await service.GetCcRecordsAsync(
            new WorkflowCcListQuery(ReadStatus: "read"),
            new WorkflowUserContext(ccUserId, "hr"));

        Assert.Empty(stillUnread.Items);
        Assert.Single(readRecords.Items);
    }

    [Fact]
    public async Task Instance_Detail_Includes_Cc_Receipt_Records()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("b1300000-0000-0000-0000-000000000001");
        var ccUserId = Guid.Parse("b1300000-0000-0000-0000-000000000002");
        var approverId = Guid.Parse("b1300000-0000-0000-0000-000000000003");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = ccUserId,
                UserName = "hr",
                RealName = "HR",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_cc_receipts",
            "Leave Cc Receipts",
            "Leave",
            null,
            """
            {
              "nodes": [
                { "id": "start", "type": "start", "label": "开始" },
                { "id": "cc-hr", "type": "cc", "label": "抄送人事" },
                { "id": "approve-manager", "type": "approve", "label": "主管审批" },
                { "id": "end", "type": "end", "label": "结束" }
              ],
              "edges": [
                { "id": "edge-start-cc", "source": "start", "target": "cc-hr" },
                { "id": "edge-cc-manager", "source": "cc-hr", "target": "approve-manager" },
                { "id": "edge-manager-end", "source": "approve-manager", "target": "end" }
              ]
            }
            """,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "cc-hr",
                    "抄送人事",
                    "User",
                    ccUserId,
                    null,
                    1,
                    true,
                    "cc"),
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    approverId,
                    null,
                    2,
                    true,
                    "approve")
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave cc receipt",
                "LEAVE-CC-RECEIPT-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var starterDetail = await service.GetInstanceAsync(
            Guid.Parse(instance.Id),
            new WorkflowUserContext(initiatorId, "starter"));
        Assert.NotNull(starterDetail);
        var receipt = Assert.Single(starterDetail.CcRecords);
        Assert.Equal("抄送人事", receipt.NodeName);
        Assert.Equal(ccUserId.ToString(), receipt.RecipientUserId);
        Assert.Equal("hr", receipt.RecipientUserName);
        Assert.False(receipt.IsRead);
        Assert.Null(receipt.ReadAt);

        var marked = await service.MarkCcRecordAsReadAsync(
            Guid.Parse(receipt.Id),
            new WorkflowUserContext(ccUserId, "hr"));
        Assert.NotNull(marked);

        var refreshedDetail = await service.GetInstanceAsync(
            Guid.Parse(instance.Id),
            new WorkflowUserContext(initiatorId, "starter"));
        Assert.NotNull(refreshedDetail);
        var refreshedReceipt = Assert.Single(refreshedDetail.CcRecords);
        Assert.True(refreshedReceipt.IsRead);
        Assert.NotNull(refreshedReceipt.ReadAt);
    }

    [Fact]
    public async Task Explains_When_Node_Approver_Is_Outside_Current_Tenant()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
        var tenantUserId = Guid.Parse("c1000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "admin",
                RealName = "Admin",
                PasswordHash = "hash"
            },
            new User
            {
                Id = tenantUserId,
                TenantId = Guid.Parse("c3000000-0000-0000-0000-000000000001"),
                UserName = "auditor",
                RealName = "主管",
                PasswordHash = "hash"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "tenant_user_mismatch",
            "Tenant User Mismatch",
            null,
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "部门负责人审批",
                    "User",
                    tenantUserId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.StartInstanceAsync(
                new StartWorkflowInstanceRequest(
                    Guid.Parse(definition.Id),
                    "Leave",
                    null,
                    """{"days":5}"""),
                new WorkflowUserContext(initiatorId, "admin")));

        Assert.Contains("部门负责人审批", exception.Message);
        Assert.Contains("主管(auditor)", exception.Message);
        Assert.Contains("不属于平台流程", exception.Message);
        Assert.Contains("如需 Admin 审批", exception.Message);
    }

    [Fact]
    public async Task Rejects_Instance_And_Creates_Initiator_Notification()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("c2000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("c2000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowReject",
            "模板驳回：{instanceTitle}",
            "模板驳回 {operatorUserName} {comment} {nodeName}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_reject",
            "Leave Reject",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave reject",
                "LEAVE-REJECT-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var rejected = await service.RejectAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("资料不足"),
            new WorkflowUserContext(approverId, "manager"));

        Assert.NotNull(rejected);
        Assert.Equal("Rejected", rejected.Status);
        var initiatorNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == initiatorId &&
                x.SourceType == "WorkflowReject"));
        Assert.Equal("模板驳回：Annual leave reject", initiatorNotification.Title);
        Assert.Equal("模板驳回 manager 资料不足 主管审批", initiatorNotification.Message);
    }

    [Fact]
    public async Task Withdraws_Instance_And_Creates_Initiator_Notification()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("c3000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("c3000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowWithdraw",
            "模板撤回：{instanceTitle}",
            "模板撤回 {operatorUserName} {comment} {businessKey}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "leave_withdraw",
            "Leave Withdraw",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "主管审批",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave withdraw",
                "LEAVE-WITHDRAW-001",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var withdrawn = await service.WithdrawAsync(
            Guid.Parse(instance.Id),
            new WorkflowActionRequest("我先撤回"),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.NotNull(withdrawn);
        Assert.Equal("Withdrawn", withdrawn.Status);
        var initiatorNotification = Assert.Single(
            dbContext.UserNotifications.Where(x =>
                x.UserId == initiatorId &&
                x.SourceType == "WorkflowWithdraw"));
        Assert.Equal("模板撤回：Annual leave withdraw", initiatorNotification.Title);
        Assert.Equal("模板撤回 starter 我先撤回 LEAVE-WITHDRAW-001", initiatorNotification.Message);
    }

    [Fact]
    public async Task Published_Definition_With_Instance_Cannot_Be_Updated_Directly()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d1000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d1000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "purchase_apply",
            "Purchase Apply",
            "Purchase",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var publishedDefinition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(publishedDefinition.Id),
                "Purchase server",
                "PURCHASE-001",
                """{"amount":6800}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.UpdateDefinitionAsync(
                Guid.Parse(publishedDefinition.Id),
                new SaveWorkflowDefinitionRequest(
                    "purchase_apply",
                    "Purchase Apply Updated",
                    "Purchase",
                    null,
                    null,
                    true,
                    [
                        new SaveWorkflowNodeRequest(
                            "approve-director",
                            "Director Approve",
                            "User",
                            approverId,
                            null,
                            1,
                            true)
                    ])));

        Assert.Contains("已发布或归档", exception.Message);
        Assert.Contains("新版本", exception.Message);
    }

    [Fact]
    public async Task Published_Definition_Cannot_Be_Updated_Directly_Even_Without_Instance()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("d1100000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "expense_apply",
            "Expense Apply",
            "Expense",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var publishedDefinition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.UpdateDefinitionAsync(
                Guid.Parse(publishedDefinition.Id),
                new SaveWorkflowDefinitionRequest(
                    "expense_apply",
                    "Expense Apply Updated",
                    "Expense",
                    null,
                    null,
                    true,
                    [
                        new SaveWorkflowNodeRequest(
                            "approve-finance",
                            "Finance Approve",
                            "User",
                            approverId,
                            null,
                            1,
                            true)
                    ])));

        Assert.Contains("新版本", exception.Message);
    }

    [Fact]
    public async Task Started_Instance_Stores_Definition_Version_Snapshot()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d1200000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d1200000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "snapshot_leave",
            "Snapshot Leave",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ],
            """[{"field":"days","label":"Days","component":"number","required":true}]"""));
        var publishedV1 = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var v1Instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(publishedV1.Id),
                "Annual leave",
                "LEAVE-V1",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var draftV2 = await service.CreateNewVersionAsync(Guid.Parse(publishedV1.Id));
        var publishedV2 = await service.PublishDefinitionAsync(Guid.Parse(draftV2!.Id));
        var v2Instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(publishedV2.Id),
                "Sick leave",
                "LEAVE-V2",
                """{"days":2}"""),
            new WorkflowUserContext(initiatorId, "starter"));
        var reloadedV1 = await service.GetInstanceAsync(
            Guid.Parse(v1Instance.Id),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.NotNull(reloadedV1);
        Assert.Equal("snapshot_leave", GetStringProperty(reloadedV1, "DefinitionCode"));
        Assert.Equal(1, GetIntProperty(reloadedV1, "DefinitionVersion"));
        Assert.Equal(2, GetIntProperty(v2Instance, "DefinitionVersion"));

        using var snapshot = JsonDocument.Parse(GetStringProperty(reloadedV1, "DefinitionSnapshotJson"));
        Assert.Equal(publishedV1.Id, snapshot.RootElement.GetProperty("id").GetString());
        Assert.Equal("snapshot_leave", snapshot.RootElement.GetProperty("code").GetString());
        Assert.Equal(1, snapshot.RootElement.GetProperty("version").GetInt32());
        Assert.True(snapshot.RootElement.GetProperty("nodes").GetArrayLength() > 0);
    }

    [Fact]
    public async Task New_Version_Can_Be_Published_And_Archives_Previous_Published_Version()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("e1000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "contract_apply",
            "Contract Apply",
            "Contract",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-legal",
                    "Legal Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var publishedV1 = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var draftV2 = await service.CreateNewVersionAsync(Guid.Parse(publishedV1.Id));
        Assert.Equal(2, draftV2.Version);
        Assert.Equal("Draft", draftV2.PublishStatus);
        Assert.Equal("contract_apply", draftV2.Code);
        Assert.Single(draftV2.Nodes);

        var publishedV2 = await service.PublishDefinitionAsync(Guid.Parse(draftV2.Id));
        var definitions = await service.GetDefinitionsAsync(new WorkflowDefinitionListQuery(Page: 1, PageSize: 10));
        var oldVersion = definitions.Items.Single(x => x.Id == publishedV1.Id);
        var newVersion = definitions.Items.Single(x => x.Id == publishedV2.Id);
        var options = await service.GetDefinitionOptionsAsync();

        Assert.Equal("Archived", oldVersion.PublishStatus);
        Assert.Equal("Published", newVersion.PublishStatus);
        Assert.Equal(2, newVersion.Version);
        Assert.DoesNotContain(options, option => option.Id == publishedV1.Id);
        Assert.Contains(options, option => option.Id == publishedV2.Id);
    }

    [Fact]
    public async Task Definition_RoundTrips_Form_Schema_Json()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("f1000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var formSchemaJson =
            """
            [
              {"field":"days","label":"请假天数","component":"number","required":true},
              {"field":"reason","label":"请假原因","component":"textarea","required":true},
              {"field":"type","label":"请假类型","component":"select","options":[{"label":"事假","value":"personal"},{"label":"年假","value":"annual"}]}
            ]
            """;
        var service = CreateService(dbContext);

        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "form_schema_leave",
            "Form Schema Leave",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ],
            FormSchemaJson: formSchemaJson));
        var publishedDefinition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));
        var options = await service.GetDefinitionOptionsAsync();

        Assert.Contains("\"field\":\"days\"", draftDefinition.FormSchemaJson);
        Assert.Contains("\"component\":\"select\"", publishedDefinition.FormSchemaJson);
        Assert.Contains(options, option =>
            option.Id == publishedDefinition.Id &&
            option.FormSchemaJson.Contains("\"field\":\"reason\""));
    }

    [Fact]
    public async Task Definition_Rejects_Duplicate_Form_Field_Code()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("f2000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
                "duplicate_form_field",
                "Duplicate Form Field",
                "Leave",
                null,
                null,
                true,
                [
                    new SaveWorkflowNodeRequest(
                        "approve-manager",
                        "Manager Approve",
                        "User",
                        approverId,
                        null,
                        1,
                        true)
                ],
                FormSchemaJson:
                    """
                    [
                      {"field":"days","label":"请假天数","component":"number"},
                      {"field":"days","label":"重复天数","component":"text"}
                    ]
                    """)));

        Assert.Contains("字段编码", exception.Message);
        Assert.Contains("重复", exception.Message);
    }

    [Fact]
    public async Task Start_Instance_Rejects_Missing_Required_Form_Field()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("f3000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f3000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "required_form_field",
            "Required Form Field",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ],
            FormSchemaJson:
                """
                [
                  {"field":"days","label":"请假天数","component":"number","required":true},
                  {"field":"reason","label":"请假原因","component":"textarea","required":true}
                ]
                """));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.StartInstanceAsync(
                new StartWorkflowInstanceRequest(
                    Guid.Parse(definition.Id),
                    "Annual leave",
                    "LEAVE-001",
                    """{"days":1}"""),
                new WorkflowUserContext(initiatorId, "starter")));

        Assert.Contains("请假原因", exception.Message);
        Assert.Contains("必填", exception.Message);
    }

    [Fact]
    public async Task Start_Instance_Rejects_Select_Value_Outside_Form_Options()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("f4000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f4000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "select_form_field",
            "Select Form Field",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ],
            FormSchemaJson:
                """
                [
                  {"field":"type","label":"请假类型","component":"select","required":true,"options":[{"label":"事假","value":"personal"},{"label":"年假","value":"annual"}]}
                ]
                """));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.StartInstanceAsync(
                new StartWorkflowInstanceRequest(
                    Guid.Parse(definition.Id),
                    "Annual leave",
                    "LEAVE-001",
                    """{"type":"sick"}"""),
                new WorkflowUserContext(initiatorId, "starter")));

        Assert.Contains("请假类型", exception.Message);
        Assert.Contains("选项", exception.Message);
    }

    [Fact]
    public async Task Start_Instance_Accepts_Valid_Form_Schema_Data()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("f5000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f5000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "valid_form_schema",
            "Valid Form Schema",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ],
            FormSchemaJson:
                """
                [
                  {"field":"days","label":"请假天数","component":"number","required":true},
                  {"field":"type","label":"请假类型","component":"select","required":true,"options":[{"label":"事假","value":"personal"},{"label":"年假","value":"annual"}]}
                ]
                """));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-001",
                """{"days":5,"type":"personal"}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.Equal("Pending", instance.Status);
        Assert.Contains("\"days\":5", instance.FormDataJson);
        Assert.Contains("\"type\":\"personal\"", instance.FormDataJson);
        Assert.Single(instance.Tasks);
    }

    [Fact]
    public async Task Definition_Rejects_Negative_Node_Sla_Minutes()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("f6000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
                "negative_sla",
                "Negative SLA",
                "Leave",
                null,
                null,
                true,
                [
                    new SaveWorkflowNodeRequest(
                        "approve-manager",
                        "Manager Approve",
                        "User",
                        approverId,
                        null,
                        1,
                        true,
                        SlaMinutes: -1)
                ])));

        Assert.Contains("处理时限", exception.Message);
    }

    [Fact]
    public async Task Start_Instance_Creates_Task_DueAt_From_Node_Sla()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("f7000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f7000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "task_due_at",
            "Task Due At",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true,
                    SlaMinutes: 30)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var beforeStart = DateTimeOffset.UtcNow;
        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-001",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var task = Assert.Single(instance.Tasks);
        Assert.NotNull(task.DueAt);
        Assert.True(task.DueAt >= beforeStart.AddMinutes(30));
        Assert.False(task.IsOverdue);
    }

    [Fact]
    public async Task Sla_Scan_Creates_Overdue_Reminder_Notification()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("f8000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f8000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowOverdue",
            "模板超时：{instanceTitle}",
            "模板超时 {nodeName} {approverUserName}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "sla_scan",
            "SLA Scan",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true,
                    SlaMinutes: 1)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));
        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-001",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var task = Assert.Single(instance.Tasks);
        var result = await service.ScanOverdueTasksAsync(DateTimeOffset.UtcNow.AddMinutes(2));

        Assert.Equal(1, result.OverdueTaskCount);
        Assert.Equal(1, result.RemindedTaskCount);
        var overdueNotification = Assert.Single(dbContext.UserNotifications.Where(x =>
            x.UserId == approverId &&
            x.SourceType == "WorkflowOverdue" &&
            x.SourceId == task.Id));
        Assert.Equal("模板超时：Annual leave", overdueNotification.Title);
        Assert.Contains("Manager Approve", overdueNotification.Message);
        Assert.Contains(dbContext.WorkflowActionLogs, log =>
            log.InstanceId == Guid.Parse(instance.Id) &&
            log.Action == "Overdue" &&
            log.NodeName == "Manager Approve");
    }

    [Fact]
    public async Task Sla_Scan_Does_Not_Duplicate_Immediate_Reminders()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("f9000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("f9000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
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
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "sla_scan_dedupe",
            "SLA Scan Dedupe",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true,
                    SlaMinutes: 1)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));
        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave",
                "LEAVE-001",
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));
        var task = Assert.Single(instance.Tasks);
        var scanTime = DateTimeOffset.UtcNow.AddMinutes(2);

        var firstResult = await service.ScanOverdueTasksAsync(scanTime);
        var secondResult = await service.ScanOverdueTasksAsync(scanTime.AddMinutes(5));

        Assert.Equal(1, firstResult.RemindedTaskCount);
        Assert.Equal(0, secondResult.RemindedTaskCount);
        Assert.Single(dbContext.UserNotifications.Where(x =>
            x.SourceType == "WorkflowOverdue" &&
            x.SourceId == task.Id));
        Assert.Single(dbContext.WorkflowActionLogs.Where(x =>
            x.Action == "Overdue" &&
            x.InstanceId == Guid.Parse(instance.Id)));
    }

    [Fact]
    public async Task Start_Instance_With_Attachments_Returns_Attachment_Dtos()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d5000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d5000000-0000-0000-0000-000000000002");
        var fileId = Guid.Parse("d5000000-0000-0000-0000-000000000101");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedManagedFile(dbContext, fileId, "quote.pdf");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "attachment_leave",
            "Attachment Leave",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Annual leave with quote",
                "LEAVE-ATTACH-001",
                """{"days":1}""",
                AttachmentFileIds: [fileId]),
            new WorkflowUserContext(initiatorId, "starter"));

        var attachment = Assert.Single(instance.Attachments);
        Assert.Equal(fileId.ToString(), attachment.FileId);
        Assert.Equal("quote.pdf", attachment.OriginalName);
        Assert.Equal("starter", attachment.UploaderUserName);
    }

    [Fact]
    public async Task Add_Attachment_Does_Not_Create_Duplicate_Links()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d6000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d6000000-0000-0000-0000-000000000002");
        var fileId = Guid.Parse("d6000000-0000-0000-0000-000000000101");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedManagedFile(dbContext, fileId, "receipt.png", "image/png");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "attachment_dedupe",
            "Attachment Dedupe",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));
        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Attachment dedupe",
                null,
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        await service.AddAttachmentAsync(
            Guid.Parse(instance.Id),
            new WorkflowAttachmentRequest(fileId, "first upload"),
            new WorkflowUserContext(initiatorId, "starter"));
        var updated = await service.AddAttachmentAsync(
            Guid.Parse(instance.Id),
            new WorkflowAttachmentRequest(fileId, "duplicate upload"),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.NotNull(updated);
        Assert.Single(updated.Attachments);
        Assert.Contains(updated.ActionLogs, log =>
            log.Action == "Attach" &&
            log.Comment != null &&
            log.Comment.Contains("receipt.png"));
    }

    [Fact]
    public async Task Add_Comment_Persists_Comment_And_Action_Log()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d7000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d7000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "comment_leave",
            "Comment Leave",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));
        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Comment leave",
                null,
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var comment = await service.AddCommentAsync(
            Guid.Parse(instance.Id),
            new WorkflowCommentRequest("请补充报价单"),
            new WorkflowUserContext(initiatorId, "starter"));
        var updated = await service.GetInstanceAsync(
            Guid.Parse(instance.Id),
            new WorkflowUserContext(initiatorId, "starter"));

        Assert.NotNull(comment);
        Assert.Equal("请补充报价单", comment.Content);
        Assert.Equal("starter", comment.AuthorUserName);
        var persisted = Assert.Single(updated!.Comments);
        Assert.Equal(comment.Id, persisted.Id);
        Assert.Contains(updated.ActionLogs, log =>
            log.Action == "Comment" &&
            log.Comment == "请补充报价单");
    }

    [Fact]
    public async Task Add_Comment_Notifies_Workflow_Participants_Except_Author()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d8000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d8000000-0000-0000-0000-000000000002");
        dbContext.Users.AddRange(
            new User
            {
                Id = initiatorId,
                UserName = "starter",
                RealName = "Starter",
                PasswordHash = "hash"
            },
            new User
            {
                Id = approverId,
                UserName = "manager",
                RealName = "Manager",
                PasswordHash = "hash"
            });
        SeedWorkflowNotificationTemplate(
            dbContext,
            "WorkflowComment",
            "模板评论：{instanceTitle}",
            "模板评论 {operatorUserName} {comment}");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "comment_notify",
            "Comment Notify",
            "Leave",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));
        var instance = await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                "Comment notify",
                null,
                """{"days":1}"""),
            new WorkflowUserContext(initiatorId, "starter"));

        var comment = await service.AddCommentAsync(
            Guid.Parse(instance.Id),
            new WorkflowCommentRequest("我已确认资料"),
            new WorkflowUserContext(approverId, "manager"));

        var notification = Assert.Single(dbContext.UserNotifications.Where(x =>
            x.UserId == initiatorId &&
            x.SourceType == "WorkflowComment" &&
            x.SourceId == comment!.Id));
        Assert.Equal("模板评论：Comment notify", notification.Title);
        Assert.Contains("manager", notification.Message);
        Assert.Empty(dbContext.UserNotifications.Where(x =>
            x.UserId == approverId &&
            x.SourceType == "WorkflowComment" &&
            x.SourceId == comment!.Id));
    }

    [Fact]
    public async Task Get_Instance_Hides_Unrelated_User()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("d9000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("d9000000-0000-0000-0000-000000000002");
        var outsiderId = Guid.Parse("d9000000-0000-0000-0000-000000000003");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "manager");
        SeedWorkflowUser(dbContext, outsiderId, "outsider");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var instance = await StartUserWorkflowAsync(
            service,
            "visibility_detail",
            "Visibility detail",
            initiatorId,
            "starter",
            approverId);

        var hidden = await service.GetInstanceAsync(
            Guid.Parse(instance.Id),
            new WorkflowUserContext(outsiderId, "outsider"));

        Assert.Null(hidden);
    }

    [Fact]
    public async Task Scope_All_Lists_Only_Participated_Instances_For_Regular_User()
    {
        await using var dbContext = CreateDbContext();
        var starterAId = Guid.Parse("da000000-0000-0000-0000-000000000001");
        var approverAId = Guid.Parse("da000000-0000-0000-0000-000000000002");
        var starterBId = Guid.Parse("da000000-0000-0000-0000-000000000003");
        var approverBId = Guid.Parse("da000000-0000-0000-0000-000000000004");
        SeedWorkflowUser(dbContext, starterAId, "starter-a");
        SeedWorkflowUser(dbContext, approverAId, "approver-a");
        SeedWorkflowUser(dbContext, starterBId, "starter-b");
        SeedWorkflowUser(dbContext, approverBId, "approver-b");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var visible = await StartUserWorkflowAsync(
            service,
            "visibility_list_a",
            "Visible Instance",
            starterAId,
            "starter-a",
            approverAId);
        await StartUserWorkflowAsync(
            service,
            "visibility_list_b",
            "Hidden Instance",
            starterBId,
            "starter-b",
            approverBId);

        var result = await service.GetInstancesAsync(
            new WorkflowInstanceListQuery(Page: 1, PageSize: 20, Scope: "all"),
            new WorkflowUserContext(approverAId, "approver-a"));

        var item = Assert.Single(result.Items);
        Assert.Equal(visible.Id, item.Id);
    }

    [Fact]
    public async Task Manager_Context_Can_View_All_Instances()
    {
        await using var dbContext = CreateDbContext();
        var starterAId = Guid.Parse("db000000-0000-0000-0000-000000000001");
        var approverAId = Guid.Parse("db000000-0000-0000-0000-000000000002");
        var starterBId = Guid.Parse("db000000-0000-0000-0000-000000000003");
        var approverBId = Guid.Parse("db000000-0000-0000-0000-000000000004");
        var managerId = Guid.Parse("db000000-0000-0000-0000-000000000005");
        SeedWorkflowUser(dbContext, starterAId, "starter-a");
        SeedWorkflowUser(dbContext, approverAId, "approver-a");
        SeedWorkflowUser(dbContext, starterBId, "starter-b");
        SeedWorkflowUser(dbContext, approverBId, "approver-b");
        SeedWorkflowUser(dbContext, managerId, "workflow-admin");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        await StartUserWorkflowAsync(
            service,
            "visibility_manager_a",
            "Managed Instance A",
            starterAId,
            "starter-a",
            approverAId);
        var unrelated = await StartUserWorkflowAsync(
            service,
            "visibility_manager_b",
            "Managed Instance B",
            starterBId,
            "starter-b",
            approverBId);

        var manager = new WorkflowUserContext(
            managerId,
            "workflow-admin",
            CanManageAllWorkflowInstances: true);
        var detail = await service.GetInstanceAsync(Guid.Parse(unrelated.Id), manager);
        var list = await service.GetInstancesAsync(
            new WorkflowInstanceListQuery(Page: 1, PageSize: 20, Scope: "all"),
            manager);

        Assert.NotNull(detail);
        Assert.Equal(2, list.Total);
    }

    [Fact]
    public async Task Non_Participant_Cannot_Add_Comment_Or_Attachment()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("dc000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("dc000000-0000-0000-0000-000000000002");
        var outsiderId = Guid.Parse("dc000000-0000-0000-0000-000000000003");
        var fileId = Guid.Parse("dc000000-0000-0000-0000-000000000101");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "manager");
        SeedWorkflowUser(dbContext, outsiderId, "outsider");
        SeedManagedFile(dbContext, fileId, "secret.pdf");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var instance = await StartUserWorkflowAsync(
            service,
            "visibility_collab",
            "Visibility Collaboration",
            initiatorId,
            "starter",
            approverId);
        var outsider = new WorkflowUserContext(outsiderId, "outsider");

        await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.AddCommentAsync(
                Guid.Parse(instance.Id),
                new WorkflowCommentRequest("我不应该能评论"),
                outsider));
        await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.AddAttachmentAsync(
                Guid.Parse(instance.Id),
                new WorkflowAttachmentRequest(fileId, null),
                outsider));
    }

    [Fact]
    public async Task Workflow_Attachment_Download_Requires_Instance_Access()
    {
        await using var dbContext = CreateDbContext();
        var initiatorId = Guid.Parse("dd000000-0000-0000-0000-000000000001");
        var approverId = Guid.Parse("dd000000-0000-0000-0000-000000000002");
        var outsiderId = Guid.Parse("dd000000-0000-0000-0000-000000000003");
        var fileId = Guid.Parse("dd000000-0000-0000-0000-000000000101");
        SeedWorkflowUser(dbContext, initiatorId, "starter");
        SeedWorkflowUser(dbContext, approverId, "manager");
        SeedWorkflowUser(dbContext, outsiderId, "outsider");
        SeedManagedFile(dbContext, fileId, "secret.pdf");
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var instance = await StartUserWorkflowAsync(
            service,
            "visibility_download",
            "Visibility Download",
            initiatorId,
            "starter",
            approverId,
            fileId);
        var attachment = Assert.Single(instance.Attachments);

        var allowed = await service.GetAttachmentDownloadAsync(
            Guid.Parse(instance.Id),
            Guid.Parse(attachment.Id),
            new WorkflowUserContext(initiatorId, "starter"));
        var denied = await service.GetAttachmentDownloadAsync(
            Guid.Parse(instance.Id),
            Guid.Parse(attachment.Id),
            new WorkflowUserContext(outsiderId, "outsider"));

        Assert.NotNull(allowed);
        Assert.Equal(fileId.ToString(), allowed.FileId);
        Assert.Null(denied);
    }

    [Fact]
    public async Task Business_Binding_Requires_Published_Definition()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("e2000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "expense_apply",
            "Expense Apply",
            "Expense",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-finance",
                    "Finance Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateBusinessBindingAsync(new SaveWorkflowBusinessBindingRequest(
                "expense",
                "Expense",
                Guid.Parse(draftDefinition.Id),
                true,
                null)));

        Assert.Contains("已发布", exception.Message);
    }

    [Fact]
    public async Task Business_Binding_Rejects_Duplicate_Business_Type()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("e3000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "contract_apply",
            "Contract Apply",
            "Contract",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-legal",
                    "Legal Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var publishedDefinition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        await service.CreateBusinessBindingAsync(new SaveWorkflowBusinessBindingRequest(
            "contract",
            "Contract",
            Guid.Parse(publishedDefinition.Id),
            true,
            null));

        var exception = await Assert.ThrowsAsync<WorkflowOperationException>(() =>
            service.CreateBusinessBindingAsync(new SaveWorkflowBusinessBindingRequest(
                "contract",
                "Contract Duplicate",
                Guid.Parse(publishedDefinition.Id),
                true,
                null)));

        Assert.Contains("业务类型", exception.Message);
    }

    [Fact]
    public async Task Disabled_Business_Binding_Cannot_Be_Resolved()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("e4000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            "purchase_apply",
            "Purchase Apply",
            "Purchase",
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    "approve-manager",
                    "Manager Approve",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var publishedDefinition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        var binding = await service.CreateBusinessBindingAsync(new SaveWorkflowBusinessBindingRequest(
            "purchase",
            "Purchase",
            Guid.Parse(publishedDefinition.Id),
            false,
            null));

        Assert.Null(await service.ResolveBusinessDefinitionAsync("purchase"));

        await service.UpdateBusinessBindingAsync(
            Guid.Parse(binding.Id),
            new SaveWorkflowBusinessBindingRequest(
                "purchase",
                "Purchase",
                Guid.Parse(publishedDefinition.Id),
                true,
                null));

        var resolved = await service.ResolveBusinessDefinitionAsync("purchase");

        Assert.NotNull(resolved);
        Assert.Equal(publishedDefinition.Id, resolved.DefinitionId);
        Assert.Equal(1, resolved.DefinitionVersion);
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MiniAdminDbContext(options);
    }

    private static WorkflowAppService CreateService(
        MiniAdminDbContext dbContext,
        StubWebhookNotificationSender? webhookSender = null,
        bool webhookEnabled = false,
        string webhookEndpointUrl = "")
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
                CreateNotificationDeliveryService(
                    dbContext,
                    currentTenant,
                    webhookSender,
                    webhookEnabled,
                    webhookEndpointUrl)),
            []);
    }

    private static void SeedWorkflowUser(
        MiniAdminDbContext dbContext,
        Guid id,
        string userName,
        string? email = null)
    {
        dbContext.Users.Add(new User
        {
            Id = id,
            UserName = userName,
            RealName = userName,
            PasswordHash = "hash",
            Email = email
        });
    }

    private static NotificationDeliveryService CreateNotificationDeliveryService(
        MiniAdminDbContext dbContext,
        ICurrentTenant currentTenant,
        StubWebhookNotificationSender? webhookSender = null,
        bool webhookEnabled = false,
        string webhookEndpointUrl = "")
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
            webhookSender ?? new StubWebhookNotificationSender(),
            Options.Create(new WebhookNotificationOptions
            {
                Enabled = webhookEnabled,
                EndpointUrl = webhookEndpointUrl
            }),
            currentTenant);
    }

    private static int GetIntProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<int>(property.GetValue(source));
    }

    private static string GetStringProperty(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<string>(property.GetValue(source));
    }

    private static async Task<WorkflowInstanceDto> StartUserWorkflowAsync(
        WorkflowAppService service,
        string code,
        string title,
        Guid initiatorId,
        string initiatorUserName,
        Guid approverId,
        Guid? attachmentFileId = null)
    {
        var draftDefinition = await service.CreateDefinitionAsync(new SaveWorkflowDefinitionRequest(
            code,
            title,
            title,
            null,
            null,
            true,
            [
                new SaveWorkflowNodeRequest(
                    $"{code}-approve",
                    "审批节点",
                    "User",
                    approverId,
                    null,
                    1,
                    true)
            ]));
        var definition = await service.PublishDefinitionAsync(Guid.Parse(draftDefinition.Id));

        return await service.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.Id),
                title,
                null,
                """{"amount":1}""",
                attachmentFileId.HasValue ? [attachmentFileId.Value] : null),
            new WorkflowUserContext(initiatorId, initiatorUserName));
    }

    private static void SeedWorkflowNotificationTemplate(
        MiniAdminDbContext dbContext,
        string code,
        string titleTemplate,
        string messageTemplate)
    {
        dbContext.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"测试模板 {code}",
            Category = "Workflow",
            Level = "Info",
            Channel = "InApp",
            TitleTemplate = titleTemplate,
            MessageTemplate = messageTemplate,
            LinkTemplate =
                code is "WorkflowTask" or "WorkflowTransfer" or "WorkflowRemind"
                    ? "/workflow/center?workflowInstanceId={instanceId}{workflowTaskQuery}"
                    : "/workflow/center?workflowInstanceId={instanceId}",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    private static void SeedManagedFile(
        MiniAdminDbContext dbContext,
        Guid id,
        string originalName,
        string contentType = "application/pdf")
    {
        dbContext.ManagedFiles.Add(new ManagedFile
        {
            Id = id,
            OriginalName = originalName,
            StoredName = $"{id:N}-{originalName}",
            ContentType = contentType,
            Size = 1024,
            StorageProvider = "Local",
            StoragePath = $"test/{id:N}/{originalName}",
            Status = "Normal",
            CreatedAt = DateTimeOffset.UtcNow
        });
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
        public List<WebhookRequest> Requests { get; } = [];

        public Task SendAsync(
            string endpointUrl,
            string payloadJson,
            string? secret,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new WebhookRequest(endpointUrl, payloadJson, secret));
            return Task.CompletedTask;
        }
    }

    private sealed record WebhookRequest(
        string EndpointUrl,
        string PayloadJson,
        string? Secret);

    private sealed class TestCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;

        public string? TenantCode => null;

        public bool IsPlatform => true;

        public bool IsTenant => false;
    }
}
