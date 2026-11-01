---
description: Enforce project issue creation conventions.
applyTo: '**/*'
---

# Issue Guidelines

## Title

- Direct sentence in US English describing the action
- No category prefix in the title
- Examples:
  - `Remove redundant tests from BotFoundry.Telegram.Tests`
  - `Implement the NuGet publish workflow`
  - `Fix the TelegramMessageSender behavior`

## Required labels

Every issue must have at least **one type label** and, when applicable, **one scope label**.

### Type labels

| Label | Use |
|---|---|
| `feat` | New feature |
| `fix` | Fix for incorrect behavior |
| `chore` | Technical task with no functional impact (e.g. reorganization, removal) |
| `docs` | Documentation |
| `refactor` | Refactoring with no behavior change |
| `test` | Adding or maintaining tests |

### Scope labels (when the issue references a specific project)

| Label | Project |
|---|---|
| `BotFoundry.Telegram` | `src/BotFoundry.Telegram` |
| `BotFoundry.Telegram.Core` | `src/BotFoundry.Telegram.Core` |
| `BotFoundry.Telegram.Abstractions` | `src/BotFoundry.Telegram.Abstractions` |
| `BotFoundry.Telegram.Tests` | `tests/UnitTests/BotFoundry.Telegram.Tests` |
| `BotFoundry.Telegram.Core.Tests` | `tests/UnitTests/BotFoundry.Telegram.Core.Tests` |
| `BotFoundry.Telegram.Integration.Tests` | `tests/IntegrationTests/BotFoundry.Telegram.Integration.Tests` |

## Language

All issue content (title, body, acceptance criteria) must be written in **US English**.

## Recommended body structure

```
## Context


## Branch
`<type>/<short-name>` (from `main`)

## What to do


## Acceptance Criteria
- [ ] ...
```
