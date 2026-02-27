# API Contract (REST + SignalR)

Р‘Р°Р·РѕРІС‹Р№ URL: /api
Р’РµСЂСЃРёСЏ: v1 (РїРѕРєР° Р±РµР· РїСЂРµС„РёРєСЃР°, РЅРѕ Р·Р°РєР»Р°РґС‹РІР°РµРј РІРѕР·РјРѕР¶РЅРѕСЃС‚СЊ)

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
РќР°Р·РЅР°С‡РµРЅРёРµ: РїРµСЂРІС‹Р№ РІС…РѕРґ РїРѕ РїСЂРёРіР»Р°С€РµРЅРёСЋ, СѓСЃС‚Р°РЅРѕРІРєР° РЅРѕРІРѕРіРѕ РїР°СЂРѕР»СЏ.
Req: { invitationToken, newPassword }
Res: { accessToken, refreshToken, user }

## 2. Clients
### GET /clients/me
Р РѕР»СЊ: CLIENT
Res: ClientProfileDto

### PUT /clients/me
Р РѕР»СЊ: CLIENT
Req: UpdateClientProfileDto (РѕРіСЂР°РЅРёС‡РµРЅРЅС‹Р№ РЅР°Р±РѕСЂ)
Res: ClientProfileDto

### GET /admin/clients
Р РѕР»СЊ: ADMIN/SUPERADMIN
Query: search, page, pageSize
Res: Paged<ClientListItemDto>

### POST /admin/clients
РЎРѕР·РґР°С‚СЊ РєР»РёРµРЅС‚Р° Рё РѕС‚РїСЂР°РІРёС‚СЊ РїСЂРёРіР»Р°С€РµРЅРёРµ.
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
РћР±РЅРѕРІРёС‚СЊ РїСЂРѕС„РёР»СЊ РєР»РёРµРЅС‚Р° (Р°РґРјРёРЅ РєРѕРЅС‚СѓСЂ).
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
Р РѕР»СЊ: HR/ADMIN/SUPERADMIN
Res: list

### POST /hr/guards
Req: CreateGuardDto
Res: { guardId, invitationLinkOrToken }

### POST /hr/shifts/start
Role: `HR/ADMIN/SUPERADMIN`
Req: `StartGuardShiftRequestDto` (`guardUserId`, `guardGroupId?`, `securityPointId?`)
Res: `GuardShiftItemDto`

### POST /hr/shifts/end
Role: `HR/ADMIN/SUPERADMIN`
Req: `EndGuardShiftRequestDto` (`guardUserId`)
Res: 204

