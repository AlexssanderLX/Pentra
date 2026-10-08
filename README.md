# Pentra

**Pentra** is a modular penetration-testing orchestration platform — project
planning, scope and authorization management, methodology tracking, a security
tool catalog, and the foundations for evidence and reporting.

> **Checkpoint 1 scope.** This is the first functional checkpoint: a
> server-rendered ASP.NET Core MVC dashboard backed by SQLite.
> **No security scanner is executed in this version.** Tool "selection" only
> records intent; execution is gated off behind an authorization policy that
> denies everything (see [`docs/architecture.md`](docs/architecture.md)).

## Features (Checkpoint 1)

- Dashboard with an overview of engagements.
- CRUD for pentest projects.
- Scope registration with **explicitly authorized** targets (domain / IP / CIDR / URL / host), validated by kind.
- The seven standard pentest phases.
- Initial tool catalog: **Nmap, Subfinder, httpx, Gobuster, ffuf, Nikto, Nuclei, OWASP ZAP**.
- Tool selection per project **and** phase.
- Local persistence with SQLite (auto-migrated and seeded on startup).
- Basic change/state history per project.
- Initial data structures for evidence and reports.

## Tech stack

ASP.NET Core MVC (.NET 10) · Razor views · C# · Tailwind CSS · EF Core · SQLite · Docker.

---

## Run with Docker (recommended)

Requires Docker with the Compose plugin (Docker Desktop on Windows, or Docker
Engine on Linux). The image builds with the .NET SDK only — the Tailwind CSS is
pre-built and committed, so **no Node toolchain is needed to build the image**.

```bash
docker compose up --build
```

Then open <http://localhost:8080>.

- Data is stored in a named volume (`pentra-data`) and **survives restarts**.
- The container runs as a non-root user.
- The Docker socket is **not** mounted into the container.

Stop it (keeping data):

```bash
docker compose down
```

Reset everything including the database:

```bash
docker compose down -v
```

---

## Run locally (without Docker)

### Prerequisites

- **.NET SDK 10.0** — <https://dotnet.microsoft.com/download>
- **Node.js 18+** — only if you want to rebuild the Tailwind CSS.

### Kali Linux

```bash
# .NET SDK (via the Microsoft feed; see Microsoft docs for the current release)
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
# If the distro feed lacks it, use the dotnet-install script:
#   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0

git clone <your-repo-url> pentra && cd pentra
dotnet run --project src/Pentra.Web
```

### Ubuntu

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
# Or the official script if the package is unavailable:
#   curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0

git clone <your-repo-url> pentra && cd pentra
dotnet run --project src/Pentra.Web
```

### Windows

```powershell
winget install Microsoft.DotNet.SDK.10
git clone <your-repo-url> pentra
cd pentra
dotnet run --project src/Pentra.Web
```

By default a local run uses the launch profile. To pin it to port 8080 like the
container:

```bash
dotnet run --project src/Pentra.Web --no-launch-profile --urls http://localhost:8080
```

The SQLite database is created at `src/Pentra.Web/data/pentra.db` on first run.

---

## Rebuilding the Tailwind CSS (optional)

The compiled stylesheet (`src/Pentra.Web/wwwroot/css/app.css`) is committed. To
change the design, edit `src/Pentra.Web/Styles/app.css` or
`tailwind.config.js` and rebuild:

```bash
cd src/Pentra.Web
npm install
npm run css:build      # one-off, minified
npm run css:watch      # rebuild on change during development
```

---

## Build & test

```bash
dotnet build Pentra.slnx
dotnet test tests/Pentra.Tests/Pentra.Tests.csproj
```

## Database migrations

Migrations live in `src/Pentra.Infrastructure/Persistence/Migrations` and are
applied automatically at startup. To add a migration after a model change:

```bash
dotnet ef migrations add <Name> \
  --project src/Pentra.Infrastructure \
  --startup-project src/Pentra.Infrastructure \
  --output-dir Persistence/Migrations
```

(Install the tool once with `dotnet tool install --global dotnet-ef --version 10.0.0`.)

---

## Project structure

```
src/Pentra.Domain          Entities, enums, future-execution contracts
src/Pentra.Application      Services, DTOs, validation, interfaces
src/Pentra.Infrastructure   EF Core context, migrations, seeding
src/Pentra.Web              MVC controllers, Razor views, Tailwind UI
tests/Pentra.Tests          xUnit test suite
docs/architecture.md        Architecture & decisions
```

See [`docs/architecture.md`](docs/architecture.md) for the design rationale and
the security posture.

## Security

This checkpoint runs **no** scanners and executes **no** arbitrary commands. It
enforces antiforgery on all forms, ships a strict Content-Security-Policy and
other security headers, validates scope input, and never exposes the Docker
socket to the application. See the architecture doc for details.
