# Alert Rules Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build enterprise-style configurable alert rules for MiniAdmin so built-in monitor alerts can be enabled, tuned, and notification-controlled from Vben.

**Architecture:** Add an `AlertRule` aggregate persisted in MySQL through EF Core, expose it through application contracts and Minimal APIs, then make `AlertAppService.ScanAsync` evaluate enabled rules instead of hard-coded thresholds. Keep alert persistence and notification delivery mostly unchanged, with a rule-level `NotifyEnabled` flag controlling notification creation.

**Tech Stack:** ASP.NET Core Minimal APIs, EF Core, MySQL migrations, Vben Admin `web-antd`, Ant Design Vue, xUnit integration tests.

---

### Task 1: Backend Contract And Failing Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Create: `src/MiniAdmin.Application.Contracts/Alerts/AlertRuleDtos.cs`

- [ ] **Step 1: Add failing tests for alert rule defaults and menu**

Add integration tests that call:

```http
GET /system/alert-rule/list
GET /menu/all
```

Expected assertions:

```csharp
Assert.Equal(5, json.Data.Total);
Assert.Contains(json.Data.Items, item => item.Code == "MemoryHigh");
Assert.Contains(json.Data.Items, item => item.Code == "AbnormalFileDetected");
Assert.Contains(systemMonitor.Children, menu => menu is { Name: "AlertRule", Path: "/system/alert-rule" });
```

- [ ] **Step 2: Add failing tests for disabled rule and notification switch**

Add tests that:

```csharp
await _client.PutAsJsonAsync($"/system/alert-rule/{ruleId}", new
{
    level = "Warning",
    threshold = 1,
    windowMinutes = 1440,
    enabled = false,
    notifyEnabled = true,
    remark = "Disabled by test"
});
```

Then run the alert scan job and assert no `AbnormalFileDetected` alert is created.

Add a second test that sets `notifyEnabled = false`, runs the scan, asserts an alert exists, and asserts `/notification/my?category=SystemAlert` does not contain a notification for that alert.

- [ ] **Step 3: Run targeted tests and confirm failure**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "AlertRule|AlertScanJob"
```

Expected result: compile fails because alert rule contracts and APIs do not exist yet.

### Task 2: Entity, Repository, DbContext, And Seeding

**Files:**
- Create: `src/MiniAdmin.Domain/Entities/AlertRule.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfAlertRuleRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] **Step 1: Add `AlertRule` entity**

Create properties:

```csharp
public Guid Id { get; set; }
public string Code { get; set; } = string.Empty;
public string Name { get; set; } = string.Empty;
public string Description { get; set; } = string.Empty;
public string Metric { get; set; } = string.Empty;
public string Operator { get; set; } = ">=";
public decimal Threshold { get; set; }
public int WindowMinutes { get; set; } = 1440;
public string Level { get; set; } = "Warning";
public bool Enabled { get; set; } = true;
public bool NotifyEnabled { get; set; } = true;
public int Sort { get; set; }
public string? Remark { get; set; }
public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
```

- [ ] **Step 2: Map table `mini_alert_rules`**

Configure unique index on `Code`, string lengths, decimal precision, and boolean fields in `MiniAdminDbContext`.

- [ ] **Step 3: Add repository methods**

Implement:

```csharp
Task<PageResult<AlertRuleDto>> GetListAsync(AlertRuleListQuery query, CancellationToken cancellationToken);
Task<IReadOnlyList<AlertRuleDto>> GetEnabledAsync(CancellationToken cancellationToken);
Task<AlertRuleDto?> UpdateAsync(Guid id, UpdateAlertRuleRequest request, CancellationToken cancellationToken);
```

Validation:

```csharp
if (request.Threshold < 0) throw new ArgumentOutOfRangeException(nameof(request.Threshold));
if (request.WindowMinutes < 1) throw new ArgumentOutOfRangeException(nameof(request.WindowMinutes));
```

- [ ] **Step 4: Seed default alert rules**

Seed:

```text
MemoryHigh: threshold 85, level Warning, window 1
DependencyUnhealthy: threshold 1, level Critical, window 1
ScheduledJobFailed: threshold 1, level Warning, window 1440
AuditFailureHigh: threshold 1, level Warning, window 1440
AbnormalFileDetected: threshold 1, level Warning, window 1
```

### Task 3: Application Service, APIs, Permissions, And Scan Evaluation

**Files:**
- Create: `src/MiniAdmin.Application/Alerts/AlertRuleAppService.cs`
- Modify: `src/MiniAdmin.Application/Alerts/AlertAppService.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Alerts/AlertDtos.cs`

- [ ] **Step 1: Add application service**

`AlertRuleAppService` delegates list and update operations to the repository.

- [ ] **Step 2: Register dependencies**

Register:

```csharp
builder.Services.AddScoped<IAlertRuleAppService, AlertRuleAppService>();
services.AddScoped<IAlertRuleRepository, EfAlertRuleRepository>();
```

- [ ] **Step 3: Add APIs**

Add:

```http
GET /system/alert-rule/list
PUT /system/alert-rule/{id}
```

Protect them with:

```text
system:alert-rule:query
system:alert-rule:update
```

- [ ] **Step 4: Add menu and permissions**

Seed menu:

```text
Name: AlertRule
Path: /system/alert-rule
Component: /system/alert-rule/index
Parent: SystemMonitor
```

Seed permissions:

```text
system:alert-rule:query
system:alert-rule:update
```

- [ ] **Step 5: Make scan use rules**

`AlertAppService.ScanAsync` should:

```csharp
var rules = await alertRuleRepository.GetEnabledAsync(cancellationToken);
```

For each rule, generate the same alert types as today when the configured threshold is met. Pass only created alerts whose matching rule has `NotifyEnabled = true` into `CreateAlertNotificationsAsync`.

### Task 4: Frontend API And Page

**Files:**
- Create: `frontend/vue-vben-admin/apps/web-antd/src/api/system/alert-rule.ts`
- Create: `frontend/vue-vben-admin/apps/web-antd/src/views/system/alert-rule/index.vue`

- [ ] **Step 1: Add frontend API**

Expose:

```ts
getAlertRuleListApi(params)
updateAlertRuleApi(id, data)
```

- [ ] **Step 2: Add page**

Build a Vben page using Ant Design Vue:

- query bar: keyword, level, enabled
- table: code, name, level, threshold, windowMinutes, enabled, notifyEnabled, updatedAt, action
- edit modal: level, threshold, windowMinutes, enabled, notifyEnabled, remark

- [ ] **Step 3: Respect permissions**

Only show edit actions when:

```ts
hasAccessByCodes(['system:alert-rule:update'])
```

### Task 5: Verification, Summary, Commit, And Push

**Files:**
- Create: `docs/features/2026-05-28-alert-rules/03-summary.md`

- [ ] **Step 1: Run targeted backend tests**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "AlertRule|AlertScanJob|MenuAll"
```

- [ ] **Step 2: Run full backend tests**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

- [ ] **Step 3: Run frontend build**

Run from `frontend/vue-vben-admin`:

```powershell
pnpm run build:antd
```

- [ ] **Step 4: Write summary and commit**

Commit message:

```text
feat: add configurable alert rules
```

- [ ] **Step 5: Push**

Run:

```powershell
git -C C:\monica\code\mini-admin push
```
