# 🤖 BotForge.Telegram

Biblioteca para construir bots Telegram em C# sem reimplementar infraestrutura a cada projeto.

Você escreve os handlers e os steps. A lib cuida do pipeline, da sessão, do roteamento e da conexão com o Telegram.

---

## 📦 Instalação

```bash
dotnet add package BotForge.Telegram
```

---

## ⚡ Setup mínimo

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBotForge("SEU_TOKEN_AQUI");

var app = builder.Build();
await app.RunAsync();
```

O bot já está conectado ao Telegram via long polling. Nenhuma configuração adicional é necessária para começar.

---

## 🧩 Criando um handler simples

Um handler responde a um comando do tipo `/comando`. Implemente `ITopicHandler` e registre no DI.

```csharp
// Responde ao comando /oi
public sealed class OiHandler : ITopicHandler
{
    public string Command => "oi";

    public async Task<FlowResult> HandleAsync(BotContext ctx, CancellationToken ct)
    {
        await ctx.SendAsync("👋 Olá! Como posso te ajudar?", ct);
        return FlowResult.Complete;
    }
}
```

```csharp
// Program.cs
builder.Services.AddSingleton<ITopicHandler, OiHandler>();
builder.Services.AddBotForge("SEU_TOKEN_AQUI");
```

O usuário envia `/oi` → o bot responde e o fluxo encerra.

---

## 💬 Fluxo multi-step com sessão

Handlers mais ricos guiam o usuário por múltiplas etapas. Cada step pode ler e escrever dados na sessão — que persiste entre as mensagens.

### Exemplo: registro de despesa

O usuário digita `/despesa`. O bot guia três steps em sequência, acumulando os dados na sessão até exibir o resumo final.

```
/despesa
  → Qual a categoria?   [Alimentação] [Transporte] [Lazer] [Outro]
  → Qual o valor? (ex: 45.90)
  → Alguma observação? (opcional — digite "pular" para ignorar)
  → ✅ Despesa registrada: Alimentação · R$ 45,90 · "almoço"
```

#### Handler — ponto de entrada

```csharp
public sealed class DespesaHandler : ITopicHandler
{
    public string Command => "despesa";

    public async Task<FlowResult> HandleAsync(BotContext ctx, CancellationToken ct)
    {
        await ctx.SendAsync(
            "📂 Qual a categoria da despesa?",
            quickReplies: ["Alimentação", "Transporte", "Lazer", "Outro"],
            ct);

        return FlowResult.Continue; // sinaliza que há steps a seguir
    }
}
```

#### Steps

```csharp
// Step 1 — captura a categoria (já enviada pelo handler)
public sealed class CategoriaDespesaStep : IConversationStep
{
    public string StepId => "despesa_categoria";

    public Task AskAsync(BotContext ctx, CancellationToken ct)
        => ctx.SendAsync(
            "📂 Qual a categoria da despesa?",
            quickReplies: ["Alimentação", "Transporte", "Lazer", "Outro"],
            ct);

    public Task<bool> ValidateAsync(BotContext ctx, CancellationToken ct)
    {
        string[] validas = ["Alimentação", "Transporte", "Lazer", "Outro"];
        return Task.FromResult(validas.Contains(ctx.Text));
    }

    public Task<StepResult> ProcessAsync(BotContext ctx, CancellationToken ct)
    {
        ctx.Session.Set("despesa_categoria", ctx.Text); // salva na sessão
        return Task.FromResult(StepResult.Next);
    }
}

// Step 2 — captura o valor
public sealed class ValorDespesaStep : IConversationStep
{
    public string StepId => "despesa_valor";

    public Task AskAsync(BotContext ctx, CancellationToken ct)
        => ctx.SendAsync("💰 Qual o valor? (ex: 45.90)", ct);

    public Task<bool> ValidateAsync(BotContext ctx, CancellationToken ct)
        => Task.FromResult(decimal.TryParse(
            ctx.Text,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var v) && v > 0);

    public Task<StepResult> ProcessAsync(BotContext ctx, CancellationToken ct)
    {
        ctx.Session.Set("despesa_valor", ctx.Text);
        return Task.FromResult(StepResult.Next);
    }
}

// Step 3 — observação opcional + exibe resumo
public sealed class ObservacaoDespesaStep : IConversationStep
{
    public string StepId => "despesa_observacao";

    public Task AskAsync(BotContext ctx, CancellationToken ct)
        => ctx.SendAsync(
            "📝 Alguma observação? (ou digite \"pular\")", ct);

    public Task<bool> ValidateAsync(BotContext ctx, CancellationToken ct)
        => Task.FromResult(true); // sempre válido

    public async Task<StepResult> ProcessAsync(BotContext ctx, CancellationToken ct)
    {
        var categoria = ctx.Session.Get<string>("despesa_categoria");
        var valor     = ctx.Session.Get<string>("despesa_valor");
        var obs       = ctx.Text == "pular" ? "—" : ctx.Text;

        await ctx.SendAsync(
            $"✅ Despesa registrada!\n\n" +
            $"📂 Categoria: {categoria}\n" +
            $"💰 Valor: R$ {valor}\n" +
            $"📝 Observação: {obs}",
            ct);

        return StepResult.Complete;
    }
}
```

#### Registro no DI

```csharp
// Program.cs
builder.Services.AddSingleton<ITopicHandler, DespesaHandler>();
builder.Services.AddSingleton<IConversationStep, CategoriaDespesaStep>();
builder.Services.AddSingleton<IConversationStep, ValorDespesaStep>();
builder.Services.AddSingleton<IConversationStep, ObservacaoDespesaStep>();

builder.Services.AddBotForge("SEU_TOKEN_AQUI");
```

> **Como a sessão funciona:** cada step lê e escreve em `ctx.Session` — um dicionário por usuário que persiste entre mensagens. O step 3 lê os valores gravados pelos steps 1 e 2 sem que o usuário precise reenviar nada.

---

## 🔌 Extensões opcionais

Todos os contratos abaixo são opcionais. Registre apenas os que o seu bot precisar.

| Contrato | Quando usar |
|---|---|
| `ISessionStore` | Persistir sessões além da memória (Redis, SQL). Padrão: in-memory |
| `IAuthMiddleware` | Restringir acesso por `chatId` ou qualquer outra regra |
| `ILlmBridge` | Repassar mensagens sem comando ativo para um agente de linguagem |

### Exemplo — restringir acesso por lista de usuários

```csharp
public sealed class AllowListAuth : IAuthMiddleware
{
    private readonly HashSet<long> _allowed = [123456789, 987654321];

    public async Task<bool> IsAllowedAsync(BotContext ctx, CancellationToken ct)
    {
        if (_allowed.Contains(ctx.ChatId)) return true;

        await ctx.SendAsync("⛔ Acesso não autorizado.", ct);
        return false;
    }
}
```

```csharp
// Registre antes de AddBotForge
builder.Services.AddSingleton<IAuthMiddleware, AllowListAuth>();
builder.Services.AddBotForge("SEU_TOKEN_AQUI");
```

---

## 📁 Estrutura do repositório

```
src/
├── BotForge.Telegram.Abstractions/   # Contratos públicos (ITopicHandler, IConversationStep, …)
├── BotForge.Telegram.Core/           # Pipeline, router, step runner
└── BotForge.Telegram/                # Adapters Telegram.Bot, DI registration
tests/
└── BotForge.Telegram.Tests/          # xUnit + NSubstitute + FluentAssertions
docs/
└── architecture/                     # Architecture Doc, Design Doc, ADRs
```

---

## 📄 Licença

MIT
