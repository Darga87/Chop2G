# 92_TEST_LOG

Технический журнал прогонов тестов и валидации.

Назначение:
- хранить историю `dotnet build` / `dotnet test`;
- фиксировать регрессии и их исправления;
- не смешивать это с управленческим прогресс-трекером.
- не хранить roadmap/план задач (для этого используется `docs/91_PROGRESS_TRACKER.md`).

Формат записи:
- Дата/время
- Контекст (какой task/phase)
- Команда
- Результат (кратко)
- Комментарий (если были падения)

- 2026-02-09
- Контекст: Task-006 Wave 2 Web Console MVP
- Команда: `dotnet build Chop2G.sln`
- Результат: успешно

- 2026-02-09
- Контекст: Task-006 Wave 2 Web Console MVP
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-09
- Контекст: Task-006 Wave 3 Web Console MVP
- Команда: `dotnet build Chop2G.sln`
- Результат: успешно

- 2026-02-09
- Контекст: Task-006 Wave 3 Web Console MVP
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-09
- Контекст: Task-008 Tailwind everywhere (remove Bootstrap)
- Команда: `npm run build:css`
- Результат: успешно (Web+Mobile `wwwroot/app.css` сгенерированы)

- 2026-02-09
- Контекст: Task-008 Tailwind everywhere (remove Bootstrap)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-09
- Контекст: Fix Dev Postgres connectivity (force IPv4 host)
- Команда: `dotnet build Chop2G.sln`
- Результат: успешно

- 2026-02-09
- Контекст: Fix Dev Postgres connectivity (force IPv4 host)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-09
- Контекст: Fix Dev Postgres connectivity (normalize localhost -> 127.0.0.1 in Infrastructure)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-09
- Контекст: Local dev doc + Postgres service start guidance
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-009 Operator Dashboard (dispatches in IncidentDetailsDto + realtime dispatch events)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-009 Yandex map integration (dashboard markers ALERT/HOME)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 30/30`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-009 Guards geo end-to-end (POST /guard/location/ping + GuardLocationUpdated + dashboard markers)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 32/32`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-010 Alerts UX (server alert_events + operator endpoints + dashboard integration)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 34/34`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Realtime hardening (unify SignalR publish via platform outbox, remove dual-path)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 34/34`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-010 Alerts UX continuation (map badges + second dispatch allowed in DISPATCHED)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 35/35`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Production-hardening MVP (rate limiting auth/guard ping + geo publish throttling)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 35/35`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Production-hardening continuation (rate limiting middleware + guard ping publish throttling)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 35/35`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-010 Alerts taxonomy (NO_ACCEPT + GUARD_NO_PING + map markers)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 36/36`, `Chop.Application.Tests 10/10`)

- 2026-02-10
- Контекст: Task-010 SLA alerts worker (NO_ACCEPT_STUCK + GUARD_OFFLINE)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 37/37`, `Chop.Application.Tests 10/10`)

- 2026-02-11
- Контекст: Task-010 SLA alerts continuation (`INCIDENT_STUCK_IN_STATUS` + map marker)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 38/38`, `Chop.Application.Tests 10/10`)

- 2026-02-11
- Контекст: Task-010 operator workflow MVP (`assign/override` for alerts)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 39/39`, `Chop.Application.Tests 10/10`)

- 2026-02-11
- Контекст: Task-010 alerts taxonomy continuation (`INCIDENT_POINT_CONFLICT` + map marker)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 40/40`, `Chop.Application.Tests 10/10`)

- 2026-02-11
- Контекст: Task-010 alerts navigation UX (map badges + jump-to-target)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 40/40`, `Chop.Application.Tests 10/10`)

- 2026-02-11
- Контекст: Task-006 RBAC extension (MANAGER/ACCOUNTANT pages/routes)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 40/40`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Task-013 security hardening + Task-012 Web API integration + Task-014 geo groundwork
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Task-013 security hardening + Task-012 Web API integration + Task-014 geo groundwork
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 55/55`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Task-014 PostGIS migration (`GeoPoint` + backfill + GiST indexes)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Task-014 PostGIS migration (`GeoPoint` + backfill + GiST indexes)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 55/55`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Task-014 nearest endpoint (`GET /api/operator/incidents/{id}/nearest`, PostGIS + SQLite fallback)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Task-014 nearest endpoint (`GET /api/operator/incidents/{id}/nearest`, PostGIS + SQLite fallback)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Task-014 GeoPoint first-class continuation (`AppDbContext` sync `GeoPoint` <-> `Latitude/Longitude`)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Task-014 GeoPoint first-class continuation (`AppDbContext` sync `GeoPoint` <-> `Latitude/Longitude`)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Task-014 GeoPoint-first read-flow (`IncidentMapper/Service`, `AlertEventsService`, `IncidentRepository` nearest)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Task-014 GeoPoint-first read-flow (`IncidentMapper/Service`, `AlertEventsService`, `IncidentRepository` nearest)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Fix local 500 on operator pages (dev auto-migrate for PostgreSQL + web backoffice error handling)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Fix local 500 on operator pages (dev auto-migrate for PostgreSQL + web backoffice error handling)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-12
- Контекст: Fix API startup crash `PendingModelChangesWarning` (migration parity `GeoModelParity`)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-12
- Контекст: Fix API startup crash `PendingModelChangesWarning` (migration parity `GeoModelParity`)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-13
- Контекст: Web mojibake fix (UTF-8 text restore for Wave 2/3 pages)
- Команда: `dotnet build Chop2G.sln -m:1`
- Результат: успешно

- 2026-02-13
- Контекст: Web mojibake fix (UTF-8 text restore for Wave 2/3 pages)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-13
- Контекст: Web auth persistence across refresh (cookie-backed session restore)
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-13
- Контекст: Backoffice 401 recovery (auto refresh + retry) + HR Guards crash-safe error handling
- Команда: `dotnet test Chop2G.sln -m:1`
- Результат: успешно (`Chop.Api.Tests 58/58`, `Chop.Application.Tests 10/10`)

- 2026-02-24
- Контекст: Task-020/021 (RBAC freeze docs + SuperAdmin Identity API tests)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 63/63`, `Chop.Application.Tests 10/10`)

- 2026-02-24
- Контекст: Task-021 Web `/superadmin/users` (BackofficeApiClient + Nav + page)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 63/63`, `Chop.Application.Tests 10/10`)

- 2026-02-24
- Контекст: Task-021 UX hardening `/superadmin/users` (confirm flow for role removal and toggle-active)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 63/63`, `Chop.Application.Tests 10/10`)

- 2026-02-24
- Контекст: Task-021 UX update `/superadmin/users` (toast notifications + UTF-8 page rewrite)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 63/63`, `Chop.Application.Tests 10/10`)

- 2026-02-24
- Контекст: Task-022/029 (guard groups + guard shifts + security points + HR pages)
- Команда: `dotnet test Chop2G.sln`
- Результат: успешно (`Chop.Api.Tests 65/65`, `Chop.Application.Tests 10/10`)
