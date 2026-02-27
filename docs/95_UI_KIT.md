# 95_UI_KIT

Цель: единый UI-kit для всего проекта (`Chop.Web` + `Chop.App.Mobile`) на базе Tailwind.

## Как пользоваться
- Выбираем один `Theme Pack` (цвета + градиенты).
- Фиксируем один набор компонентов и состояний.
- После выбора переносим токены в `styles/tailwind.css` и используем только их.
- Для быстрого визуального просмотра без запуска сайта: `docs/96_UI_KIT_PREVIEW.html`.
- Текущий default в приложении: `Pack B` (`theme-b`).

## Переключение темы в приложении (без правок компонентов)
- Тема задается классом на `body`: `theme-a`, `theme-b`, `theme-c`.
- По умолчанию включена `theme-b`.
- В Web Console добавлен UI-переключатель в шапке (select `UI-kit A/B/C`).
- В рантайме можно переключить в браузере:
- `window.chopTheme.set('theme-a')`
- `window.chopTheme.set('theme-b')`
- `window.chopTheme.set('theme-c')`
- Выбор сохраняется в `localStorage` (`chop.ui.theme`).

## Theme Pack (варианты для выбора)

### Pack A: Slate + Cyan (операционный, спокойный)
- `bg`: `#0B1220`
- `surface-1`: `#111A2B`
- `surface-2`: `#182338`
- `text-primary`: `#E6EDF7`
- `text-secondary`: `#93A4BD`
- `primary`: `#22D3EE`
- `primary-strong`: `#0891B2`
- `success`: `#22C55E`
- `warning`: `#F59E0B`
- `danger`: `#EF4444`
- `info`: `#38BDF8`

Градиенты:
- `hero`: `from-cyan-500 to-blue-600`
- `success`: `from-emerald-500 to-teal-600`
- `warning`: `from-amber-500 to-orange-600`
- `critical`: `from-rose-500 to-red-700`

### Pack B: Graphite + Lime (контрастный, дежурный пульт)
- `bg`: `#0F1115`
- `surface-1`: `#171A21`
- `surface-2`: `#1E2430`
- `text-primary`: `#F3F5F7`
- `text-secondary`: `#A2ACBA`
- `primary`: `#84CC16`
- `primary-strong`: `#65A30D`
- `success`: `#22C55E`
- `warning`: `#F59E0B`
- `danger`: `#DC2626`
- `info`: `#38BDF8`

Градиенты:
- `hero`: `from-lime-500 to-emerald-600`
- `success`: `from-emerald-500 to-green-600`
- `warning`: `from-amber-500 to-orange-600`
- `critical`: `from-rose-500 to-red-700`

### Pack C: Navy + Amber (корпоративный, строгий)
- `bg`: `#0A1630`
- `surface-1`: `#10213F`
- `surface-2`: `#16305A`
- `text-primary`: `#F8FAFC`
- `text-secondary`: `#A7B7D1`
- `primary`: `#F59E0B`
- `primary-strong`: `#D97706`
- `success`: `#16A34A`
- `warning`: `#F59E0B`
- `danger`: `#DC2626`
- `info`: `#38BDF8`

Градиенты:
- `hero`: `from-amber-500 to-orange-600`
- `success`: `from-emerald-500 to-teal-600`
- `warning`: `from-amber-500 to-orange-600`
- `critical`: `from-rose-500 to-red-700`

## Базовые токены (предлагаемый стандарт)
- Радиусы:
- `control`: `10px`
- `card`: `14px`
- `modal`: `16px`
- Тени:
- `card`: `0 6px 20px rgba(0,0,0,0.12)`
- `overlay`: `0 12px 40px rgba(0,0,0,0.2)`
- Отступы:
- шаг `4px`
- базовый ритм `8 / 12 / 16 / 24`

## Компоненты UI-kit (MVP)

### 1. Buttons
- `btn-primary`
- `btn-secondary`
- `btn-outline`
- `btn-danger`
- `btn-ghost`
- Размеры: `sm / md / lg`
- Состояния: `default / hover / active / disabled / loading`

### 2. Inputs
- `input-text`
- `input-password`
- `select`
- `textarea`
- `checkbox`
- `radio`
- `switch`
- Состояния: `default / focus / error / disabled`

### 3. Layout
- `card`
- `panel`
- `table` (`th`, `td`, row states)
- `divider`
- `section-title`

### 4. Feedback
- `alert-info`
- `alert-success`
- `alert-warning`
- `alert-danger`
- `badge`
- `toast`
- `skeleton`
- `empty-state`

### 5. Navigation/Overlays
- `tabs`
- `pagination`
- `dropdown`
- `modal`
- `drawer`

### 6. Domain-specific
- `incident-status-chip`:
- `NEW`, `ACKED`, `DISPATCHED`, `ACCEPTED`, `EN_ROUTE`, `ON_SCENE`, `RESOLVED`, `CANCELED`
- `presence-chip`:
- `ONLINE`, `OFFLINE`
- `alert-severity-chip`:
- `INFO`, `WARN`, `CRITICAL`

## Typography
- Основной текст: `14px` (desktop), `15px` (mobile)
- Вторичный/табличный: `12-13px`
- Заголовки: `font-semibold`
- Технические идентификаторы: monospace

## Принципы
- Один source of truth: токены в Tailwind, без локальных "разовых" цветов.
- Компоненты переиспользуемые: если паттерн повторяется 2+ раза, выносим в `tw-*`.
- Без Bootstrap-возврата.
- Контраст не ниже WCAG AA для текстов интерфейса.
- Для dark-тем: не использовать напрямую `bg-*-50` + `text-ink-*` для интерактивных состояний.
- Для состояний/выделений использовать только `tw-*` классы или theme-bridge в `styles/tailwind.css`.

## Что нужно выбрать сейчас
1. `Theme Pack`: A / B / C
2. Плотность интерфейса: `compact` или `normal`
3. Стиль кнопок: `мягкие` (более rounded) или `строгие` (менее rounded)
4. По умолчанию для таблиц: `zebra` или `flat`

После выбора фиксируем финальную версию UI-kit и переходим к имплементации токенов.
