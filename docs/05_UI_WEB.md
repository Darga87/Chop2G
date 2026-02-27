# UI - Web Console (Blazor Web App)

Цель: веб-пульт для операционных ролей (в первую очередь OPERATOR и SUPERADMIN) и отдельных backoffice-ролей (ACCOUNTANT/MANAGER) без доступа к инцидентам.

Важно:
- `COMMANDER` это НЕ веб-роль. Это роль Mobile (GUARD+) для командира группы охранников.
- Для инцидента у клиента всегда 2 точки: `ALERT` (откуда вызвали SOS) и `HOME` (адрес проживания).
- По политике реагирования: на инцидент по умолчанию выезжают 2 группы (Group A + Group B).

## 1) Роли и видимость
- OPERATOR (Web): инциденты, карта, силы/посты, назначения (dispatch), алерты, аудит действий по инцидентам.
- SUPERADMIN (Web): системные настройки, аудит, мониторинг платформы (outbox/health/lag).
- HR (Web): охранники, смены/назначения (кадровая часть).
- ADMIN (Web): всё выше + клиенты + платежи + импорт.
- ACCOUNTANT (Web, planned): только платежи (импорт 1C, сопоставление, ручная очередь, применение), без инцидентов/карт/сил.
- MANAGER (Web, planned): только клиенты/договоры/биллинг summary, без инцидентов/карт/сил.

## 2) Основной UX OPERATOR (как должна работать смена)
### 2.1 Центральный экран
`/operator/dashboard` (implemented, MVP)
- Карта + справа live-очередь инцидентов + панель Alerts.
- Realtime: новые инциденты, смены статусов, события dispatch, обновления гео охранников, новые алерты.
- Быстрые действия: открыть инцидент, ACK, создать/поправить dispatch, сменить статус (с обязательным comment где требуется state machine).

### 2.2 Карточка инцидента
`/operator/incidents/{id}`
- Core: статус, created/updated, адрес снапшот.
- Client: ФИО, телефоны, адреса (как в API).
- Points (2 точки):
  - `ALERT` point: адрес/гео из события SOS (может быть без гео).
  - `HOME` point: адрес/гео проживания (fallback ориентир).
- Dispatch (2 группы по умолчанию):
  - Group A: recipients + статус (CREATED/ACCEPTED/EN_ROUTE/ON_SCENE/...) + ETA/last update.
  - Group B: то же самое.
  - Если второй группы нет: алерт `SECOND_GROUP_MISSING`.
- Alerts:
  - Point alerts: `ALERT_POINT_NO_GEO`, `HOME_POINT_NO_GEO`, `POINT_CONFLICT` (ALERT далеко от HOME).
  - Group alerts: `GROUP_*_NO_ACCEPT`, `GROUP_*_STUCK`, `GROUP_*_OFFLINE`.
- History: таймлайн статусов/комментариев.

### 2.3 Силы и посты
`/operator/forces`
- Список сил на смене: availability, online/offline, last geo timestamp, контакты.
- Интеграция с картой: выбор силы центрирует маркер и показывает карточку.

`/operator/points`
- Список объектов/постов с поиском.
- Интеграция с картой: выбор точки центрирует и показывает связанные силы/инциденты.

## 3) Карта (Yandex) - модель отображения
Показываем минимум:
- Инциденты: маркер(ы) `ALERT` и `HOME` + бейдж статуса/давности.
- Силы: маркеры охранников/групп, статус availability, last geo age.
- Точки/посты: маркеры объектов (POST/SITE/HUB).
- Alerts: бейджи на маркерах и в отдельном списке.

Правило: если `ALERT` без гео, всё равно показываем `HOME` (если есть), а сам `ALERT` остаётся в списке с флагом "no geo".

## 4) Backoffice (платежи/клиенты) - строгая сегментация
### 4.1 Accounting (planned)
`/accounting/payments/import`
- импорт только `1CClientBankExchange`
- итоги: matched/ambiguous/invalid
- manual queue: ручное сопоставление, применение

### 4.2 Manager (planned)
`/manager/clients` (или ограниченный view на `/admin/clients`, TBD)
- billing summary, статусы, долги
- без доступа к инцидентам/карте/силам

## 5) Realtime / события
Web Console подписывается на SignalR (инциденты/dispatch/guards geo/alerts) и обновляет:
- live queue
- карту
- карточку инцидента

## 6) План / задачи
### Task-006 (уже сделано): Web Console MVP
- Wave 1: login + incidents list/details + realtime
- Wave 2: HR/Admin страницы (UI MVP)
- Wave 3: forces/points/superadmin (UI MVP)

### Task-009 (planned): Operator Dashboard + Map (2 points + 2 groups)
- `/operator/dashboard` как основной экран
- Визуализация 2 точек (ALERT + HOME) в карточке и на карте
- UX для "2 группы обязательно": Group A + Group B
- Realtime связка списка/карты/деталей

### Task-010 (planned): Alerts UX (points + groups)
- Нормализованный список алертов и фильтры
- Отображение алертов на карте
- Workflow оператора: acknowledge/assign/override/resolve

### Task-011 (planned): Commander (Mobile, GUARD+) MVP
- Командир группы охранников (Mobile-only)
- View "моя группа" + дневной/сменный отчёт (минимально)

## Task-010 Status Update (2026-02-11)
- Alert badges are shown on map overlay and grouped by rule code.
- Operator can navigate from alert item/badge to linked object section (`map`, `dispatch`, `alerts`) and focus related map marker.
