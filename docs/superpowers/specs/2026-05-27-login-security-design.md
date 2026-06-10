# Login Security Design

## Goal

Add enterprise-style login protection without hurting normal admin usage: show a captcha only after suspicious login failures, temporarily lock repeated failed login attempts, and keep all outcomes visible in login logs.

## Scope

- Add a captcha endpoint for the Vben login page.
- Extend login requests with optional captcha fields.
- Track failed login attempts by `username + clientIp`.
- Require captcha after repeated failures.
- Temporarily lock login after more repeated failures.
- Store captcha and failure state in the existing distributed cache stack, so Redis is used when configured and memory cache is used otherwise.
- Keep writing success and failure records to login logs.

## Rules

- Normal first login does not require captcha.
- After 3 failed attempts for the same `username + ip`, the next login requires captcha.
- After 5 failed attempts for the same `username + ip`, login is locked for 10 minutes.
- A successful login clears the failure counter.
- Captcha codes expire after 2 minutes and are single-use on successful validation.
- Login responses use the existing API envelope and error handling pattern.

## Architecture

- `ICaptchaStore` owns captcha code storage and validation.
- `ILoginSecurityService` owns failure counting, captcha requirement, and lockout decisions.
- `AuthAppService` receives client IP and captcha values through `LoginRequest`, checks login security before password verification, and records failures through `ILoginSecurityService`.
- `Program.cs` exposes `/auth/captcha` and passes request IP to `/auth/login`.
- Vben login page asks `/auth/captcha` only when the backend says captcha is required.

## Error Handling

- Invalid username/password returns a generic login failure message.
- Missing or invalid captcha returns `captchaRequired = true` so the frontend can display captcha immediately.
- Locked login returns a message with remaining lock minutes.
- Backend logs still record the concrete failure reason for administrators.

## Testing

- Unit/integration tests verify captcha is not required initially.
- Tests verify captcha is required after 3 failures.
- Tests verify lockout after 5 failures.
- Tests verify successful login clears failure state.
- Frontend build verifies the Vben login page compiles after adding captcha fields.
