# Security Policy

## Supported versions

Only the latest stable release of `BotFoundry.Telegram` receives security fixes.

## Reporting a vulnerability

**Do not open a public issue for a security problem.**

Use GitHub's private vulnerability reporting: open the repository's **Security** tab and choose **Report a vulnerability**
(or go to <https://github.com/ferdinandcastilho/botfoundry-telegram/security/advisories/new>).

Please include:

- what is affected and the version,
- steps to reproduce or a proof of concept,
- the impact you see.

You can expect an acknowledgement within a few days. Fixes are released as a new stable version, and the advisory is published after the fix is available.

## Notes for bot authors

- Without an `IAuthMiddleware`, anyone who finds your bot can use every handler. See the README.
- Keep the bot token in configuration or user secrets, never in source code. If a token leaks, revoke it with @BotFather.
