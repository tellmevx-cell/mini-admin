# Workflow Approval Mode Design

**Goal:** Add configurable approval behavior for workflow approve nodes: Any approver or All approvers.

**Scope**
- Add `approvalMode` to workflow nodes with values `Any` and `All`.
- Default existing and newly created nodes to `Any` so current behavior remains compatible.
- `Any`: one approver approval completes the node and closes other pending tasks in the same node.
- `All`: each approver must approve their own task before the node completes.
- Any rejection still rejects the whole workflow and closes sibling pending tasks.
- Sequential approval, percentage thresholds, add-sign, and remove-sign are not included in this version.

**Data Model**
- `WorkflowNode.ApprovalMode` stores the node strategy.
- `SaveWorkflowNodeRequest` and `WorkflowNodeDto` expose `ApprovalMode`.
- Frontend node forms send and display `approvalMode`.

**Runtime Rules**
- Role approver nodes keep resolving to all enabled users in the role.
- Task generation remains one pending task per approver.
- When an approver approves:
  - If node is `Any`, keep current behavior: close sibling tasks and move forward.
  - If node is `All` and sibling pending tasks remain, keep the instance on the current node.
  - If node is `All` and no sibling pending tasks remain, move to the next runtime node.
- When an approver rejects, reject the workflow and close sibling pending tasks for both modes.

**UI**
- The workflow definition node property panel adds an approval mode select for approve nodes.
- The workflow detail task list continues to show each task status, making counters visible from task states.

**Testing**
- Add backend tests for `Any`, `All`, and `All` rejection.
- Run workflow regression tests and frontend build after implementation.
