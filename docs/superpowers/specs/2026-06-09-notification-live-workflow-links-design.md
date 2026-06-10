# Notification Live Workflow Links Design

## Goal

Make the message center feel live enough for daily workflow use: unread counts should stay synchronized between the top notification bell and the message center, workflow notifications should continue to deep-link into the approval drawer, and operators should be able to filter messages by human-friendly source groups.

## Scope

- Reuse existing `/notification/my`, mark-read, mark-all-read, delete, and clear APIs.
- Keep the current workflow notification links, such as `/workflow/center?workflowInstanceId=...&workflowTaskId=...`.
- Add a frontend shared notification store so layout and message center mutate the same unread state.
- Refresh notifications on polling, dropdown open, tab focus, and page visibility return.
- Improve source filtering labels around workflow, alerts, and business messages.

## Non-Goals

- No WebSocket or SignalR push channel in this step.
- No new notification tables.
- No email/Webhook retry implementation.
- No broad workflow engine changes.

## Architecture

The frontend will own a small `notification` Pinia store under `apps/web-antd/src/store`. The store wraps existing notification APIs and exposes recent notifications, unread count, loading state, and mutation helpers. The top layout consumes the store instead of maintaining local notification state. The message center keeps its paged table query state locally, but updates the shared unread count after loading and uses store mutations for read/delete actions.

Workflow deep links remain URL-based. `createRouteLocationFromLink` continues to parse the link and preserve `workflowInstanceId` / `workflowTaskId`, while the approval center opens the drawer from route query.

## Error Handling

- Polling and focus refresh failures are logged and do not interrupt the page.
- Manual message actions still surface normal API failures through the existing request client behavior.
- If a notification has no link, clicking it only marks it as read.

## Verification

- `pnpm run build:antd`
- Existing backend workflow/notification tests remain sufficient because no backend contract changes are required.
- Manual checks: open the notification dropdown, mark a message read in the message center, confirm the top unread badge updates immediately, and click a workflow message to open the workflow drawer.
