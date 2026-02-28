# API Contract (REST + SignalR)

Базовый URL: /api
Версия: v1 (пока без префикса, но закладываем возможность)

## 1. Auth
### POST /auth/login
Req: { login: string, password: string }
Res: { accessToken, refreshToken, expiresInSeconds, user: {id, roles[]} }

### POST /auth/refresh
Req: { refreshToken }
Res: { accessToken, refreshToken, expiresInSeconds }

### POST /auth/logout
Req: { refreshToken }
Res: 204

### POST /auth/invitations/accept
Назначение: первый вход по приглашению, установка нового пароля.
Req: { invitationToken, newPassword }
Res: { accessToken, refreshToken, user }

## 2. Clients
### GET /clients/me
Роль: CLIENT
Res: ClientProfileDto

### PUT /clients/me
Роль: CLIENT
Req: UpdateClientProfileDto (ограниченный набор)
Res: ClientProfileDto

### GET /admin/clients
Роль: ADMIN/SUPERADMIN
Query: search, page, pageSize
Res: Paged<ClientListItemDto>

### POST /admin/clients
Создать клиента и отправить приглашение.
Req: CreateAdminClientRequestDto
- login (required)
- fullName (required)
- phone?, email?
- homeAddress?, homeLatitude?, homeLongitude? (lat/lon only as pair)
Res: CreateAdminClientResponseDto
- clientId
- invitationToken
- invitationExpiresAtUtc

### PUT /admin/clients/{id}
Обновить профиль клиента (админ контур).
Req: UpdateAdminClientRequestDto
- fullName (required)
- phone?, email?
- tariff, billingStatus, hasDebt, lastPaymentAtUtc?
- homeAddress?, homeLatitude?, homeLongitude? (lat/lon only as pair)
Res: AdminClientItemDto

### GET /admin/tariffs
Role: `MANAGER/ADMIN/SUPERADMIN`
Query: `includeInactive`
Res: `BillingTariffItemDto[]`

## 3. Guards / HR
### GET /hr/guards
Роль: HR/ADMIN/SUPERADMIN
Res: list

### POST /hr/guards
Req: CreateGuardDto
Res: { guardId, invitationLinkOrToken }

### POST /hr/shifts/start
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Req: `StartGuardShiftRequestDto` (`guardUserId`, `guardGroupId?`, `securityPointId?`)
Res: `GuardShiftItemDto`

### POST /hr/shifts/end
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Req: `EndGuardShiftRequestDto` (`guardUserId`)
Res: 204

## 4. Incidents (SOS)
### POST /incidents
Роль: CLIENT
Header (optional): `Idempotency-Key: <string>`
Req: CreateIncidentDto
- clientLocation { lat, lon, accuracyM? }
- deviceTimeUtc?
- addressText? (если есть)
Res: IncidentDto + ACK (incidentId)
Поведение:
- если тот же `Idempotency-Key` уже использовался этим клиентом, сервер возвращает тот же `incidentId`, новый инцидент не создаётся;
- если тот же `Idempotency-Key` использован с другим payload, сервер возвращает `409 Conflict` (`IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD`);
- без ключа действует server-side дедупликация: при активном инциденте клиента в окне 60 секунд (`NEW/ACKED/DISPATCHED/ACCEPTED/EN_ROUTE/ON_SCENE`) возвращается существующий `incidentId`.
- записи idempotency хранятся ограниченное время (TTL, 24 часа), затем очищаются фоновым процессом.

### GET /operator/incidents
Роль: OPERATOR/ADMIN/SUPERADMIN
Query: status, from, to, page, pageSize
Res: Paged<IncidentListItemDto>

### GET /operator/incidents/{id}
Роль: OPERATOR/ADMIN/SUPERADMIN
Res: IncidentDetailsDto
- core: id, status, createdAt, lastUpdatedAt, location?, addressSnapshot?
- client: { fullName, phones[] }
- addresses[]: { label, address, location? }
- history[]: { fromStatus?, toStatus, actorUserId, actorRole, comment?, createdAt }
- PII policy (MVP): для роли `OPERATOR` телефоны в `client.phones[]` возвращаются в маске; `ADMIN/SUPERADMIN` получают полные значения.

