# MiniAdmin Learning Rebuild + Official Vben Integration Design

## 1. Final Goal

MiniAdmin will be built as a personal learning-oriented backend management system.

The backend will learn from the RBAC-focused design of `yiabp-mini`, but it will not copy that codebase or depend on its bundled `Yi.Vben5` frontend.

The frontend integration target is the official Vben Admin project:

- Official docs: https://doc.vben.pro/
- Official repository: https://github.com/vbenjs/vue-vben-admin

The preferred integration style is:

- keep the official Vben frontend close to its default structure;
- implement backend endpoints that match Vben's expected authentication, user info, access code, and backend menu contracts;
- only adjust Vben configuration when necessary, such as API base URL and backend access mode.

## 2. Reference Scope

`C:\tmp\yiabp-mini` is used as a reference project only.

Useful reference areas:

- RBAC module boundaries.
- User, role, menu, department, post, dictionary, config, notice, and log concepts.
- Permission code style, such as `system:user:query` and `system:role:edit`.
- Dynamic menu and button permission ideas.

Out of scope:

- Do not copy the Yi.Abp framework infrastructure.
- Do not reuse the bundled `Yi.Vben5` frontend as the target frontend.
- Do not start from tenant management, file management, workflow, code generation, or AI skill automation.
- Do not introduce ABP framework complexity before the core ideas are understood.

## 3. Product Direction

The first complete product milestone is a small but real RBAC backend that can power an official Vben Admin frontend.

The system should eventually support:

- login and logout;
- current user profile;
- JWT authentication;
- role-based access;
- button permission codes;
- backend dynamic menus;
- user management;
- role management;
- menu management;
- department management;
- basic audit and login logs.

The early teaching goal is not to finish all modules quickly. The goal is to build each capability in a way that explains:

- where the code belongs;
- why the layer owns that responsibility;
- how the frontend contract shapes the backend API;
- how the temporary fake-data version evolves into the database-backed version.

## 4. First Milestone

The first milestone is the official Vben login loop.

Target flow:

```text
Official Vben Login Page
  -> POST /auth/login
  -> store access token
  -> GET /user/info
  -> GET /auth/codes
  -> GET backend menu data
  -> enter the admin home page
```

This milestone starts with fake data. The fake data is intentional because it isolates the frontend-backend contract before adding database persistence.

The first backend endpoints are:

```text
POST /auth/login
GET  /user/info
GET  /auth/codes
GET  /menu/all
```

The exact menu endpoint name can be adjusted after checking the official Vben project version in use, but the backend concept is fixed: Vben should receive dynamic menu data from MiniAdmin when backend access mode is enabled.

## 5. Backend Architecture

The current project structure remains the foundation:

```text
src/
  MiniAdmin.Api/
  MiniAdmin.Application/
  MiniAdmin.Application.Contracts/
  MiniAdmin.Domain/
  MiniAdmin.Domain.Shared/
  MiniAdmin.Infrastructure/
  MiniAdmin.Shared/
```

Responsibilities:

- `MiniAdmin.Api`: HTTP endpoints, middleware, authentication setup, dependency injection entry.
- `MiniAdmin.Application.Contracts`: request DTOs, response DTOs, service interfaces, frontend-facing contracts.
- `MiniAdmin.Application`: application services and use-case orchestration.
- `MiniAdmin.Domain`: domain entities and business rules.
- `MiniAdmin.Domain.Shared`: shared domain enums, constants, and permission names.
- `MiniAdmin.Infrastructure`: database, repositories, password hashing, token persistence if needed, and external integrations.
- `MiniAdmin.Shared`: truly cross-cutting primitives such as API response wrappers and pagination models.

The first fake-data milestone should still respect these boundaries. Fake data can live in Application temporarily, then move behind repository interfaces when persistence is introduced.

## 6. API Response Contract

MiniAdmin should provide a consistent response shape that Vben can transform easily.

The preferred initial response shape is:

```json
{
  "code": 0,
  "data": {},
  "message": "ok"
}
```

Reason:

- It is simple for learning.
- It matches common Vben mock API examples.
- It is easier to map in the official Vben request client.

If a selected official Vben version expects a different shape, such as a custom transform response, the backend response wrapper can be adapted while keeping the internal `ApiResponse<T>` abstraction stable.

