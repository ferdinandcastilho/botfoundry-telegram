# 🤖 BotFoundry.Telegram

Library for building Telegram bots in C# without reimplementing infrastructure for every project.

You write the handlers and steps. The library takes care of the pipeline, session, routing, and connection to Telegram.

> **Contributing:** contributions come in through forks and pull requests; see [CONTRIBUTING.md](CONTRIBUTING.md). To report a vulnerability, see [SECURITY.md](SECURITY.md).

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

> **Security warning — open by default.** Without an `IAuthMiddleware`, **anyone who finds your bot can use every handler**. If the bot must not be public, register an allowlist (see [Example — restrict access with an allowlist](#example--restrict-access-with-an-allowlist)) before putting it online.

### Keep the token out of your code

Do not commit the bot token. Read it from configuration or user secrets instead of a literal:

```bash
dotnet user-secrets init
dotnet user-secrets set "Telegram:Token" "<your bot token>"
```

```csharp
builder.Services.AddBotFoundry(builder.Configuration["Telegram:Token"]!);
```

The examples below use `YOUR_TOKEN_HERE` only as a placeholder.

### Options

```csharp
builder.Services.AddBotFoundry(token, options =>
{
    options.SessionExpiry = TimeSpan.FromHours(12);   // default: 24 hours, renewed on every interaction
    options.SessionCleanupIntervalSeconds = 30;       // default: 60, must be greater than zero
});
```

| Option | Default | Description |
|---|---|---|
| `SessionExpiry` | 24 hours | How long a session lives without interaction. An expired session is discarded and replaced by a new one. |
| `SessionCleanupIntervalSeconds` | 60 | How often expired sessions are removed from the default in-memory store. Ignored when you register your own `ISessionStore`. |

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

## 🚢 CI/CD and release process

Technical information about the repository (workflows, branches, versioning, protections) lives here. When it changes, update this section in the same pull request.

### Branch flow

```
feature branch ──squash──▶ qa ──merge commit──▶ main
```

- Work happens on a branch created from `qa` and goes back through a pull request into `qa` (**squash merge only**).
- Releases are a pull request from `qa` into `main` (**merge commit only**). Squash and rebase are not allowed on `main`: the stable workflow reads the second parent of the merge commit to tag the tip of `qa`, and a squash would make `qa` and `main` diverge.
- The `qa` branch is never deleted, never force-pushed and never receives direct pushes. The same applies to `main`.
- Merged feature branches are deleted automatically by the `Delete merged branch` workflow. The repository-wide "delete head branches" option stays off, because it would also delete `qa` when it is merged into `main`.

### Workflows

| Workflow | Trigger | What it does |
|---|---|---|
| `CI` (`ci.yml`) | Pull requests into `qa` or `main` | `Verify source branch` (a PR into `main` must come from `qa` of this repository) and `Build and test`. Both checks are required. |
| `Publish alpha` (`publish-alpha.yml`) | Push to `qa` (documentation-only pushes are ignored) | Builds, tests and publishes an alpha package to nuget.org. |
| `Publish stable` (`publish-stable.yml`) | Push to `main` (the merge of `qa`) | Computes the next stable version, builds, tests, publishes, tags the release and creates the GitHub release. |
| `Delete merged branch` (`delete-merged-branch.yml`) | PR into `qa` closed | Deletes the source branch if the PR was merged and came from this repository. Never deletes `main` or `qa`. |
| `Notify` (`notify.yml`) | `CI`, publish and cleanup workflows finish | Sends the result to the Telegram DevOps bot (see Notifications). |

Dependabot opens weekly pull requests against `qa` for GitHub Actions and NuGet packages.

### Versioning

- **Alpha:** [MinVer](https://github.com/adamralph/minver) computes `<next patch>-alpha.0.<commit height>` from the last stable tag (tag prefix `v`).
- **Stable:** the bump comes from the labels of the pull request merged into `main`: `major` > `minor` > none (patch). The script `.github/scripts/next-version.sh` takes the highest `vX.Y.Z` tag and applies the bump.
- **Labels:** `minor` and `major` exist in the repository; put one of them on the `qa` -> `main` pull request. Use `minor` for backward compatible features (including new public options) and `major` for breaking changes to the public API. Without a label the release is a patch.
- **Tags and releases:** the stable workflow creates the `v<version>` tag on the tip of `qa` and a GitHub release with generated notes.
- **Tags are immutable:** never delete, move or force a published tag. To fix a wrong release, publish a new version. The only exception was moving `v1.0.0`, `v1.0.1` and `v1.0.2` once, when the history was rewritten to replace e-mail addresses (#75).

### Publishing to nuget.org (trusted publishing)

No NuGet API key is stored anywhere. The publish workflows request a short-lived key (valid for one hour) with [`NuGet/login`](https://github.com/NuGet/login) through GitHub OIDC.

- Two GitHub environments: `nuget-alpha` (deployments only from `qa`) and `nuget-stable` (only from `main`). Each has the secret `NUGET_USER` with the nuget.org profile name.
- Two trusted publishing policies on nuget.org, one per workflow file (`publish-alpha.yml` with `nuget-alpha`, and `publish-stable.yml` with `nuget-stable`), scoped to the `BotFoundry.Telegram` package.
- The workflows need `id-token: write`; third-party actions are pinned by commit SHA.

### Notifications

The `Notify` workflow (`notify.yml`) sends pipeline results to the maintainer's Telegram DevOps bot. It runs when `CI`, `Publish alpha`, `Publish stable` or `Delete merged branch` finish:

- failures (and cancelled or timed-out publish runs) are always reported;
- successful alpha and stable publishes are reported;
- successful `CI` and `Delete merged branch` runs are silent.

It uses the repository secrets `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` and skips silently when they are not available (forks, unconfigured clones). It never checks out or runs pull request code, and all values are escaped before they go into the message. Because `workflow_run` workflows always run from the default branch, changes to `notify.yml` only take effect after they reach `main`.

### Repository protections

- Rulesets `protect-main` and `protect-qa`: pull request required, checks `Build and test` and `Verify source branch` required, deletion and force push blocked, no bypass actors, no required approvals (the maintainer merges their own pull requests).
- Merge methods: squash on `qa`, merge commit on `main`; rebase merge disabled. Wiki and Projects are disabled.
- Default workflow token permissions are read-only, and workflows cannot approve pull requests.
- **GitHub Actions policy:** only actions owned by GitHub (`actions/*`) and `NuGet/login` are allowed, and every `uses:` must be pinned to a full commit SHA (`sha_pinning_required`), with the version in a trailing comment. Dependabot proposes updates to the pinned SHAs. To use a new action, pin it by SHA and add it to the allowed list in Settings -> Actions -> General; a workflow that references an action by tag fails.
- Secrets live in the repository: `TELEGRAM_BOT_TOKEN` and `TELEGRAM_CHAT_ID` (notifications) and, per environment, `NUGET_USER`. No NuGet API key exists.

---

## 📁 Repository structure

```
src/
├── BotFoundry.Telegram.Abstractions/   # Public contracts (ITopicHandler, IConversationStep, …)
├── BotFoundry.Telegram.Core/           # Pipeline, router, step runner
└── BotFoundry.Telegram/                # Telegram.Bot adapters, DI registration
tests/
├── UnitTests/                          # xUnit + NSubstitute + FluentAssertions
└── IntegrationTests/
docs/
├── architecture.md                     # Architecture document and ADRs
└── architecture/                       # Design docs (HTML)
.github/
├── workflows/                          # CI, publish alpha/stable, branch cleanup
├── scripts/                            # next-version.sh
├── ISSUE_TEMPLATE/                     # Bug and suggestion templates
└── dependabot.yml
```

---

## 💬 Feedback and contact

Questions, feedback or suggestions: [hey@fernandocastilho.tech](mailto:hey@fernandocastilho.tech).

To report a security problem, follow [SECURITY.md](SECURITY.md) instead of sending it by email.

---

## 📄 License

MIT
