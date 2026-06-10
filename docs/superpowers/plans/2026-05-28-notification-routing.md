# Notification Routing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable alert recipients and email notification delivery so alert rules can notify selected roles and users through station notifications and SMTP email.

**Architecture:** Extend `AlertRule` with email channel state and a child recipient table. Resolve recipients during alert scan, deduplicate users, create station notifications through the existing notification table, and call an SMTP-backed email sender that records delivery status. Keep SMTP secrets in configuration, not the database.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core, MySQL migrations, xUnit integration tests, Vben Admin `web-antd`, Ant Design Vue.

---

### Task 1: Failing Tests For Routing And Email

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] **Step 1: Add test records**

Add records near the existing alert and notification test records:

```csharp
private sealed record AlertRuleRecipientData(
    string Id,
    string RecipientType,
    string RecipientId,
    string RecipientName);

private sealed record NotificationDeliveryData(
    string Id,
    string Channel,
    string UserId,
    string RecipientAddress,
    string SourceType,
    string SourceId,
    string Status,
    string? ErrorMessage);
```

- [ ] **Step 2: Add login overload for recipient tests**

Replace the current helper with an overload pair:

```csharp
private Task<HttpResponseMessage> LoginAsync()
{
    return LoginAsync("admin", "123456");
}

private Task<HttpResponseMessage> LoginAsync(string username, string password)
{
    return _client.PostAsJsonAsync("/auth/login", new
    {
        username,
        password
    });
}

private Task AuthorizeAsync()
{
    return AuthorizeAsync("admin", "123456");
}

private async Task AuthorizeAsync(string username, string password)
{
    var response = await LoginAsync(username, password);
    var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

    Assert.NotNull(json);
    _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.Data.AccessToken);
}
```

- [ ] **Step 3: Add default recipient test**

Add a test named `NotificationRouting_AlertRules_Default_To_Admin_Role`:

```csharp
[Fact]
public async Task NotificationRouting_AlertRules_Default_To_Admin_Role()
{
    await AuthorizeAsync();

    var rules = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertRuleData>>>(
        "/system/alert-rule/list?page=1&pageSize=20");

    Assert.NotNull(rules);
    Assert.All(rules.Data.Items, rule =>
    {
        Assert.Contains(rule.Recipients, recipient =>
            recipient.RecipientType == "Role" &&
            recipient.RecipientName == "admin");
    });
}
```

Expected first run: compile fails because `AlertRuleData.Recipients` does not exist.

- [ ] **Step 4: Add recipient routing test**

Add a test named `NotificationRouting_AlertScan_Notifies_Selected_User_And_Deduplicates`:

```csharp
[Fact]
public async Task NotificationRouting_AlertScan_Notifies_Selected_User_And_Deduplicates()
{
    await AuthorizeAsync();
    await ClearAlertsAndNotificationsAsync();
    var rule = await GetAlertRuleByCodeAsync("AbnormalFileDetected");
    var demoUserId = MiniAdminSeedIds.DemoUserId.ToString();

    try
    {
        await UpdateAlertRuleRecipientsAsync(
            rule,
            roleIds: [MiniAdminSeedIds.AdminRoleId.ToString()],
            userIds: [demoUserId],
            emailEnabled: false);
        await CreateMissingManagedFileAsync();

        await RunAlertScanJobAsync();

        await AuthorizeAsync("demo", "123456");
        var demoNotifications = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20&category=SystemAlert");
        Assert.NotNull(demoNotifications);
        Assert.Single(demoNotifications.Data.Items, item => item.SourceType == "Alert");
    }
    finally
    {
        _client.DefaultRequestHeaders.Authorization = null;
        await AuthorizeAsync();
        await RestoreAlertRuleAsync(rule);
    }
}
```

- [ ] **Step 5: Add email delivery test**

Add `NotificationRouting_EmailEnabled_Creates_Email_Delivery_Record`:

```csharp
[Fact]
public async Task NotificationRouting_EmailEnabled_Creates_Email_Delivery_Record()
{
    await AuthorizeAsync();
    await ClearAlertsAndNotificationsAsync();
    var rule = await GetAlertRuleByCodeAsync("AbnormalFileDetected");

    await using var scope = _factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
    var admin = await dbContext.Users.SingleAsync(user => user.UserName == "admin");
    admin.Email = "admin@example.com";
    await dbContext.SaveChangesAsync();

    try
    {
        await UpdateAlertRuleRecipientsAsync(
            rule,
            roleIds: [MiniAdminSeedIds.AdminRoleId.ToString()],
            userIds: [],
            emailEnabled: true);
        await CreateMissingManagedFileAsync();

        await RunAlertScanJobAsync();

        var deliveries = await dbContext.Set<NotificationDelivery>()
            .Where(item => item.Channel == "Email" && item.UserId == admin.Id)
            .ToArrayAsync();
        Assert.Single(deliveries);
        Assert.Contains(deliveries.Single().Status, ["Pending", "Succeeded", "Failed"]);
    }
    finally
    {
        await RestoreAlertRuleAsync(rule);
    }
}
```

