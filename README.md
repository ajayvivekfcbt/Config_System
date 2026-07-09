# Utility Configuration System — Modernized

Modernization of the IBM i (AS/400) **Utility Configuration System** (RPG IV + DDS +
DB2, members `UT20xx` / `UTCFGxxx`) into a **.NET API + React** stack.

| Folder | Contents |
|--------|----------|
| `FROMDEVS.FILE/` | Original EBCDIC source members (`*.MBR`) |
| `decoded/` | Source decoded to readable text (code page 037, 108‑char records) |
| `docs/MIGRATION_PLAN.md` | Full assessment, mapping and phased plan |
| `docs/data-model.sql` | Portable DDL mirroring the DB2 tables |
| `backend/ConfigSystem.Api` | ASP.NET Core (.NET 10) Web API + EF Core (SQLite) |
| `frontend/` | React 18 + Vite + TypeScript UI |

## Run

```powershell
# 1. Backend  ->  http://localhost:5198  (Swagger at /swagger)
cd backend/ConfigSystem.Api
dotnet run

# 2. Frontend ->  http://localhost:5173  (proxies /api to the backend)
cd frontend
npm install
npm run dev
```

The database (`configsystem.db`) is created and seeded automatically on first run.

## What it does

- CRUD "work‑with" screens for all 9 configuration entities (Variable Types,
  Resolution Methods, Servers, Contexts, Extents, Scopes, Variable Definitions,
  Valid Values, Variable Values) — replacing the RPG `UT20xx` subfile programs.
- A **Resolve Value** screen that ports the consumption logic: given a variable and a
  server it returns the effective value, with an exact scope overriding the default.

See `docs/MIGRATION_PLAN.md` for the legacy‑to‑modern mapping and remaining phases.
