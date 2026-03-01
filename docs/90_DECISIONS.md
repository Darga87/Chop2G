# Decisions Log (реестр проектных решений)

Формат записи:
- Дата
- Решение
- Почему
- Последствия (что учитывать в коде)

---

## 2026-02-08 — Мобильное приложение одно (CLIENT + GUARD)
Решение:
- Используем один MAUI-клиент для ролей CLIENT и GUARD.
- Ролевой UX разделяется по токену/роли после входа.

Почему:
- Меньше дублирования кода и инфраструктуры.

Последствия:
- Проверки прав должны быть строгими и на API, и в UI.

---

## 2026-02-08 — Auth
Решение:
- JWT access token + refresh token.
- Роли берутся только из claims валидированного JWT.
- Header-auth и любые role headers запрещены.

Почему:
- Закрытие CRITICAL-риска подмены роли.

Последствия:
- Все endpoint-ы защищаются `[Authorize]`/`[Authorize(Roles=...)]`.

---

## 2026-02-08 — Realtime
Решение:
- SignalR используется для событий инцидентов и диспетчеризации.
- Маршрутизация через группы `ops:*` и role-группы.

Почему:
- Операторский контур требует near-real-time обновления.

Последствия:
- Realtime события должны быть идемпотентны на уровне UI.

---

## 2026-02-09 — Дедупликация SOS
Решение:
- Поддержка `Idempotency-Key` для `POST /api/incidents`.
- Дополнительно server-side dedup окно по `clientId` (MVP: 60 сек).

Почему:
- Защита от дублей при ретраях/плохой сети.

Последствия:
- Нужны тесты на повтор с тем же ключом и на dedup без ключа.

---

## 2026-02-09 — SignalR (Task-005)
Решение:
- В MVP публикуются `IncidentCreated`, `IncidentStatusChanged`, `DispatchCreated`, `DispatchAccepted`, `GuardLocationUpdated`.

Почему:
- Это минимально достаточный набор для операторского дашборда.

Последствия:
- Любое новое событие добавляется через контракт в `Chop.Shared` + тесты.

---

## 2026-02-08 — Геоданные
Решение:
- Базовый формат гео: `lat/lon` в DTO + подготовка к PostGIS.

Почему:
- Нужны карта, nearest-логика и расширяемость.

Последствия:
- Миграции и индексы пространственных полей обязательны в roadmap.

---

## 2026-02-08 — Импорт банка
Решение:
- Формат импорта только `1CClientBankExchange`.
- Парсим только поля, которые описаны в `docs/07_PAYMENTS_1C.md`.

Почему:
- Исключить выдуманные поля/эвристики.

Последствия:
- Любая новая эвристика должна быть описана в документации и покрыта тестами.

---

## TODO (заполнить по ходу)
- 2FA для админских ролей (когда включаем).
- Push-уведомления (провайдер и политика ретраев).
- Финальные SLA-таймауты для production.

## 2026-02-09 - Identity User Store (Task-007 Phase A)
Решение:
- Login работает через user-store в БД.
- Добавлены таблицы identity: `users`, `user_credentials`, `user_roles`, `invitations`, `password_resets`.
- Пароли хранятся как `PBKDF2-SHA256` hash (+ salt/iterations).

Почему:
- Переход от dev-auth к production-пригодной схеме.

Последствия:
- Развивать invite/reset flow и password policy.

## 2026-02-09 - Dispatch Persistence (Task-007 Phase B)
Решение:
- Добавлены `dispatches`, `dispatch_recipients`, `incident_assignments`.
- Реализован `POST /api/operator/incidents/{id}/dispatch` с переходом в `DISPATCHED`.
- `POST /api/guard/incidents/{id}/accept` валидирует assignment.

Почему:
- Data-driven lifecycle диспетчеризации.

Последствия:
- Нужны realtime события и аудит переходов.

