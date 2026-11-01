# BotFoundry.Telegram — Architecture Document

> **Note:** This is the project's architecture document. It describes the problem, requirements, structure and design decisions in an agnostic way — with no code or implementation details. For interface contracts, folder structure and usage examples, see the Design Doc.

---

## 1. Context and Problem

Developers who build multiple Telegram bots in C# face a recurring problem: with every new project, the same infrastructure is reimplemented from scratch — per-user session management, command routing, multi-step conversation flows and administrative access control.

This repetition has three direct consequences:

- **Inefficiency:** development time spent on non-differentiating code
- **Inconsistency:** each bot solves the same problem slightly differently, increasing the cognitive load when switching between projects
- **Fragility:** without a common, tested foundation, infrastructure bugs repeat in every bot

**BotFoundry.Telegram** exists to eliminate this problem: it provides the common infrastructure as a reusable library, letting the developer focus exclusively on each bot's domain logic, backed by a standardized and resilient foundation.

---

## 2. Requirements

### Functional

1. Route command messages (`/command`) to registered handlers
2. Manage an individual session per user — create, retrieve and expire
3. Run multi-step conversation flows with support for conditional branching
4. Control administrative access through a list of user identifiers
5. Expose an extension point for integration with language agents (LLM)
6. Allow swapping the session persistence strategy without changing the bot's code

### Non-Functional

1. **Lightness:** memory and CPU footprint compatible with low-cost hardware; vertical scaling is the responsibility of the consumer's infrastructure
2. **Standardization:** all bots built with the library follow the same entry point and flow structure
3. **Controlled extensibility:** the consumer extends only domain behavior; infrastructure is not exposed
4. **Testability:** business handlers and steps must be testable without depending on the Telegram SDK

### Operational

1. Communication via long polling — no public endpoint required
2. One process per bot — full isolation between projects

### Out of Scope (v0.1)

1. Native external session store — the library provides `InMemorySessionStore`; external implementations (Redis, SQL) are the consumer's responsibility through a pluggable contract
2. Support for multiple bots in the same process
3. Processing of inline button callbacks, message edits and user-sent media — planned for future versions

---

## 3. Constraints and Assumptions

### Constraints

External factors the architecture does not control but must respect:

1. **Telegram SDK:** the library depends on the `Telegram.Bot` package to talk to the Telegram API. Breaking changes between major SDK versions are an external risk managed by architectural isolation (see ADR-003)
2. **.NET ecosystem:** the library follows the ecosystem's conventions — dependency injection through `Microsoft.Extensions.DependencyInjection`, hosted services through `IHostedService`, and options through `IOptions<T>`
3. **Long polling:** communication with Telegram is done exclusively through polling. Environments that require webhooks are not supported in this version

### Assumptions

Truths assumed about the usage context that, if they change, may require an architectural review:

1. **Personal use:** the primary consumer is the library's own author — DX (Developer Experience) decisions are optimized for that profile
2. **One bot per process:** each library instance serves exactly one bot with one access token
3. **Text messages as the primary input:** the processing flow is optimized for text messages; other input types are treated as secondary
4. **Session per `chatId`:** a user's identity is their Telegram `chatId` — there is no external authentication or identity federation

---

## 4. Architectural Overview

**BotFoundry.Telegram** is organized into two zones with distinct responsibilities and explicit boundaries.

### Zone 1 — Infrastructure (managed by the library)

A fixed set of modules that operate transparently to the consumer. It cannot be changed, only configured through declarative options at registration time.

| Module | Responsibility |
|---|---|
| **Hosting** | Starts and maintains the connection to the Telegram API through long polling |
| **Pipeline** | Orchestrates the processing flow of each received message |
| **Session** | Resolves, creates and persists the user's state on every interaction |
| **Routing** | Identifies whether the message is a new command or the continuation of an active flow |
| **Step Runner** | Runs the steps of a multi-step flow, managing state transitions |
| **Messaging** | Abstracts sending replies, isolating the Telegram SDK from the rest of the library |

