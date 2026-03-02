# 91_PROGRESS_TRACKER

Цель: управленческий трекер реализации — что уже сделано и что предстоит сделать.

## Правила
- Фиксируем только статус реализации и ближайшие шаги.
- Без логов прогонов тестов и технического шума.
- Новые идеи и неформализованные задачи добавляем в отдельный backlog-блок.
- Последнее обновление: 2026-02-28 (завершен UTF-8 sweep по Web/docs, подтверждена сборка и полный прогон API тестов)
- Технический журнал проверок: `docs/92_TEST_LOG.md`

## Легенда
- `[x]` реализовано
- `[ ]` запланировано / в работе

## Контур контроля (единый)
### Now
- [x] A1. Закрыть CRITICAL: guard ping authorization gap (`POST /api/guard/location/ping` только для назначенного guard)
- [x] A2. Закрыть CRITICAL: JWT production hardening (fail-fast при дефолтном ключе вне Development)

### Next
- [x] A3. Contract parity с `docs/03_API.md`: закрыть отсутствующие endpoints (HR/Admin/Invitations/Payments)
- [x] A4. Убрать in-memory backoffice store и подключить реальный backend для Web Console
- [x] A5. Payments backend по `docs/07_PAYMENTS_1C.md` + обязательные parser/matching tests

### Risks/Blockers
- [x] R1. PostGIS-риск закрыт: geo-модель переведена на `GeoPoint` + spatial индексы (Task-014)
- [x] R2. Refresh token hardening закрыт: reuse detection + revoke chain реализованы (Task-013)
- [x] R3. Зафиксированы security-policies: каталог API ошибок, RBAC-матрица Web, production rate-limit/password policy

### Где и что контролируем
- `docs/91_PROGRESS_TRACKER.md`: статус реализации (Now/Next/Risks/Tasks)
- `docs/99_TASK_TEMPLATE.md`: структура постановки каждой новой задачи (DoD/зависимости/файлы/тесты)
- `docs/90_DECISIONS.md`: архитектурные и security решения
- `docs/92_TEST_LOG.md`: только результаты прогонов build/test

## Task-001: Базовый скелет решения
### Сделано
- [x] Создана solution и проекты (`Api/Web/Mobile/Domain/Application/Infrastructure/Shared` + тестовые проекты)
- [x] Настроены базовые зависимости между слоями
- [x] Добавлен `GET /health`
- [x] Создана базовая структура модулей (`Incidents/Auth/Payments`)

## Task-003: Incidents API и state machine
### Сделано
- [x] `POST /api/incidents`
- [x] `GET /api/operator/incidents`
- [x] `GET /api/operator/incidents/{id}`
- [x] Расширен `IncidentDetailsDto` (client summary, phones, addresses, history)
- [x] Реализованы endpoint-ы смены статусов:
- [x] `POST /api/operator/incidents/{id}/status`
- [x] `POST /api/guard/incidents/{id}/accept`
- [x] `POST /api/guard/incidents/{id}/progress`
- [x] Реализована идемпотентность и дедупликация SOS (`Idempotency-Key` + dedup window)
- [x] Добавлены hardening-механизмы дедупликации (request-hash, TTL cleanup, race protection)
### Осталось
- [x] Политика маскирования PII (телефоны/контакты) в operator views: `OPERATOR` получает маску контактов, `ADMIN/SUPERADMIN` — полный вид

## Task-003A/Security Critical: Auth hardening
### Сделано
- [x] Удалён небезопасный header-auth
- [x] Подключён JWT bearer
- [x] Реализованы `/api/auth/login`, `/api/auth/refresh`, `/api/auth/logout`
- [x] Refresh token хранится в hash-виде, ротация и revoke реализованы
- [x] Rate limiting (MVP): `/api/auth/*`, `/api/guard/location/ping`
### Осталось
- [x] Password policy (сложность, ротация, блокировки) — зафиксирована в `docs/04_SECURITY.md` (production baseline)
- [x] Финальный вынос секретов в production secret manager (fail-fast guard + `Secrets:KeyPerFilePath` для non-Development)

