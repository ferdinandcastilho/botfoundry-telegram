# 🤖 BotFoundry.Telegram

Library for building Telegram bots in C# without reimplementing infrastructure for every project.

You write the handlers and steps. The library takes care of the pipeline, session, routing, and connection to Telegram.

> **Repository status:** This repository is intended to be public once the CI/CD setup is complete.

---

## 📦 Installation

```bash
dotnet add package BotFoundry.Telegram
```

---

## ⚡ Minimal setup

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBotFoundry("YOUR_TOKEN_HERE");

var app = builder.Build();
await app.RunAsync();
```

The bot is already connected to Telegram via long polling. No additional configuration is required to get started.

---

## 🧩 Creating a simple handler

A handler responds to a command such as `/command`. Implement `ITopicHandler` and register it with DI.

```csharp
// Responds to the /hello command
public sealed class HelloHandler : ITopicHandler
{
    public string Command => "hello";

    public async Task<FlowResult> HandleAsync(BotContext ctx, CancellationToken ct)
    {
        await ctx.SendAsync("👋 Hello! How can I help you?", ct);
        return FlowResult.Complete();
    }
}
```

```csharp
// Program.cs
builder.Services.AddSingleton<ITopicHandler, HelloHandler>();
builder.Services.AddBotFoundry("YOUR_TOKEN_HERE");
```

The user sends `/hello` → the bot replies and the flow ends.

---

## 💬 Multi-step flow with a session

Richer handlers guide the user through multiple steps. Each step can read and write session data, which persists between messages.

### Example: expense registration

The user types `/expense`. The bot guides them through three consecutive steps, accumulating data in the session until it displays the final summary.

```
/expense
  → What is the category?   [Food] [Transport] [Leisure] [Other]
  → What is the amount? (e.g. 45.90)
  → Any notes? (optional — type "skip" to ignore)
  → ✅ Expense registered: Food · $45.90 · "lunch"
```

#### Handler — entry point

```csharp
public sealed class ExpenseHandler : ITopicHandler
{
    public string Command => "expense";

    public async Task<FlowResult> HandleAsync(BotContext ctx, CancellationToken ct)
    {
        await ctx.SendAsync(
            "📂 What is the expense category?",
            quickReplies: ["Food", "Transport", "Leisure", "Other"],
            ct);

        ctx.Session.ActiveStepId = "expense_category";
        return FlowResult.Continue(); // indicates that there are steps to follow
    }
}
```

#### Steps

```csharp
// Step 1 — captures the category (already sent by the handler)
public sealed class ExpenseCategoryStep : IConversationStep
{
    public string StepId => "expense_category";

    public Task AskAsync(BotContext ctx, CancellationToken ct)
        => ctx.SendAsync(
            "📂 What is the expense category?",
            quickReplies: ["Food", "Transport", "Leisure", "Other"],
            ct);

    public Task<bool> ValidateAsync(BotContext ctx, CancellationToken ct)
    {
        string[] validCategories = ["Food", "Transport", "Leisure", "Other"];
        return Task.FromResult(validCategories.Contains(ctx.Text));
    }

    public Task<StepResult> ProcessAsync(BotContext ctx, CancellationToken ct)
    {
        ctx.Session.Data["expense_category"] = ctx.Text; // saves to the session
        return Task.FromResult(StepResult.JumpTo("expense_amount"));
    }
}

// Step 2 — captures the amount
public sealed class ExpenseAmountStep : IConversationStep
{
    public string StepId => "expense_amount";

    public Task AskAsync(BotContext ctx, CancellationToken ct)
        => ctx.SendAsync("💰 What is the amount? (e.g. 45.90)", ct);

    public Task<bool> ValidateAsync(BotContext ctx, CancellationToken ct)
        => Task.FromResult(decimal.TryParse(
            ctx.Text,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) && value > 0);

    public Task<StepResult> ProcessAsync(BotContext ctx, CancellationToken ct)
    {
        ctx.Session.Data["expense_amount"] = ctx.Text;
        return Task.FromResult(StepResult.JumpTo("expense_note"));
    }
}

// Step 3 — optional note and final summary
public sealed class ExpenseNoteStep : IConversationStep
{
    public string StepId => "expense_note";

    public Task AskAsync(BotContext ctx, CancellationToken ct)
        => ctx.SendAsync(
            "📝 Any notes? (or type \"skip\")", ct);

    public Task<bool> ValidateAsync(BotContext ctx, CancellationToken ct)
        => Task.FromResult(true); // always valid

    public async Task<StepResult> ProcessAsync(BotContext ctx, CancellationToken ct)
    {
        var category = ctx.Session.Data["expense_category"];
        var amount   = ctx.Session.Data["expense_amount"];
        var note     = ctx.Text == "skip" ? "—" : ctx.Text;

        await ctx.SendAsync(
            $"✅ Expense registered!\n\n" +
            $"📂 Category: {category}\n" +
            $"💰 Amount: ${amount}\n" +
            $"📝 Note: {note}",
            ct);

        return StepResult.Complete();
    }
}
```

#### DI registration

```csharp
// Program.cs
builder.Services.AddSingleton<ITopicHandler, ExpenseHandler>();
builder.Services.AddSingleton<IConversationStep, ExpenseCategoryStep>();
builder.Services.AddSingleton<IConversationStep, ExpenseAmountStep>();
builder.Services.AddSingleton<IConversationStep, ExpenseNoteStep>();

builder.Services.AddBotFoundry("YOUR_TOKEN_HERE");
```

> **How the session works:** each step reads and writes to `ctx.Session`, a per-user dictionary that persists between messages. Step 3 reads the values saved by steps 1 and 2 without requiring the user to send anything again.

---

## 🔌 Optional extensions

All of the contracts below are optional. Register only the ones your bot needs.

| Contract | When to use |
|---|---|
| `ISessionStore` | Persist sessions beyond memory (Redis, SQL). Default: in-memory |
| `IAuthMiddleware` | Restrict access by `chatId` or any other rule |
| `ILlmBridge` | Forward messages without an active command to a language agent |

### Example — restrict access with an allowlist

```csharp
public sealed class AllowListAuth : IAuthMiddleware
{
    private readonly HashSet<long> _allowed = [123456789, 987654321];

    public async Task<bool> IsAllowedAsync(BotContext ctx, CancellationToken ct)
    {
        if (_allowed.Contains(ctx.ChatId)) return true;

        await ctx.SendAsync("⛔ Access not authorized.", ct);
        return false;
    }
}
```

```csharp
// Register before AddBotFoundry
builder.Services.AddSingleton<IAuthMiddleware, AllowListAuth>();
builder.Services.AddBotFoundry("YOUR_TOKEN_HERE");
```

---

## 📁 Repository structure

```
src/
├── BotFoundry.Telegram.Abstractions/   # Public contracts (ITopicHandler, IConversationStep, …)
├── BotFoundry.Telegram.Core/           # Pipeline, router, step runner
└── BotFoundry.Telegram/                # Telegram.Bot adapters, DI registration
tests/
└── BotFoundry.Telegram.Tests/          # xUnit + NSubstitute + FluentAssertions
docs/
└── architecture/                       # Architecture docs, design docs, ADRs
```

---

## 📄 License

MIT
