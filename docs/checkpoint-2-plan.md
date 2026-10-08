# Pentra — Checkpoint 2 Technical Plan: Operational Execution Engine

> Goal: turn Pentra from a project manager into an **operational pentest
> workspace** that can run Nmap, Subfinder and httpx against **authorized**
> targets, in isolated ephemeral containers, with live logs, persisted
> structured results, cancellation and timeouts — end to end, safely.

## 0. Diagnosis of the existing codebase (done)

- **Architecture**: modular monolith, layers `Web → Application → Domain` and
  `Infrastructure → Application/Domain`. Preserved as-is.
- **Contracts already present** in `src/Pentra.Domain/Abstractions/ToolExecutionContracts.cs`:
  `IToolAdapter`, `IToolRunner`, `IToolExecutionPolicy`, `ToolExecutionRequest`,
  `ToolExecutionResult`, `ToolExecutionAuthorization`. These are the extension points.
- **Policy today**: `DenyAllToolExecutionPolicy` (Application/Security) — denies all.
- **Catalog**: `SecurityTool` (slug/command/category/default phase). Reused to key adapters.
- **Persistence**: EF Core + SQLite, migrations auto-applied + idempotent seed on startup
  (`DatabaseInitializer`, `DataSeeder`). Clock via `ISystemClock`.
- **Web**: MVC + Razor + Tailwind; global antiforgery; `SecurityHeadersMiddleware` with a
  strict CSP that already allows `connect-src 'self'` and `script-src 'self'`.
- **Build/tests**: `dotnet build` clean; 30 xUnit tests green.

## 1. Core architectural decision — the privileged runner boundary

**The web app must not have access to the Docker Engine** (hard requirement).
So execution cannot happen in the web process. Resolution:

- Add **`Pentra.Runner`** — a .NET Worker Service (`BackgroundService`) that reuses
  the shared projects (Domain/Application/Infrastructure). It is the **only**
  container that mounts the Docker socket.
- The web app and the runner communicate through a **DB-backed job queue** (the
  shared SQLite database on a volume), not a network API and not the socket.
  - Web: after authorization + explicit confirmation, inserts a `ToolRun` with
    status `Pending`. It **never** touches Docker.
  - Runner: claims `Pending` runs (atomic `Pending → Running` conditional update),
    **re-authorizes** (defense in depth), builds argv via the adapter, launches an
    ephemeral tool container with limits, streams logs, captures stdout/stderr/exit,
    parses results, and writes the final state back.

This is one extra process, justified purely by the socket-isolation requirement —
not an "unnecessary microservice". SQLite runs in WAL mode with a busy timeout to
tolerate the two writers (local single-instance usage).

```
┌────────────┐   writes Pending run    ┌──────────────┐
│  Pentra.Web │ ───────────────────────▶│  SQLite (vol) │
│ (no socket) │◀─── reads state/logs ───│  WAL mode     │
└────────────┘                         └──────▲────────┘
                                              │ claim/update
                                      ┌───────┴────────┐  docker.sock
                                      │ Pentra.Runner  │────────────▶ ephemeral
                                      │ (privileged)   │              tool containers
                                      └────────────────┘
```

## 2. Data model (new entities + migration)

- `ExecutionStatus` enum: `Pending, Running, Completed, Failed, Cancelled, TimedOut`.
- `ToolRun : AuditableEntity`
  - FKs: `ProjectId`, `PhaseId`, `SecurityToolId`, `ScopeTargetId`.
  - `Status`, `CancelRequested` (bool), `IsActiveScan` (bool), `ConfirmedActive` (bool).
  - `ToolSlug`, `ImageRef` (pinned image\:tag), `ApprovedArgumentsJson` (validated argv),
    `ParametersJson` (the approved high-level parameter set).
  - `AuthorizationAllowed` (bool) + `AuthorizationReason` (recorded decision).
  - `StartedAt`, `CompletedAt`, `DurationMs`, `ExitCode`, `FailureReason`.
  - `RawOutput` (sanitized, truncated), `ErrorOutput` (truncated), `ResultJson` (parsed),
    `ArtifactSha256` (hash of raw output).
  - Concurrency: `ClaimedAt`, `RunnerId`, `RowVersion`/token for safe claiming.