## Task-005: SignalR realtime
### Сделано
- [x] Hub `/hubs/incidents` с ролевым доступом `OPERATOR/ADMIN/SUPERADMIN`
- [x] События MVP: `IncidentCreated`, `IncidentStatusChanged`, `DispatchCreated`, `DispatchAccepted`, `GuardLocationUpdated`
- [x] Маршрутизация по группе `ops:*`
- [x] Унификация публикации: SignalR события доставляются через platform outbox (без direct-send/dual-path)
- [x] Scoped routing phase 1: добавлены incident-scopes (`scope:incident:{id}`) + Hub методы `SubscribeIncident/UnsubscribeIncident` + web-подписка страниц оператора
- [x] Payload realtime-событий расширен `scope`-метаданными (`incidentId/clientUserId/regionCode/shiftKey`)
### Осталось
- [x] Полностью перевести operator-поток на operational scopes по клиентам/регионам/сменам (с выдачей scope claims в JWT и policy mapping)

## Task-006: Web Console
### Сделано
- [x] План страниц и логики зафиксирован в `docs/05_UI_WEB.md`
- [x] Wave 1: `/login`, `/operator/incidents`, `/operator/incidents/{id}`, realtime UX
- [x] Wave 2: `/hr/guards`, `/admin/clients`, `/admin/payments/import` (MVP UI)
- [x] Wave 3: `/operator/forces`, `/operator/points`, `/superadmin/settings`, `/superadmin/audit` (MVP UI)
### Осталось
- [x] Wave 2 API-integration: подключить реальные backend endpoint-ы для HR/Clients/Payments import вместо in-memory store
- [x] Wave 3 API-integration: перевести `/operator/forces`, `/operator/points`, `/superadmin/*` с `BackofficeConsoleStore` на реальные backend endpoint-ы
- [x] RBAC Web: добавлены отдельные маршруты для `MANAGER` (`/manager/clients`) и `ACCOUNTANT` (`/accountant/payments/import`) с изоляцией от operator/admin/superadmin разделов
- [x] Operator pages hardening: `OperatorForces` и `OperatorPoints` обрабатывают backend ошибки без падения Blazor circuit (показывают error banner)
- [x] Исправлена битая кодировка (mojibake) в Wave 2/3 web-страницах: тексты восстановлены в нормальный русский UTF-8
- [x] Исправлен logout-on-refresh в Web: сессия сохраняется в cookie при login, восстанавливается в `WebAuthSession` на новом запросе, очищается при logout
- [x] Backoffice Web auth hardening: при `401` клиент делает `POST /api/auth/refresh`, повторяет запрос и обновляет cookie/session (без ручного relogin)
- [x] HR Guards page защищена от circuit-crash: ошибки API показываются banner-ом
- [x] Восстановлен русский UTF-8 в `OperatorIncidentDetails.razor` (все подписи, статусы, сообщения)
- [x] Восстановлена читаемость документации без крокозябр: `docs/04_SECURITY.md`, `docs/90_DECISIONS.md`
- [x] Восстановлен русский UTF-8 в `ManagerClients.razor` (заголовки, фильтры, таблица, billing-статусы)
- [x] Исправлена секция SignalR в `docs/03_API.md` (группы и события MVP без mojibake)
- [x] Восстановлен UTF-8 в `tests/Chop.Api.Tests/BackofficeApiTests.cs` и `tests/Chop.Api.Tests/IncidentsAuthTests.cs` (русские данные и 1C ключи без искажений)
- [x] Выполнен дополнительный UTF-8 sweep по `docs` и `src/Chop.Web/Components` — явных mojibake-строк не осталось
- [x] PII/RBAC polishing: для роли `MANAGER` в `/api/admin/clients*` включено маскирование телефонов и скрытие email (полный вид остается у `ADMIN/SUPERADMIN`)

## Task-007: Data platform roadmap
### Планирование
- [x] Целевая модель БД согласована
- [x] Детальный roadmap оформлен в `docs/08_DB_ROADMAP.md`

### Phase A (Identity/User store)
- [x] `users`, `user_credentials`, `user_roles`, `invitations`, `password_resets`
- [x] Отказ от `Auth:Dev:Users` в runtime
- [x] Созданы реальные dev-учетки в Postgres для ролей (`SUPERADMIN/ADMIN/OPERATOR/HR/GUARD/CLIENT/MANAGER/ACCOUNTANT`)

### Phase B (Dispatch persistence)
- [x] `dispatches`, `dispatch_recipients`, `incident_assignments`
- [x] `POST /api/operator/incidents/{id}/dispatch` переведён на persistence

### Phase C (Alerts/Notifications)
- [x] `alert_rules`, `alert_events`, `notification_outbox`, `notification_deliveries`
- [x] Worker доставки уведомлений + retry

