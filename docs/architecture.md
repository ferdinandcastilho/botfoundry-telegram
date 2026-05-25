***

```markdown
# BotForge.Telegram — Architecture Handoff Document

## Visão Geral

**BotForge.Telegram** é uma biblioteca .NET 10 que abstrai toda a infraestrutura necessária para criar bots do Telegram, permitindo que o consumidor foque exclusivamente na lógica de negócio dos fluxos de conversa.

### Experiência de uso esperada

```csharp
builder.Services
    .AddTelegramBot<ChatSession>("BOT_TOKEN")
    .WithSessionFactory((sp, chatId) => new ChatSession(chatId))
    .WithSessionExpiration(TimeSpan.FromHours(2))
    .WithAdmins(123456789)
    .WithAuthMiddleware(async ctx => await db.IsAllowedAsync(ctx.Session.ChatId))
    .AddHandler<StartHandler>()
    .AddHandler<RegisterHandler>();
```

---

## Princípios Arquiteturais

1. **Isolamento do Telegram.Bot**: apenas `TelegramBotSender` referencia tipos do pacote `Telegram.Bot` diretamente. Todo o restante usa `IBotSender` e tipos agnósticos.
2. **Tudo interno por padrão**: toda classe que não faz parte da API pública é `internal`.
3. **Generics para sessão**: sessão tipada via `TSession : class, IBotSession`.
4. **Zero dependências além do necessário**: apenas `Microsoft.Extensions.*` e `Telegram.Bot`.
5. **Complexidade explícita**: cada camada resolve um problema específico.

---

## API Pública

| Tipo | Namespace | Responsabilidade |
|------|-----------|-----------------|
| `AddTelegramBot<TSession>` | `Extensions` | Entry point — registra todos os serviços no DI |
| `TelegramBotBuilder<TSession>` | `Extensions` | Fluent builder pós-registro |
| `IBotSession` | `Core` | Contrato mínimo de sessão que o consumidor implementa |
| `ConversationState` | `Core` | Estado atual do fluxo (topic ativo, step atual) |
| `BotContext<TSession>` | `Core` | Contexto rico passado a cada step |
| `IBotSender` | `Messaging` | Abstração de envio de mensagens |
| `ITopicHandler<TSession>` | `Routing` | Contrato de um handler de fluxo de conversa |
| `IConversationStep<TSession>` | `Conversation` | Contrato de um step dentro de um fluxo |
| `StepResult` | `Conversation` | Resultado de um step (Continue, Complete, JumpTo) |
| `AskStep<TSession>` | `Conversation` | Step padrão: pergunta → valida → processa |
| `FinalStep<TSession>` | `Conversation` | Step terminal: executa delegate e encerra o fluxo |
| `BranchStep<TSession>` | `Conversation` | Step condicional: redireciona para outro step |

---

## Camadas e Componentes

### Core
```
IBotSession
  + ChatId: long
  + IsAdmin: bool
  + ConversationState: ConversationState
  + ExpiresAt: DateTime

ConversationState
  + TopicName: string
  + CurrentStep: string
```

### Session
```
ISessionStore<TSession>
InMemorySessionStore<TSession>   [internal]
ISessionFactory<TSession>
DelegateSessionFactory<TSession> [internal]
SessionOptions
  + Expiration: TimeSpan (default: 1h)
```

### Conversation
```
StepResultType: enum { Continue, Complete, JumpTo }
StepResult: record
  static Continue | Complete | JumpTo(name)

IConversationStep<TSession>
  + Name: string
  + AskAsync / ValidateAsync / ProcessAsync

AskStep    → pergunta + valida + processa
FinalStep  → executa action + Complete
BranchStep → pergunta + evaluator + JumpTo

StepRunner<TSession> [internal]
  1. Se CurrentStep vazio → set steps, AskAsync
  2. ValidateAsync → erro: reply + repeat
  3. ProcessAsync → Continue | Complete | JumpTo
```

### Routing
```
ITopicHandler<TSession>
  + TopicName: string
  + AdminOnly: bool
  + Steps: IConversationStep[]

TopicHandlerRegistry<TSession> [internal]
BotRouter<TSession> [internal]
  1. "/" → busca no registry
  2. TopicName ativo → usa handler ativo
  3. AdminOnly && !IsAdmin → ignora
  4. Delega para StepRunner
```

### Messaging
```
IBotSender [public]
  + SendTextAsync / SendStickerAsync / SendPhotoAsync

TelegramBotSender [internal]
  → única classe que importa Telegram.Bot

BotContext<TSession> [public]
  + Session / Message / UserInput / Sender / CancellationToken
  + ReplyAsync / ReplyStickerAsync / ReplyPhotoAsync