- `ToolRunLogLine : entity` — `ToolRunId`, `Seq`, `Stream` (stdout/stderr), `Text`, `CreatedAt`.
  Backs the live log stream (append-only, queried by `Seq` delta for SSE).
- Duplicate guard: reject a new run when an active (`Pending`/`Running`) run exists for the
  same `(ProjectId, PhaseId, SecurityToolId, ScopeTargetId, ParametersJson)`.

## 3. Execution engine (Application, scanner-agnostic)

- `IExecutionService` (Application):
  - `RequestRunAsync(...)` — validates project/phase/tool/target exist, builds the
    `ToolExecutionRequest`, calls `IToolExecutionPolicy.Authorize`, enforces the
    active-scan confirmation and the duplicate guard, persists `Pending` + records
    an authorization decision in history. Returns the run id or a `Result` failure.
  - `RequestCancelAsync(runId)` — sets `CancelRequested`.
  - `GetRunAsync` / `GetRunsForProjectAsync` / `GetLogDeltaAsync(runId, afterSeq)`.
- `IExecutionQueue` (Infrastructure impl) — claim/complete/fail/timeout transitions
  used by the runner; atomic claim via conditional `UPDATE ... WHERE Status=Pending`.
- State machine with explicit, tested transitions; illegal transitions rejected.
- **Startup recovery**: on runner start, any run left `Running` with no live container is
  moved to `Failed` (reason: "interrupted") — satisfies "recuperar execuções interrompidas".
- Concurrency: configurable max parallel runs; the runner claims up to the limit.

## 4. Docker tool runner (Infrastructure)

- `DockerToolRunner : IToolRunner` using **Docker.DotNet** (talks to the socket via the
  Engine API — no `docker` CLI, no shell, no string concatenation; argv passed as a list).
- Per run: `create container → start → stream logs → wait (with timeout) → inspect exit →
  remove container` (always cleaned up, even on failure/timeout/cancel).
- Hardening on every container:
  - pinned, approved image (`ImageRef`); pull policy = only approved images.
  - no `--privileged`; `CapDrop: [ALL]`; `ReadonlyRootfs` where the tool allows; `NoNewPrivileges`.
  - no host bind mounts; work in a `tmpfs` or container-local dir.
  - CPU, memory, PID limits; `--network` constrained (none/bridge per tool capability).
  - run as non-root inside the tool container where the image supports it.
- Cancellation = stop/kill+remove the container; timeout = same → status `TimedOut`.
- The interface exposed to the engine is narrow (`RunAsync` + a cancellation token +
  a log callback); the runner never grants arbitrary Docker control upward.

### Network limitations to document (README/architecture)
- **Linux**: socket at `/var/run/docker.sock`; container networking native.
- **Windows / WSL2 (Docker Desktop)**: the Linux socket is used; `host.docker.internal`
  resolves the host; scanning `localhost` from a tool container means the container, not the
  Windows host — document that targets should be real hostnames/IPs.

## 5. Tool adapters + definitions (Application)

A `ToolDefinition` registry (keyed by slug) carrying: identifier, pinned image\:tag,
capabilities, allowed parameters (typed schema), execution requirements, risk class
(passive/active), execution limits (timeout, cpu, mem), and the parser.

- **Nmap** (`instrumentisto/nmap:<pinned>`) — active. Output `-oX -` (XML). Allowed params:
  service detection on/off, top-ports N (bounded), timing template (bounded). Parser →
  hosts/ports/services. Target = the authorized value only.
- **Subfinder** (`projectdiscovery/subfinder:<pinned>`) — passive. Output `-oJ`/JSONL.
  Parser → discovered subdomains. Discovery only; never auto-scans results.
