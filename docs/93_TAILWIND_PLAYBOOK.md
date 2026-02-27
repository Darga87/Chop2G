# 93_TAILWIND_PLAYBOOK

Цель: Tailwind как единый styling-слой для всех UI (Web Console + Mobile), без Bootstrap.

## Как это собрано
- Tailwind input: `styles/tailwind.css`
- Tailwind output:
  - Web: `src/Chop.Web/wwwroot/app.css`
  - Mobile: `src/Chop.App.Mobile/wwwroot/app.css`
- Сборка CSS:
  - `npm run build:css` (оба таргета)
  - `npm run watch:css:web`, `npm run watch:css:mobile`

Почему так: MAUI Blazor Hybrid и Web Console читают `wwwroot/app.css`, поэтому делаем 2 выходных файла из одного источника.

## Конвенции (чтобы не утонуть в utility-sprawl)
- Предпочитать `tw-*` компонентные классы в `@layer components` для повторяющихся паттернов:
  - `.tw-card`, `.tw-input`, `.tw-btn-*`, `.tw-alert-*`, `.tw-table`, `.tw-th`, `.tw-td`
- На страницах допустимы утилиты Tailwind, но:
  - не копировать большие наборы классов между файлами
  - если блок повторяется 2+ раза, вынести в `tw-*` или компонент Razor

## Критичные будущие риски и как их закрывать

### 1) Размер CSS (Tailwind bundle size)
Риск: output разрастается, влияет на Web performance и MAUI WebView.
Митигировать:
- Держать `content` globs в `tailwind.config.cjs` точными.
- Не генерировать классы динамически (например `class=$"text-{color}-500"`): Tailwind их не увидит.
- Если нужны динамические варианты: либо фиксированный whitelist, либо `safelist` (и документировать зачем).

### 2) Dynamic classnames (Blazor + Tailwind)
Риск: условные классы через строки и интерполяцию могут не попасть в build.
Митигировать:
- Делать ветвления через готовые классы: `condition ? "tw-alert-danger" : "tw-alert-success"`.
- Избегать построения tailwind-utility через переменные.
- При необходимости: `safelist` в `tailwind.config.cjs` с минимальным набором.

### 3) “Смешивание” UI-стэков (Bootstrap остатки)
Риск: возвращение Bootstrap/частей темы приведёт к непредсказуемым стилям.
Митигировать:
- Bootstrap CSS/JS не подключать.
- При code review: блокировать добавление `<link ...bootstrap...>` и `lib/bootstrap`.

### 4) Node/npm как build-зависимость
Риск: сборка UI будет требовать Node в CI/на машинах.
Митигировать:
- Для dev: команды `npm run build:css` документированы.
- Для CI: добавить step `npm ci && npm run build:css` (позже, отдельная задача).
- Опционально (позже): MSBuild target, но это усложняет чистую .NET сборку.

### 5) MAUI safe-area и WebView quirks
Риск: после генерации CSS потерять специфичные mobile-стили (safe-area).
Митигировать:
- Всё mobile-specific держать в `styles/tailwind.css` (как `.status-bar-safe-area`).
- Перед релизом Mobile: smoke test на iOS notch/Android status bar.

## Критерии “готово”
- Нет подключений Bootstrap.
- UI pages выглядят приемлемо с Tailwind (`tw-*`/utility classes).
- Tailwind CSS собирается одной командой и не ломает Mobile safe-area.