### Phase D (Platform reliability)
- [x] `outbox_messages`, `audit_log`
- [x] `OutboxMessageProcessor` + retention cleanup
- [x] Audit запись критичных операций (auth/incidents)
- [x] Hardening: `NextAttemptAt`, `LastError`, exponential backoff + jitter
- [x] Hardening: health check `outbox_lag` + publisher abstraction
### Осталось по Task-007
- [x] Подключить реальный брокер/шину в publish-контур (`Realtime:Bus` + RabbitMQ publisher/consumer; fallback в прямой SignalR при disabled)
- [x] Метрики и алерты по retry/failure/lag в операционном мониторинге (`OutboxMetrics` + health checks `outbox_lag`/`outbox_failures`)

## Task-PAY: Payments
### Сделано
- [x] Реализован толерантный parser `1CClientBankExchange` (key=value, `СекцияДокумент/КонецДокумента`, UTF-8 + CP1251 fallback)
- [x] Реализована нормализация полей: `doc_type/doc_no/doc_date/amount/payer_name/payer_inn/payer_account/receiver_account/purpose`
- [x] Реализовано хранение дублей ключей в `extra` (массив значений), для нормализованных полей берется последнее значение
- [x] Реализован auto-match MVP (P1): ключи из `purpose` (`ID`, `ДОГОВОР №`, `СЧЕТ №`) -> `client_profiles.user_id`
- [x] Реализованы статусы сопоставления: `MATCHED/AMBIGUOUS/UNMATCHED/INVALID`
- [x] Добавлены unit-тесты parser (корректный, пустой, битая кодировка/разделители, дубли)
- [x] Добавлены тесты сопоставления (точное совпадение, 2 кандидата -> ambiguous/manual queue)
- [x] Переведён import с in-memory store на persistent таблицы (`bank_imports`, `bank_import_rows`, `payments`)
- [x] `Apply` создаёт реальные `payments` и помечает импорт `APPLIED`
- [x] Добавлен audit для upload/apply/manual-match/duplicate в payments-контуре
### Осталось
- [x] Task-PAY закрыт

## Task-008: Tailwind everywhere (убрать Bootstrap)
### Сделано
- [x] Подключён Tailwind toolchain (генерация `wwwroot/app.css` для Web и Mobile)
- [x] Bootstrap CSS удалён из Web и Mobile host pages (больше не грузится)
- [x] Bootstrap assets удалены из репозитория (css/js/maps)
- [x] Web Console Layout/NavMenu переведены на Tailwind
- [x] Основные страницы Web Console переведены на Tailwind (Login/Operator/Admin/HR/SuperAdmin + шаблонные Counter/Weather)
- [x] Mobile safe-area (`.status-bar-safe-area`) возвращён через общий Tailwind input
- [x] Создан отдельный UI-kit документ `docs/95_UI_KIT.md` (палитры, градиенты, компоненты, токены)
- [x] Добавлен офлайн preview UI-kit: `docs/96_UI_KIT_PREVIEW.html` (просмотр вне Web Console)
- [x] Применён default `UI-kit B` в Tailwind-слое (`styles/tailwind.css`)
- [x] Добавлена инфраструктура переключения тем `theme-a/theme-b/theme-c` без переписывания компонентов (`src/Chop.Web/wwwroot/theme.js`)
- [x] Добавлен UI-переключатель темы в шапке Web Console (`src/Chop.Web/Components/Layout/MainLayout.razor`)
- [x] Добавлен dark-theme bridge для legacy utility-классов (selected rows / alert badges), чтобы убрать белые блоки и плохой контраст
- [x] Переиспользуемый `FilterToolbar` подключен в дополнительных страницах (`/operator/points`, `/admin/clients`) для снижения дублирования layout-фильтров
- [x] Добавлен переиспользуемый `TableShell` (empty-state + overflow wrapper) и применён в `operator/forces`, `operator/points`, `manager/clients`
- [x] Добавлен переиспользуемый `StatusPill` и применён для унификации статусов в `admin/clients`, `manager/clients`, `operator/points`, `operator/forces`
### Осталось
- [x] Добавить CI шаг сборки CSS (`npm ci && npm run build:css`) чтобы исключить “забыли собрать CSS”
- [x] Начата декомпозиция повторяющегося UI: добавлены переиспользуемые Razor-компоненты `LoadingMessage` и `FlashMessages`, подключены на ключевых Web-страницах
- [x] Добавлен переиспользуемый `FilterToolbar` и применён в страницах `operator/forces`, `operator/incidents`, `manager/clients`, `superadmin/audit`
- [x] Вынести повторяющиеся UI блоки в Razor-компоненты (таблицы/фильтры/плашки) для контроля utility-sprawl
- [x] Tailwind hardening: safelist для динамических классов (только при необходимости, документировать причины)
- [x] Audit на “везде Tailwind”: убраны legacy bootstrap classnames из `src/**` (в т.ч. mobile template pages/layout)
- [x] Зафиксировать финальный Theme Pack (A/B/C) и перенести выбранные токены из `docs/95_UI_KIT.md` в `styles/tailwind.css`
### Риски (фиксируем, чтобы не забыть)
- [x] Размер CSS / performance (Web + MAUI WebView) -> введены budget-порог и регламент регулярного мониторинга в `docs/93_TAILWIND_PLAYBOOK.md`
- [x] Node/npm как build-dependency -> зафиксировано в CI (`.github/workflows/tailwind-css.yml`)
- [ ] MAUI safe-area/quirks -> smoke-check перед релизом Mobile
- [x] Возврат Bootstrap (случайно) -> добавлен anti-bootstrap guard в CI (`scripts/ci/check-no-bootstrap.ps1`)
- [x] Добавлен CSS perf budget в CI для `src/Chop.Web/wwwroot/app.css` и `src/Chop.App.Mobile/wwwroot/app.css` (`scripts/ci/check-css-budget.ps1`, лимит 128 KB)
См. `docs/93_TAILWIND_PLAYBOOK.md`.