- **httpx** (`projectdiscovery/httpx:<pinned>`) — active-ish. JSONL output; status/title/tech.
  Redirects **off by default** (no out-of-scope following). Parser → url/status/title/tech.

Adapters build argv from the **approved parameter set only**; the target is injected from
the authorized `ScopeTarget`. No field is ever concatenated into a shell string.

## 6. Execution policy (replaces DenyAll)

`ScopedToolExecutionPolicy : IToolExecutionPolicy`:
- Deny by default.
- Require the target to belong to the project scope **and** be `IsAuthorized`.
- Require the project to be in an executable state (e.g., `Active`).
- Require explicit active-scan confirmation for active tools.
- Reject unknown tools / unknown parameters / anything not from the typed schema.
- Decisions are returned with a reason and recorded (history + `ToolRun` fields).
- Rate/concurrency limits enforced by the engine (policy exposes the allow decision).
`DenyAllToolExecutionPolicy` kept for reference/tests but no longer the default.

## 7. Dashboard (Web, Razor + Tailwind + SSE)

Project page gains an **Executions** panel:
- Start form: tool + authorized target + allowed parameters + active-scan confirm.
- Run list with status badges, duration, exit code.
- Run detail: live **log console** via **SSE** (`/projects/{id}/runs/{runId}/stream`),
  structured results table, and a **Cancel** button.
- Why SSE over SignalR: one-way server→client streaming is all we need; SSE needs no client
  library, works within the existing strict CSP (`connect-src 'self'`), and is simpler/robust.
  A tiny external `wwwroot/js/execution.js` (CSP `script-src 'self'`) drives `EventSource`.
- Visual identity (black + neon green) unchanged.

## 8. Automatic evidence

Each `ToolRun` persists: tool + image version, project + target, timestamps + duration,
approved parameters, structured result, sanitized raw output, errors, `ArtifactSha256`,
and final state. **No result is auto-labelled a confirmed vulnerability.**

## 9. Testing strategy

- **Unit (no internet, no Docker)**: policy (authorized/unauthorized/scope/active-confirm),
  state-machine transitions, duplicate guard, adapters' argv building, **parsers** for Nmap
  XML / Subfinder JSONL / httpx JSONL (sample fixtures), command-injection resistance
  (adapters reject/encode hostile parameter values), queue claim/concurrency with in-memory
  SQLite, startup recovery, and a **fake `IToolRunner`** driving the engine through all states
  incl. timeout/cancel/failure.
- **Integration (opt-in, trait `Docker`)**: real `DockerToolRunner` against a pinned image,
  skipped unless Docker is present and an env flag is set. Never runs in the default suite.

## 10. Delivery plan (small, verifiable commits on `feature/execution-engine`)

1. Plan doc + branch. *(this commit)*
2. Domain: `ExecutionStatus`, `ToolRun`, `ToolRunLogLine`, contract tweaks + EF config + migration.
3. Application: `ToolDefinition` registry, adapters (nmap/subfinder/httpx) + parsers + unit tests.
4. Application: `ScopedToolExecutionPolicy` + `IExecutionService`/engine + unit tests.
5. Infrastructure: `DockerToolRunner` (Docker.DotNet) + queue/claim + startup recovery.
6. `Pentra.Runner` worker project + docker-compose service (socket only here) + WAL config.
7. Web: executions controllers, SSE endpoint, Razor views, `execution.js`, Tailwind bits.
8. Integration tests (opt-in), README + architecture updates, final end-to-end validation.

## Risks / open items
- SQLite two-writer contention — mitigated by WAL + busy timeout; acceptable for local single
  instance. If it bites, a later checkpoint can move the queue to a dedicated store.
- Tool images must be pulled on first run (needs internet, as scanning does); unit tests never
  depend on this.
- WSL2/Windows networking nuances for `localhost` targets — documented, not auto-resolved.
