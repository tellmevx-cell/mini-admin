# Contributing to MiniAdmin

Thanks for taking the time to improve MiniAdmin. The project aims to stay practical: small, reviewable changes with clear verification are preferred over large rewrites.

## Local Setup

1. Install .NET SDK 10, Node.js 22.18+ or 24+, and pnpm 11+.
2. Restore and run the backend:

```powershell
dotnet restore
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
```

3. Install and run the frontend:

```powershell
cd frontend/vue-vben-admin
pnpm install
pnpm run dev:antd
```

4. Open `http://localhost:5666`.

Default local accounts:

| Scenario | Tenant code | User name | Password |
| --- | --- | --- | --- |
| Platform admin | empty | `admin` | `123456` |
| Demo tenant | `demo` | `demo` | `123456` |

Change default passwords and `Jwt:SigningKey` before deploying outside a local development machine.

## Branches and Commits

- Use focused branches such as `feat/workflow-attachments`, `fix/tenant-menu-seed`, or `docs/docker-guide`.
- Keep commits small enough to review.
- Do not commit generated build output, local logs, `appsettings.Development.json`, `.env.local`, uploaded files, or database dumps.

## Verification

Run the checks that match your change:

```powershell
dotnet build MiniAdmin.slnx --no-restore
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
pnpm --dir docs-site build
```

For frontend changes:

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd run typecheck
pnpm -F @vben/web-antd run build
```

For Docker changes:

```powershell
docker compose config
docker compose up -d --build
```

## Database, Menus, and Permissions

When a change adds database structures, permissions, menus, or seed data:

- Include the migration or initializer change.
- Mention the new permission codes in the PR.
- Verify a fresh database can initialize successfully.
- Verify an existing database can start without duplicate seed errors.

## Security and Secrets

Never commit real values for database passwords, Redis passwords, MinIO keys, SMTP passwords, webhook secrets, JWT signing keys, or private host names. Use environment variables, `.env` files ignored by Git, or a secret manager.

If you suspect a secret was committed, rotate it first, then clean repository history if needed.

## Pull Requests

Every PR should explain:

- What changed and why.
- Which areas are affected.
- Whether migrations, seed data, permissions, menus, or deployment settings changed.
- Which verification commands were run.
