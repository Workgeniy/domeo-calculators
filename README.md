# domeo-calculators

Разбор калькуляторов с calcal.ru и подготовка к переносу их логики в Domeo.

## Что здесь

| Файл | О чём |
|---|---|
| [docs/PLAN.md](docs/PLAN.md) | План работ, статусы, развилка и риски |
| [docs/calc-lighting.md](docs/calc-lighting.md) | Калькулятор освещённости: входы, формула, справочники |
| [docs/calc-outlets.md](docs/calc-outlets.md) | Калькулятор розеток и выключателей: то же самое |
| [docs/parsing-notes.md](docs/parsing-notes.md) | Как это вообще извлекается и почему не через DOM |
| [data/lighting.json](data/lighting.json) | Справочники освещённости в машиночитаемом виде |
| [data/outlets.json](data/outlets.json) | Пресеты комнат и прайсы электрофурнитуры |
| `src/Domeo.Calculators` | Порт обоих калькуляторов на C# |
| `tests/Domeo.Calculators.Tests` | Golden-тесты: сверка порта с оригиналом |
| `src/Domeo.Calculators.Web` | Локальный сервер: API + страница |
| [deploy/README.md](deploy/README.md) | Развёртывание на стенде и откат |
| `tools/golden/generate.mjs` | Снятие эталона с оригинального кода через Node |

## Коротко по итогам разведки

Оба калькулятора — клиентские React-компоненты. **API нет, форм нет, сетевых запросов при
расчёте нет.** Вся арифметика и все справочные таблицы зашиты в постраничный JS-чанк
Next.js, оттуда и извлечены.

Парсить по HTML-тегам нечего: у инпутов нет `name`/`id`, варианты выбора — это `<button>`
с различием только в Tailwind-классах. Подробности и устойчивая процедура извлечения —
в [parsing-notes.md](docs/parsing-notes.md).

Объём логики небольшой: ~20 строк арифметики и 3–4 таблицы на калькулятор. Обе формулы
выписаны дословно и лежат в спеках.

## Тестовый стенд

**https://testdesign.domeohome.ru/calc/** — открыт, без авторизации.
Живёт на ВМ `domeo-testdesign` рядом с backend, подробности и откат — в
[deploy/README.md](deploy/README.md).

## Запуск локально

```bash
dotnet run --project src/Domeo.Calculators.Web
```

Дальше — http://localhost:5099. Считает библиотека на сервере, страница только рисует:
своей копии формул и справочников у неё нет, всё приходит через API.

| Эндпоинт | Что делает |
|---|---|
| `GET /api/reference` | Справочники и дефолтное состояние комнат |
| `POST /api/lighting` | Расчёт освещённости |
| `POST /api/electrical` | Комплектация и смета электрики |
| `POST /api/report/pdf` | Отчёт A4 в PDF по обоим калькуляторам |

Консольный прогон без браузера:

```bash
dotnet run --project samples/Domeo.Calculators.Demo
```

## Сборка и тесты

```bash
dotnet test Domeo.Calculators.sln
```

Эталон пересобирается только после правки `data/*.json`:

```bash
node tools/golden/generate.mjs
```

## Статус

Разведка и порт закрыты: обе формулы перенесены на C# и сверены с оригиналом —
**296 тестов зелёные, сборка без предупреждений**. Порт намеренно точный, вместе со всеми
особенностями оригинала; они перечислены в спеках и снабжены комментариями в коде.

Дальше — решение, куда калькуляторы встраиваются, и правки модели под реальную смету.
См. [PLAN.md](docs/PLAN.md).

## Источники

- https://calcal.ru/osveshchyonnost-pomeshcheniya
- https://calcal.ru/outlets-switches-calculator

Снято 19.08.2026. Имена чанков — хеши содержимого и меняются при любой правке страницы.
