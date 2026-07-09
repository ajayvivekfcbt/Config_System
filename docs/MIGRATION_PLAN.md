# Utility Configuration System — Modernization Plan (RPG/IBM i → .NET + React)

## 1. Source assessment

The folder `FROMDEVS.FILE/` contains **EBCDIC-encoded IBM i (AS/400) source physical
file members** (`*.MBR`). They were decoded with code page **037** at a fixed record
width of **108** characters into `decoded/*.txt`.

The application is the **Utility Configuration System** — a green‑screen (5250) RPG IV
application that stores hierarchical configuration variables and resolves their values
per server/scope. It is composed of:

| Layer | Members | Notes |
|-------|---------|-------|
| DB2 tables (SQL DDL) | `UTCFGVTP, UTCFGSRM, UTCFGSRV, UTCFGCTX, UTCFGXTN, UTCFGSCP, UTCFGVDF, UTCFGVVL, UTCFGVAL` | 9 core config tables (+ `*01/02` logical views) |
| Menu | `UT2000` | Primary configuration menu |
| Work‑with lists | `UT2010‑UT2018, UT2052, UT2050…` | Subfile CRUD list per entity (SQL cursor) |
| Row maintainers | `UT2020, UT2021…` | Add / Change / Copy / Delete / Display one row |
| Where‑used / cross‑ref | `UT2040, UT2041` | Impact analysis |
| Resolution / consumption | `UT2061‑UT2087` | Resolve a variable to a value for a server/scope |
| Conform / sync | `UT2072` | Conform configuration across servers |
| Audit / reporting | `UT2090, UT2099` | Maintenance audit report |
| `D` suffixed members | `UT20xxD` | DDS display files (the green screens) |

### Data model

```
Context (CTX) ─┐
               └─< Extent (XTN) ─┐
                                 └─< VariableDefinition (VDF) ─┬─> VariableType (VTP, has validation proc)
                                                               ├─< ValidValue (VVL)   (when VDF.restricted)
                                                               └─< VariableValue (VAL) >── Scope (SCP)
Server (SRV) ──< Scope (SCP) >── ScopeResolutionMethod (SRM)
```

A **VariableValue** binds a *variable definition* and a *scope* to a concrete value.
A **scope** belongs to a *server* and a *resolution method*. Consumption resolves a
variable name + a server into the most specific scope's value.

## 2. Target architecture

```
frontend/  React 18 + Vite + TypeScript        (the modern replacement for 5250 screens)
backend/   ASP.NET Core (.NET 10) Web API       (replaces RPG programs + service procs)
           EF Core + SQLite (portable)          (replaces DB2 tables)
```

| RPG concept | .NET / React replacement |
|-------------|--------------------------|
| DB2 physical files | EF Core entities + `DbContext` (SQLite by default; swap to SQL Server/DB2 via provider) |
| SQL cursors in work‑with | EF Core `IQueryable` + paged REST endpoints |
| Subfile screens (`UT20xxD`) | React list pages with a data grid |
| Row maintainer (`UT2020`) | React edit form + `POST/PUT/DELETE` endpoints |
| Options 2/3/4/5/8/9 | Grid row actions: Edit, Copy, Delete, View, Info, Where‑used |
| Validation procedures (`VTP_PROC`) | Pluggable `IVariableValidator` strategy in the API |
| Consumption procedures | `IConfigResolutionService.Resolve(variable, server)` |
| Conform across servers (`UT2072`) | `POST /api/conform` service endpoint |
| Maintenance audit (`UT2090`) | Audit table + `GET /api/audit` |

## 3. Migration phases

1. **Foundation (this delivery)** — decode source, data model, EF Core entities, REST
   CRUD for all 9 entities, resolution service, React menu + list/edit UI, seed data.
2. **Business rules** — port type‑validation procedures, restricted valid‑value
   enforcement, where‑used queries, copy semantics.
3. **Resolution engine** — port consumption programs (`UT2061‑UT2087`): scope matching
   precedence and server `MATCH` evaluation.
4. **Operational features** — conform across servers (`UT2072`), maintenance audit
   reporting (`UT2090`), info windows (option 8).
5. **Hardening** — authn/authz (replaces IBM i object authority `CRUD` rights),
   automated tests, CI, containerization, and DB2/SQL Server provider for production.

## 4. What is included in this repository now

- `backend/ConfigSystem.Api` — runnable .NET Web API: entities, `DbContext`, REST
  endpoints for the 9 config entities, the resolution service, and DB auto‑seed.
- `frontend/` — runnable React + Vite app: configuration menu and work‑with screens
  for the core entities, wired to the API.
- `docs/data-model.sql` — portable DDL mirroring the original DB2 tables.

## 5. Running it

```powershell
# Backend
cd backend/ConfigSystem.Api
dotnet run                 # serves https://localhost:5198 (Swagger at /swagger)

# Frontend (separate terminal)
cd frontend
npm install
npm run dev                # serves http://localhost:5173, proxies /api to the backend
```
