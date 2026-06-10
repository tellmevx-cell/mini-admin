# Workflow SLA Auto Reminder Design

**Goal:** Add first-version workflow SLA support so pending approval tasks can show deadlines, become overdue, and trigger automatic reminder notifications.

**Scope**
- Add a per-approval-node SLA setting in minutes.
- Calculate task deadline when pending tasks are created.
- Show deadline and overdue state in workflow todo lists and workflow details.
- Add a scheduled job that scans overdue pending tasks.
- Create one automatic reminder notification per task per scan window, with deduplication to avoid repeated spam.
- Add scheduled job execution details for each reminded overdue task.

**Out Of Scope**
- Automatic transfer, escalation, or reassignment.
- Working calendar / holiday calendar.
- Per-tenant SLA policy templates.
- SLA pause/resume.
- Email/SMS/DingTalk delivery.
- Complex reminder frequency rules beyond built-in deduplication.

**Data Model**
- `WorkflowNode.SlaMinutes`: nullable integer. `null` or `0` means no SLA.
- `WorkflowTask.DueAt`: nullable timestamp calculated from `CreatedAt + SlaMinutes`.
- `WorkflowTask.LastAutoRemindedAt`: nullable timestamp used to deduplicate automatic reminders.
- Existing pending/completed task status remains unchanged. Overdue is derived from `Status == Pending && DueAt < now`.

**Backend Flow**
- Definition save validates `SlaMinutes >= 0`.
- Task creation copies SLA from the runtime node and sets `DueAt`.
- Scheduled job key: `workflow-sla-scan`.
- Scheduled executor calls a workflow SLA scanner.
- Scanner finds pending tasks with a due time earlier than scan time and no recent auto reminder.
- Scanner writes a `WorkflowOverdue` action log and a `WorkflowOverdue` user notification.
- Scanner sets `LastAutoRemindedAt` to the scan time.
- Scanner returns counts and scheduled-job details.

**Frontend Behavior**
- Approval node properties show an optional "处理时限（分钟）" input.
- Todo/done/instance task DTOs include `dueAt`, `isOverdue`, and `lastAutoRemindedAt`.
- Approval center tables show deadline and overdue tag.
- Workflow detail task cards show deadline and overdue status.
- Message center source type filter includes "审批超时".

**Testing**
- Backend tests cover SLA validation, task deadline generation, overdue scan notification creation, and scan deduplication.
- Existing workflow notification tests should keep passing.
- Frontend build verifies type/template correctness.