## 2026-02-09 - Alerts & Notifications Outbox (Task-007 Phase C)
Решение:
- Введены `alert_rules`, `alert_events`, `notification_outbox`, `notification_deliveries`.
- Фоновый dispatcher с retry/backoff.

Почему:
- Надежная доставка уведомлений.

Последствия:
- Для production нужен retention и четкая политика retries.

## 2026-02-10 - SignalR Publish Unification (Task-005/Task-007)
Решение:
- Realtime публикация идет через `outbox_messages` -> processor -> publisher.
- Прямые отправки SignalR из HTTP-пайплайна убраны.

Почему:
- Избежать double-send и race-эффектов.

Последствия:
- UI должен корректно обрабатывать задержку доставки.

## 2026-02-10 - Alerts SLA Worker (Task-010)
Решение:
- Добавлен SLA worker для time-based alert-правил.
- Базовые правила: нет accept в SLA-окно, guard offline, stuck statuses.

Почему:
- Раннее выявление операционных проблем.

Последствия:
- SLA параметры вынесены в конфиг и требуют калибровки.

## 2026-02-09 - Platform Reliability (Task-007 Phase D)
Решение:
- Введены health checks и базовые guardrails платформы.

Почему:
- Стабильность и наблюдаемость в dev/prod.

Последствия:
- Нужно расширять метрики и алертинг.

## 2026-02-09 - Platform D Hardening (retry/backoff/health)
Решение:
- Унифицированы retry/backoff паттерны для фоновых воркеров.

Почему:
- Снижение потерь событий при временных сбоях.

Последствия:
- Необходим контроль «ядовитых» сообщений (poison queue pattern).

## 2026-02-11 - Alerts Operator Workflow MVP (assign/override)
Решение:
- Добавлены действия по алертам: assign/ack/override/resolve.

Почему:
- Операторам нужен управляемый triage-процесс.

Последствия:
- В payload alert_event хранится audit-контекст действия.

## 2026-02-11 - Point Conflict Alert (Task-010 taxonomy)
Решение:
- Правило `INCIDENT_POINT_CONFLICT` при конфликте HOME/ALERT геоточек.

Почему:
- Сигнал о потенциальной ошибке данных до dispatch.

Последствия:
- Rule taxonomy расширяется строго документированно.

## 2026-02-11 - Alerts Navigation UX (Task-010)
Решение:
- Добавлены навигационные действия из alert в нужный контекст dashboard.

Почему:
- Сокращение времени реакции оператора.

Последствия:
- Маппинг rule->target поддерживать централизованно.

## 2026-02-12 - Audit Closure Priority (Task-012/013/014)
Решение:
- Приоритизация: security critical -> contract parity -> platform upgrades.

Почему:
- Сначала закрывать высокорисковые зоны.

Последствия:
- Обновления прогресса ведутся в `docs/91_PROGRESS_TRACKER.md`.

## 2026-02-12 - Task-013 Security Closure (guard ping + JWT + refresh reuse)
Решение:
- `guard/location/ping` валидирует assignment.
- Усилены требования к JWT ключам вне Development.
- Refresh token reuse-detection добавлен.

Почему:
- Снижение рисков hijack/replay.

Последствия:
- Требуется жесткая секрет-стратегия в production.

## 2026-02-12 - Task-012 Web Backoffice API Integration (Wave 2/3)
Решение:
- Web подключен к backend endpoint-ам вместо in-memory store.

Почему:
- Единый источник данных и реальный RBAC.

Последствия:
- Ошибки API должны маппиться в user-friendly UX.

## 2026-02-12 - Task-014 Geo groundwork (NTS enabled)
Решение:
- Подключен NetTopologySuite для PostgreSQL provider.

Почему:
- Подготовка к PostGIS geography/nearest.

Последствия:
- Миграции геополей и индексов обязательны.

## 2026-02-12 - Task-014 PostGIS migration (GeoPoint + GiST)
Решение:
- Добавлены `GeoPoint` (`geography(point,4326)`) и GiST индексы.
- Сделан backfill из существующих lat/lon.