### Zone 2 — Domain (implemented by the consumer)

A set of public contracts that the consumer implements to express their bot's business logic. The library invokes these contracts at the right moments of the pipeline.

| Contract | Consumer's responsibility |
|---|---|
| **Topic Handler** | Defines what happens when a command is triggered |
| **Conversation Step** | Defines one step of a flow — what to ask, how to validate, how to process |
| **Session Store** | Defines where and how the session is persisted |
| **Auth Middleware** | Defines whether a user is allowed to continue in a given context |
| **LLM Bridge** | Defines how to forward messages without an active command to an external agent |

### Boundary principle

No Zone 1 type is exposed in the library's public API. Communication between zones happens exclusively through Zone 2 contracts. This guarantees that the consumer never depends on infrastructure implementation details — and that the library can evolve internally without affecting existing bots.

---

## 5. Main Flows

### Flow 1 — New command (`/command`)

Triggered when the user sends a message starting with `/`.

```
Message received by Hosting
  → Pipeline filters: is it a text message? No → discard
  → Session is resolved (exists?) or created (does not exist?)
  → Session expired? Yes → discarded and recreated. No → expiration renewed
  → BotContext is built with session + message
  → Auth Middleware (if configured): allowed? No → discard (session is not persisted)
  → Routing detects "/" → looks up the Topic Handler in the registry
  → Handler found? No → ignore. Yes → start flow
  → Step Runner runs the handler's first step
  → Reply sent through Messaging
  → Session persisted with the new state
```

### Flow 2 — Continuation of an active flow (multi-step)

Triggered when the user is already in the middle of a previously started flow.

```
Message received by Hosting
  → Pipeline filters and resolves the session (session already exists with active state)
  → BotContext built
  → Auth Middleware (if configured)
  → Routing detects an active topic in the session → delegates to the Step Runner
  → Step Runner runs ValidateAsync: valid? No → repeat the question
  → Valid → ProcessAsync → Continue | Complete | JumpTo
  → Continue: advances to the next step, runs AskAsync
  → Complete: clears the session state, flow ends
  → JumpTo: redirects to the named step
  → Reply sent through Messaging
  → Session persisted with the updated state
```

### Flow 3 — Message without a command and without an active flow (LLM Bridge)

Triggered when the user sends free text with no active flow context.

```
Message received by Hosting
  → Pipeline filters and resolves the session (no active state)
  → BotContext built
  → Auth Middleware (if configured)
  → Routing: no "/" and no active topic → checks the LLM Bridge
  → LLM Bridge configured? No → silently discard
  → Yes → consumer forwards the message to the external agent
  → Reply sent through Messaging
  → Session persisted
```

### Session lifecycle

- The session lifetime is `BotFoundryOptions.SessionExpiry` (24 hours by default) and is renewed on every accepted interaction (sliding expiration).
- An expired session is deleted and replaced by a new one before the `BotContext` is built, so it can never resume an old flow.
- A chat denied by the Auth Middleware does not get a stored session.
- `InMemorySessionStore` drops expired sessions when they are read, and a hosted service (`SessionCleanupService`) removes the remaining ones every `BotFoundryOptions.SessionCleanupIntervalSeconds` seconds (60 by default), so chats that never come back do not accumulate. It is registered only together with the default store; a custom `ISessionStore` handles its own expiration.

---

## 6. ADRs (Architecture Decision Records)

### ADR-001 — Pipeline Model

**Context:** The library must process messages in a standardized way across all bots, but allow the consumer to inject business behavior without knowing the internal infrastructure.

**Options considered:**
- Fixed linear pipeline — no extensibility
- Pure middleware chain — full extensibility, high complexity for the consumer
- Hybrid pipeline — fixed infrastructure with optional domain hooks

**Decision:** Hybrid pipeline.

**Rationale:** The consumer wants standardization, not infinite flexibility. The fixed infrastructure removes repetitive decisions in every bot; the hooks cover the real extension cases without exposing the internal pipeline.

**Accepted trade-off:** Behaviors outside the defined hooks require evolving the library, not the consumer.

