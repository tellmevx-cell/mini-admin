# Workflow Visibility Permissions Design

## Goal

Tighten workflow instance visibility and collaboration permissions before business modules depend on approval data. Workflow details, comments, attachments, and workflow attachment downloads should be available only to workflow participants or workflow managers.

## Scope

- Restrict workflow instance detail access to participants or workflow managers.
- Restrict instance lists so regular users see only participated instances, while workflow managers keep the all-instance management view.
- Restrict adding comments and attachments to participants or workflow managers.
- Add a workflow-scoped attachment download endpoint that checks workflow access before returning file content.
- Update the frontend to use the workflow-scoped download endpoint and clearer list labels.

## Access Model

Regular participants are:

- Instance initiator.
- Any current or historical task approver.
- Any user recorded in a `Cc` action log.

Workflow managers are users whose JWT permission claims include `workflow:definition:manage`. The existing `WorkflowUserContext` will carry a boolean `CanManageAllWorkflowInstances` flag with a default of `false`, so old tests and internal calls keep working unless they explicitly opt into management visibility.

## Backend Behavior

- `GetInstancesAsync` with `scope=all` returns participated instances for regular users and all tenant-scoped instances for workflow managers.
- `GetInstancesAsync` with `scope=startedByMe` remains initiator-only.
- `GetCcInstancesAsync` remains copied-only.
- `GetInstanceAsync`, `AddAttachmentAsync`, and `AddCommentAsync` return `null` or throw a workflow operation error when the user cannot access the instance.
- A new `GetAttachmentDownloadAsync(instanceId, attachmentId, user)` method returns authorized attachment file metadata only when the attachment belongs to the instance and the user has access. The API then calls the existing file service to stream bytes.

## Frontend Behavior

- Workflow center list label changes from "全部实例" to "我参与的"; users with definition-management permission see "全部管理视图".
- Workflow detail attachment download calls `/workflow/instance/{id}/attachments/{attachmentId}/download`.
- If a user reaches a workflow deep link without access, the API returns not found/forbidden-style failure and the existing empty-detail state is shown.

## Testing

- Regular users cannot see unrelated workflow detail.
- Regular users see participated instances in `scope=all`, not unrelated instances.
- Workflow managers can see unrelated workflow detail and all instances.
- Non-participants cannot add comments or attachments.
- Participants can download workflow attachments through the new endpoint.
- Non-participants cannot download workflow attachments through the new endpoint.
