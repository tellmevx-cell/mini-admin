# Workflow Version Governance Design

## Goal

Strengthen workflow version governance so published definitions are immutable, changes go through a new draft version, and every started instance preserves the definition version that created it.

## Current State

- Workflow definitions already support `Draft`, `Published`, and `Archived` statuses.
- Publishing a new version archives the previous published version with the same code.
- Starting a workflow already requires a published, enabled definition.
- Workflow instances currently store the definition id and name, but not the definition code, version, or a frozen launch-time snapshot.

## Design

### Immutable Published Definitions

Only `Draft` definitions can be saved from the designer. Once a definition is `Published` or `Archived`, direct edits are rejected with guidance to create a new draft version. This keeps historical process meaning stable even before any instance is started.

### Version Snapshot on Start

When an instance is started, the backend stores:

- `DefinitionCode`: the definition code at launch time.
- `DefinitionVersion`: the numeric version at launch time.
- `DefinitionSnapshotJson`: a compact JSON snapshot containing definition identity, version metadata, form JSON, designer JSON, and enabled nodes.

The running instance still references the original `DefinitionId` for relational integrity, but UI and audit views use the stored launch-time fields for historical clarity.

### UI Behavior

Workflow definition editing should clearly indicate that published or archived versions are read-only. The details drawer should show the launch version, for example `请假审批示例 v1`, so users can distinguish old instances from newer versions.

### Database Compatibility

Existing MySQL databases receive additive columns through the startup initializer:

- `mini_workflow_instances.DefinitionCode`
- `mini_workflow_instances.DefinitionVersion`
- `mini_workflow_instances.DefinitionSnapshotJson`

Existing rows are backfilled from the linked workflow definition when possible, with safe defaults otherwise.

## Testing

- Published definitions cannot be updated directly, even if no instance exists.
- Started instances store definition code, version, and snapshot JSON.
- Old instances keep their original version after a new definition version is published.
- Frontend build verifies TypeScript/template compatibility.
