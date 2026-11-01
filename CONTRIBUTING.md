# Contributing to BotFoundry.Telegram

Thanks for your interest. Contributions come in through **forks and pull requests**.

## Before you start

- Open an issue first for anything bigger than a small fix, using the bug or suggestion template.
- Everything in this repository is written in **US English**: code comments, log messages, docs, issues, commits and pull requests.
- Never commit secrets (bot tokens, API keys). See [SECURITY.md](SECURITY.md) to report a vulnerability.

## Workflow

1. Fork the repository and create a branch from `qa`, named `<type>/<short-name>` (`feat`, `fix`, `docs`, `refactor`, `test`, `chore` or `ci`).
2. Make your change and run the tests: `dotnet test`.
3. Open a pull request **into `qa`** (never into `main`). It is merged with **squash**.
4. The `CI` workflow must pass: `Build and test` and `Verify source branch`.

The maintainer merges `qa` into `main` to release. A pull request into `main` from any other branch is rejected by the `Verify source branch` check.

### Commits

One line, no body: `<type>: short message, closes #<issue>`. Example: `fix: renew session expiration on every interaction, closes #64`.

### Pull requests

- Title: `#<issue> Pascal Case Title`, for example `#64 Respect Session Expiry`.
- Body: a short summary, the changed files and the checks you ran.

## Release process

Releases are made by the maintainer. In short:

- Every merge into `qa` publishes an **alpha** package (`<next patch>-alpha.0.<height>`) to nuget.org.
- Merging `qa` into `main` (merge commit only) publishes a **stable** package. The bump comes from the label of that pull request: `major`, `minor`, or none for a patch. The workflow creates the `v<version>` tag and the GitHub release.
- Versioning follows [SemVer](https://semver.org): patch for fixes, minor for backward compatible features (including new public options), major for breaking changes to the public API.
- **Tags are immutable.** Never delete, move or force a published tag. To fix a wrong release, publish a new version. The only exception was a one-time move of `v1.0.0`, `v1.0.1` and `v1.0.2` when the history was rewritten to replace e-mail addresses.

The full description of workflows, versioning, trusted publishing and repository protections is in the [README](README.md#-cicd-and-release-process).