## Task-009: Operator Dashboard + Map (2 points + 2 groups)
### Сделано
- [x] `/operator/dashboard` (map MVP + live queue + incident panel)
- [x] Инцидент: 2 точки клиента в UI (ALERT из инцидента + HOME из primary address)
- [x] Диспетчеризация: показ Group A/Group B на основе dispatches (и создание dispatch групп через UI input, MVP)
- [x] Realtime: инциденты + смена статуса + dispatch events (created/accepted)
- [x] Guards geo: `POST /api/guard/location/ping` + публикация `GuardLocationUpdated` + отображение guard markers на карте
- [x] Guards geo throttling (MVP): публикация `GuardLocationUpdated` ограничена по частоте
- [x] Dispatch UX в `/operator/dashboard`: выбор Group A/B только из групп, заступивших на дежурство (без ручного ввода guard userId)
- [x] Источник групп для dispatch исправлен: активные смены (`/api/hr/shifts/active`), а не availability-фильтр forces
- [x] Dispatch cards в `/operator/dashboard` расширены: отображаются группа, цель выезда (ТРЕВОГА/ДОМ), состав и статусы принятия по каждому получателю
### Осталось
- [x] Реальный Yandex Map (JS API key + маркеры) вместо map placeholder
- [x] Alerts panel: перейти от derived alerts к серверным alert_events + acknowledge/resolve

## Task-010: Alerts UX (points + groups)
### Сделано
- [x] Серверные alerts: `alert_events` workflow (`OPEN/ACKED/RESOLVED`) + endpoints оператора для списка/ack/resolve
- [x] Web: `/operator/dashboard` использует серверные alerts вместо derived alerts
- [x] Map badges (MVP): overlay + marker на ALERT point для `INCIDENT_SECOND_GROUP_MISSING`
- [x] Group alert: `INCIDENT_NO_ACCEPT` (dispatch создан, но никто не принял) + авто-resolve при accept
- [x] Geo alert: `INCIDENT_GUARD_NO_PING` (accept был, но гео-пингов по incidentId ещё нет) + resolve при первом ping
- [x] SLA worker: time-based alerts `INCIDENT_NO_ACCEPT_STUCK` и `INCIDENT_GUARD_OFFLINE`
- [x] Map badges: markers для `INCIDENT_NO_ACCEPT_STUCK` и `INCIDENT_GUARD_OFFLINE`
- [x] SLA worker: `INCIDENT_STUCK_IN_STATUS` для зависания в `EN_ROUTE/ON_SCENE`
- [x] Map badges: marker для `INCIDENT_STUCK_IN_STATUS`
- [x] Workflow оператора (MVP): assign/override для alerts (`/operator/alerts/{id}/assign`, `/override`)
- [x] Point alert: `INCIDENT_POINT_CONFLICT` (ALERT vs HOME distance threshold)
- [x] Alerts navigation UX: map overlay badges + jump-to-target (section + marker focus)
### Осталось
- [x] Alerts taxonomy: point alerts (no geo / conflict) + group alerts (missing 2nd group / no accept / stuck / offline)
- [x] Отображение алертов: бейджи на карте + переход в связанный объект (incident/group/point)

