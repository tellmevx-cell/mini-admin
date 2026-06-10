# Common Import Export Design

## Goal

Add reusable Excel import/export infrastructure, prove it on position management, then let the code generator emit the same enterprise-style import/export surface for generated CRUD modules.

## Backend Design

Create a common workbook contract in `MiniAdmin.Application.Contracts.Common`: `IWorkbookService` with `CreateWorkbook(rows)` and `ReadWorkbook(stream)`. Move the existing lightweight xlsx implementation from the user-specific service to a shared infrastructure implementation, and keep the user service compatible by adapting it to the common service or replacing its registration.

Position management gets import/export DTOs:

- `PositionImportErrorDto(rowNumber, code, message)`
- `PositionImportResultDto(createdCount, errors)`

`IPositionAppService` gains:

- `GetExportRowsAsync(query)`
- `GetImportTemplateRows()`
- `PreviewImportAsync(stream)`
- `ImportAsync(stream)`
- `CreateImportErrorReport(errors)`

The repository stays responsible for tenant-aware create/list behavior. The app service validates rows before creation: code and name required, order must be an integer, code cannot duplicate existing positions in the current tenant scope or within the same upload.

Minimal API endpoints:

- `GET /system/position/export`
- `GET /system/position/import-template`
- `POST /system/position/import/preview`
- `POST /system/position/import`
- `POST /system/position/import/error-report`

## Frontend Design

The position page keeps its current compact management style. Add toolbar buttons for export, template download and import. Import uses a hidden file input and a modal similar to user import preview: summary cards, error table and error report download.

## Code Generator Design

`CodeGeneratorPreviewRequest` adds `EnableImportExport`. The code generator page shows a switch under enterprise template options. When enabled, generated backend code includes import/export endpoints and generated frontend code includes toolbar actions. Generated menus include import/export permissions with deterministic IDs.

## Safety Rules

- Preview never writes data.
- Confirm import refuses to proceed when validation errors exist.
- Export follows existing query and tenant filtering.
- No long-term storage of uploaded Excel files.
- Generated import/export defaults to simple scalar fields only; complex relation fields stay out of scope for this phase.

