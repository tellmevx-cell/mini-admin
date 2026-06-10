# Code Generator Install Wizard Summary

## Result

- Code generator preview now returns an install plan.
- The install plan reports file readiness, table existence, generation, backend restart, and verification steps.
- When the target table does not exist, preview returns a MySQL `CREATE TABLE` draft generated from selected fields.
- Tenant mode SQL includes `TenantId` and a tenant index.
- Vben code generator page now shows install readiness and the SQL draft before file preview.

## Notes

- The first version focuses on guidance, not automatic database changes.
- The generated SQL is intentionally a draft so developers can review naming, indexes, and field lengths before applying it to MySQL.
- A template guard now filters reserved system fields such as `Id`, `TenantId`, and `CreatedAt` even if they are manually passed to the generator.
- Existing generated `Customer` demo files were adjusted to remove duplicated `CreatedAt` definitions so the workspace can compile.

## Verification

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGeneratorPreview_Returns_InstallPlan"` passed: 1/1.
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"` passed: 6/6.
- `pnpm run build:antd` passed for `@vben/web-antd`.
- `npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue` returned `[]`.
- Backend health check passed: `http://localhost:5320/health` returned `MiniAdmin.Api Healthy`.
- Frontend dev server check passed: `http://localhost:5666/` returned `200 OK`.
