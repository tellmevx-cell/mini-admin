# Workflow Attachments And Comments Design

**Goal:** Add workflow collaboration basics so approval instances can carry uploaded files and discussion comments.

**Scope**
- Attach already-uploaded files to a workflow instance when starting an approval.
- Add attachments to an existing workflow instance from the detail drawer.
- Show workflow attachments in instance details with download support.
- Add plain comments to workflow instances from the detail drawer.
- Show comments in instance details.
- Notify relevant workflow participants when a new comment is added.

**Out Of Scope**
- Nested comment replies.
- File preview rendering beyond download links.
- Field-level attachment permissions.
- Attachment deletion and comment deletion.
- External delivery channels for comments.

**Data Model**
- `WorkflowAttachment`: instance-level link to `ManagedFile`, with uploader metadata and optional remark.
- `WorkflowComment`: instance-level plain-text comment, with author metadata and timestamp.
- `WorkflowInstance` gains `Attachments` and `Comments` navigation collections.

**Backend Flow**
- `StartWorkflowInstanceRequest` accepts optional attachment file IDs.
- Starting an instance validates file IDs and creates attachment links.
- `GetInstanceAsync` returns attachments and comments.
- `AddAttachmentAsync` validates access to the instance and file existence before linking.
- `AddCommentAsync` validates content, stores a comment, writes an action log, and sends `WorkflowComment` notifications to workflow participants except the author.

**Frontend Behavior**
- The start form includes an attachment uploader using existing `/system/file/upload`.
- Workflow detail shows attachments with download buttons.
- Workflow detail has a comment composer and comment list.
- After adding an attachment or comment, the drawer reloads the selected instance.
- Message center source type filter includes `WorkflowComment`.

**Testing**
- Backend tests cover start-with-attachments, adding comments, comment notifications, and duplicate attachment protection.
- Frontend build verifies new API types and templates.
