# 98_RBAC_MATRIX_WEB

Цель: финальная матрица доступа Web Console по страницам, действиям и чувствительным полям.

Роли:
- `OPERATOR`
- `HR`
- `MANAGER`
- `ACCOUNTANT`
- `ADMIN`
- `SUPERADMIN`

## 1) Страницы (доступ)
| Раздел/страница | OPERATOR | HR | MANAGER | ACCOUNTANT | ADMIN | SUPERADMIN |
|---|---|---|---|---|---|---|
| `/operator/dashboard` | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/operator/incidents` | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/operator/incidents/{id}` | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/operator/forces` | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/operator/points` | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/operator/shifts` | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ |
| `/hr/guards` | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ |
| `/hr/groups` | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ |
| `/admin/clients` | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/manager/clients` | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ |
| `/admin/payments/import` | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| `/accountant/payments/import` | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| `/superadmin/settings` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `/superadmin/users` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| `/superadmin/audit` | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

## 2) Действия (CRUD/операции)
| Действие | OPERATOR | HR | MANAGER | ACCOUNTANT | ADMIN | SUPERADMIN |
|---|---|---|---|---|---|---|
| Создать/сменить статус инцидента | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Создать dispatch | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Управление alerts (`ack/resolve/assign/override`) | ✅ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Создание/редактирование охранников | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ |
| Управление группами охраны | ❌ | ✅ | ❌ | ❌ | ✅ | ✅ |
| Управление сменами | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ |
| Создание/редактирование клиентов | ❌ | ❌ | ❌ | ❌ | ✅ | ✅ |
| Просмотр клиентов (read-only) | ❌ | ❌ | ✅ | ❌ | ✅ | ✅ |
| Импорт платежей / apply / manual-match | ❌ | ❌ | ❌ | ✅ | ✅ | ✅ |
| Управление тарифами | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Управление пользователями и ролями | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| Toggle доступа к клиентскому PII | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |

## 3) Чувствительные поля (PII)
| Поле | OPERATOR | HR | MANAGER | ACCOUNTANT | ADMIN | SUPERADMIN |
|---|---|---|---|---|---|---|
| Телефоны клиента | Маска | ❌ | Маска (или full при `CanViewClientPii`) | ❌ | Full | Full |
| Email клиента | ❌ | ❌ | Скрыт (или full при `CanViewClientPii`) | ❌ | Full | Full |
| Контакты охранников | Ограничено контекстом операций | Full (кадровый контур) | ❌ | ❌ | Full | Full |

Примечания:
- `MANAGER` получает полный PII только при включенном флаге `users.CanViewClientPii`.
- `ACCOUNTANT` не должен видеть контур инцидентов/карт/охранников.
- `OPERATOR` не должен иметь доступ к созданию пользователей/клиентов/охранников.

## 4) UX-правила
- `401` -> redirect `/login`.
- `403` -> redirect `/forbidden`.
- Любые скрытые действия должны быть недоступны и на UI, и на API (двойная защита).
