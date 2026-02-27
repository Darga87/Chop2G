# ERD — PostgreSQL + PostGIS

## 1. Общие принципы
- Все даты/время храним в UTC (`timestamptz`).
- Гео-точки храним как `geography(Point, 4326)` (или `geometry(Point,4326)` — но тогда аккуратно с расстояниями).
- Для быстрых “ближайших” используем GiST индекс по гео-полю.
- “Неизвестные поля” из банковского файла — сохраняем в JSONB.

## 2. Таблицы (минимум для MVP)

### 2.1 Identity/Users
#### users
- id (uuid, PK)
- email (text, unique, nullable)
- phone (text, unique, nullable)
- password_hash (text)
- is_active (bool)
- created_at (timestamptz)

#### user_roles
- user_id (uuid, FK users)
- role (text) // CLIENT, GUARD, OPERATOR, HR, ADMIN, SUPERADMIN
- PK (user_id, role)

### 2.2 Клиенты и адреса
#### clients
- id (uuid, PK)
- user_id (uuid, FK users, unique)
- full_name (text)
- primary_phone (text)
- contract_no (text, nullable)
- created_at (timestamptz)

#### client_addresses
- id (uuid, PK)
- client_id (uuid, FK clients)
- label (text) // "Дом", "Офис"...
- address_text (text)
- location (geography(Point,4326), nullable)
- is_primary (bool)
- created_at (timestamptz)

Индексы:
- idx_client_addresses_client_id
- idx_client_addresses_location_gist (GiST по location)

### 2.3 Охрана, посты, экипажи, смены
#### guards
- id (uuid, PK)
- user_id (uuid, FK users, unique)
- full_name (text)
- badge_no (text, unique)
- call_sign (text, nullable)
- radio_channel (text, nullable)
- phone (text, nullable)
- is_active (bool)

#### posts (стационарные)
- id (uuid, PK)
- name (text)
- address_text (text)
- location (geography(Point,4326))
- phones (text[] или jsonb)
- responsible_names (text[] или jsonb)
- radio_channel (text, nullable)
- is_active (bool)

Индексы:
- idx_posts_location_gist

#### patrol_units (экипажи)
- id (uuid, PK)
- name (text)
- vehicle_no (text, nullable)
- phone (text, nullable)
- call_sign (text, nullable)
- radio_channel (text, nullable)
- is_active (bool)

#### patrol_unit_members
- patrol_unit_id (uuid, FK patrol_units)
- guard_id (uuid, FK guards)
- PK (patrol_unit_id, guard_id)

#### shifts
- id (uuid, PK)
- guard_id (uuid, FK guards)
- post_id (uuid, FK posts, nullable)
- patrol_unit_id (uuid, FK patrol_units, nullable)
- started_at (timestamptz)
- ended_at (timestamptz, nullable)
- status (text) // ON_DUTY, OFF_DUTY

Правило:
- либо post_id, либо patrol_unit_id, либо оба null (если ещё не назначен) — но не оба одновременно (constraint).

### 2.4 Инциденты и диспетчеризация
#### incidents
- id (uuid, PK)
- client_id (uuid, FK clients)
- created_by_user_id (uuid, FK users)
- created_at (timestamptz)
- status (text) // см. STATE_MACHINE
- location (geography(Point,4326), nullable)
- address_snapshot (text, nullable) // строка адреса на момент события
- client_phone_snapshot (text, nullable)
- details (text, nullable) // кратко
- last_updated_at (timestamptz)

Индексы:
- idx_incidents_created_at
- idx_incidents_status
- idx_incidents_location_gist (если используете по инцидентам поиск)

#### incident_status_history
- id (uuid, PK)
- incident_id (uuid, FK incidents)
- from_status (text)
- to_status (text)
- actor_user_id (uuid, FK users, nullable) // null для системных
- actor_role (text, nullable)
- comment (text, nullable)
- created_at (timestamptz)

#### dispatches
- id (uuid, PK)
- incident_id (uuid, FK incidents)
- created_by_user_id (uuid, FK users)
- method (text) // RADIO, PHONE, APP, MIXED
- comment (text, nullable)
- created_at (timestamptz)

#### dispatch_recipients
- id (uuid, PK)
- dispatch_id (uuid, FK dispatches)
- recipient_type (text) // POST, PATROL_UNIT, GUARD
- recipient_id (uuid)
- distance_meters (int, nullable) // вычисленное расстояние на момент назначения
- accepted_by (text, nullable) // кто принял по рации/телефону
- accepted_at (timestamptz, nullable)
- accepted_via (text, nullable) // RADIO/PHONE/APP
- status (text) // SENT, ACCEPTED, DECLINED

Индексы:
- idx_dispatch_recipients_dispatch_id

### 2.5 Геопинги охранников
#### guard_location_pings
- id (uuid, PK)
- guard_id (uuid, FK guards)
- shift_id (uuid, FK shifts, nullable)
- incident_id (uuid, FK incidents, nullable) // если пинг в режиме активной заявки
- location (geography(Point,4326))
- accuracy_m (int, nullable)
- device_time (timestamptz, nullable)
- received_at (timestamptz)

Индексы:
- idx_guard_pings_guard_id_received_at
- idx_guard_pings_location_gist

### 2.6 Платежи и импорт банка
#### payments
- id (uuid, PK)
- client_id (uuid, FK clients, nullable) // может быть null до сопоставления
- amount (numeric(12,2))
- paid_at (date) // дата платежа
- purpose (text, nullable)
- payer_name (text, nullable)
- payer_inn (text, nullable)
- payer_account (text, nullable)
- receiver_account (text, nullable)
- external_id (text, nullable) // номер документа/платежа
- match_status (text) // AUTO_MATCHED, MANUAL_MATCHED, UNMATCHED
- created_at (timestamptz)

#### bank_imports
- id (uuid, PK)
- uploaded_by_user_id (uuid, FK users)
- filename (text)
- file_hash (text) // чтобы не загружать дубль
- imported_at (timestamptz)
- status (text) // PARSED, APPLIED, NEEDS_REVIEW, FAILED
- stats (jsonb) // counts
- raw_header (jsonb) // распарсенный заголовок

#### bank_import_rows
- id (uuid, PK)
- bank_import_id (uuid, FK bank_imports)
- row_no (int)
- doc_type (text, nullable)
- doc_no (text, nullable)
- doc_date (date, nullable)
- amount (numeric(12,2), nullable)
- payer_name (text, nullable)
- payer_inn (text, nullable)
- purpose (text, nullable)
- receiver_account (text, nullable)
- payer_account (text, nullable)
- extra (jsonb) // ВСЁ остальное ключ-значение из файла
- match_candidate_client_ids (uuid[], nullable)
- match_status (text) // AUTO, MANUAL, UNMATCHED
- matched_payment_id (uuid, FK payments, nullable)

## 3. Гео-запрос “ближайшие N”
- Делаем запрос по posts.location и по актуальным локациям экипажей/охранников (последний пинг не старше X минут).
- Храним координаты в geography и используем ST_Distance / KNN (через <->) в зависимости от типа.

## 4. Минимальные constraints
- shifts: нельзя одновременно post_id и patrol_unit_id (CHECK).
- incidents: status должен быть из enum/списка (CHECK или enum тип).
