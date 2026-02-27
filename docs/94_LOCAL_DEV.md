# 94_LOCAL_DEV

Минимальные шаги, чтобы проект запускался локально на Windows.

## 1) Postgres (обязательно для login/auth и большинства API)
Проект ожидает Postgres на `127.0.0.1:5432` и БД `chop2g` (user/pass: `postgres/postgres`).

Проверка:
```powershell
$env:PGPASSWORD='postgres'
psql -h 127.0.0.1 -p 5432 -U postgres -d postgres -w -c "select 1;"
psql -h 127.0.0.1 -p 5432 -U postgres -d postgres -w -c "select datname from pg_database where datname='chop2g';"
```

Если видишь `Connection refused (10061)`:
- Postgres не запущен или не слушает `5432`.

Если Postgres установлен как Windows service (частый кейс):
```powershell
Get-Service postgresql-x64-15
Start-Service postgresql-x64-15
```

Примечание: сразу после старта сервис может отвечать `ВАЖНО: система баз данных запускается` несколько секунд.

## 2) Запуск API
```powershell
cd src/Chop.Api
dotnet run --urls http://127.0.0.1:5261
```

## 3) Запуск Web Console
```powershell
cd src/Chop.Web
dotnet run --urls http://127.0.0.1:5001
```

## 4) Tailwind CSS (если меняешь стили)
```powershell
cd <repo-root>
npm run build:css
```

## Common issues
- **429 Too Many Requests** on login or guard ping:
  - API includes rate limiting for `/api/auth/*` and `/api/guard/location/ping`.
  - If you spam-click Login or run load tests, wait 60 seconds or increase limits in `src/Chop.Api/Program.cs` (dev-only).
