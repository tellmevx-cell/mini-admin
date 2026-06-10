# Code Generator History Entry And Customer Demo Summary

## Result

- Code generator history is now a top-level tab beside `生成配置`, so it is no longer hidden at the bottom of the page.
- Successful generation switches the page to the `生成记录` tab after reloading history.
- The generated customer demo was renamed from `mini_notices` copy to customer-oriented labels and messages.
- The generated sample order demo was renamed from `mini_files` copy to sample-order-oriented labels and messages.
- `Customer` now maps to `mini_customer`, and `SampleOrder` now maps to `biz_sample_order`, avoiding conflicts with real system tables.
- MySQL startup schema repair now creates `mini_customer` and `biz_sample_order` when they are missing.

## Verification

- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`: passed with 0 warnings and 0 errors.
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator|Customer|SampleOrder"`: passed, 9/9 tests.
- `npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue frontend\vue-vben-admin\apps\web-antd\src\views\business\customer\index.vue frontend\vue-vben-admin\apps\web-antd\src\views\business\sample-order\index.vue`: passed with `[]`.
- `pnpm run build:antd`: Vben `@vben/web-antd` build tasks completed successfully, 11/11 tasks. The existing environment still prints `Requested version v22.22.0 is not currently installed` after the successful build output.
- `Invoke-RestMethod -Uri http://localhost:5320/health`: returned `MiniAdmin.Api Healthy`.
- `Invoke-WebRequest -Uri http://localhost:5666/ -UseBasicParsing`: returned HTTP 200.

## Notes

The in-app browser plugin failed to attach because its local runtime exited during setup. Backend and frontend service checks still passed through direct HTTP requests.
