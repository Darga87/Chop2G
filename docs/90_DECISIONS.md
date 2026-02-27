# Decisions Log (СЂРµС€РµРЅРёСЏ РїСЂРѕРµРєС‚Р°)

Р¤РѕСЂРјР°С‚ Р·Р°РїРёСЃРё:
- Р”Р°С‚Р°
- Р РµС€РµРЅРёРµ
- РџРѕС‡РµРјСѓ
- РђР»СЊС‚РµСЂРЅР°С‚РёРІС‹
- РџРѕСЃР»РµРґСЃС‚РІРёСЏ (С‡С‚Рѕ СѓС‡РёС‚С‹РІР°С‚СЊ РІ РєРѕРґРµ)

---

## 2026-02-08 вЂ” РњРѕР±РёР»СЊРЅРѕРµ РїСЂРёР»РѕР¶РµРЅРёРµ РѕРґРЅРѕ (CLIENT + GUARD)
Р РµС€РµРЅРёРµ: 1 MAUI app, СЂРѕР»СЊ РѕРїСЂРµРґРµР»СЏРµС‚ СЂР°Р·РґРµР»С‹ UI.
РџРѕС‡РµРјСѓ: РїСЂРѕС‰Рµ РґРµРїР»РѕР№ Рё РїРѕРґРґРµСЂР¶РєР°, РµРґРёРЅС‹Р№ РєРѕРґ РіРµРѕР»РѕРєР°С†РёРё/СЃРµС‚Рё.
РђР»СЊС‚РµСЂРЅР°С‚РёРІС‹: 2 РѕС‚РґРµР»СЊРЅС‹С… РїСЂРёР»РѕР¶РµРЅРёСЏ.
РџРѕСЃР»РµРґСЃС‚РІРёСЏ: СЃС‚СЂРѕРіР°СЏ РїСЂРѕРІРµСЂРєР° СЂРѕР»РµР№ РЅР° API + СЃРєСЂС‹С‚РёРµ UI РїРѕ СЂРѕР»СЏРј.

---

## 2026-02-08 вЂ” Auth
Р РµС€РµРЅРёРµ:
- API: JWT Bearer access token + refresh token.
- Refresh token С…СЂР°РЅРёС‚СЃСЏ РІ Р‘Р” С‚РѕР»СЊРєРѕ РІ hash РІРёРґРµ, СЂРѕС‚Р°С†РёСЏ РЅР° РєР°Р¶РґРѕРј /auth/refresh.
- Р РѕР»Рё РґР»СЏ [Authorize(Roles=...)] Р±РµСЂСѓС‚СЃСЏ С‚РѕР»СЊРєРѕ РёР· JWT claims РїРѕСЃР»Рµ РІР°Р»РёРґР°С†РёРё РїРѕРґРїРёСЃРё.
- Р›СЋР±С‹Рµ client-driven role headers Р·Р°РїСЂРµС‰РµРЅС‹.
- Dev login РґРѕСЃС‚СѓРїРµРЅ С‚РѕР»СЊРєРѕ РІ Development Рё РѕС‚РєР»СЋС‡С‘РЅ РІ Production.
РџРѕС‡РµРјСѓ: Р·Р°РєСЂС‹РІР°РµС‚ СЂРёСЃРє СЌСЃРєР°Р»Р°С†РёРё СЂРѕР»РµР№ С‡РµСЂРµР· РїРѕРґРґРµР»РєСѓ Р·Р°РіРѕР»РѕРІРєРѕРІ, СЃРѕС…СЂР°РЅСЏРµС‚ Р±РµР·РѕРїР°СЃРЅС‹Р№ mobile flow.
РџРѕСЃР»РµРґСЃС‚РІРёСЏ: РґРѕР±Р°РІР»РµРЅ lifecycle refresh-С‚РѕРєРµРЅРѕРІ (issue/rotate/revoke), РѕР±СЏР·Р°С‚РµР»СЊРЅР° РІР°Р»РёРґР°С†РёСЏ JWT (iss/aud/signature/lifetime).

---

## 2026-02-08 вЂ” Realtime
Р РµС€РµРЅРёРµ: SignalR РґР»СЏ РёРЅС†РёРґРµРЅС‚РѕРІ Рё СЃС‚Р°С‚СѓСЃРѕРІ.
РџРѕС‡РµРјСѓ: РѕРїРµСЂР°С‚РѕСЂСѓ РЅСѓР¶РЅР° РјРіРЅРѕРІРµРЅРЅР°СЏ РґРѕСЃС‚Р°РІРєР°.
РџРѕСЃР»РµРґСЃС‚РІРёСЏ: reconnect/РїРѕРІС‚РѕСЂ СЃРѕР±С‹С‚РёР№/РёРґРµРјРїРѕС‚РµРЅС‚РЅРѕСЃС‚СЊ.