## 7. Authentication Design

Initial authentication is username and password login.

First version:

- Accept a username and password.
- Validate against fake users.
- Return an access token-like string.
- Use a simple authenticated user context for protected endpoints.

Second version:

- Replace fake token with JWT.
- Add JWT bearer authentication in `MiniAdmin.Api`.
- Add expiration.
- Store user ID, username, role codes, and permission codes as claims or resolve them from the database.

Later version:

- Add refresh token if the official Vben setup requires it.
- Add login log.
- Add account lockout and password policy.

## 8. Access Control Design

MiniAdmin will support two related access concepts:

- Role codes, such as `admin`.
- Permission codes, such as `system:user:query`.

Role codes are useful for broad access decisions. Permission codes are used for page actions and buttons.

The first milestone returns hard-coded access codes from:

```text
GET /auth/codes
```

Later, these codes will come from:

```text
User -> UserRoles -> Roles -> RoleMenus -> Menus -> PermissionCode
```

The permission code naming style should follow the RuoYi/Yi.Mini style because it is clear and practical:

```text
system:user:query
system:user:add
system:user:edit
system:user:remove
system:role:query
system:menu:query
```

## 9. Menu Design

The backend will eventually own dynamic menus.

A menu has two responsibilities:

- It appears in the sidebar or route tree.
- It can carry a permission code for access control.

The first fake-data menu response should include only enough routes to prove Vben can render backend menus.

Later, menu persistence should include:

- menu ID;
- parent ID;
- menu name;
- route path;
- route name;
- component path or component key;
- icon;
- sort order;
- visibility;
- cache flag;
- permission code;
- status.

## 10. Learning Phases

### Phase 1: Clean Backend Foundation

Status: started.

Goals:

- keep the solution buildable;
- remove template noise;
- keep a `/health` endpoint;
- understand project layers.

### Phase 2: Official Vben Login Loop With Fake Data

Goals:

- implement unified response wrapper;
- implement `POST /auth/login`;
- implement `GET /user/info`;
- implement `GET /auth/codes`;
- implement backend menu endpoint;
- configure official Vben to call MiniAdmin;
- enter the Vben home page successfully.

### Phase 3: Real JWT Authentication

Goals:

- add JWT bearer authentication;
- protect user info, access code, and menu endpoints;
- attach current user claims;
- handle 401 and 403 responses clearly.

### Phase 4: Database Persistence

Goals:

- configure MySQL as the database provider;
- add entities for users, roles, menus, departments, and relationship tables;
- seed the first admin user;
- replace fake data with repository-backed data.

### Phase 5: RBAC Management APIs

Goals:

- user CRUD;
- role CRUD;
- menu CRUD;
- department CRUD;
- assign roles to users;
- assign menus to roles;
- generate user permission codes from role-menu relations.

### Phase 6: Vben Management Pages

Goals:

- use official Vben views and components where practical;
- add or adapt pages for user, role, menu, and department management;
- keep frontend changes small and understandable.

## 11. Testing And Verification

Each phase must have concrete verification.

Examples:

- `dotnet build MiniAdmin.slnx` succeeds.
- `/health` returns success.
- `/auth/login` returns token data.
- `/user/info` returns the logged-in user shape expected by Vben.
- `/auth/codes` returns permission code strings.
- official Vben can log in and enter the app.
- protected endpoints reject missing or invalid tokens after JWT is introduced.

## 12. Decisions

Confirmed decisions:

- Build a personal system through learning-oriented reconstruction.
- Use `yiabp-mini` as reference only.
- Use official Vben Admin as the frontend target.
- Prefer backend compatibility with official Vben defaults.
- Start with fake data to validate the frontend-backend contract.
- Add database and real RBAC after the first login loop is visible.

Default choices for later phases:

- Use the official Vben Ant Design Vue app variant first, because the reference ecosystem and common admin examples are closest to it.
- Use MySQL for the first persistent version, because the target environment already has an online MySQL database available. Keep connection strings in configuration or user secrets and do not commit real credentials.
- Do not add refresh tokens in the first JWT implementation. Add them after basic login, protected endpoints, and token expiration are understood.
