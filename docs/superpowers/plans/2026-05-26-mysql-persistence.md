# MySQL Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace fake login/user/menu data with EF Core persistence, using MySQL first while keeping the database provider replaceable.

**Architecture:** Domain owns RBAC entities. Application owns use cases and depends on repository abstractions. Infrastructure owns EF Core, MySQL provider registration, repositories, password hashing, and seed data. API selects the database provider from configuration and initializes the database at startup.

**Tech Stack:** .NET 10, EF Core, Pomelo.EntityFrameworkCore.MySql, MySQL, xUnit integration tests.

---

## Tasks

### Task 1: Red Tests

- Add tests proving login succeeds from seeded database data.
- Add tests proving wrong password is rejected.
- Add tests proving `/auth/codes` and `/menu/all` come from seeded role-menu data.
- Verify tests fail because current services still use fake data.

### Task 2: Domain And Contracts

- Add `User`, `Role`, `Menu`, `UserRole`, `RoleMenu` entities.
- Add `IAuthRepository`, `IUserRepository`, and `IMenuRepository` contracts.
- Update auth service to depend on repositories and password hasher abstraction.

### Task 3: Infrastructure EF Core

- Add EF Core packages.
- Add `MiniAdminDbContext`.
- Add EF repository implementations.
- Add password hashing service.
- Add database initializer with admin/user/role/menu seed data.
- Add provider registration extension supporting `mysql` and test-time in-memory/SQLite provider replacement.

### Task 4: API Wiring

- Add `Database` settings to configuration.
- Register infrastructure persistence.
- Run database initialization on startup when enabled.
- Keep real secrets out of committed config.

### Task 5: Verification

- Run tests.
- Run build.
- If a MySQL connection string is available, run database initialization against MySQL.
- Confirm Vben login still works.

## Initial Seed Data

```text
User:
  username: admin
  password: 123456
  realName: Admin

Role:
  code: admin
  name: Administrator

Menus:
  Dashboard
    Analytics

Permission codes:
  system:user:query
  system:user:add
  system:user:edit
  system:user:remove
  system:role:query
  system:menu:query
```

## Self-Review

- Database provider is not hard-coded into Application or Domain.
- MySQL is the first real provider.
- Seed data is explicit.
- Tests drive replacement of fake data.