Почему:
- Производительные spatial-запросы.

Последствия:
- В локальной среде должен быть установлен PostGIS.

## 2026-02-12 - Task-014 nearest endpoint (PostGIS + test fallback)
Решение:
- `GET /api/operator/incidents/{id}/nearest` использует PostGIS.
- Для SQLite тестов — fallback на haversine.

Почему:
- Стабильные тесты без обязательного PostGIS в CI-среде тестов.

Последствия:
- Поведение nearest в PostgreSQL и SQLite может отличаться по точности.

## 2026-02-24 - Task-020/021 RBAC freeze + SuperAdmin identity management
Решение:
- Зафиксирована матрица ролей для backoffice.
- Управление backoffice-пользователями вынесено в `SUPERADMIN`.
- Guardrails:
- нельзя снять последнюю роль пользователя;
- нельзя снять у себя `SUPERADMIN`;
- нельзя убрать последнего `SUPERADMIN` в системе;
- нельзя деактивировать себя.

Почему:
- Безопасная эксплуатация identity-контура.

Последствия:
- Все операции логируются в аудит (`identity.user.*`).

## DEC-026: 1C parser duplicate-key policy (Payments)
- Date: 2026-02-25
- Context: в `1CClientBankExchange` встречаются дубли ключей внутри документа.
- Decision:
  - Для служебных/не-критичных полей дубли пишем в `extra`.
  - Для критичных полей (`doc_no`, `amount`, `purpose` и т.д.) используем последнее валидное значение.
- Rationale:
  - Не теряем полезные данные.
  - Сохраняем предсказуемое поведение парсинга.

## DEC-027: Security points address normalization + optional geocoding
- Date: 2026-02-27
- Context: `operator/points` должны хранить чистые адреса для карты и dispatch.
- Decision:
  - На `POST/PUT /api/operator/points*` нормализуем `address` (trim, whitespace, пунктуация).
  - Если `latitude/longitude` не заданы и включен geocoding, пробуем заполнить координаты через Yandex Geocoder.
  - Если geocoding не удался, сохраняем нормализованный адрес без координат (backward-compatible).
- Rationale:
  - Улучшаем качество данных без ломки DTO/контрактов.
  - Снижаем долю ручных исправлений на карте.

## DEC-031: Targeted PII override for manager contour
- Date: 2026-02-28
- Context: часть менеджеров должна видеть полные контакты клиентов, но базово `MANAGER` работает в masked-режиме.
- Decision:
  - Базовое поведение сохраняется: для `MANAGER` в `/api/admin/clients*` телефоны маскируются, email скрывается.
  - Добавлен per-user флаг `users.CanViewClientPii` (default `false`).
  - `SUPERADMIN` управляет флагом через `POST /api/superadmin/users/{id}/toggle-client-pii`.
- Rationale:
  - Даем точечный доступ без расширения роли и без ослабления глобального RBAC.
  - Сохраняем принцип минимально необходимого доступа по умолчанию.

## DEC-032: Tailwind MVP finalization (Theme Pack + Safelist policy)
- Date: 2026-03-01
- Context: в Task-008 нужно зафиксировать финальную тему и завершить hardening Tailwind-конфига.
- Decision:
  - Финальная тема MVP: `Theme Pack B` (`theme-b`) как default в приложении.
  - `theme-a` и `theme-c` остаются как альтернативные профили без смены токенов по умолчанию.
  - `safelist` в `tailwind.config.cjs` зафиксирован как пустой (`[]`) после аудита динамических классов.
  - Любое будущее добавление в `safelist` — только с документированной причиной в `docs/93_TAILWIND_PLAYBOOK.md`.
- Rationale:
  - Стабилизировать визуальную базу UI для MVP.
  - Избежать бесконтрольного роста CSS и скрытых зависимостей от runtime-генерации классов.
