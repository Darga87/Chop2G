# AGENTS.md — CHOP platform (MAUI + Blazor + Postgres/PostGIS)

## Цель
Строим систему ЧОП:
- Web: Blazor Web App (Оператор/Админ/Кадровик/Суперадмин)
- Mobile: .NET MAUI Blazor Hybrid (Клиент/Охранник)
- Backend: ASP.NET Core API + SignalR
- DB: PostgreSQL + PostGIS

## Источники истины (обязательно читать перед изменениями)
- docs/00_PRD.md
- docs/01_STATE_MACHINE.md
- docs/02_ERD.md
- docs/03_API.md
- docs/90_DECISIONS.md

Если в коде/запросе конфликт с документами — следуй документам и обнови их при необходимости.

## Рабочие правила
- Делай небольшие, проверяемые изменения (1 задача = 1 логически связанный набор правок).
- Всегда обновляй/добавляй тесты, если меняешь поведение.
- После правок запускай:
  - dotnet format (если настроено)
  - dotnet test
- Не добавляй новых внешних зависимостей без явного запроса.
- Не добавляй секреты/ключи в репозиторий. Любые секреты — через env/secret manager.

## Архитектура (обязательное)
- Решение: Chop.App.Mobile, Chop.Web, Chop.Api, Chop.Domain, Chop.Application, Chop.Infrastructure, Chop.Shared
- Domain не зависит от Infrastructure.
- DTO/контракты — в Chop.Shared.
- Гео-поиск ближайших — через PostGIS, не “вручную”.

## Definition of Done для любой задачи
- Код компилируется
- Тесты проходят
- Нет явных TODO/заглушек на критических местах
- Документация (docs/*) обновлена, если менялись контракты/сущности/правила
- Для миграций БД: добавлена миграция + краткая заметка в docs/02_ERD.md
