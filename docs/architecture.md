# Pentra — Architecture (Checkpoint 1)

This document records the architecture and the relevant decisions for the first
functional checkpoint of **Pentra**, a modular pentest orchestration platform.

## 1. Goals of Checkpoint 1

A working, server-rendered MVC application that lets a user:

- see a dashboard overview of engagements;
- perform basic CRUD on pentest projects;
- register scope and **authorized** targets;
- view the seven standard pentest phases;
- browse an initial catalog of eight security tools;
- select tools per project and per phase;
- persist everything locally in SQLite;
- see a basic change/state history;
- have the initial data structures for evidence and reports.

**No scanner is executed in this checkpoint.** Execution is deliberately gated
off (see §6).

## 2. Solution layout

A **modular monolith** (no microservices) with a clean, layered separation.

```
Pentra.slnx
├── src/
│   ├── Pentra.Domain          # Entities, enums, domain abstractions (no dependencies)
│   ├── Pentra.Application     # Services, DTOs, validation, service interfaces
│   ├── Pentra.Infrastructure  # EF Core DbContext, migrations, seeding, clock
│   └── Pentra.Web             # ASP.NET Core MVC (controllers, Razor views, Tailwind)
└── tests/
    └── Pentra.Tests           # xUnit tests (services, validation, seeding, policy)
```

Dependency direction (inward only):

```
Web  ─────▶ Application ─────▶ Domain
  └────────▶ Infrastructure ──▶ Application / Domain
```

- **Domain** has no external dependencies. It holds entities, enums and the
  contracts for future tool execution.
- **Application** depends only on Domain (plus EF Core abstractions). It holds
  the use-case services and input validation. It talks to persistence through
  the `IPentraDbContext` abstraction, not a concrete context.
- **Infrastructure** implements persistence (`PentraDbContext`), the system
  clock and the DI wiring for data access. It owns the EF Core migrations.
- **Web** is the only composition root that knows about all layers. It renders
  MVC/Razor views and wires DI via `AddApplication()` + `AddInfrastructure()`.

Backend is the source of truth; the UI is fully server-rendered.

## 3. Domain model

| Entity                | Purpose                                                        |
|-----------------------|----------------------------------------------------------------|
| `Project`             | Aggregate root for an engagement.                              |
| `ScopeTarget`         | An in-scope target with an explicit `IsAuthorized` flag.       |
| `PentestPhase`        | One of the seven standard methodology phases (reference data). |
| `SecurityTool`        | A catalog entry (Nmap, Nuclei, …). Catalog only — never run.   |
| `ProjectToolSelection`| Tool chosen for a given project **and** phase.                 |
| `Evidence`            | Initial structure for engagement evidence (metadata only).     |
| `ReportDraft`         | Initial structure for engagement reports.                      |
| `ChangeHistoryEntry`  | Append-only record of changes (the state history).             |

All persisted entities derive from `AuditableEntity` (`Id`, `CreatedAt`,
`UpdatedAt`).

### The seven phases (seeded)

1. Reconnaissance
2. Scanning & Enumeration
3. Vulnerability Analysis
4. Exploitation
5. Post-Exploitation
6. Reporting
7. Remediation & Retest

### The tool catalog (seeded)

Nmap, Subfinder, httpx, Gobuster, ffuf, Nikto, Nuclei, OWASP ZAP — each mapped
to a suggested default phase and a category.

## 4. Key decisions

- **MVC + Razor + Tailwind, server-rendered.** Simple, SEO-irrelevant internal
  tool; server rendering keeps the backend authoritative and avoids a SPA.
- **EF Core + SQLite.** Zero-config local persistence suitable for Kali and for
  a single-file Docker volume. Migrations are applied automatically on startup
  and reference data is seeded idempotently (`DatabaseInitializer` +
  `DataSeeder`).
- **Timestamps stored as UTC `DateTime`.** SQLite cannot `ORDER BY` a
  `DateTimeOffset`; using UTC `DateTime` keeps queries sortable and portable.
  Time is obtained through `ISystemClock` so services and auditing stay testable.
- **`IPentraDbContext` abstraction.** Application services depend on a narrow
  persistence interface rather than the concrete context, which keeps them
  isolated and unit-testable (tests use in-memory SQLite).
- **`Result` / `Result<T>` for expected failures.** Validation and domain
  failures are returned, not thrown, so controllers can surface them cleanly.
- **Change history shares the caller's unit of work.** `ChangeHistoryService`
  adds the entry but does not call `SaveChanges`, so the originating change and
  its audit row commit atomically.
- **No unnecessary abstraction.** No repository-per-entity, no CQRS, no
  mediator — the service layer talks to EF directly through the context
  interface. Contracts for future adapters/runners exist but are not generalized
  further than needed.

## 5. Future execution boundary (contracts only)

`Pentra.Domain.Abstractions` defines the shape of future tool execution without
implementing any run:

- `ToolExecutionRequest` / `ToolExecutionResult` — a validated request and its
  outcome.
- `IToolExecutionPolicy` — the **authorization gate**. The only implementation
  shipped is `DenyAllToolExecutionPolicy`, which denies every request.
- `IToolAdapter` — per-tool command-line builder (pure, no execution).
- `IToolRunner` — isolated executor (e.g. ephemeral container). **No runtime
  implementation is provided.**

This fixes the boundary that later checkpoints will fill in behind the
authorization policy, without reshaping the domain.

## 6. Security posture (Checkpoint 1)

- **No scanners run.** `IToolRunner` has no implementation; the execution policy
  denies everything.
- **No arbitrary commands.** The UI never accepts or runs a command line. Tool
  "selection" only creates a database row.
- **Docker socket is never exposed** to the web application (see
  `docker-compose.yml`).
- **Scope/authorization validation.** Target values are validated by kind
  (domain/IP/CIDR/URL) in `ScopeValidator`; targets carry an explicit
  `IsAuthorized` flag intended as the precondition for any future action.
- **Antiforgery** is enforced globally via `AutoValidateAntiforgeryTokenAttribute`;
  every state-changing form posts an antiforgery token.
- **Security headers** (`SecurityHeadersMiddleware`): a strict Content-Security-
  Policy (`default-src 'self'`, no inline scripts or styles), `X-Frame-Options:
  DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`.
  Because the CSP forbids inline styles, dynamic progress-bar widths use
  safelisted Tailwind utility classes rather than `style="..."`.
- **No plaintext credentials.** The app stores no credentials in this
  checkpoint; nothing secret is persisted.
- **Runs as a non-root user** in the container.

## 7. Known limitations

- Evidence and reports are data structures only; no capture/rendering yet.
- No authentication/authorization of users (single-operator local tool for now).
- SQLite is single-writer; adequate for local/single-instance use.
- **Transitive advisory warnings (NU1903):** restore reports known advisories in
  `System.Security.Cryptography.Xml` (pulled transitively by the design-time
  `Microsoft.EntityFrameworkCore.Design` package) and in the SQLite native
  library. These are build/design-time transitive dependencies and do not fail
  the build; they will clear as the EF Core 10 servicing packages update.

## 8. Testing

`Pentra.Tests` (xUnit, FluentAssertions, in-memory SQLite) covers:

- scope validation per target kind;
- project create/update/delete + history recording and status-change auditing;
- target add validation, duplicate rejection, authorization flag;
- tool selection toggle (add/remove);
- seeding (seven phases, eight tools, idempotency, valid phase mapping);
- the deny-all execution policy.
