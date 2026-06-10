# Code Generator Enterprise Template Design

## Objective

Enhance the code generator so generated business modules can opt into enterprise data-scope behavior while preserving the current preview-first workflow.

## Scope

This version adds generator-level configuration for `DataScopeMode`, `DataScopeField`, and `EnableAudit`. The implementation focuses on backend generated template behavior and the Vben generator configuration page. It does not create a new runtime audit subsystem because request audit and EF entity change audit already exist.

## Architecture

`CodeGeneratorPreviewRequest` carries enterprise template options. `CodeGeneratorAppService` validates those options and includes them in install guidance. `CodeGeneratorTemplateRenderer` branches only when data scope is enabled, keeping simple modules simple. Generated endpoints pass the authenticated username into generated queries and write operations, while generated repositories combine tenant filtering with `IDataScopeProvider` data-scope filtering.

## Data Scope Rules

- `None`: no generated data-scope code.
- `Department`: generated repository filters by the configured department field and supports `Department` and `DepartmentAndChildren` role scopes.
- `Self`: generated repository filters by the configured user field and supports self-owned data.

Update and delete use the same scoped source as list queries, so write operations cannot bypass data permissions.

## UI Design

The generator configuration area adds two controls near tenant mode:

- data-scope mode select
- data-scope field select

The field select uses currently selected fields. It is disabled when data scope is `None`.

## Verification

- Backend tests assert generated content contains data-scope dependencies and write guards.
- Existing code generator tests ensure no regression in tenant mode and history detail.
- Frontend build verifies the Vben page still compiles.