### POST /operator/incidents/{id}/dispatch
Роль: OPERATOR/ADMIN/SUPERADMIN
Req: CreateDispatchDto
- method
- recipients[]: { type: POST|PATROL_UNIT|GUARD, id }
- comment?
Res: DispatchDto

### POST /operator/incidents/{id}/status
Роль: OPERATOR/ADMIN/SUPERADMIN
Req: ChangeIncidentStatusDto
- toStatus
- comment (обязателен для ручной смены; сервер валидирует по STATE_MACHINE)
Res: IncidentDto

### POST /guard/incidents/{id}/accept
Роль: GUARD
Req: { comment? }
Res: IncidentDto

### POST /guard/incidents/{id}/progress
Роль: GUARD
Req: { toStatus: EN_ROUTE|ON_SCENE|RESOLVED, comment? }
- `comment` обязателен при `toStatus=RESOLVED`.
Res: IncidentDto

## 5. Geo / nearest forces
### GET /operator/incidents/{id}/nearest
Роль: OPERATOR/ADMIN/SUPERADMIN
Query: limitPosts=5, limitUnits=5
Res:
{
  posts: [{id,name,distanceMeters,phone,radioChannel,responsibles[]}],
  patrolUnits: [{id,name,distanceMeters,phone,radioChannel,lastLocationAgeSeconds}]
}

## 6. Guard location pings
### POST /guard/location/ping
Роль: GUARD
Req: GuardLocationPingDto
- lat, lon, accuracyM?
- deviceTimeUtc?
- shiftId?
- incidentId? (если активная заявка)
Res: 204

## 6.1 Alerts (Operator)
### GET /operator/incidents/{id}/alerts
Роль: OPERATOR/ADMIN/SUPERADMIN
Query: includeResolved=false
Res: AlertListItemDto[]

### POST /operator/alerts/{alertId}/ack
Роль: OPERATOR/ADMIN/SUPERADMIN
Req: { comment? }
Res: 204

### POST /operator/alerts/{alertId}/resolve
Роль: OPERATOR/ADMIN/SUPERADMIN
Req: { comment? }
Res: 204

### POST /operator/alerts/{alertId}/assign
Роль: OPERATOR/ADMIN/SUPERADMIN
Req: { assigneeUserId?, comment? }
Res: 204

### POST /operator/alerts/{alertId}/override
Роль: OPERATOR/ADMIN/SUPERADMIN
Req: { comment } // required
Res: 204

Notes (MVP rules implemented):
- `INCIDENT_SECOND_GROUP_MISSING`: после 1-го dispatch, resolve после 2-го.
- `INCIDENT_NO_ACCEPT`: dispatch создан, но ни один guard не принял (resolve при первом accept).
- `INCIDENT_GUARD_NO_PING`: guard принял, но гео-пингов по `incidentId` ещё нет (resolve при первом ping).
- `INCIDENT_NO_ACCEPT_STUCK`: dispatch создан, но нет accept в SLA-окне (фоновой worker).
- `INCIDENT_GUARD_OFFLINE`: после accept нет гео-пингов в SLA-окне (фоновой worker).
- `INCIDENT_STUCK_IN_STATUS`: инцидент долго находится в `EN_ROUTE` или `ON_SCENE` без обновления (фоновой worker).
- `INCIDENT_POINT_CONFLICT`: ALERT и HOME точки имеют geo, но расстояние между ними превышает порог.

Alerts workflow (MVP):
- acknowledge: `POST /operator/alerts/{id}/ack`
- assign: `POST /operator/alerts/{id}/assign`
- override: `POST /operator/alerts/{id}/override` (manual resolve with mandatory comment)
- resolve: `POST /operator/alerts/{id}/resolve`

## 7. Payments import (Admin)
### POST /admin/payments/import
Роль: ADMIN/SUPERADMIN
Body: multipart/form-data (file)
Res: { importId, status, stats }

### GET /admin/payments/imports/{importId}
Res: ImportDetailsDto + rows paged

