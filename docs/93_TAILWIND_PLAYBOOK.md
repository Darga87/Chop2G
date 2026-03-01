# 93_TAILWIND_PLAYBOOK

Цель: Tailwind как единый слой стилизации для Web Console и Mobile, без Bootstrap.

## Сборка CSS
- Input: `styles/tailwind.css`
- Output:
- Web: `src/Chop.Web/wwwroot/app.css`
- Mobile: `src/Chop.App.Mobile/wwwroot/app.css`
- Команды:
- `npm run build:css`
- `npm run watch:css:web`
- `npm run watch:css:mobile`

## Правила
- Повторяющиеся UI-паттерны выносить в `tw-*` классы и общие Razor-компоненты.
- Не генерировать Tailwind-классы динамически через строки.
- `safelist` использовать только при реальной необходимости и документировать причину.

Текущее состояние:
- `tailwind.config.cjs` -> `safelist: []` (runtime-генерации классов не обнаружено).

## CI hardening
- Anti-bootstrap guard: `scripts/ci/check-no-bootstrap.ps1`
- CSS budget: `scripts/ci/check-css-budget.ps1`
- Workflow: `.github/workflows/tailwind-css.yml`
- Порядок:
1. `npm ci`
2. `npm run build:css`
3. `check-no-bootstrap.ps1`
4. `check-css-budget.ps1`
5. Проверка, что сгенерированный CSS закоммичен

## Perf budget (актуальный)
- `src/Chop.Web/wwwroot/app.css` <= 128 KB
- `src/Chop.App.Mobile/wwwroot/app.css` <= 128 KB

## Регулярный контроль производительности CSS
- В CI: budget проверяется на каждом `push/pull_request`.
- Локально перед merge:
1. `npm run build:css`
2. `powershell -ExecutionPolicy Bypass -File scripts/ci/check-css-budget.ps1 -MaxWebKb 128 -MaxMobileKb 128`
- Периодический контроль (раз в 2 недели):
- фиксировать фактический размер двух `app.css`,
- если рост > 15% за период, проводить аудит новых utility/component слоёв.

## Запрет на возврат Bootstrap
- Нельзя подключать Bootstrap CSS/JS и `wwwroot/lib/bootstrap`.
- Любое временное исключение только через документированное решение в `docs/90_DECISIONS.md` с датой и планом удаления.

## Критерии готовности
- Bootstrap отсутствует в runtime.
- CSS проходит budget-проверку в CI.
- Tailwind-стили одинаково корректно работают в Web и Mobile.
