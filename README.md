# SatbayevUniversity.Syllabus — Электронный силлабус + УМКД

Модуль «Электронный силлабус + учебно-методический комплекс дисциплины»: бэкенд, фронтенд и Docker-окружение в одном репозитории.
Силлабус формируется по форме **Ф КазНИТУ 703-08** (13 разделов шаблона кафедры), проходит
согласование «преподаватель → кафедра (протокол) → директор института» и выгружается в DOCX.
К каждому силлабусу прикрепляется УМКД: материалы по компонентам с проверкой заведующим кафедрой.


## Стек
- **Фронтенд:** Angular 19 (standalone-компоненты, signals), Bootstrap 5, nginx
- **Бэкенд:** ASP.NET Core 8 Web API, EF Core 8, **MS SQL Server 2022**
- ASP.NET Identity + **JWT** (роли: Admin, Director, HeadOfDepartment, Teacher, Student)
- DocumentFormat.OpenXml — генерация DOCX; Serilog — журналирование; Swagger
- xUnit + WebApplicationFactory (SQLite in-memory)

## Структура
```
docker-compose.yml        MS SQL + API + веб (nginx + Angular)
.env.example              пароли и ключи для docker compose (скопировать в .env)
backend/                  .NET-решение SatbayevUniversity.Syllabus.sln, Dockerfile
  src/SatbayevUniversity.Syllabus.Domain          сущности и перечисления
  src/SatbayevUniversity.Syllabus.Infrastructure  EF Core (AppDbContext, миграции, начальные данные),
                                                   сервисы: AccessPolicy, SyllabusValidator, WorkflowService,
                                                   SyllabusService, UmkdService; экспорт SyllabusDocxExporter
  src/SatbayevUniversity.Syllabus.Api             контроллеры, JWT, обработка ошибок, Program.cs
  tests/SatbayevUniversity.Syllabus.Tests         модульные и интеграционные тесты
  tests/system/api_system_test.py                 системный тест API на поднятом стеке
frontend/                 Angular-приложение, Dockerfile, nginx.conf
  src/app/core            API-сервис, JWT, перехватчик ошибок, логгер, guards
  src/app/pages           страницы: вход, рабочий стол, реестр, карточка, редактор, печать, УМКД, админка
  e2e/system.e2e.mjs      сквозной UI-тест (Playwright)
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
Бэкенд (каталог `backend/`):
```bash
cd backend
dotnet run --project src/SatbayevUniversity.Syllabus.Api   # http://localhost:5080/swagger
dotnet test
dotnet tool restore && dotnet ef migrations add <Name> -p src/SatbayevUniversity.Syllabus.Infrastructure -s src/SatbayevUniversity.Syllabus.Api -o Data/Migrations
```
Фронтенд (каталог `frontend/`):
```bash
cd frontend
npm ci
npx ng serve            # http://localhost:4200, /api проксируется на http://localhost:5080 (proxy.conf.json)
```
Строка подключения для разработки — в `appsettings.Development.json` (SQL Server на localhost:1433).

## Интерфейс (frontend)
- Вход по JWT; интерфейс зависит от роли (преподаватель, заведующий кафедрой, директор, обучающийся, администратор).
- Рабочий стол: счётчики по статусам, «Мои силлабусы», «Требуют вашего решения».
- Реестр силлабусов с фильтрами (поиск, год, статус, ОП) и прогрессом УМКД.
- Редактор силлабуса по 13 разделам формы Ф КазНИТУ 703-08: динамические таблицы (календарный план, литература,
  реквизиты, график работ), рамка компетенций 5×5, живой подсчёт баллов 60/40/100, проверка по шаблону на лету.
- Карточка: документ в виде формы шаблона, согласование (протокол кафедры, гриф «УТВЕРЖДАЮ»), история, копирование на новый год.
- Печать / PDF (A4) и скачивание DOCX.
- УМКД: загрузка файлов/ссылок по компонентам, проверка, видимость обучающимся, архив ZIP, матрица комплектности.
- Администрирование справочников, пользователей и текстов шаблона.

### Обработка ошибок на клиенте
`core/auth.interceptor.ts` добавляет токен и централизованно обрабатывает ошибки HTTP: 401 → выход и
«Сеанс истёк»; 0/403/404/409/413/429/5xx → уведомление на русском (для 5xx — с кодом обращения `traceId`).
`core/global-error-handler.ts` перехватывает прочие исключения, `core/logger.service.ts` пишет в консоль.

## Основные эндпоинты API
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
- `cd backend && dotnet test` — 45 тестов: правила шаблона, права ролей, маршрут согласования, УМКД, валидность DOCX по схеме OpenXML,
  HTTP-сценарии всех ролей и ошибок (401/403/404/400/413/429).
- `python3 backend/tests/system/api_system_test.py http://localhost:8080` — 52 проверки API на поднятом стеке.
- `cd frontend && node e2e/system.e2e.mjs http://localhost:8080` — 24 проверки интерфейса в браузере
  (нужен playwright: `npm i -D playwright && npx playwright install chromium`).
