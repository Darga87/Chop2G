# 94_LOCAL_DEV

Цель: быстрый и стабильный локальный запуск `API + Web`.

## Обязательные зависимости
- .NET SDK 9/10
- Node.js 20+ (для Tailwind)
- PostgreSQL (рекомендуемо 15+)
- PostGIS extension (для spatial-миграций)

## Порты по умолчанию
- API: `http://127.0.0.1:5261`
- Web: `http://127.0.0.1:5001`
- Postgres: `127.0.0.1:5432`

## База данных
Ожидаемые параметры dev:
- DB: `chop2g`
- user/password: `postgres/postgres`

Проверка:
```powershell
$env:PGPASSWORD='postgres'
psql -h 127.0.0.1 -p 5432 -U postgres -d postgres -w -c "select version();"
psql -h 127.0.0.1 -p 5432 -U postgres -d chop2g -w -c "select 1;"
```

Проверка PostGIS:
```powershell
psql -h 127.0.0.1 -p 5432 -U postgres -d chop2g -w -c "create extension if not exists postgis;"
psql -h 127.0.0.1 -p 5432 -U postgres -d chop2g -w -c "select postgis_full_version();"
```

## Запуск API
```powershell
dotnet run --project src/Chop.Api/Chop.Api.csproj --urls http://127.0.0.1:5261
```

## Запуск Web
```powershell
dotnet run --project src/Chop.Web/Chop.Web.csproj --urls http://127.0.0.1:5001
```

## Tailwind (локально)
```powershell
npm ci
npm run build:css
```

## Частые проблемы и решения

### 1) `Failed to connect to 127.0.0.1:5432` / `Connection refused (10061)`
Причина: PostgreSQL не запущен или неверный порт/host.  
Решение: запустить postgres-service, проверить `listen_addresses` и порт `5432`.

### 2) `extension "postgis" does not exist`
Причина: PostGIS не установлен в системе.  
Решение: установить PostGIS для вашей версии PostgreSQL и выполнить `create extension postgis`.

### 3) `CS2012 ... file is being used by another process`
Причина: lock на `obj/bin` (часто `VBCSCompiler` или другой `dotnet run`).  
Решение:
```powershell
taskkill /F /IM dotnet.exe
dotnet clean
```
После этого запустить проект снова.

### 4) `401 Unauthorized` в Web после refresh
Проверить:
- API действительно запущен на `5261`;
- Web указывает на правильный `Api:BaseUrl`;
- refresh token cookie не поврежден (выйти/войти заново).

### 5) `429 Too Many Requests` на login или guard ping
Причина: сработал rate limit (`/api/auth/*`, `/api/guard/location/ping`).  
Решение: подождать окно лимита или уменьшить частоту запросов.

### 6) Yandex Maps не загружается
Проверить:
- `Yandex:MapsApiKey` задан;
- ключ активен и разрешен для текущего домена/localhost;
- в браузере нет блокировки внешних скриптов.

## Мини-чек перед коммитом
1. `npm run build:css`
2. `dotnet build Chop2G.sln -c Release /p:UseSharedCompilation=false`
3. `dotnet test tests/Chop.Api.Tests/Chop.Api.Tests.csproj -c Release /p:UseSharedCompilation=false`
