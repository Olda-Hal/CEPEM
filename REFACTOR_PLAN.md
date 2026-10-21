# CEPEM Refactoring Plan

A full-stack audit (backend HealthcareAPI + DatabaseAPI, frontend, AIService, Docker/nginx/Grafana, scripts) produced the tiered plan below. Work is executed in tier order because later tiers depend on earlier ones: nothing can be verified until the build and test pipeline work, and large refactors should not start before the architecture decisions are settled.

Work is done on the `refactor` branch.

- **Tier 0** — ✅ Complete (commits `7352c62`, `d723646`, `64a67f9`, `7f52c74`, `3be1d51`)
- **Tier 1** — ✅ Complete (commits `274223a`, `ad818fe`, `6b4f716`)
- **Tier 2** — ✅ Complete (commits `b0d41e5`, plus `c1a0b7f`)
- **Tier 3** — 🔶 Mostly done: global exception handling + permission cache (`c1a0b7f`), UID-allocation/PatientDto/Include-chain/event-creation dedup, AsNoTracking reads, race-safe slot creation (`02b06d4`, `bfa7d55`), the 24-Include patient-detail query split with live verification (`951a134`), and the Persons.UID unique index with legacy backfill (`2947659`); remaining: enum statuses + soft-delete/timestamp conventions, and an optional EventsController/PatientsController service-layer split (low value now that the duplication is gone)
- **Tier 4** — ✅ Mostly done: Vite/TS5/vitest (`2ed1282`), API-layer fixes (`1b8e304`), toasts, BaseModal, route guards (`816f3da`), PatientDetailPage decomposed (`906e5b1`), React.lazy code-splitting + login rate limiting (`ebeef4b`), all modals on BaseModal (`e703156`), dynamic document language (`d164d17`); deliberately not done: consolidating the patient-record vs intake-questionnaire forms (different domains, needs a UX decision) and content skeletons
- **Tier 5** — 🔶 Partially absorbed: login rate limiting done, docs truth pass done (`2947659`); remaining: TanStack Query for data fetching, deeper test coverage of the risky paths

---

## Tier 0 — Security hotfixes and unblocking work

*Fix the things that are actively dangerous, or that block all other work (build and tests). Done before anything else.*

| # | Item | Status |
|---|------|--------|
| 0.1 | Close public DatabaseAPI exposure — remove `/api/database/` from nginx, bind dev compose ports (mysql, database-api) to `127.0.0.1` | ✅ |
| 0.2 | Fix the broken permission build — add missing `DbSet<Permission>`/`RolePermission`/`EmployeePermission`, regenerate migration + snapshot (was non-compiling; next `migrations add` would have dropped the tables) | ✅ |
| 0.3 | Make tests able to fail — remove `\|\| true` from all `Dockerfile.test`s; repair both broken .NET test projects (stale types, wrong DTOs, phantom properties) | ✅ |
| 0.4 | Remove committed secrets and fail fast — JWT secret fallback, real document encryption key in source, MySQL root password in `migrate.py`, keys in `.env.example`; missing config now throws at startup instead of silently using a public key | ✅ |
| 0.5 | New encryption format — `CEPM1` magic + per-file random IV (was static IV for every file); `reencrypt.py` rewritten to match | ✅ (code; see manual steps) |
| 0.6 | Quick unauth patches — `[AllowAnonymous]` off `GetPatientPhoto`, `[Authorize]` + path-traversal fix on `TestResultsController`, delete `SeedController` (unsalted SHA-256, password in response), IDOR fixes on patient/examination document endpoints (route ids were ignored) | ✅ |

**Manual steps still required (cannot be done from the repo):**

1. **Rotate all secrets on the server** — the old JWT secret and encryption keys were committed to git and must be considered compromised. Generate new ones (`openssl rand -hex 32`) and set `JWT_SECRET_KEY`, `PHOTO_ENCRYPTION_KEY`, `DOCUMENT_ENCRYPTION_KEY` in the server `.env`.
2. **Re-encrypt patient files** — `python scripts/reencrypt.py --old-key <current> --old-iv <current> --new-key <new>`. After completion the legacy IV env vars can be dropped.
3. **Reload nginx** on the server (`nginx -s reload`) — the config change is inert until then.
4. `ADMIN_INITIAL_PASSWORD` env var now controls the seeded admin account; without it, admin creation is skipped (was hardcoded `admin`/`admin`).

---

## Tier 1 — Architectural decisions