### POST /admin/payments/imports/{importId}/apply
Применить авто-сопоставленные платежи.
Req: {}
Res: 204

### POST /admin/payments/imports/{importId}/rows/{rowId}/match
Ручное сопоставление спорной строки.
Req: { clientUserId? , clientDisplayName? } // хотя бы одно поле
Res: 204

## 8. DTO (кратко)
### IncidentDto
- id, status, createdAt, location?, addressSnapshot?, clientSummary, lastUpdatedAt

### IncidentDetailsDto
- id, status, createdAt, lastUpdatedAt
- location?, addressSnapshot?
- client: { fullName, phones[] }
- addresses[]: { label, address, location?, isPrimary }
- history[]
- `client.phones[]`: по `OPERATOR` значения маскируются на API-слое.

### CreateDispatchDto
- method, recipients[], comment?

### DispatchDto
- id, incidentId, method, comment?, createdByUserId, createdAt
- recipients[]: { id, type, recipientId, distanceMeters?, status, acceptedBy?, acceptedAt?, acceptedVia? }

### DispatchRecipientInputDto
- type, id, distanceMeters?

### GuardLocationPingDto
- lat, lon, accuracyM?, deviceTimeUtc?, shiftId?, incidentId?

### AlertListItemDto
- id, ruleCode, severity, status, summary, createdAtUtc
- assigneeUserId?, assignedAtUtc? (operator workflow)

## 9. SignalR
Hub: /hubs/incidents
Auth: JWT, роли `OPERATOR/ADMIN/SUPERADMIN`.

Группы:
- `ops:*` — общий поток для пульта.
- `role:OPERATOR`, `role:ADMIN`, `role:SUPERADMIN` — role-группы для маршрутизации.

События MVP:
- `IncidentCreated`
  - payload: `{ eventId, occurredAtUtc, type, incident: IncidentDto }`
- `IncidentStatusChanged`
  - payload: `{ eventId, occurredAtUtc, type, incidentId, fromStatus, toStatus, actorUserId, actorRole, comment?, incident: IncidentDto }`
- `DispatchCreated`
  - payload: `{ eventId, occurredAtUtc, type, incidentId }`
- `DispatchAccepted`
  - payload: `{ eventId, occurredAtUtc, type, incidentId, guardUserId, comment? }`
- `GuardLocationUpdated`
  - payload: `{ eventId, occurredAtUtc, type, incidentId, guardUserId, location: { lat, lon, accuracyM? } }`

## 10. Web Backoffice (MVP endpoints)
### GET /hr/guards
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Query: `search`, `status`, `onShiftOnly`
Res: `GuardItemDto[]`

### POST /hr/guards/{guardId}/toggle-active
Role: `HR/ADMIN/SUPERADMIN`
Res: `204`

### GET /hr/groups
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Res: `GuardGroupItemDto[]`

### POST /hr/groups
Role: `HR/ADMIN/SUPERADMIN`
Req: `CreateGuardGroupRequestDto` (`name`)
Res: `GuardGroupItemDto`

### POST /hr/groups/{groupId}/members
Role: `HR/ADMIN/SUPERADMIN`
Req: `AddGuardToGroupRequestDto` (`guardUserId`, `isCommander`)
Res: `204`

### DELETE /hr/groups/{groupId}/members/{guardUserId}
Role: `HR/ADMIN/SUPERADMIN`
Res: `204`

### GET /hr/shifts/active
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Res: `GuardShiftItemDto[]`

### POST /hr/shifts/start
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Req: `StartGuardShiftRequestDto` (`guardUserId`, `guardGroupId?`, `securityPointId?`)
Res: `GuardShiftItemDto`

### POST /hr/shifts/end
Role: `HR/OPERATOR/ADMIN/SUPERADMIN`
Req: `EndGuardShiftRequestDto` (`guardUserId`)
Res: `204`

### GET /operator/forces
Role: `OPERATOR/ADMIN/SUPERADMIN`
Query: `search`, `availability`, `onlineOnly`
Res: `OperatorForceItemDto[]`

