# Official Vben Login Loop

This stage connects the official Vben Admin frontend to MiniAdmin with fake backend data.

## What We Built

The backend now exposes the first four endpoints needed by the official Vben login flow:

```text
POST /auth/login
GET  /user/info
GET  /auth/codes
GET  /menu/all
```

These endpoints are intentionally backed by fake data for now. This lets us verify the frontend-backend contract before introducing MySQL, JWT, password hashing, and real RBAC tables.

## Temporary Credentials

```text
username: admin
password: 123456
```

## Response Wrapper

The backend returns a simple Vben-friendly response envelope:

```json
{
  "code": 0,
  "data": {},
  "message": "ok"
}
```

This is defined in:

```text
src/MiniAdmin.Shared/ApiResponse.cs
```

## Layer Responsibilities In This Stage

`MiniAdmin.Api` owns HTTP concerns:

- endpoint routes;
- CORS for local Vben development;
- dependency injection;
- wrapping application results into `ApiResponse<T>`.

`MiniAdmin.Application.Contracts` owns frontend-facing contracts:

- login request and result DTOs;
- current user DTO;
- Vben menu DTO;
- service interfaces.

`MiniAdmin.Application` owns fake use cases:

- fake login;
- fake current user;
- fake permission codes;
- fake backend menus.

## Why Fake Data First

Fake data is a learning tool here. It proves the contract before we add infrastructure.

The next evolution is:

```text
fake token
  -> real JWT
  -> MySQL users, roles, menus
  -> permission codes generated from role-menu relations
```

## Verification

The integration tests live in:

```text
tests/MiniAdmin.Tests/VbenLoginLoopTests.cs
```

Run:

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
dotnet build MiniAdmin.slnx
```

Expected result:

```text
4 tests pass.
Build succeeds with 0 errors.
```

## Official Vben Next Step

The official Vben Admin repository should be cloned to:

```text
frontend/vue-vben-admin
```

Command:

```powershell
git clone https://github.com/vbenjs/vue-vben-admin.git frontend/vue-vben-admin
```

After clone, configure the official Vben Ant Design Vue app to call MiniAdmin:

```text
backend API: http://localhost:5320
login:       POST /auth/login
user info:   GET  /user/info
access code: GET  /auth/codes
menus:       GET  /menu/all
```

Vben should use backend access mode:

```text
accessMode = backend
```

## Official Vben Changes Applied

The official Vben Ant Design Vue app is located at:

```text
frontend/vue-vben-admin/apps/web-antd
```

The development proxy was updated in:

```text
frontend/vue-vben-admin/apps/web-antd/vite.config.ts
```

The `/api` prefix is removed before forwarding requests to MiniAdmin:

```text
/api/auth/login -> http://localhost:5320/auth/login
```

The development mock server was disabled in:

```text
frontend/vue-vben-admin/apps/web-antd/.env.development
```

```text
VITE_NITRO_MOCK=false
```

Backend access mode was enabled in:

```text
frontend/vue-vben-admin/apps/web-antd/src/preferences.ts
```

```text
accessMode: 'backend'
```

The official Vben API files already matched our MiniAdmin endpoint names:

```text
frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts
frontend/vue-vben-admin/apps/web-antd/src/api/core/user.ts
frontend/vue-vben-admin/apps/web-antd/src/api/core/menu.ts
```

## Running Locally

Start the backend:

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5320
```

Start the official Vben frontend:

```powershell
cd frontend/vue-vben-admin
pnpm run dev:antd
```

Open:

```text
http://localhost:5666
```

Login with:

```text
username: admin
password: 123456
```

## Current Verification

Backend tests:

```text
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
4 tests passed.
```

Backend build:

```text
dotnet build MiniAdmin.slnx
0 warnings, 0 errors.
```

Frontend build:

```text
pnpm -F @vben/web-antd run build
built successfully.
```

Vben dev proxy:

```text
POST http://localhost:5666/api/auth/login
returns code=0 and accessToken=mini-admin-fake-access-token.
```

## JWT Upgrade

The login token is no longer a fixed fake string. It is now a real JWT signed by MiniAdmin.

The fake data still exists:

- username: `admin`
- password: `123456`
- role: `admin`
- permission codes: `system:user:query`, `system:user:add`, and related demo codes

But the access token is now real:

```text
header.payload.signature
```

Protected endpoints now require:

```text
Authorization: Bearer <accessToken>
```

Protected endpoints:

```text
GET /user/info
GET /auth/codes
GET /menu/all
```

Public endpoint:

```text
POST /auth/login
```

JWT settings are currently stored in:

```text
src/MiniAdmin.Api/appsettings.json
```

The development signing key is only for local learning. Before production, move secrets out of committed config and use environment variables, user secrets, or a deployment secret manager.

The token issuer implementation is in:

```text
src/MiniAdmin.Infrastructure/Auth/JwtTokenService.cs
```

This keeps technical token generation outside the Application layer.