## 4. Incidents (SOS)
### POST /incidents
Р РѕР»СЊ: CLIENT
Header (optional): `Idempotency-Key: <string>`
Req: CreateIncidentDto
- clientLocation { lat, lon, accuracyM? }
- deviceTimeUtc?
- addressText? (РµСЃР»Рё РµСЃС‚СЊ)
Res: IncidentDto + ACK (incidentId)
РџРѕРІРµРґРµРЅРёРµ:
- РµСЃР»Рё С‚РѕС‚ Р¶Рµ `Idempotency-Key` СѓР¶Рµ РёСЃРїРѕР»СЊР·РѕРІР°Р»СЃСЏ СЌС‚РёРј РєР»РёРµРЅС‚РѕРј, СЃРµСЂРІРµСЂ РІРѕР·РІСЂР°С‰Р°РµС‚ С‚РѕС‚ Р¶Рµ `incidentId`, РЅРѕРІС‹Р№ РёРЅС†РёРґРµРЅС‚ РЅРµ СЃРѕР·РґР°С‘С‚СЃСЏ;
- РµСЃР»Рё С‚РѕС‚ Р¶Рµ `Idempotency-Key` РёСЃРїРѕР»СЊР·РѕРІР°РЅ СЃ РґСЂСѓРіРёРј payload, СЃРµСЂРІРµСЂ РІРѕР·РІСЂР°С‰Р°РµС‚ `409 Conflict` (`IDEMPOTENCY_KEY_REUSED_WITH_DIFFERENT_PAYLOAD`);
- Р±РµР· РєР»СЋС‡Р° РґРµР№СЃС‚РІСѓРµС‚ server-side РґРµРґСѓРїР»РёРєР°С†РёСЏ: РїСЂРё Р°РєС‚РёРІРЅРѕРј РёРЅС†РёРґРµРЅС‚Рµ РєР»РёРµРЅС‚Р° РІ РѕРєРЅРµ 60 СЃРµРєСѓРЅРґ (`NEW/ACKED/DISPATCHED/ACCEPTED/EN_ROUTE/ON_SCENE`) РІРѕР·РІСЂР°С‰Р°РµС‚СЃСЏ СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёР№ `incidentId`.
- Р·Р°РїРёСЃРё idempotency С…СЂР°РЅСЏС‚СЃСЏ РѕРіСЂР°РЅРёС‡РµРЅРЅРѕРµ РІСЂРµРјСЏ (TTL, 24 С‡Р°СЃР°), Р·Р°С‚РµРј РѕС‡РёС‰Р°СЋС‚СЃСЏ С„РѕРЅРѕРІС‹Рј РїСЂРѕС†РµСЃСЃРѕРј.

### GET /operator/incidents
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Query: status, from, to, page, pageSize
Res: Paged<IncidentListItemDto>

### GET /operator/incidents/{id}
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Res: IncidentDetailsDto
- core: id, status, createdAt, lastUpdatedAt, location?, addressSnapshot?
- client: { fullName, phones[] }
- addresses[]: { label, address, location? }
- history[]: { fromStatus?, toStatus, actorUserId, actorRole, comment?, createdAt }

### POST /operator/incidents/{id}/dispatch
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Req: CreateDispatchDto
- method
- recipients[]: { type: POST|PATROL_UNIT|GUARD, id }
- comment?
Res: DispatchDto

### POST /operator/incidents/{id}/status
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Req: ChangeIncidentStatusDto
- toStatus
- comment (РѕР±СЏР·Р°С‚РµР»РµРЅ РґР»СЏ СЂСѓС‡РЅРѕР№ СЃРјРµРЅС‹; СЃРµСЂРІРµСЂ РІР°Р»РёРґРёСЂСѓРµС‚ РїРѕ STATE_MACHINE)
Res: IncidentDto

### POST /guard/incidents/{id}/accept
Р РѕР»СЊ: GUARD
Req: { comment? }
Res: IncidentDto

### POST /guard/incidents/{id}/progress
Р РѕР»СЊ: GUARD
Req: { toStatus: EN_ROUTE|ON_SCENE|RESOLVED, comment? }
- `comment` РѕР±СЏР·Р°С‚РµР»РµРЅ РїСЂРё `toStatus=RESOLVED`.
Res: IncidentDto

## 5. Geo / nearest forces
### GET /operator/incidents/{id}/nearest
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Query: limitPosts=5, limitUnits=5
Res:
{
  posts: [{id,name,distanceMeters,phone,radioChannel,responsibles[]}],
  patrolUnits: [{id,name,distanceMeters,phone,radioChannel,lastLocationAgeSeconds}]
}

## 6. Guard location pings
### POST /guard/location/ping
Р РѕР»СЊ: GUARD
Req: GuardLocationPingDto
- lat, lon, accuracyM?
- deviceTimeUtc?
- shiftId?
- incidentId? (РµСЃР»Рё Р°РєС‚РёРІРЅР°СЏ Р·Р°СЏРІРєР°)
Res: 204

## 6.1 Alerts (Operator)
### GET /operator/incidents/{id}/alerts
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Query: includeResolved=false
Res: AlertListItemDto[]

### POST /operator/alerts/{alertId}/ack
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Req: { comment? }
Res: 204

