# Security Policy

MiniAdmin is an open-source admin template and should be configured carefully before internet-facing deployment.

## Supported Versions

Security fixes are accepted for the latest `main` branch. If you maintain a private fork, please backport relevant fixes to your deployment branch.

## Reporting a Vulnerability

Please do not open a public issue with exploit details or secrets. Instead:

1. Open a private security advisory on GitHub if available.
2. If advisories are not available, contact the repository owner privately.
3. Include affected version or commit, reproduction steps, impact, and suggested mitigation if known.

We aim to acknowledge valid reports within 7 days.

## Deployment Hardening Checklist

- Replace default passwords for `admin` and demo users.
- Replace `Jwt:SigningKey` with a strong environment-specific secret.
- Keep database, Redis, MinIO, SMTP, and webhook secrets outside Git.
- Use HTTPS at the reverse proxy or load balancer.
- Restrict CORS origins in production.
- Use least-privilege database and storage accounts.
- Review tenant packages and menu permissions before granting production access.
- Keep logs free of passwords, tokens, connection strings, and private keys.

## Secret Handling

The repository intentionally ignores local config and runtime data such as:

- `appsettings.Development.json`
- `appsettings.Local.json`
- `.env.local`
- logs, uploads, and build artifacts

If a real secret is accidentally published, cleaning Git history is not enough. Rotate the secret and invalidate existing credentials or tokens.