**Review trigger:** If a legitimate use case that fits none of the existing hooks shows up twice in a row.

---

### ADR-002 — Session Persistence Strategy

**Context:** Quick-interaction bots do not need durable persistence; continuous-conversation bots need to survive restarts. Both cases coexist in the consumer's ecosystem.

**Options considered:**
- Always in-memory — simple, no resilience to restarts
- Always persisted — robust, but with unnecessary overhead for simple cases
- Pluggable through the `ISessionStore` contract, with in-memory as the default by DI convention

**Decision:** Pluggable `ISessionStore`; `InMemorySessionStore` is registered automatically only if the consumer has not registered an implementation beforehand.

**Rationale:** The consumer picks the right strategy for each bot without the library imposing overhead. The DI convention is the idiomatic standard of the .NET ecosystem.

**Accepted trade-off:** The library does not ship an external store implementation — the consumer is responsible for implementing one when needed.

**Review trigger:** If the consumer implements a durable store in two or more bots, evaluate an official companion package.

---

### ADR-003 — Telegram.Bot SDK Isolation

**Context:** The `Telegram.Bot` package has a history of breaking changes between major versions. Exposing its types in the public API would tie the consumer to a specific version and make handlers untestable.

**Options considered:**
- Use `Telegram.Bot` directly throughout the library
- Isolate it in two dedicated internal components, exposing only the library's own abstractions

**Decision:** Only two internal infrastructure components reference `Telegram.Bot`: the messaging adapter (`TelegramMessageSender`) and the polling adapter (`TelegramPollingAdapter`). The rest of the library and the whole public API use agnostic types.

**Rationale:** SDK updates stay contained in two well-defined places. The consumer's handlers and steps are 100% testable without depending on Telegram.

**Accepted trade-off:** Advanced SDK features must be explicitly exposed through the messaging contract — they are not available automatically.

**Review trigger:** When the consumer needs an SDK capability not exposed by the messaging contract twice in a row.

---

### ADR-004 — One Process per Bot

**Context:** The consumer operates multiple independent bots, each with its own domain, log and lifecycle.

**Options considered:**
- Support for multiple bots in the same process
- One process per bot

**Decision:** One process per bot. The library does not support multiple tokens or Telegram clients in the same instance.

**Rationale:** Full isolation between bots — a failure in one does not affect the others. Independent deployment and lifecycle. The complexity of multiple bots in the same process has no real return for the current use case.

**Accepted trade-off:** No resource savings from process co-location.

**Review trigger:** If the consumer needs to co-locate multiple bots in the same process because of hardware resource constraints.

---

## 7. Out of Scope

This section explicitly defines what **BotFoundry.Telegram** does not do in v0.1. Its purpose is to avoid wrong assumptions about the library's capabilities and to guide future evolution decisions.

### Permanently out of scope

Capabilities that contradict fundamental architectural decisions and will not be incorporated without an ADR review:

1. **Multiple bots in the same process** — violates the process isolation principle (ADR-004)
2. **Direct exposure of Telegram.Bot SDK types** — violates SDK isolation (ADR-003)
3. **Webhook** — requires a public HTTP endpoint, outside the defined operational constraints

### Out of scope for v0.1 — planned for future versions

Valid and desired capabilities, postponed because they are not essential to the library's core:

1. Processing of inline button callbacks
2. Processing of message edits
3. Processing of user-sent media (images, audio, documents)
4. Native external store implementations (Redis, SQLite) — delivered as companion packages when there is recurring demand

### Out of scope by design

Responsibilities that belong to the consumer's domain, not to the library:

1. **Business logic of any bot** — handlers, steps and domain rules are always the consumer's responsibility
2. **Internal integration with LLMs** — the library exposes the hook; the actual integration is implemented by the consumer
3. **External authentication** — the library exposes the auth middleware; the source of truth is the consumer's responsibility
4. **Horizontal scaling** — the library is designed for a single instance; balancing and orchestration are the responsibility of the consumer's infrastructure