---

## 2026-02-09 вЂ” Р”РµРґСѓРїР»РёРєР°С†РёСЏ SOS
Р РµС€РµРЅРёРµ:
- `POST /incidents` РїРѕРґРґРµСЂР¶РёРІР°РµС‚ `Idempotency-Key` (РїРѕ `clientId + key` РїРѕРІС‚РѕСЂ РІРѕР·РІСЂР°С‰Р°РµС‚ С‚РѕС‚ Р¶Рµ `incidentId`).
- Р”РѕРїРѕР»РЅРёС‚РµР»СЊРЅРѕ РІРєР»СЋС‡РµРЅРѕ server-side dedup РѕРєРЅРѕ 60 СЃРµРєСѓРЅРґ РїРѕ Р°РєС‚РёРІРЅС‹Рј СЃС‚Р°С‚СѓСЃР°Рј (`NEW/ACKED/DISPATCHED/ACCEPTED/EN_ROUTE/ON_SCENE`).
- Р”Р»СЏ `Idempotency-Key` РґРѕР±Р°РІР»РµРЅ `request-hash`: РїРѕРІС‚РѕСЂ СЃ С‚РµРј Р¶Рµ РєР»СЋС‡РѕРј Рё РґСЂСѓРіРёРј payload РІРѕР·РІСЂР°С‰Р°РµС‚ `409 Conflict`.
- Р”Р»СЏ idempotency-Р·Р°РїРёСЃРµР№ РІРєР»СЋС‡С‘РЅ TTL 24 С‡Р°СЃР° + С„РѕРЅРѕРІР°СЏ РѕС‡РёСЃС‚РєР°.
- РЎРѕР·РґР°РЅРёРµ РёРЅС†РёРґРµРЅС‚Р° РІС‹РїРѕР»РЅСЏРµС‚СЃСЏ РІ `SERIALIZABLE` С‚СЂР°РЅР·Р°РєС†РёРё СЃ retry, С‡С‚РѕР±С‹ СЃРЅРёР·РёС‚СЊ СЂРёСЃРє РґСѓР±Р»РµР№ Р±РµР· `Idempotency-Key` РїСЂРё РїР°СЂР°Р»Р»РµР»СЊРЅС‹С… Р·Р°РїСЂРѕСЃР°С… СЃ РЅРµСЃРєРѕР»СЊРєРёС… СѓСЃС‚СЂРѕР№СЃС‚РІ РѕРґРЅРѕРіРѕ РєР»РёРµРЅС‚Р°.
РџРѕС‡РµРјСѓ: РёР·Р±РµР¶Р°С‚СЊ РґСѓР±Р»РµР№ SOS РїСЂРё СЂРµС‚СЂР°СЏС…/РїР»РѕС…РѕР№ СЃРµС‚Рё Рё РїРѕРІС‚РѕСЂРЅС‹С… С‚Р°РїР°С….
РџРѕСЃР»РµРґСЃС‚РІРёСЏ:
- РґРѕР±Р°РІР»РµРЅРѕ С…СЂР°РЅРёР»РёС‰Рµ `incident_idempotency`;
- РїРѕРІС‚РѕСЂРЅС‹Р№ Р·Р°РїСЂРѕСЃ РјРѕР¶РµС‚ РІРµСЂРЅСѓС‚СЊ СЂР°РЅРµРµ СЃРѕР·РґР°РЅРЅС‹Р№ РёРЅС†РёРґРµРЅС‚ РІРјРµСЃС‚Рѕ СЃРѕР·РґР°РЅРёСЏ РЅРѕРІРѕРіРѕ.
- СЃРЅРёР¶РµРЅС‹ СЂРёСЃРєРё РєРѕР»Р»РёР·РёР№ РєР»СЋС‡Р° Рё РЅР°РєРѕРїР»РµРЅРёСЏ idempotency-РґР°РЅРЅС‹С….
- РґРѕР±Р°РІР»РµРЅР° Р·Р°С‰РёС‚Р° РѕС‚ race condition РїРѕ `clientId` РґР»СЏ Р·Р°РїСЂРѕСЃРѕРІ Р±РµР· РєР»СЋС‡Р° (С‡РµСЂРµР· СѓСЂРѕРІРµРЅСЊ РёР·РѕР»СЏС†РёРё Р‘Р” Рё РїРѕРІС‚РѕСЂ С‚СЂР°РЅР·Р°РєС†РёРё).