*Decide the big structural questions before refactoring, so code isn't rewritten twice.*

Three decisions were made (recommended options chosen):

1. **Authorization model → code-based permissions.** The URL-pattern ACL (`AccessControlMiddleware`, seeded `"POST:/api/patients/{id}"`-style rules that silently rot when routes change) was deleted and replaced by the `Permission`/`RolePermission`/`EmployeePermission` system with codes like `patients.read`:
   - 33-code permission catalog seeded with role grants preserving the previous per-role policy (SysAdmin, Examiner, Doctor, Center Admin, Country Admin)
   - `[RequirePermission]` on every secured action; `PermissionFilter` fixed (JSON error bodies, logging, abort propagation)
   - Frontend: `RoleManagementPage` editor is now Allow/Deny/None per permission code; `PermissionsModal` wired into `AdminEmployeesPage`
   - Migration drops the four old ACL tables
2. **AIService → deleted.** It was a stub (only `/health` + `/api/info`; the 347-line "AI core" was never imported and was a hardcoded mock; the frontend `aiApi.ts` client was dead code). Service, compose entries, nginx route and docs removed. Can be rebuilt properly later if wanted.
3. **Gateway → YARP.** 9 pure-proxy controllers (~2,700 lines of copy-paste `try/parse/catch` forwarding) replaced by ~35 explicit YARP routes with per-route permission policies (`perm:{code}`) and a claims→`X-Actor-*` header transform. `ReservationSlotsController` keeps only `ConfirmSlot` (defaults the doctor to the authenticated employee).