## Task-011: Commander (Mobile, GUARD+) MVP (planned)
### Сделано
- [ ] Реализация не начата
### Осталось
- [ ] Роль `COMMANDER` только в Mobile (guard+), не в Web Console
- [ ] Экран "моя группа": статусы/онлайн/последняя геопозиция
- [ ] Дневной/сменный отчёт по группе (MVP: чеклист + комментарии)

## Task-012: Contract Parity + Web API Integration (новый приоритет)
### Сделано
- [x] Добавлены backend endpoints для Web Console: `/api/hr/guards`, `/api/admin/clients`, `/api/operator/forces`, `/api/operator/points`, `/api/superadmin/settings`, `/api/superadmin/audit`
- [x] Добавлены backend endpoints для payments workflow (MVP): `/api/admin/payments/import*`
- [x] Web страницы Wave 2/3 переведены с `BackofficeConsoleStore` на `BackofficeApiClient` и реальные `/api/*` вызовы
- [x] Добавлен `POST /api/auth/invitations/accept` (first-login по приглашению с установкой пароля и выдачей access/refresh)
- [x] Добавлен `GET /api/admin/payments/imports/{importId}` для contract parity по payments import
- [x] Добавлен `GET /api/admin/clients/{id}` для детализации клиента в Web edit-flow
- [x] Реализованы `GET/PUT /api/clients/me` (CLIENT profile self-service) + интеграционные тесты
- [x] Финальная сверка API-контракта для web-контура выполнена (2026-02-27)
- [x] Parity подтвержден по auth/payments/admin/hr/operator/superadmin web-endpoints (включая invitations accept и import details)
- [x] Инциденты list endpoint соответствует контракту по пагинации (`PagedResult`) и фильтрам (`status/from/to/page/pageSize`)
- [x] Contract mismatch в разделе payments imports закрыт: реализован `GET /api/admin/payments/imports/{importId}`
### Осталось
- [x] Закрыть non-web часть общего контракта: `GET/PUT /api/clients/me` (CLIENT profile self-service)
- [x] Уточнены роли в `docs/03_API.md` для `POST /hr/shifts/start|end` (приведены к фактической реализации `HR/OPERATOR/ADMIN/SUPERADMIN`)
- [x] Управление сменами перенесено в операторский контур (`/operator/shifts` в навигации), доступ по API открыт для `OPERATOR` без потери доступа `HR`
- [x] Перевести Wave 2/3 Web страницы с in-memory store на реальные backend endpoints

## Task-013: Security Hardening (новый приоритет)
### Сделано
- [x] Закрыт guard authorization gap: `POST /api/guard/location/ping` разрешён только назначенному guard по incident assignment
- [x] JWT hardening: fail-fast в non-Development при weak/default signing key
- [x] Refresh token hardening: reuse detection + revoke active token chain пользователя
### Осталось
- [x] Запретить запуск API в non-Development при дефолтном JWT signing key (fail-fast)
- [x] Закрыть guard authorization gap в `POST /api/guard/location/ping`
- [x] Добавить refresh-token reuse detection и отзыв семейства токенов
- [x] Уточнить/документировать production rate-limit policy и password policy (`docs/04_SECURITY.md`)

## Task-014: Geo Platform Upgrade (новый приоритет)
### Сделано
- [x] Groundwork: в Infrastructure подключён `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`
- [x] Groundwork: `UseNetTopologySuite()` включён в Postgres provider
- [x] Добавлена EF migration `PostGisGeographyUpgrade`: `GeoPoint` (`geography(point,4326)`) для `incidents`, `client_addresses`, `guard_locations`
- [x] Backfill `GeoPoint` из текущих `Latitude/Longitude`
- [x] Добавлены GiST индексы по `GeoPoint` для spatial запросов
- [x] Реализован `GET /api/operator/incidents/{id}/nearest` (PostGIS distance в PostgreSQL + fallback расчёт для SQLite tests)
- [x] Write-flow sync: `GeoPoint` и `Latitude/Longitude` синхронизируются в `AppDbContext` при сохранении (обратная совместимость + first-class GeoPoint)
- [x] Read-flow core переведён на `GeoPoint` (Incident DTO mapping, geo-alert checks, nearest queries/fallback)
- [x] Development startup: авто-применение EF миграций только для PostgreSQL provider (снижение риска schema drift -> HTTP 500)
- [x] Добавлена migration parity `GeoModelParity` (no-op schema) для синхронизации EF model snapshot и снятия `PendingModelChangesWarning`
### Осталось
- [x] Перевести геомодель на PostGIS geography(Point,4326) + spatial индексы
- [x] Добавить миграции и обратную совместимость с текущими данными
- [x] Реализовать nearest-поиск экипажей на spatial запросах PostGIS
- [ ] Финальная зачистка legacy `Latitude/Longitude`: миграция удаления/архивации после стабильного периода совместимости

