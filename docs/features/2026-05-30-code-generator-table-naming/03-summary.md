# Code Generator Table Naming Summary

## Result

- Code generator default form no longer points to `SampleOrder`.
- Selecting a table now derives module name, route path, and permission prefix from that table.
- Common generated table prefixes `biz_` and `mini_` are stripped before deriving names.
- The page now shows generated target paths before preview/generate, making conflict reasons visible.

## Verification

- `npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue`: passed with `[]`.
- `pnpm run build:antd`: Vben `@vben/web-antd` build tasks completed successfully, 11/11 tasks. The existing environment still prints `Requested version v22.22.0 is not currently installed` after successful build output.
- `Invoke-RestMethod -Uri http://localhost:5320/health`: returned `MiniAdmin.Api Healthy`.
- `Invoke-WebRequest -Uri http://localhost:5666/ -UseBasicParsing`: returned HTTP 200.