### GET /operator/points
Role: `OPERATOR/HR/ADMIN/SUPERADMIN`
Query: `search`, `type`, `includeInactive` (default: `false`)
Res: `OperatorPointItemDto[]`
- `OperatorPointItemDto`: `id`, `code`, `label`, `type`, `address`, `latitude?`, `longitude?`, `isActive`, `shiftStatus`, `activeForces`, `lastEventAtUtc`

### POST /operator/points
Role: `OPERATOR/HR/ADMIN/SUPERADMIN`
Req: `CreateSecurityPointRequestDto`
Res: `OperatorPointItemDto`
Validation:
- `latitude/longitude` передаются парой (оба либо отсутствуют)
- `latitude` в диапазоне `[-90..90]`
- `longitude` в диапазоне `[-180..180]`
- `address` нормализуется сервером (trim + collapse spaces + normalized commas)
- если `latitude/longitude` не переданы и включен geocoding в конфиге, сервер пытается заполнить координаты автоматически по `address`

### PUT /operator/points/{id}
Role: `OPERATOR/HR/ADMIN/SUPERADMIN`
Req: `UpdateSecurityPointRequestDto`
Res: `OperatorPointItemDto`
Validation:
- `code`, `label`, `address` обязательны
- `code` уникален
- `latitude/longitude` передаются парой и в валидных диапазонах
- `address` нормализуется сервером (trim + collapse spaces + normalized commas)
- если `latitude/longitude` не переданы и включен geocoding в конфиге, сервер пытается заполнить координаты автоматически по `address`

### POST /operator/points/{id}/toggle-active
Role: `OPERATOR/HR/ADMIN/SUPERADMIN`
Res: `204`

### GET /superadmin/settings
Role: `SUPERADMIN`
Query: `scope`
Res: `SuperAdminSettingItemDto[]`

### POST /superadmin/tariffs
Role: `SUPERADMIN`
Req: `UpsertBillingTariffRequestDto`
Res: `BillingTariffItemDto`

### PUT /superadmin/tariffs/{code}
Role: `SUPERADMIN`
Req: `UpsertBillingTariffRequestDto`
Res: `BillingTariffItemDto`
Business rules:
- Нельзя деактивировать тариф, если он назначен хотя бы одному клиенту.

### DELETE /superadmin/tariffs/{code}
Role: `SUPERADMIN`
Res: `204`
Business rules:
- Нельзя удалить тариф, если он назначен хотя бы одному клиенту.

### GET /superadmin/audit
Role: `SUPERADMIN`
Query: `search`
Res: `SuperAdminAuditItemDto[]`

### GET /superadmin/users
Role: `SUPERADMIN`
Query: `search`, `role`, `active`
Res: `BackofficeUserItemDto[]`

### POST /superadmin/users
Role: `SUPERADMIN`
Req: `CreateBackofficeUserRequestDto`
- `login`, `password`
- `email?`, `phone?`
- `roles[]` (`HR|OPERATOR|MANAGER|ACCOUNTANT|ADMIN|SUPERADMIN`)
Res: `BackofficeUserItemDto`

### POST /superadmin/users/{userId}/roles/add
Role: `SUPERADMIN`
Req: `ChangeBackofficeUserRoleRequestDto`
Res: `204`

### POST /superadmin/users/{userId}/roles/remove
Role: `SUPERADMIN`
Req: `ChangeBackofficeUserRoleRequestDto`
Res: `204`
Business rules:
- Нельзя снять последнюю роль пользователя.
- Нельзя снять у себя роль `SUPERADMIN`.
- Нельзя снять последнюю роль `SUPERADMIN` в системе.

### POST /superadmin/users/{userId}/toggle-active
Role: `SUPERADMIN`
Res: `204`
Business rules:
- Нельзя деактивировать собственную учетку.

### GET /admin/payments/imports
Role: `ADMIN/SUPERADMIN/ACCOUNTANT`
Res: `PaymentImportItemDto[]`

### GET /admin/payments/imports/{importId}/rows
Role: `ADMIN/SUPERADMIN/ACCOUNTANT`
Res: `PaymentImportRowItemDto[]`