---

## 2026-02-09 вЂ” SignalR (Task-005)
Р РµС€РµРЅРёРµ:
- Hub `/hubs/incidents` СЃ JWT auth РґР»СЏ СЂРѕР»РµР№ `OPERATOR/ADMIN/SUPERADMIN`.
- РџСѓР±Р»РёРєР°С†РёСЏ СЃРѕР±С‹С‚РёР№ РІ РіСЂСѓРїРїСѓ `ops:*` (Рё role-РіСЂСѓРїРїС‹ РґР»СЏ РґР°Р»СЊРЅРµР№С€РµР№ РјР°СЂС€СЂСѓС‚РёР·Р°С†РёРё).
- Р’ MVP РїСѓР±Р»РёРєСѓРµРј realtime РЅР°РїСЂСЏРјСѓСЋ РїРѕСЃР»Рµ СѓСЃРїРµС€РЅРѕРіРѕ HTTP workflow (Р±РµР· outbox).
РџРѕС‡РµРјСѓ: Р±С‹СЃС‚СЂРѕ Р·Р°РїСѓСЃС‚РёС‚СЊ realtime РґР»СЏ РїСѓР»СЊС‚Р°, РЅРµ Р±Р»РѕРєРёСЂСѓСЏ РґРѕСЃС‚Р°РІРєСѓ Р±Р°Р·РѕРІС‹С… incident-СЃС†РµРЅР°СЂРёРµРІ.
РџРѕСЃР»РµРґСЃС‚РІРёСЏ:
- РІРѕР·РјРѕР¶РЅР° РїРѕС‚РµСЂСЏ СЃРѕР±С‹С‚РёСЏ РїСЂРё СЃР±РѕРµ РјРµР¶РґСѓ commit Рё publish;
- РѕС‚РґРµР»СЊРЅС‹Рј С€Р°РіРѕРј РЅСѓР¶РµРЅ transactional outbox + retry/worker.

---

## 2026-02-08 вЂ” Р“РµРѕРґР°РЅРЅС‹Рµ
Р РµС€РµРЅРёРµ: PostgreSQL + PostGIS, С‚РѕС‡РєРё РєР°Рє geography(Point,4326), GiST РёРЅРґРµРєСЃ.
РџРѕС‡РµРјСѓ: Р±С‹СЃС‚СЂС‹Р№ Рё РєРѕСЂСЂРµРєС‚РЅС‹Р№ вЂњnearestвЂќ.
РџРѕСЃР»РµРґСЃС‚РІРёСЏ: РјРёРіСЂР°С†РёРё + spatial С‚РёРїС‹ РІ EF Core.

---

## 2026-02-08 вЂ” РРјРїРѕСЂС‚ Р±Р°РЅРєР°
Р РµС€РµРЅРёРµ: РїРѕРґРґРµСЂР¶РёРІР°РµРј TXT `1CClientBankExchange` С‚РѕР»РµСЂР°РЅС‚РЅС‹Рј РїР°СЂСЃРµСЂРѕРј.
РџРѕС‡РµРјСѓ: Р±Р°РЅРєРё РѕС‚Р»РёС‡Р°СЋС‚СЃСЏ, РЅСѓР¶РµРЅ СЃС‚Р°Р±РёР»СЊРЅС‹Р№ РёРјРїРѕСЂС‚ Р±РµР· вЂњР¶С‘СЃС‚РєРёС…вЂќ РїРѕР»РµР№.
РџРѕСЃР»РµРґСЃС‚РІРёСЏ: extra JSONB + СЂСѓС‡РЅРѕР№ СЂР°Р·Р±РѕСЂ.

---

## TODO (Р·Р°РїРѕР»РЅРёС‚СЊ РїРѕ С…РѕРґСѓ)
- 2FA РґР»СЏ Р°РґРјРёРЅРѕРІ: РєРѕРіРґР° РІРєР»СЋС‡Р°РµРј
- Push-СѓРІРµРґРѕРјР»РµРЅРёСЏ: РєРѕРіРґР° РІРєР»СЋС‡Р°РµРј Рё С‡РµСЂРµР· С‡С‚Рѕ
- SLA Рё С‚Р°Р№РјР°СѓС‚С‹: С‚РѕС‡РЅС‹Рµ С†РёС„СЂС‹

