## Backlog M1–M6

**M1 — Fundação**
- #1 Configurar `.csproj`: nullable, implicit usings, package metadata
- #2 Criar estrutura de pastas interna
- #3 Definir `IBotSession` e `ConversationState`
- #4 Setup projetos de teste (xUnit, FluentAssertions, NSubstitute)
- #5 Configurar CI (`.sln` na raiz, `working-directory`, `build-and-test`)

**M2 — Session**
- #6 Definir `ISessionStore<TSession>`
- #7 Implementar `InMemorySessionStore<TSession>`
- #8 Definir `ISessionFactory` + `DelegateSessionFactory`
- #9 Expiração de sessão + `SessionOptions`

**M3 — Conversation**
- #10 Definir `StepResult`, `StepResultType`, `IConversationStep`
- #11 Implementar `AskStep`
- #12 Implementar `FinalStep`
- #13 Implementar `BranchStep`
- #14 Implementar `StepRunner`

**M4 — Routing**
- #15 Definir `ITopicHandler` + `TopicHandlerRegistry`
- #16 Implementar `BotRouter`

**M5 — Hosting**
- #17 Definir `IBotSender` + implementar `BotContext`
- #18 Implementar `TelegramBotSender`
- #19 Implementar `BotPipeline`
- #20 Implementar `TelegramBotHostedService`
- #21 Sample bot mínimo (prova de conceito end-to-end)

**M6 — DX & Publish**
- #22 Implementar `AddTelegramBot<TSession>`
- #23 Implementar `TelegramBotBuilder<TSession>`
- #24 `WithSessionFactory`
- #25 `WithSessionExpiration`
- #26 `WithAdmins`
- #27 `WithAuthMiddleware`
- #28 Finalizar README
- #29 Validar workflow de publish (`dotnet pack` + GitHub Packages)
```

***

Pode copiar tudo isso e colar como primeira mensagem no novo chat. Boa implementação!