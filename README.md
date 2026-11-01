# SatbayevUniversity.Syllabus — Электронный силлабус + УМКД (API)

Бэкенд модуля «Электронный силлабус + учебно-методический комплекс дисциплины».
Силлабус формируется по форме **Ф КазНИТУ 703-08** (13 разделов шаблона кафедры), проходит
согласование «преподаватель → кафедра (протокол) → директор института» и выгружается в DOCX.
К каждому силлабусу прикрепляется УМКД: материалы по компонентам с проверкой заведующим кафедрой.

Клиент (Angular): `../SatbayevUniversity.Syllabus.Web.Client`.

## Стек
- ASP.NET Core 8 Web API, EF Core 8, **MS SQL Server 2022**
- ASP.NET Identity + **JWT** (роли: Admin, Director, HeadOfDepartment, Teacher, Student)
- DocumentFormat.OpenXml — генерация DOCX; Serilog — журналирование; Swagger
- xUnit + WebApplicationFactory (SQLite in-memory)

## Структура
```
src/SatbayevUniversity.Syllabus.Domain          сущности и перечисления
src/SatbayevUniversity.Syllabus.Infrastructure  EF Core (AppDbContext, миграции, начальные данные),
                                                 сервисы: AccessPolicy, SyllabusValidator, WorkflowService,
                                                 SyllabusService, UmkdService; экспорт SyllabusDocxExporter
src/SatbayevUniversity.Syllabus.Api             контроллеры, JWT, обработка ошибок, Program.cs
tests/SatbayevUniversity.Syllabus.Tests         модульные и интеграционные тесты
tests/system/api_system_test.py                 системный тест на поднятом стеке
```

## Запуск в Docker (рекомендуется)
```bash
cp .env.example .env   # задайте MSSQL_SA_PASSWORD, JWT_SIGNING_KEY, ADMIN_PASSWORD
docker compose up -d --build
```
Открыть http://localhost:8080 (порт — `WEB_PORT`). Сервисы: `mssql`, `api` (миграции применяются при старте),
`web` (nginx + Angular, проксирует `/api`). Тома: `mssql-data`, `umkd-files` (файлы УМКД), `api-logs`.
`SEED_DEMO_DATA=true` создаёт демо-данные: силлабус CSE7942 из шаблона и учётные записи
`teacher@ / head@ / director@ / student@ / admin@esyllabus.local` с паролем `Demo123!`.

Сборка за корпоративным прокси с TLS-инспекцией: `docker build --secret id=corp_ca,src=ca-bundle.crt .`

## Локальная разработка
```bash
dotnet run --project src/SatbayevUniversity.Syllabus.Api   # http://localhost:5080/swagger
dotnet test
dotnet tool restore && dotnet ef migrations add <Name> -p src/SatbayevUniversity.Syllabus.Infrastructure -s src/SatbayevUniversity.Syllabus.Api -o Data/Migrations
```
Строка подключения для разработки — в `appsettings.Development.json` (SQL Server на localhost:1433).

## Основные эндпоинты
| Метод | Путь | Назначение |
|---|---|---|
| POST | `/api/auth/login` | вход, выдача JWT (лимит 20 попыток/мин с IP → 429) |
| GET | `/api/dashboard`, `/api/reference` | рабочий стол; справочники для форм |
| GET/POST/PUT/DELETE | `/api/syllabi[/{id}]` | реестр, карточка, создание, правка, удаление черновика |
| GET | `/api/syllabi/new` | заготовка: типовые политики, 15 недель, график 60+40 |
| POST | `/api/syllabi/validate` | проверка по шаблону без сохранения |
| POST | `/api/syllabi/{id}/workflow` | Submit / Withdraw / DepartmentApprove / Return / Approve / Archive |
| POST | `/api/syllabi/{id}/clone` | копия на новый учебный год |
| GET | `/api/syllabi/{id}/docx` | силлабус в DOCX по форме Ф КазНИТУ 703-08 |
| GET/POST | `/api/syllabi/{id}/umkd` | состав УМКД; загрузка материала (файл до 50 МБ или ссылка) |
| POST | `/api/umkd/materials/{id}/review`, `/visibility` | проверка материала; видимость обучающимся |
| GET | `/api/syllabi/{id}/umkd/zip`, `/api/umkd/overview` | УМКД архивом; матрица комплектности |
| * | `/api/admin/*` | институты, кафедры, ОП, дисциплины, компоненты УМКД, настройки шаблона, пользователи |
| GET | `/health` | состояние API и БД |

## Ошибки и журналирование
- Все ошибки — `ProblemDetails` с русским `title` и `traceId` (по нему запись ищется в журнале).
  Валидация 400, вход 401, права 403, не найдено 404, 405, 409 (конфликт/уникальность), 413 (размер),
  415, 429 (перебор паролей), 503 (БД недоступна), 500 (прочее, без деталей исключения).
- Serilog: консоль контейнера + `logs/syllabus-YYYYMMDD.log` (30 дней, ротация по 50 МБ).
  Журнал каждого запроса (метод, путь, статус, время, пользователь, IP) и аудит действий:
  вход/неудачные попытки, создание/правка/удаление, шаги согласования, загрузка/проверка материалов.

## Тестирование
- `dotnet test` — 45 тестов: правила шаблона, права ролей, маршрут согласования, УМКД, валидность DOCX по схеме OpenXML,
  HTTP-сценарии всех ролей и ошибок (401/403/404/400/413/429).
- `python3 tests/system/api_system_test.py http://localhost:8080` — 52 проверки на поднятом стеке.
- UI E2E: `node e2e/system.e2e.mjs http://localhost:8080` в репозитории клиента.