## 2026-02-09 - Identity User Store (Task-007 Phase A)
Решение:
- `POST /api/auth/login` больше не использует `Auth:Dev:Users`, а проверяет пользователя/хеш пароля/роли из БД.
- Добавлены таблицы identity: `users`, `user_credentials`, `user_roles`, `invitations`, `password_resets`.
- Пароли хранятся в виде `PBKDF2-SHA256` hash (с солью и iterations), в токен попадают только роли из БД.
Почему: убрать конфиговые dev-учётки из runtime auth-контура и перейти к production-модели user-store.
Последствия:
- для логина требуется сид/создание пользователей в БД;
- тестовый стенд сидит пользователей через EF;
- следующий шаг: endpoints для invite/reset + password policy + rate limits.

## 2026-02-09 - Dispatch Persistence (Task-007 Phase B)
Решение:
- Добавлены таблицы `dispatches`, `dispatch_recipients`, `incident_assignments`.
- Реализован endpoint `POST /api/operator/incidents/{id}/dispatch` с переводом инцидента `ACKED/NEW -> DISPATCHED` и audit history.
- При `POST /api/guard/incidents/{id}/accept` обновляются dispatch recipient/assignment (если есть).
Почему: закрыть требование state-machine «DISPATCHED только через Dispatch workflow» и подготовить data-driven lifecycle назначений.
Последствия:
- realtime получает `DispatchCreated` + `IncidentStatusChanged` при создании dispatch;
- в тестах закреплён happy path dispatch и валидация recipients.

## 2026-02-09 - Alerts & Notifications Outbox (Task-007 Phase C)
Решение:
- Добавлены таблицы `alert_rules`, `alert_events`, `notification_outbox`, `notification_deliveries`.
- Realtime-публикации инцидентов теперь дополнительно пишут alert_event + notification_outbox запись.
- Добавлен worker `NotificationOutboxDispatcher` с retry/backoff и фиксацией delivery attempts.
Почему: подготовить надёжную доставку уведомлений и наблюдаемость инцидентных событий.
Последствия:
- текущий MVP оставляет прямую SignalR-публикацию + outbox-канал (переходный dual-path);
- следующий шаг (Phase D): унификация через общий transactional outbox и retention jobs.

## 2026-02-10 - SignalR Publish Unification (Task-005/Task-007)
Решение:
- Для realtime SignalR событий оставляем **один** путь доставки: `outbox_messages` -> `OutboxMessageProcessor` -> `IOutboxEventPublisher` (SignalR publisher).
- Убрана прямая публикация в SignalR из HTTP pipeline (нет `hubContext.SendAsync` в `IncidentRealtimePublisher`).
- `notification_outbox` больше не используется для realtime событий инцидентов (остается для будущих реальных нотификаций/каналов).
Почему: устранить double-send/дубли в UI, добиться предсказуемой доставки и единых retry/lag метрик.
Последствия:
- UI/клиенты должны быть tolerant к задержке доставки (poll interval outbox).
- Тесты SignalR опираются на outbox processor (в тестах poll interval низкий).

## 2026-02-10 - Alerts SLA Worker (Task-010)
Решение:
- Для time-based алертов добавлен фоновый worker `IncidentAlertSlaWorker`.
- Worker рассчитывает и поддерживает:
  - `INCIDENT_NO_ACCEPT_STUCK` (нет accept в SLA-окне после dispatch),
  - `INCIDENT_GUARD_OFFLINE` (нет свежих geo ping после accept).
Почему: статические алерты по факту действия не покрывают сценарии "застряли во времени", нужен периодический контроль SLA.
Последствия:
- добавлены SLA-настройки в конфиг (`Alerts:Sla:*`);
- в тестах SLA пороги занижены для быстрого и детерминированного прогона.

## 2026-02-09 - Platform Reliability (Task-007 Phase D)
Решение:
- Добавлены `outbox_messages` и `audit_log`.
- Реализован фоновый `OutboxMessageProcessor` (PENDING -> PUBLISHED/FAILED, retries).
- Реализован `PlatformRetentionCleanupService` для retention outbox/audit.
- Критичные API-операции (auth/incidents) пишут audit entries.
Почему: повысить трассируемость и операционную устойчивость, снизить риск silent-fail по событиям.
Последствия:
- появился отдельный reliability-контур (outbox + audit + cleanup);
- следующий шаг production-hardening: брокерная публикация в OutboxProcessor и расширенные метрики/alerting.