## Task-020: RBAC Web Matrix (policy freeze)
### Сделано
- [x] RBAC-матрица ролей зафиксирована в `docs/04_SECURITY.md` (Operator/HR/Manager/Accountant/Admin/SuperAdmin)
- [x] Решение по RBAC freeze зафиксировано в `docs/90_DECISIONS.md`
- [x] Route hardening Web: demo-страницы `/counter` и `/weather` ограничены ролью `SUPERADMIN`, неавторизованные пользователи уходят на `/login`
- [x] Страница `/forbidden` восстановлена в корректной UTF-8 локализации
- [x] Единый формат пользовательских ошибок API в Web (`401/403/500` и др.) через `ApiErrorFormatter` для `IncidentsApiClient` и `BackofficeApiClient`
- [x] Единая нормализация ошибок на страницах Web: `catch(Exception ex)` использует `UiErrorMapper.ToUserMessage(ex)` вместо raw `ex.Message`
### Осталось
- [x] Формально закрепить правила:
- [x] `OPERATOR` — только операционный контур инцидентов
- [x] `HR` — только охранники/активность/смены, без платежей
- [x] `MANAGER` — только клиенты + billing summary, без инцидентов/карт
- [x] `ACCOUNTANT` — только платежи/импорт, без HR/инцидентов
- [x] `ADMIN` — клиенты + платежи + часть справочников
- [x] `SUPERADMIN` — полный доступ + аудит + системные настройки

## Task-021: Identity Admin API (backoffice users)
### Сделано
- [x] Добавлены endpoints `GET/POST /api/superadmin/users`, `POST /api/superadmin/users/{id}/roles/add|remove`, `POST /api/superadmin/users/{id}/toggle-active`
- [x] Добавлены DTO в `Chop.Shared` для identity backoffice API
- [x] Добавлены server-side ограничения: last-role, last-superadmin, self-superadmin-remove, self-deactivate
- [x] Добавлен аудит `identity.user.*`
- [x] Добавлена Web страница `/superadmin/users` (список, создание, управление ролями, активация/деактивация)
- [x] Добавлены toast-уведомления (успех/ошибка) с авто-скрытием для действий на `/superadmin/users`
### Осталось
- [x] Добавить UX-подтверждения для опасных действий (снятие роли, деактивация)

## Task-022: HR Guards Management (create/edit)
### Сделано
- [x] Добавлены сущности и миграция: `guard_groups`, `guard_group_members`, `security_points`, `guard_shifts`
- [x] Реализованы API:
- [x] `GET/POST /api/hr/groups`
- [x] `POST /api/hr/groups/{id}/members`
- [x] `DELETE /api/hr/groups/{id}/members/{guardUserId}`
- [x] `GET /api/hr/shifts/active`
- [x] `POST /api/hr/shifts/start`
- [x] `POST /api/hr/shifts/end`
- [x] Обновлён `GET /api/hr/guards`: `OnShift/GroupName/AssignedPost/ShiftStartedAtUtc` из активных смен
- [x] Добавлены web-страницы: `/hr/groups`, `/operator/shifts` + ссылки в меню
- [x] Добавлены интеграционные тесты API для групп и смен
- [x] Реализован `POST /api/hr/guards` (создание охранника)
- [x] Страница `/hr/guards` дополнена формой создания охранника
- [x] Для охранника добавлены и сохраняются поля `ФИО` и `Позывной` (API + БД + HR UI + operator forces display)
### Осталось
- [x] Добавить редактирование профиля охранника
- [x] Добавить базовое управление сменами/назначениями
- [x] Сохранить строгую изоляцию HR от payments/admin функций