- [ ] **Step 6: Run tests and confirm red**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "NotificationRouting"
```

Expected: compile failures for missing DTO fields, helper overloads, entities, and APIs.

### Task 2: Domain Model, EF Mapping, And Migration

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/User.cs`
- Modify: `src/MiniAdmin.Domain/Entities/AlertRule.cs`
- Create: `src/MiniAdmin.Domain/Entities/AlertRuleRecipient.cs`
- Create: `src/MiniAdmin.Domain/Entities/NotificationDelivery.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] **Step 1: Add entity properties**

Add to `User`:

```csharp
public string? Email { get; set; }
```

Add to `AlertRule`:

```csharp
public bool EmailEnabled { get; set; }

public List<AlertRuleRecipient> Recipients { get; set; } = [];
```

Create `AlertRuleRecipient`:

```csharp
namespace MiniAdmin.Domain.Entities;

public sealed class AlertRuleRecipient
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public AlertRule AlertRule { get; set; } = null!;
    public string RecipientType { get; set; } = string.Empty;
    public Guid RecipientId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

Create `NotificationDelivery`:

```csharp
namespace MiniAdmin.Domain.Entities;

public sealed class NotificationDelivery
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string RecipientAddress { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
}
```

- [ ] **Step 2: Map EF tables**

In `MiniAdminDbContext` add DbSets and mappings:

```csharp
public DbSet<AlertRuleRecipient> AlertRuleRecipients => Set<AlertRuleRecipient>();
public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();
```

Map:

```csharp
entity.Property(x => x.Email).HasMaxLength(256);
```

For recipients:

```csharp
entity.ToTable("mini_alert_rule_recipients");
entity.HasKey(x => x.Id);
entity.HasIndex(x => new { x.AlertRuleId, x.RecipientType, x.RecipientId }).IsUnique();
entity.Property(x => x.RecipientType).HasMaxLength(16).IsRequired();
entity.HasOne(x => x.AlertRule)
    .WithMany(x => x.Recipients)
    .HasForeignKey(x => x.AlertRuleId)
    .OnDelete(DeleteBehavior.Cascade);
```

For deliveries:

```csharp
entity.ToTable("mini_notification_deliveries");
entity.HasKey(x => x.Id);
entity.HasIndex(x => new { x.Channel, x.SourceType, x.SourceId, x.UserId }).IsUnique();
entity.HasIndex(x => x.CreatedAt);
entity.Property(x => x.Channel).HasMaxLength(32).IsRequired();
entity.Property(x => x.RecipientAddress).HasMaxLength(256).IsRequired();
entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
entity.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
entity.Property(x => x.SourceId).HasMaxLength(64).IsRequired();
entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
```

- [ ] **Step 3: Generate migration**

Run:

```powershell
dotnet ef migrations add AddNotificationRouting --project C:\monica\code\mini-admin\src\MiniAdmin.Infrastructure\MiniAdmin.Infrastructure.csproj --startup-project C:\monica\code\mini-admin\src\MiniAdmin.Api\MiniAdmin.Api.csproj --context MiniAdminDbContext --output-dir Persistence\Migrations
```

Expected: migration adds the two tables and two columns only.

### Task 3: Contracts, Repository, And Recipient Resolution

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Alerts/AlertRuleDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfAlertRuleRepository.cs`
- Modify: `src/MiniAdmin.Application/Alerts/AlertAppService.cs`

- [ ] **Step 1: Extend DTOs**

Add:

```csharp
public sealed record AlertRuleRecipientDto(
    string Id,
    string RecipientType,
    string RecipientId,
    string RecipientName);
```

Extend `AlertRuleDto` with:

```csharp
bool EmailEnabled,
IReadOnlyList<AlertRuleRecipientDto> Recipients,
```

Extend `UpdateAlertRuleRequest` with:

```csharp
bool EmailEnabled,
IReadOnlyList<Guid> RecipientRoleIds,
IReadOnlyList<Guid> RecipientUserIds,
```

- [ ] **Step 2: Update repository query**

Update `EfAlertRuleRepository.GetListAsync` and `GetEnabledAsync` to include recipients:

```csharp
var rulesQuery = dbContext.AlertRules
    .Include(rule => rule.Recipients)
    .AsNoTracking();
