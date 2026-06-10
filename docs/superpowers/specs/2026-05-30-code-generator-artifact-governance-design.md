# Code Generator Artifact Governance Design

## Goal

Add a safe rollback path for generated artifacts so test modules and mistaken generated modules can be removed from code and authorization data. Business tables are preserved by default, with an explicit dangerous option for deleting generated tables and data.

## Backend Design

Add `RollbackAsync(historyId, request, operatorUserId, operatorUserName)` to the code generator app service.

The app service loads the generation history, validates that it exists and is not already rolled back, then deletes only paths recorded in `FilesJson`. Paths continue to use the existing allowed-root guard. After file deletion, it removes empty directories created for generated module folders.

If the history is already `RolledBack`, the app service only allows a follow-up request when `dropTable` is true. This handles the case where a user first rolled back code/menu artifacts and later decides to clean the generated business table.

Database cleanup is delegated to the repository:

- calculate generated menu IDs from the original preview using the same deterministic GUID helper used by generation.
- delete `RoleMenus` for those menu IDs.
- delete generated `Menus`.
- update history status to `RolledBack`.
- clear Admin authorization cache.
- if `dropTable` is true, validate the table name and try to drop the table in MySQL.

The rollback endpoint accepts `system:code-generator:rollback` or `system:code-generator:generate` so existing Admin users with code generation permission can roll back before the new permission is propagated.

## Frontend Design

The code generator page adds a rollback action in the generation history table and history detail drawer. The action is shown only for successful records and opens a rollback modal.

The UI copy explicitly states that rollback removes generated code files and menu permissions. A checkbox is required before the request sends `dropTable: true`.

## Safety Rules

- Do not delete business tables by default.
- Only delete a business table after explicit user confirmation.
- Only attempt table deletion for safe table names and supported MySQL connections.
- Do not delete paths outside generator allowed roots.
- Do not allow rollback of failed or already rolled-back records.
- Do not treat missing files as fatal; if a file has already been manually removed, continue cleanup and mark the record rolled back.