## Task-029: Security Points Management (operator/hr)
### Сделано
- [x] `GET /api/operator/points` переведён на `security_points` + active forces из `guard_shifts`
- [x] Добавлен `POST /api/operator/points` (создание точки охраны)
- [x] Страница `/operator/points` расширена формой создания точки
- [x] `/operator/dashboard`: добавлен блок "Группы на дежурстве сейчас" (состав групп из активных смен)
- [x] `/operator/dashboard`: Group A/Group B теперь определяются по `slot` tag (`[slot:A]`, `[slot:B]`), а не только по порядку dispatch
- [x] Добавлены `PUT /api/operator/points/{id}` и `POST /api/operator/points/{id}/toggle-active`
- [x] `GET /api/operator/points` поддерживает `includeInactive=true/false`
- [x] Добавлена серверная валидация координат точек (`lat/lon` пара + диапазоны)
- [x] `/operator/points`: добавлен UI редактирования и активации/деактивации точки
- [x] `POST/PUT /api/operator/points*`: серверная нормализация адреса точки (trim + пробелы + запятые)
- [x] `POST/PUT /api/operator/points*`: опциональный geocoding для автозаполнения `lat/lon` при пустых координатах (feature-flag)
- [x] `/operator/points` UX: добавлены подсказки по автогеокодингу, отображение `Geo` (lat/lon) в таблице и явный результат после create/update (нормализация адреса/автозаполнение координат)
### Осталось
- [x] Geocoding/normalization адреса (MVP: always normalize + optional geocoding by config)

## Task-023: Admin Clients Management (create/edit)
### Сделано
- [x] Реализован `POST /api/admin/clients` (ADMIN/SUPERADMIN): создание client-user + `CLIENT` role + `client_profile` + invitation token
- [x] Web `/admin/clients`: добавлена форма создания клиента и вывод invitation token/expiry
- [x] Реализован `PUT /api/admin/clients/{id}`: редактирование ФИО/контактов/домашнего адреса и billing-полей
- [x] Billing summary для admin/manager views переведен на реальные поля профиля (`tariff`, `billingStatus`, `lastPaymentAtUtc`, `hasDebt`)
- [x] Ограничения ролей для create/edit клиентов зафиксированы в API (`ADMIN/SUPERADMIN`), `MANAGER` — только read-only
- [x] Исправлена стабильность `PUT /api/admin/clients/{id}` (устранен `500`/concurrency issue в update-flow)
- [x] Добавлены/пройдены API-тесты на create/update + RBAC (`ADMIN` success, `MANAGER` forbidden)
- [x] Статусы биллинга в Web UI переведены на русский (формы/фильтры/таблицы)
- [x] Поле `Тариф` в `/admin/clients` переведено на dropdown из backend-справочника тарифов
### Осталось
- [x] Task-023 закрыт; расширение billing-модели вынесено в отдельный backlog task

## Task-030: Tariffs Management (SUPERADMIN)
### Сделано
- [x] Добавлена БД-сущность `billing_tariffs` + EF migration с базовыми тарифами (`STANDARD`, `PREMIUM`, `VIP`)
- [x] Добавлен API `GET /api/admin/tariffs` (MANAGER/ADMIN/SUPERADMIN)
- [x] Добавлены API `POST /api/superadmin/tariffs` и `PUT /api/superadmin/tariffs/{code}` (SUPERADMIN)
- [x] На `/superadmin/settings` добавлен UI управления тарифами (создание/редактирование)
- [x] Валидация тарифа в `POST/PUT /api/admin/clients*` (неизвестный тариф -> 400)
- [x] Добавлены и пройдены API-тесты на тарифы и RBAC
- [x] Добавлен API `DELETE /api/superadmin/tariffs/{code}` (SUPERADMIN)
- [x] Запрещена деактивация тарифа, назначенного клиентам (business-rule в `PUT /superadmin/tariffs/{code}`)
- [x] Запрещено удаление тарифа, назначенного клиентам (business-rule в `DELETE /superadmin/tariffs/{code}`)
- [x] `/superadmin/settings`: добавлена кнопка удаления тарифа
### Осталось
- [x] Task-030 закрыт

## Task-024: Manager Read-Only Area
### Сделано
- [x] Закреплён `GET`-only доступ менеджера к `/api/admin/clients` и `/api/admin/tariffs`
- [x] Подтверждён запрет manager на operator/payments write endpoints (интеграционные API-тесты)
- [x] Выделен отдельный UX-маршрут `/manager/clients` и route guard по роли
### Осталось
- [x] Task-024 закрыт

## Task-025: Accountant Payments-Only Area
### Сделано
- [x] Закреплён доступ `ACCOUNTANT` к payments import/match/apply endpoints
- [x] Подтверждён запрет accountant на HR/Admin clients/Operator endpoints (интеграционные API-тесты)
- [x] Выделен отдельный UX-маршрут `/accountant/payments/import` и route guard по роли
- [x] Страница бухгалтера усилена обработкой `401/403` (redirect в `/login` и `/forbidden`)
### Осталось
- [x] Task-025 закрыт

