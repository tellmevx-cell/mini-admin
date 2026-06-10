# Notification Policy Management Design

## Goal

Add a configurable notification policy layer for workflow and message center events so administrators can decide which events create notifications without changing code.

## Current State

- Notification templates already support list, preview, and update.
- Workflow events create in-app notifications through `EfWorkflowRepository`.
- Email delivery exists for alert notifications, while workflow notifications currently only create in-app messages.
- The missing layer is policy: whether a workflow event should notify, which in-app channel toggle is active, and which future channels are reserved.

## Scope

This increment adds policy management for workflow notification events:

- `WorkflowTask`
- `WorkflowApprove`
- `WorkflowReject`
- `WorkflowWithdraw`
- `WorkflowTransfer`
- `WorkflowRemind`
- `WorkflowOverdue`
- `WorkflowCc`
- `WorkflowComment`

The first executable channel remains `InApp`. `Email` and `Webhook` are stored and shown as reserved toggles, but they do not send workflow deliveries yet.

## Data Model

Create `NotificationPolicy` with:

- `EventCode`: stable event key, aligned with source type and template code.
- `EventName`: display name.
- `Category`: group, initially `Workflow`.
- `RecipientStrategy`: display-only strategy text, initially `WorkflowDefault`.
- `EnableInApp`: whether to create `UserNotification`.
- `EnableEmail`: reserved for future workflow email delivery.
- `EnableWebhook`: reserved for future webhook delivery.
- `IsEnabled`: master switch.
- `Remark`, `CreatedAt`, `UpdatedAt`.

Missing policies are treated as enabled for backward compatibility.

## Backend Flow

Before workflow creates an in-app notification, it checks:

- no policy exists: allow
- policy exists and `IsEnabled && EnableInApp`: allow
- otherwise: skip notification

Template rendering stays unchanged and still decides the message title/body/link.

## Frontend Flow

Add a `通知策略` tab in the existing notification center:

- list workflow event policies
- show event, recipient strategy, channel toggles, status, remark
- edit toggles and remark in a modal

Template configuration remains in the existing tab.

## Testing

- A disabled in-app policy prevents workflow task notification creation.
- Missing policy keeps existing behavior.
- Policy app service can list and update strategy toggles.
- Frontend build verifies API and page wiring.