## 2026-02-09 - Platform D Hardening (retry/backoff/health)
Решение:
- Для `outbox_messages` добавлены `next_attempt_at` и `last_error`.
- `OutboxMessageProcessor` переведён на publisher abstraction (`IOutboxEventPublisher`) и exponential backoff с jitter.
- Добавлен health-check `outbox_lag` для раннего обнаружения зависшей очереди.
Почему: для production нужен контролируемый retry/fail-path и видимость деградации по доставке событий.
Последствия:
- unsupported `event_type` больше не теряется silently, а уходит в retries и затем в `FAILED` с ошибкой;
- можно подключать реальные внешние брокеры/каналы без переписывания процессора.

## 2026-02-11 - Alerts Operator Workflow MVP (assign/override)
Решение:
- Добавлены endpoints:
  - `POST /api/operator/alerts/{id}/assign`
  - `POST /api/operator/alerts/{id}/override`
- Assignment/override пока хранятся в `alert_events.payload_json` (без отдельной миграции схемы).
- `AlertListItemDto` расширен полями `assigneeUserId`, `assignedAtUtc` для web workflow.
Почему: закрыть операторский контур `acknowledge/assign/override/resolve` в MVP и не тормозить релиз новой миграцией.
Последствия:
- Web Dashboard теперь поддерживает "Assign to me" и "Override" прямо из панели alerts;
- при следующем этапе можно вынести assignment в отдельные поля/таблицу для аналитики и индексов.

## 2026-02-11 - Point Conflict Alert (Task-010 taxonomy)
Решение:
- Добавлен rule `INCIDENT_POINT_CONFLICT` для инцидента, если обе точки `ALERT` и `HOME` имеют geo и расстояние между ними превышает 1000 м.
- Rule создаётся в `EnsureGeoAlertsForIncidentAsync` и отображается в operator dashboard map как отдельный marker.
Почему: оператору нужен явный сигнал о потенциальной ошибке геопозиции/адреса до dispatch decisions.
Последствия:
- taxonomy point alerts закрывает базовый MVP (`NO_GEO` + `POINT_CONFLICT`);
- порог 1000 м зафиксирован как MVP-значение, дальше может быть вынесен в конфиг по региону.

## 2026-02-11 - Alerts Navigation UX (Task-010)
Решение:
- В operator dashboard добавлены rule badges в map overlay (группировка open alerts по rule code).
- Из каждого alert item и badge можно перейти к связанному объекту: секция (`map`/`dispatch`/`alerts`) + фокус соответствующего marker.
Почему: оператору нужен быстрый "jump-to-context" при работе с несколькими типами алертов без ручного поиска по экрану.
Последствия:
- ускорен triage алертов в веб-пульте;
- mapping rule->target зафиксирован централизованно в UI, можно расширять без изменения API.

## 2026-02-12 - Audit Closure Priority (Task-012/013/014)
Решение:
- После аудита фиксируем единый порядок реализации: сначала security critical, затем contract parity, затем platform upgrades.
- Приоритетный поток:
  1) `Task-013 Security Hardening`: guard authorization gap + JWT non-dev fail-fast + refresh reuse detection.
  2) `Task-012 Contract Parity`: закрытие несоответствий `docs/03_API.md` и фактического API, Web переход на real backend.
  3) `Task-014 Geo Platform Upgrade`: PostGIS migration + spatial indexes + nearest-ready model.
- Управление прогрессом ведём через `docs/91_PROGRESS_TRACKER.md` (Now/Next/Risks), а тестовые прогоны через `docs/92_TEST_LOG.md`.
Почему: в проекте одновременно открыты security, contract и data-platform gaps; без фиксированного порядка высокий риск распыления.
Последствия:
- новые задачи оформляются через `docs/99_TASK_TEMPLATE.md` с явными dependency и DoD;
- при планировании сначала закрываем CRITICAL/HIGH, потом расширяем функциональность.

