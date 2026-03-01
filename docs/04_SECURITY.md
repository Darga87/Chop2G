# Security / Auth / Audit

## 1. Общий подход
- API auth: JWT Bearer access token + refresh token (ротация).
- Web-пульт: может работать через cookie/BFF, но роли для API берутся только из валидированного токена.
- Mobile: JWT accessToken + refreshToken.
- Роли: CLIENT, GUARD, OPERATOR, HR, ADMIN, SUPERADMIN.
- Все критичные изменения (статусы, назначения, платежи) — в аудит.

### 1.1 Что запрещено
- Нельзя доверять заголовкам клиента для ролей/пользователя (`X-User-*` и аналоги).
- Роли и userId извлекаются только из проверенного JWT claims.

### 1.2 JWT / refresh в MVP
- Access token: короткоживущий, содержит `sub` + `role` claims.
- Refresh token: хранится в БД только в виде SHA-256 hash.
- Refresh выполняется с ротацией: старый токен немедленно ревокается, создаётся новый.
- Logout ревокает refresh token.
- Login выполняется только через user-store в БД; конфиговые dev-пользователи не участвуют в runtime-аутентификации.

## 2. Приглашения (Invites)
### 2.1 Как работает
- Админ/кадровик создаёт учётку → система генерирует invitationToken (одноразовый).
- Токен имеет срок жизни (например, 72 часа).
- Первый вход: пользователь устанавливает новый пароль.
- После успешного accept invitation — токен становится недействительным.

### 2.2 Требования
- Токен хранить в БД в хэшированном виде (как пароль), чтобы утечка БД не дала готовых ссылок.
- Ограничить число попыток accept invitation (rate limit).

## 3. Пароли и политика
- Минимум 10 символов, запрещать 1000 самых популярных паролей (по возможности).
- Для ADMIN/SUPERADMIN: рекомендуем 2FA (Next, но можно сразу).
- Смена пароля фиксируется в аудите (без хранения пароля).

## 4. Восстановление доступа
MVP вариант (минимально рискованный):
- Восстановление через “одноразовый код” на телефон/почту (если реально есть провайдер).
Если провайдера нет:
- “сброс через администратора” (админ генерирует новый invite/reset-token).

## 5. Rate limiting
- /auth/login, /auth/invitations/accept, /incidents (создание SOS) — отдельные лимиты.
- Защита от спама SOS: можно вводить cooldown (например 30 сек) + серверная проверка.

## 6. Аудит
### 6.1 Что пишем
- Изменения статуса инцидента.
- Создание Dispatch и изменения recipients.
- Применение платежей (auto/manual).
- Создание/блокировка пользователей.
- Смена ролей.
- Экспорт данных (если будет).

### 6.2 Как
- Таблица incident_status_history (см. ERD).
- Таблица admin_audit_log (можно добавить позже), формат:
  - id, actorUserId, action, entityType, entityId, detailsJson, createdAt, ip, userAgent.

## 7. Данные и приватность
- В логах не печатать: телефоны, адреса полностью, токены, банковские реквизиты.
- В БД: минимально необходимые данные.
- PII masking (operator contour): в `GET /api/operator/incidents/{id}` поле `client.phones[]` маскируется для роли `OPERATOR`; полный номер доступен только `ADMIN/SUPERADMIN`.
- PII masking (manager contour): в `GET /api/admin/clients*` для роли `MANAGER` телефоны маскируются, `email` в деталях клиента не возвращается; полный вид — только `ADMIN/SUPERADMIN`.
- PII override (manager contour): `SUPERADMIN` может точечно включать/выключать право просмотра PII клиентов для конкретного backoffice-пользователя через `POST /api/superadmin/users/{id}/toggle-client-pii`.

## 8. RBAC Matrix Freeze (Task-020)
- `OPERATOR`: only incident operations (`/operator/incidents*`, `/operator/alerts*`, `/operator/forces`, `/operator/points`, `/hubs/incidents`).
- `HR`: only guard management (`/hr/guards*`), no payments, no incidents map operations.
- `MANAGER`: read-only client and billing-summary scope, no incidents/maps/HR.
- `ACCOUNTANT`: only payments/import scope (`/admin/payments/import*`), no incidents/HR.
- `ADMIN`: incidents + clients + payments, no `SUPERADMIN` endpoints.
- `SUPERADMIN`: full access including `/api/superadmin/*`, audit, system settings.

## 9. Backoffice Identity Admin Rules (Task-021)
- Backoffice user lifecycle is managed only by `SUPERADMIN` endpoints `/api/superadmin/users*`.
- Allowed managed roles: `HR`, `OPERATOR`, `MANAGER`, `ACCOUNTANT`, `ADMIN`, `SUPERADMIN`.
- Security restrictions:
- cannot remove last role of a user.
- cannot remove own `SUPERADMIN` role.
- cannot remove the last `SUPERADMIN` in system.

Отдельный артефакт по Web-доступам (страницы/действия/поля): `docs/98_RBAC_MATRIX_WEB.md`.
- cannot deactivate own account.
- All operations are audited under `identity.user.*` actions.