**Result:** HealthcareAPI went from ~5,700 lines of controller code to ~1,100; both test suites fully green (DatabaseAPI 59/59, HealthcareAPI 36/36 — previously the latter didn't compile and failures were hidden by `|| true`).

**Still needed before production:** smoke test against the running stack (YARP route table + re-seeded permissions haven't been exercised end-to-end; needs MySQL + both APIs up), then `docker compose restart`.

---

## Tier 2 — Infrastructure and plumbing

*Makes subsequent changes verifiable and deployable.*

| # | Item |
|---|------|
| 2.1 | ~~Fix `IActivityLogService` double registration + `DatabaseApiUrl` config key mismatch~~ (fixed as part of Tier 0/1) |
| 2.2 | Service-to-service auth between HealthcareAPI → DatabaseAPI (shared secret/mTLS). DatabaseAPI still has no authentication of its own — it is network-isolated but trusts spoofable `X-Actor-*` headers from anyone on the Docker network |
| 2.3 | Docker hardening — healthchecks + `depends_on: condition: service_healthy` in all compose files, remove `build-incrementer`/tracked `BUILD_NUMBER`, remove `test-results` mount from the prod healthcare-api container, non-root containers, aligned Python versions (now moot for AI), remove the commented-out test services block |
| 2.4 | Real CI — GitHub Actions running build + tests on PR (current `deploy.yml` only runs `docker compose build --no-cache`; no tests run) |
| 2.5 | Nginx hardening — security headers (CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy), `resolver` for OCSP stapling (currently silently broken), remove hardcoded domain, drop deprecated `http2` listen syntax |
| 2.6 | Repo hygiene — untrack `scripts/__pycache__/*.pyc`, delete `data.zip` (9.3 GB duplicate), fix `.gitignore` (global `*.xml` rule is dangerous), delete dead `DatabaseAPI/Models/` folder, stale `create_database.sql`, regenerate or delete `database_structure.DBML` (badly outdated), decide Grafana's fate (disabled in compose but ships a datasource with a plaintext password) |

---

## Tier 3 — DatabaseAPI and HealthcareAPI internals

*The real code refactor, now on top of one auth model and a slim gateway.*

| # | Item |
|---|------|
| 3.1 | Global exception handler + ProblemDetails + one consistent error contract (stop per-action try/catch, stop leaking `ex.Message` to clients) |
| 3.2 | ~~YARP for passthrough routes~~ (done in Tier 1) |
| 3.3 | Cache/batch permission evaluation — the `PermissionFilter`/YARP policy makes one HTTP call to DatabaseAPI per action per request; add a short-lived in-memory cache keyed by (employee, permission, acl state) |
| 3.4 | Split `EventsController` (1,166 lines: event CRUD, 6 option endpoints, intake forms, magic links, patient history, Czech comment building) and `PatientsController` (980 lines) into services; kill the duplication: `PatientDto` mapping ×5, slot `Include` chain ×6, `CreateEvent`/`CreateEventGroup` ~90% identical, UID allocation copy-pasted ×3 with a race condition (move to a DB sequence/unique index) |
| 3.5 | Query hygiene — `AsNoTracking` on read paths (used twice in the whole service today), break up the 24-`Include` patient detail query (cartesian explosion), fix per-row `SaveChangesAsync` loops, make the reservation-slot overlap check race-safe |
| 3.6 | Schema hygiene — enums instead of free-text statuses (`"AVAILABLE"`/`"Scheduled"`…), one soft-delete pattern (three coexist: `IsDeleted`/`Active`/`IsActive`/hard delete), one timestamp convention (`Patient`/`Event`/`Examination` have none), string lengths instead of `longtext`, unique indexes (`Persons.UID`, `ContactEmails.Email`), drop the legacy `Reservation` entity (still merged into patient detail with an `Id = -r.Id` hack) |
| 3.7 | Tests for the risky paths as each service is extracted — auth flows (BCrypt, expiration, lockout), permission evaluation, slot booking races, intake-form magic links, document access control (the IDOR cases). Use Testcontainers/real MySQL instead of InMemory |

---

## Tier 4 — Frontend refactor

| # | Item |
|---|------|
| 4.1 | **Vite migration** — CRA (`react-scripts` 5.0.1) is deprecated and blocks everything; TypeScript 5.x; drop wrong `@types/react-router-dom@5`; add prettier + real ESLint config + `tsc --noEmit` in CI; move test deps to devDependencies (fixes the `--legacy-peer-deps` friction) |
| 4.2 | API layer fixes — structured `ApiError` (the fetch wrapper throws `Error` with raw body text; `AuthContext` reads `error.response?.data` which never exists on a fetch error, so server messages are silently dropped), 401 → auto-logout (expired tokens currently never log out), single token source (localStorage read directly by `ApiClient` **and** kept in context state), centralize the 86 scattered endpoint string literals |
| 4.3 | Shared primitives — one `Modal` component (portal, focus trap, ESC, `aria-modal`) replacing 7 hand-copied overlay CSS blocks; a toast system replacing 38 `alert()` calls; a layout route for `AppHeader` (manually rendered in 10 pages) |
| 4.4 | Route guards — `/admin/*` pages are only behind login, no role/permission check; wire permission awareness from the new backend model |
| 4.5 | Component surgery — `PatientDetailPage.tsx` (1,114 + 1,237 CSS lines; extract the ~160-line `parseIntakeSummary` Czech-label parsing engine), `AddEventModal` (849), `ReservationSlotsPage` (744, 25 `useState`), `EditPatientPage` (748); consolidate the **four** separate patient-form implementations onto `PatientFormFields` |
| 4.6 | UX/accessibility — skeletons, empty states, `aria` (3 attributes in the whole app today), focus management, dynamic `<html lang>` (hardcoded `cs` despite bilingual i18n), `React.lazy` code splitting (zero today — every page ships in the main bundle) |
| 4.7 | Cleanup — delete `TestDashboard` prod route, CRA boilerplate (`App.css` leftovers, `logo.svg`, `reportWebVitals`), `ExtractableText.tsx`; split `types/index.ts` (692-line monolith); chip away at the 43 `any`s |

---

## Tier 5 — Nice to have / later

- **TanStack Query** for data fetching (best introduced per-page during 4.5 — deletes the per-page `useEffect`+`useState` boilerplate)
- **Grafana**: revive properly (secrets-managed datasource) or delete
- **Documentation truth pass** — `TESTING.md` claims 80% coverage minimums that are fiction; `DEPLOYMENT.md` references a nonexistent `.env.production` and a `sed`-replace that doesn't match the actual nginx file; `QUICK_START.md` has the wrong path and omits the mandatory `.env` step
- **AIService real implementation** (if AI features are wanted — build against a real provider with auth from the start)
- **Login rate limiting / account lockout** — currently none; password policy is min 6 characters (should be prioritized earlier if the platform is public-facing)
- Delete or repair the `generate-test-report.sh` machinery and the `/tests` dashboard (the report script fabricates test counts: "estimated based on coverage")

---

## Critical path (one line)

**Close exposure → make it compile → make tests able to fail → rotate keys → pick auth model → then everything else.**

Tiers 0–2 removed roughly 5,700 lines while fixing every security hole found in the audit. Tier 3 internals, Tier 4 component surgery and Tier 5 remain.
