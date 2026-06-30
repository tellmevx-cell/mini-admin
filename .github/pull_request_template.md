## Summary

- 

## Affected Areas

- [ ] Backend API
- [ ] Frontend UI
- [ ] Workflow / message center
- [ ] Tenant / SaaS
- [ ] Code generator
- [ ] Database schema or seed data
- [ ] Documentation / deployment

## Verification

Run the relevant commands and paste the result summary.

- [ ] `dotnet build MiniAdmin.slnx --no-restore`
- [ ] `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj`
- [ ] `pnpm --dir docs-site build`
- [ ] `pnpm -C frontend/vue-vben-admin -F @vben/web-antd run typecheck`
- [ ] Manual UI verification completed

## Compatibility Checklist

- [ ] No real secrets, passwords, tokens, private URLs, or local config files are committed.
- [ ] New permissions, menus, seed data, or migrations are documented.
- [ ] User-facing behavior changes are reflected in README or docs-site when needed.
- [ ] Existing workflow, tenant, and message-center flows are not unintentionally changed.