### POST /operator/alerts/{alertId}/resolve
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Req: { comment? }
Res: 204

### POST /operator/alerts/{alertId}/assign
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
Req: { assigneeUserId?, comment? }
Res: 204

### POST /operator/alerts/{alertId}/override
Р РѕР»СЊ: OPERATOR/ADMIN/SUPERADMIN
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
Р РѕР»СЊ: ADMIN/SUPERADMIN
Body: multipart/form-data (file)
Res: { importId, status, stats }

### GET /admin/payments/imports/{importId}
Res: ImportDetailsDto + rows paged

### POST /admin/payments/imports/{importId}/apply
РџСЂРёРјРµРЅРёС‚СЊ Р°РІС‚Рѕ-СЃРѕРїРѕСЃС‚Р°РІР»РµРЅРЅС‹Рµ РїР»Р°С‚РµР¶Рё.
Req: {}
Res: 204

### POST /admin/payments/imports/{importId}/rows/{rowId}/match
Р СѓС‡РЅРѕРµ СЃРѕРїРѕСЃС‚Р°РІР»РµРЅРёРµ СЃРїРѕСЂРЅРѕР№ СЃС‚СЂРѕРєРё.
Req: { clientUserId? , clientDisplayName? } // хотя бы одно поле
Res: 204

## 8. DTO (РєСЂР°С‚РєРѕ)
### IncidentDto
- id, status, createdAt, location?, addressSnapshot?, clientSummary, lastUpdatedAt

### IncidentDetailsDto
- id, status, createdAt, lastUpdatedAt
- location?, addressSnapshot?
- client: { fullName, phones[] }
- addresses[]: { label, address, location?, isPrimary }
- history[]

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
Auth: JWT, СЂРѕР»Рё `OPERATOR/ADMIN/SUPERADMIN`.

Р“СЂСѓРїРїС‹:
- `ops:*` вЂ” РѕР±С‰РёР№ РїРѕС‚РѕРє РґР»СЏ РїСѓР»СЊС‚Р°.
- `role:OPERATOR`, `role:ADMIN`, `role:SUPERADMIN` вЂ” role-РіСЂСѓРїРїС‹ РґР»СЏ РјР°СЂС€СЂСѓС‚РёР·Р°С†РёРё.

РЎРѕР±С‹С‚РёСЏ MVP:
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
Role: `HR/ADMIN/SUPERADMIN`
Query: `search`, `status`, `onShiftOnly`
Res: `GuardItemDto[]`

### POST /hr/guards/{guardId}/toggle-active
Role: `HR/ADMIN/SUPERADMIN`
Res: `204`

### GET /hr/groups
Role: `HR/ADMIN/SUPERADMIN`
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
Role: `HR/ADMIN/SUPERADMIN`
Res: `GuardShiftItemDto[]`

### POST /hr/shifts/start
Role: `HR/ADMIN/SUPERADMIN`
Req: `StartGuardShiftRequestDto` (`guardUserId`, `guardGroupId?`, `securityPointId?`)
Res: `GuardShiftItemDto`

### POST /hr/shifts/end
Role: `HR/ADMIN/SUPERADMIN`
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

### POST /operator/points
Role: `OPERATOR/HR/ADMIN/SUPERADMIN`
Req: `CreateSecurityPointRequestDto`
Res: `OperatorPointItemDto`
Validation:
- `latitude/longitude` передаются парой (оба либо отсутствуют)
- `latitude` в диапазоне `[-90..90]`
- `longitude` в диапазоне `[-180..180]`

### PUT /operator/points/{id}
Role: `OPERATOR/HR/ADMIN/SUPERADMIN`
Req: `UpdateSecurityPointRequestDto`
Res: `OperatorPointItemDto`
Validation:
- `code`, `label`, `address` обязательны
- `code` уникален
- `latitude/longitude` передаются парой и в валидных диапазонах

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