## 2026-02-12 - Task-013 Security Closure (guard ping + JWT + refresh reuse)
Решение:
- `POST /api/guard/location/ping` теперь принимает `incidentId` только от guard, назначенного на инцидент (`incident_assignments`).
- В non-Development API стартует только с безопасным JWT ключом (fail-fast на default/dev/weak key).
- Refresh token ротация дополнена reuse-detection: повторное использование уже ротированного токена отзывает активную цепочку refresh-токенов пользователя.
Почему: закрыть критичные риски эскалации и сессионного hijack/replay.
Последствия:
- старые тесты realtime с ping обновлены под обязательный dispatch/assignment;
- для production обязателен явный secret key (без fallback на dev placeholders).

## 2026-02-12 - Task-012 Web Backoffice API Integration (Wave 2/3)
Решение:
- Wave 2/3 страницы Web переведены с `BackofficeConsoleStore` на `BackofficeApiClient`.
- Добавлены backend endpoints для HR/Admin/Operator/SuperAdmin и payments workflow (`/api/admin/payments/import*`).
Почему: убрать in-memory данные из Web и перейти на единый серверный источник данных.
Последствия:
- RBAC проверяется на API уровне для backoffice endpoints;
- payments workflow пока MVP, дальнейшая углублённая реализация по `docs/07_PAYMENTS_1C.md`.

## 2026-02-12 - Task-014 Geo groundwork (NTS enabled)
Решение:
- В Infrastructure подключён `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`.
- Postgres provider включает `UseNetTopologySuite()`.
Почему: подготовить платформу к миграции на PostGIS geography/nearest queries без массового одномоментного рефакторинга.
Последствия:
- следующий шаг Task-014: миграция lat/lon -> geography(Point,4326) и spatial индексы.

## 2026-02-12 - Task-014 PostGIS migration (GeoPoint + GiST)
Решение:
- Добавлена миграция `PostGisGeographyUpgrade`.
- В БД добавлены колонки `GeoPoint` (`geography(point,4326)`) в `incidents`, `client_addresses`, `guard_locations`.
- Выполнен backfill `GeoPoint` из исторических `Latitude/Longitude`.
- Добавлены GiST индексы `IX_*_GeoPoint` для spatial операций.
Почему: перейти от подготовки к реальной spatial-готовности БД без поломки текущих API/DTO.
Последствия:
- текущая прикладная логика пока продолжает использовать lat/lon, но данные/индексы для PostGIS уже готовы;
- следующий шаг: перевести nearest и гео-бизнес-логику на `GeoPoint`.

## 2026-02-12 - Task-014 nearest endpoint (PostGIS + test fallback)
Решение:
- Реализован endpoint `GET /api/operator/incidents/{id}/nearest`.
- Для PostgreSQL используется `ST_Distance` c `GeoPoint` (и `COALESCE` на lat/lon->geography для обратной совместимости данных).
- Для тестового SQLite добавлен fallback на haversine-расчёт в репозитории.
Почему: дать рабочий nearest API без ломки существующего test-контурa на SQLite.
Последствия:
- прод контур использует spatial расстояния PostGIS;
- тесты остаются детерминированными без поднятия PostGIS в CI.

## 2026-02-24 - Task-020/021 RBAC freeze + SuperAdmin identity management
Decision:
- Freeze backoffice RBAC matrix for roles: `OPERATOR`, `HR`, `MANAGER`, `ACCOUNTANT`, `ADMIN`, `SUPERADMIN`.
- Implement identity admin APIs under `/api/superadmin/users*` for create/list/role-add/role-remove/toggle-active.
- Restrict identity lifecycle operations to `SUPERADMIN` only.
Why:
- Prevent role-scope drift in Web Console.
- Centralize privileged account lifecycle and keep auditability.
Consequences:
- UI navigation and feature visibility must be enforced by role and backed by API checks.
- Security guardrails are enforced server-side:
- cannot remove last role from user.
- cannot remove own `SUPERADMIN`.
- cannot remove last `SUPERADMIN`.
- cannot deactivate self.
- Audit log entries added for `identity.user.create`, `identity.user.role.add`, `identity.user.role.remove`, `identity.user.toggle-active`.
## DEC-026: 1C parser duplicate-key policy (Payments)
- Date: 2026-02-25
- Context: `1CClientBankExchange` может содержать дубли ключей внутри одной `СекцияДокумент`.
- Decision:
  - В `extra` сохраняем все значения дублирующегося ключа как массив в порядке появления.
  - Для нормализованных полей (`doc_no`, `amount`, `purpose` и т.д.) используем последнее встретившееся значение ключа.
- Rationale:
  - Не теряем данные источника.
  - Поведение детерминировано и просто для тестирования/объяснения.
