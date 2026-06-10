# Code Generator History Detail Summary

## Result

Added a generation history detail flow for the code generator.

- Backend now exposes `GET /system/code-generator/history/{id}` with `system:code-generator:query` permission.
- The detail response includes generation metadata, operator information, original preview request JSON, generated files, and a rebuilt install plan.
- The frontend history table now has a detail action that opens a drawer with summary information, install steps, SQL draft, file list, and request JSON.

## Verification

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGeneratorHistoryDetail"`: passed, 2/2 tests.
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"`: passed, 9/9 tests.
- `npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue`: passed with `[]`.
- `pnpm run build:antd`: Vben `@vben/web-antd` build tasks completed successfully. The existing environment still prints `Requested version v22.22.0 is not currently installed` after the build task output.
- `Invoke-RestMethod -Uri http://localhost:5320/health`: returned `MiniAdmin.Api Healthy`.
- `Invoke-WebRequest -Uri http://localhost:5666/ -UseBasicParsing`: returned HTTP 200.