## Task-026: Web Navigation + Route Guards
### Сделано
- [x] Default redirect по RBAC обновлен: `OPERATOR` по умолчанию уходит на `/operator/dashboard`
- [x] Добавлен централизованный route guard на уровне `Routes.razor` (`GuardedRouteView`) вместо опоры только на page-level проверки
- [x] Добавлена policy-модель доступа по route-prefix (`operator/hr/admin/manager/accountant/superadmin`) с единым поведением
- [x] Унифицирован UX доступа: неаутентифицированные -> `/login`, недостаточно прав -> `/forbidden`
- [x] Навигация и shell (NavMenu/MainLayout/Login/Home/Forbidden) переведены на корректный русский UTF-8 без крокозябр
- [x] Page-level проверки оставлены как defense-in-depth, а не как единственный механизм доступа
### Осталось
- [x] Task-026 закрыт

## Task-027: Web CRUD Screens (interaction)
### Сделано
- [x] HR UI: добавлено создание охранников на `/hr/guards`
- [x] HR UI: редактирование профиля охранника (`ФИО/позывной/телефон/email`) через `PUT /api/hr/guards/{id}`
- [x] HR UI: быстрые действия по сменам на `/hr/guards` (старт/завершение смены, выбор группы/поста при старте)
- [x] UX cleanup: actions по сменам удалены из `/hr/guards`, управление сменами оставлено только во вкладке `/operator/shifts`
- [x] UX cleanup `/hr/guards`: убраны постоянные вторые строки в таблице (показываются только при редактировании), информация о старте смены перенесена в колонку "Смена"
- [x] Admin UI: создание/редактирование клиентов (базовый CRUD: профиль, контакты, HOME-адрес, billing-поля)
- [x] Admin UI: расширенный CRUD клиентов (мульти-телефоны и мульти-адреса в форме + загрузка деталей клиента `GET /api/admin/clients/{id}`)
- [x] SuperAdmin UI: hardening управления учетками/ролями (`/superadmin/users`) — локализация статусов, блокировка self-deactivate, защита от снятия последней/критичной роли, добавление только отсутствующих ролей
### Осталось
- [x] Manager/Accountant UI: только разрешенные read/write операции (read-only banner для manager, safeguards и локализация статусов на accountant payments UI)

## Task-028: RBAC Security Regression Pack
### Сделано
- [x] Добавлены интеграционные тесты 401/403 для manager/accountant контуров (`/api/admin/clients`, `/api/admin/payments/imports`, запреты на `hr/*` и `operator/*`)
- [x] Добавлены тесты попыток эскалации роли (`/api/superadmin/users/{id}/roles/add`): запрет для `HR` и `unsupported role -> 400`
- [x] Добавлена проверка audit trail для чувствительных операций доступа (`identity.user.role.add/remove/toggle-active`)
### Осталось
- [x] Task-028 закрыт

## Backlog: Новые идеи и внеплановые задачи
- [x] RBAC matrix как отдельный артефакт (кто какие поля/действия видит) -> `docs/98_RBAC_MATRIX_WEB.md`
- [x] Data retention policy документом (таблица -> срок -> основание) -> `docs/100_DATA_RETENTION.md`
- [x] API error catalog (единый список кодов ошибок для frontend/mobile) -> `docs/97_API_ERRORS.md` + `ApiErrorFormatter/UiErrorMapper`
- [x] Incident SLA alerts (время без назначения, время до принятия, время до закрытия) реализованы в Task-010 (`INCIDENT_NO_ACCEPT_STUCK`, `INCIDENT_GUARD_OFFLINE`, `INCIDENT_STUCK_IN_STATUS`)
- [x] Local dev bootstrap: документировать обязательные зависимости (Postgres service, порты) и типовые ошибки запуска (см. `docs/94_LOCAL_DEV.md`)

## Task-031: PII override per user (SUPERADMIN)
### Сделано
- [x] Добавлен флаг `users.CanViewClientPii` (EF + миграция `UserClientPiiAccessFlag`).
- [x] Добавлен endpoint `POST /api/superadmin/users/{id}/toggle-client-pii`.
- [x] В `/api/admin/clients*` включена проверка per-user флага для роли `MANAGER`.
- [x] Обновлены web-контракты и UI `/superadmin/users` (показ статуса и переключение доступа к PII).
- [x] Добавлены интеграционные тесты на toggle PII и RBAC-запрет для не-superadmin.

