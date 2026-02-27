# DB Roadmap (Task-007)

Цель: поэтапно перейти от MVP-схемы к production-модели БД без поломки текущих API.

## Принципы
- Все даты/время: UTC (`timestamptz`).
- Явные constraints для статусных переходов/обязательных связей.
- Идемпотентность и аудит на критичных операциях.
- Индексы под реальные фильтры (`status`, `created_at`, `incident_id`, `client_id`, `user_id`).
- Retention/cleanup для больших потоков (ping/alerts/outbox).

## Текущее состояние (уже есть)
- `incidents`
- `incident_status_history`
- `incident_idempotency`
- `refresh_tokens`
- `client_profiles`, `client_phones`, `client_addresses`

## Phase A — Identity/User Store
Задача: убрать зависимость от `Auth:Dev:Users`, перенести учетные записи и роли в БД.

### Таблицы
- `users`
  - `id uuid pk`
  - `login text unique not null`
  - `email text unique null`
  - `phone text unique null`
  - `is_active bool not null default true`
  - `created_at timestamptz not null`
- `user_credentials`
  - `user_id uuid pk fk users(id)`
  - `password_hash text not null`
  - `password_algo text not null`
  - `password_changed_at timestamptz not null`
- `user_roles`
  - `user_id uuid fk users(id)`
  - `role text not null` (`CLIENT|GUARD|OPERATOR|HR|ADMIN|SUPERADMIN`)
  - `pk (user_id, role)`
- `invitations`
  - `id uuid pk`
  - `user_id uuid fk users(id)`
  - `token_hash text unique not null`
  - `expires_at timestamptz not null`
  - `used_at timestamptz null`
  - `created_at timestamptz not null`
- `password_resets`
  - `id uuid pk`
  - `user_id uuid fk users(id)`
  - `token_hash text unique not null`
  - `expires_at timestamptz not null`
  - `used_at timestamptz null`
  - `created_at timestamptz not null`

### Изменения приложения
- `POST /api/auth/login` читает пользователя/роль из БД.
- Dev-login отключается в runtime-контуре.
- Пароли: только hash + policy.

## Phase B — Dispatch Persistence
Задача: закрыть lifecycle назначений и подтверждений в БД.

### Таблицы
- `dispatches`
  - `id uuid pk`
  - `incident_id uuid fk incidents(id)`
  - `created_by_user_id uuid fk users(id)`
  - `method text not null` (`RADIO|PHONE|APP|MIXED`)
  - `comment text null`
  - `created_at timestamptz not null`
- `dispatch_recipients`
  - `id uuid pk`
  - `dispatch_id uuid fk dispatches(id)`
  - `recipient_type text not null` (`POST|PATROL_UNIT|GUARD`)
  - `recipient_id uuid not null`
  - `distance_meters int null`
  - `status text not null` (`SENT|ACCEPTED|DECLINED`)
  - `accepted_by text null`
  - `accepted_at timestamptz null`
  - `accepted_via text null` (`RADIO|PHONE|APP`)
- `incident_assignments`
  - `id uuid pk`
  - `incident_id uuid fk incidents(id)`
  - `guard_user_id uuid fk users(id) null`
  - `patrol_unit_id uuid null`
  - `status text not null` (`ASSIGNED|ACCEPTED|FINISHED`)
  - `created_at timestamptz not null`

### Изменения приложения
- `POST /operator/incidents/{id}/dispatch` начинает писать dispatch/recipients.
- `DispatchAccepted` event становится полностью data-driven из БД.

## Phase C — Alerts & Notifications
Задача: системные алерты и надежная доставка уведомлений.

### Таблицы
- `alert_rules`
  - `id uuid pk`
  - `code text unique not null`
  - `is_enabled bool not null`
  - `settings jsonb not null`
  - `created_at timestamptz not null`
- `alert_events`
  - `id uuid pk`
  - `rule_code text not null`
  - `severity text not null` (`INFO|WARN|CRITICAL`)
  - `entity_type text not null` (`INCIDENT|GUARD|SHIFT|PAYMENT`)
  - `entity_id uuid null`
  - `payload jsonb not null`
  - `created_at timestamptz not null`
- `notification_outbox`
  - `id uuid pk`
  - `channel text not null` (`SIGNALR|PUSH|SMS|EMAIL`)
  - `destination text not null`
  - `payload jsonb not null`
  - `status text not null` (`PENDING|SENT|FAILED`)
  - `attempt_count int not null default 0`
  - `next_attempt_at timestamptz null`
  - `created_at timestamptz not null`
- `notification_deliveries`
  - `id uuid pk`
  - `outbox_id uuid fk notification_outbox(id)`
  - `status text not null`
  - `provider_response text null`
  - `created_at timestamptz not null`

### Изменения приложения
- SignalR/push/email отправка через outbox worker.
- Алерты по SLA и состоянию смен.

## Phase D — Platform Reliability
Задача: надежность, трассируемость, операционная поддержка.

### Таблицы
- `outbox_messages`
  - `id uuid pk`
  - `aggregate_type text not null`
  - `aggregate_id uuid not null`
  - `event_type text not null`
  - `payload jsonb not null`
  - `status text not null` (`PENDING|PUBLISHED|FAILED`)
  - `attempt_count int not null default 0`
  - `created_at timestamptz not null`
  - `published_at timestamptz null`
- `audit_log`
  - `id uuid pk`
  - `actor_user_id uuid null`
  - `actor_role text null`
  - `action text not null`
  - `entity_type text not null`
  - `entity_id uuid null`
  - `changes jsonb null`
  - `created_at timestamptz not null`

### Операционные задачи
- Cleanup jobs (idempotency/outbox/pings/alerts).
- Retention policy для больших таблиц.
- Метрики и алерты по retry/failures.

## Порядок внедрения
1. Phase A (identity) — критический блок безопасности.
2. Phase B (dispatch) — закрыть основной операционный workflow.
3. Phase C (alerts/notifications) — контроль и доставка.
4. Phase D (reliability) — production hardening.

## Критерии готовности по фазе
- Есть миграции и rollback plan.
- Есть интеграционные тесты happy/fail path.
- Нет регрессии по текущим endpoint.
- Обновлены `docs/03_API.md`, `docs/90_DECISIONS.md`, `docs/91_PROGRESS_TRACKER.md`.
