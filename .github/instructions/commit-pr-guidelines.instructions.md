---
description: Enforce project commit and PR message conventions.
applyTo: '**/*'
---

# Commit and PR Message Guidelines

## Commit Messages

- Single-line format only: `<type>: very short message, closes #<TASK_NUMBER>`
- type examples: feat, fix, chore, docs, refactor
- Do not add a commit body or Co-authored-by trailer unless explicitly requested.
- Example: `feat(adapters): implement infrastructure adapters, closes #7`

## PR Titles

- Use PascalCase, short and concise
- Do NOT reference the issue number in the title
- Example: `Add Infrastructure Adapters`

## PR Descriptions

- Include a brief list of changed files or components
- Include the issue reference at the end: `Closes #7`
- This enables automatic issue closure when the PR is merged to main
- Example format:
  ```
  Implements the infrastructure adapters described in issue #7.
  
  Changes
  - Added Adapters/InMemorySessionStore.cs
  - Added Adapters/TelegramMessageSender.cs
  
  Closes #7
  ```
