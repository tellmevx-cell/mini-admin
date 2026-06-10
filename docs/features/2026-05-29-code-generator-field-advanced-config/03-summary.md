# Code Generator Field Advanced Config Summary

## Result

- Field config now supports query mode, max length, unique index, default value, and dictionary code.
- Generated contracts include query DTO fields for `Contains`, `Equals`, and `Range`.
- Generated repositories apply field-specific filters.
- Generated EF mappings use configured string length and unique indexes.
- Generated Vben pages include query controls and form controls based on field config.
- The code generator page exposes these options in the field configuration table.

## Verification

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGeneratorPreview_Uses_Field_Advanced_Config"` passed: 1/1.
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"` passed: 7/7.
- `npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue` returned `[]`.
- `pnpm run build:antd` passed for `@vben/web-antd`.
- Backend health check passed: `http://localhost:5320/health` returned `MiniAdmin.Api Healthy`.
- Frontend dev server check passed: `http://localhost:5666/` returned `200 OK`.