```

Resolve names by joining roles/users when mapping DTOs. Role names use `Role.Code`; user names use `User.UserName`.

- [ ] **Step 3: Update repository mutation**

In `UpdateAsync`, replace existing recipients for the rule:

```csharp
dbContext.AlertRuleRecipients.RemoveRange(rule.Recipients);
foreach (var roleId in request.RecipientRoleIds.Distinct())
{
    rule.Recipients.Add(new AlertRuleRecipient
    {
        Id = Guid.NewGuid(),
        AlertRuleId = rule.Id,
        RecipientType = "Role",
        RecipientId = roleId
    });
}
foreach (var userId in request.RecipientUserIds.Distinct())
{
    rule.Recipients.Add(new AlertRuleRecipient
    {
        Id = Guid.NewGuid(),
        AlertRuleId = rule.Id,
        RecipientType = "User",
        RecipientId = userId
    });
}
```

### Task 4: Station Notification And Email Delivery

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/UserNotifications/UserNotificationDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfUserNotificationRepository.cs`
- Create: `src/MiniAdmin.Infrastructure/Notifications/EmailNotificationOptions.cs`
- Create: `src/MiniAdmin.Infrastructure/Notifications/IEmailNotificationSender.cs`
- Create: `src/MiniAdmin.Infrastructure/Notifications/SmtpEmailNotificationSender.cs`
- Create: `src/MiniAdmin.Infrastructure/Notifications/NotificationDeliveryService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`

- [ ] **Step 1: Add create-for-users contract**

Add to `IUserNotificationRepository`:

```csharp
Task<int> CreateForUsersAsync(
    IReadOnlyList<Guid> userIds,
    IReadOnlyList<CreateUserNotificationRequest> requests,
    DateTimeOffset now,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Resolve recipients in alert scan**

Add a private method in `AlertAppService`:

```csharp
private async Task<IReadOnlyList<Guid>> ResolveRecipientUserIdsAsync(
    AlertRuleDto rule,
    CancellationToken cancellationToken)
```

It should:

- Expand role recipients through `UserRoles`.
- Add user recipients directly.
- Keep only enabled users.
- Deduplicate IDs.
- Fall back to admin role users when empty.

- [ ] **Step 3: Add email options and sender**

Create `EmailNotificationOptions`:

```csharp
public sealed class EmailNotificationOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 465;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "MiniAdmin";
    public bool EnableSsl { get; set; } = true;
}
```

Create `IEmailNotificationSender`:

```csharp
public interface IEmailNotificationSender
{
    Task SendAsync(
        string recipientEmail,
        string title,
        string content,
        CancellationToken cancellationToken = default);
}
```

Implement SMTP with `System.Net.Mail.SmtpClient`.

- [ ] **Step 4: Record delivery status**

`NotificationDeliveryService` creates `NotificationDelivery` rows and updates status to `Succeeded`, `Failed`, or `Skipped`. It must catch SMTP exceptions and never throw into alert scan.

### Task 5: User Email API And Frontend

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Users/UserListItemDto.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Users/CreateUserRequest.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Users/UpdateUserRequest.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

- [ ] **Step 1: Add email to user contracts**

Add `string? Email` to list, create, and update contracts.

- [ ] **Step 2: Persist email in repository**

Map create and update:

```csharp
Email = NormalizeEmail(request.Email)
```

Use:

```csharp
private static string? NormalizeEmail(string? email)
{
    return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
}
```

- [ ] **Step 3: Add frontend fields**

Add email column and form input labeled `邮箱`. Use basic email validation only:

```ts
{ type: 'email', message: '请输入正确的邮箱地址' }
```

### Task 6: Alert Rule Frontend

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/alert-rule.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/alert-rule/index.vue`
- Read: `frontend/vue-vben-admin/apps/web-antd/src/api/system/role.ts`
- Read: `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`

- [ ] **Step 1: Extend API types**

Add:

```ts
export interface AlertRuleRecipient {
  id: string;
  recipientId: string;
  recipientName: string;
  recipientType: 'Role' | 'User';
}
```

Add `emailEnabled`, `recipients`, `recipientRoleIds`, and `recipientUserIds` to request/response types.

- [ ] **Step 2: Add edit modal controls**

Add controls:

- 站内信 switch.
- 邮件 switch.
- 接收角色 multiple select.
- 指定用户 multiple select.

Populate role options from role list and user options from user list.

### Task 7: Verification, Docs, Commit

**Files:**
- Create: `docs/features/2026-05-28-notification-routing/03-summary.md`

- [ ] **Step 1: Run focused tests**

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "NotificationRouting|AlertRule|AlertScanJob|UserEmail"
```

- [ ] **Step 2: Run EF model check**

```powershell
dotnet ef migrations has-pending-model-changes --project C:\monica\code\mini-admin\src\MiniAdmin.Infrastructure\MiniAdmin.Infrastructure.csproj --startup-project C:\monica\code\mini-admin\src\MiniAdmin.Api\MiniAdmin.Api.csproj --context MiniAdminDbContext
```

- [ ] **Step 3: Run full backend tests**

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

- [ ] **Step 4: Run frontend build**

```powershell
pnpm run build:antd
```

- [ ] **Step 5: Write summary and commit**

Commit message:

```text
feat: add notification routing and email channel
```
