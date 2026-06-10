# Code Generator Auto Install Design

## Goal

Make generated modules install their database-side foundation immediately after file generation: table creation when possible, menu permissions, Admin role assignment, and authorization cache refresh.

## Scope

This phase installs database records only. Newly generated C# endpoints, repositories, services, and EF configurations still require rebuilding and restarting the backend.

## Backend Design

`CodeGeneratorGenerateRequest` gets an `AutoInstall` flag with default `true`.

After files are written successfully, `CodeGeneratorAppService.GenerateAsync` runs an auto-install step when the flag is enabled:

- Build the same create-table SQL used by preview.
- Ask the repository whether the table exists.
- Execute the create-table SQL only when the provider is MySQL and the table is missing.
- Upsert the generated menu and four button permission records.
- Ensure Admin role owns all five generated menu IDs.
- Remove Admin authorization cache so menus and permission codes refresh.

The generated menu IDs use `CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, purpose)` so runtime auto-install and generated `MenuSeed` produce the same rows.

## Frontend Design

The code generator page adds a small switch in the action area:

- label: `生成后自动安装数据库表和菜单权限`
- default: enabled

Generate calls `generateCodeApi(preview, overwrite, autoInstall)`.

The success message explains that database table and menu permissions have been installed when the switch is enabled, while backend restart is still needed to load the new API code.

## Testing

Add integration tests under the existing code generator tests:

- enabled auto-install creates menu permissions and Admin role assignments immediately.
- disabled auto-install does not create generated menu permissions.

The test environment uses EF InMemory, so table execution itself is not asserted there; MySQL execution is covered by provider guard and the existing generated SQL plan.
