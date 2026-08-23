---
description: Enforce project issue creation conventions.
applyTo: '**/*'
---

# Issue Guidelines

## Título

- Frase direta em português (pt-BR) descrevendo a ação
- Sem prefixo de categoria no título
- Exemplos:
  - `Remover testes redundantes do BotFoundry.Telegram.Tests`
  - `Implementar workflow de publish do NuGet`
  - `Corrigir comportamento do TelegramMessageSender`

## Labels obrigatórias

Cada issue deve ter ao menos **uma label de tipo** e, quando aplicável, **uma label de escopo**.

### Labels de tipo

| Label | Uso |
|---|---|
| `feat` | Nova funcionalidade |
| `fix` | Correção de comportamento incorreto |
| `chore` | Tarefa técnica sem impacto funcional (ex: reorganização, remoção) |
| `docs` | Documentação |
| `refactor` | Refatoração sem mudança de comportamento |
| `test` | Adição ou manutenção de testes |

### Labels de escopo (quando a issue referencia um projeto específico)

| Label | Projeto |
|---|---|
| `BotFoundry.Telegram` | `src/BotFoundry.Telegram` |
| `BotFoundry.Telegram.Core` | `src/BotFoundry.Telegram.Core` |
| `BotFoundry.Telegram.Abstractions` | `src/BotFoundry.Telegram.Abstractions` |
| `BotFoundry.Telegram.Tests` | `tests/UnitTests/BotFoundry.Telegram.Tests` |
| `BotFoundry.Telegram.Core.Tests` | `tests/UnitTests/BotFoundry.Telegram.Core.Tests` |
| `BotFoundry.Telegram.Integration.Tests` | `tests/IntegrationTests/BotFoundry.Telegram.Integration.Tests` |

## Idioma

Todo o conteúdo da issue (título, corpo, critérios de aceite) deve ser escrito em **português pt-BR**.

## Estrutura recomendada do corpo

```
## Contexto


## Branch
`/` (a partir de `main`)

## O que deve ser feito


## Critérios de Aceite
- [ ] ...
```

## Critérios de Aceite

- [ ] Arquivo `.github/instructions/issue-guidelines.instructions.md` criado
- [ ] Conteúdo cobre: formato do título, labels de tipo, labels de escopo e idioma
- [ ] Arquivo segue o mesmo frontmatter dos demais arquivos da pasta (`description`, `applyTo`)