```

### Hosting
```
BotPipeline<TSession> [internal]
  1. Filtro: apenas Message com Text
  2. Resolve/cria TSession
  3. Expiry check
  4. Constrói BotContext
  5. BotRouter.RouteAsync
  6. store.SetAsync

TelegramBotHostedService<TSession> [internal]
  + StartAsync: polling via StartReceiving
  + StopAsync: cancela CancellationTokenSource
```

### Extensions
```
AddTelegramBot<TSession>(services, token) → TelegramBotBuilder
  Registra: TelegramBotClient, InMemorySessionStore,
  ISessionFactory, TopicHandlerRegistry, BotRouter,
  StepRunner, BotPipeline, TelegramBotSender, HostedService

TelegramBotBuilder<TSession>
  + AddHandler<T>()
  + WithSessionFactory(Func<IServiceProvider, long, TSession>)
  + WithSessionExpiration(TimeSpan)
  + WithAdmins(params long[])
  + WithAuthMiddleware(Func<BotContext, Task<bool>>)
```

---

## Fluxo End-to-End

```
Telegram API
    ↓
TelegramBotHostedService (polling)
    ↓
BotPipeline.ProcessAsync
  ├─ filtro: Message/Text only
  ├─ resolve/cria TSession
  ├─ expiry check
  ├─ constrói BotContext
    ↓
BotRouter.RouteAsync
  ├─ detecta "/cmd" → TopicHandlerRegistry
  ├─ ou continua TopicName ativo
  ├─ AdminOnly check
    ↓
StepRunner.RunAsync
  ├─ AskAsync → ValidateAsync → ProcessAsync
  ├─ Continue → próximo step
  ├─ Complete → limpa ConversationState
  └─ JumpTo → step por nome
    ↓
IBotSender → TelegramBotSender → Telegram API
```

---

## Estrutura de Pastas

```
src/
  BotForge.Telegram/
    Core/           IBotSession.cs, ConversationState.cs
    Session/        ISessionStore, InMemorySessionStore,
                    ISessionFactory, DelegateSessionFactory, SessionOptions
    Conversation/   IConversationStep, StepResult, StepResultType,
                    AskStep, FinalStep, BranchStep, StepRunner
    Routing/        ITopicHandler, TopicHandlerRegistry, BotRouter
    Messaging/      IBotSender, TelegramBotSender, BotContext
    Hosting/        BotPipeline, TelegramBotHostedService
    Extensions/     ServiceCollectionExtensions, TelegramBotBuilder
    BotForge.Telegram.csproj

tests/
  BotForge.Telegram.Unit/
  BotForge.Telegram.Integration/

samples/
  MinimalBot/

BotForge.Telegram.sln  ← raiz do repositório
```

---

## .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GeneratePackageOnBuild>false</GeneratePackageOnBuild>
    <PackageId>BotForge.Telegram</PackageId>
    <Version>0.1.0</Version>
    <Authors>Fernando Castilho</Authors>
    <Description>Lightweight .NET library for building Telegram bots with DI, session management, and multi-step conversation flows.</Description>
    <RepositoryUrl>https://github.com/ferdinandcastilho/botforge-telegram</RepositoryUrl>
    <PackageTags>telegram;bot;dotnet;di;conversation</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Telegram.Bot" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="*" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="*" />
  </ItemGroup>
</Project>
```

---

## Decisões Arquiteturais (ADR resumido)

| Decisão | Escolha | Alternativa descartada | Motivo |
|---------|---------|----------------------|--------|
| Sessão | In-memory `ConcurrentDictionary` | Redis, banco relacional | Zero deps externas |
| Polling vs Webhook | Long polling `StartReceiving` | Webhook HTTP | Sem endpoint público necessário |
| Generics para sessão | `TSession : IBotSession` | `dynamic` / `object` | Type-safety, IntelliSense |
| Isolamento Telegram.Bot | Apenas `TelegramBotSender` | Uso direto nos handlers | Testabilidade total |
| `internal` por padrão | Sim | Público | API mínima, sem breaking changes |
| StepRunner stateless | Sim (estado na sessão) | StepRunner stateful | Concorrência sem conflito |

---

## Lições do Experimento com Agentes

1. CI deve ser o primeiro item funcional — sem verde, nada mais pode ser validado
2. `.sln` deve estar na raiz — `dotnet` sem argumentos exige isso; CI precisa de `working-directory` correto
3. Uma issue por vez — só abrir a próxima após CI verde + merge aprovado
4. Agentes entregam incompleto para fechar a issue — revisar AC real, não apenas CI
5. AC deve ser verificado manualmente — CI verde ≠ issue completa

---