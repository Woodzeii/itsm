# Анализ проекта ITSM NC

Снимок состояния репозитория на **2026-10-19**. Сверка выполнена с техническим заданием «ITSM NC» (полная поставка к 1 кв. 2027, без этапности). Наличие сущности, миграции или экрана не означает, что соответствующее требование ТЗ закрыто end-to-end.

## Кратко

Стек соответствует ТЗ (НФТ-03): **Angular**, **ASP.NET Core 9**, **PostgreSQL**, Docker Compose.

**Наиболее зрелые вертикали backend:** аутентификация (регистрация, подтверждение email, вход по BCrypt, email-код 2FA и TOTP), справочники, классы активов и атрибуты, **ядро Service Desk** (очередь заявок, создание/изменение, переходы статусов, назначение, ручная эскалация, переписка и вложения, тикеты на ремонт с привязкой к жизненному циклу актива, правила эскалации, руководитель службы поддержки, история заявок по активу).

**Frontend:** развёрнут каркас UI под все разделы ТЗ (очередь, активы, портал, админка, отчёты, тенанты и т.д.), маршруты защищены `authGuard` и role-guards. В **локальной dev-сборке** данные рабочих разделов по умолчанию идут из **`MockWorkspaceApiService`** (демо-данные); **`HttpWorkspaceApiService`** подключён только в **production**-сборке Angular и **не совпадает по контракту** с реализованными контроллерами (другие пути и HTTP-методы). Флаг `useMockWorkspaceApi` в `environment` сейчас **нигде не используется**.

**Каталог `openapi/`** описывает целевой полный API (в т.ч. ещё не реализованные модули); это контракт/ориентир, а не перечень работающих endpoint’ов.

---

## Структура и технологии

| Каталог | Назначение |
|---|---|
| `frontend/` | Angular standalone, Material/CDK, маршрутизация, JWT interceptor, сгенерированные типы из OpenAPI (`README-API-TYPES.md`) |
| `backend/itsm.WebApi` | Контроллеры, JWT, OpenAPI + Scalar (Development) |
| `backend/itsm.Application` | DTO, интерфейсы сервисов, FluentValidation |
| `backend/itsm.Domain` | Сущности, константы (этапы актива, типы движений, роли и т.д.) |
| `backend/itsm.Infrastructure` | EF Core + Npgsql, сервисы, BCrypt, TOTP/email 2FA, логирующая «почта» |
| `backend/itsm.Tests` | xUnit, Testcontainers PostgreSQL, интеграционные тесты `TicketService` |
| `openapi/` | Модульный сводный OpenAPI-контракт (целевая модель API) |
| `initdb/` | SQL-инициализация (volume в Compose **закомментирован**) |
| `docker-compose.yml` | PostgreSQL 16, backend, frontend |

При старте API в **не-Testing** окружении выполняется **`Database.Migrate()`** (pending-миграции применяются автоматически). В **Development** дополнительно вызывается **`DatabaseSeeder`** (тестовые пользователи, если БД пуста).

---

## Соответствие ТЗ (разделы 2–10)

Условные статусы: **Да** — рабочий сценарий есть в коде; **Частично** — модель/фрагмент логики/UI без полного покрытия ТЗ; **Нет** — не реализовано или только заготовка/контракт.

### 2. Учёт активов (АКТ-*)

| Требование | Статус | Комментарий |
|---|---|---|
| АКТ-01 Карточка актива CRUD | **Нет** (API) / **Частично** (UI) | Сущности `Asset`, `AssetMovement`, `AssetHistoryLog` в БД; экран `/assets` на mock-данных. **Контроллера `/api/assets` нет.** |
| АКТ-05 Классы и атрибуты | **Да** | `AssetClassesController`, UI `/asset-classes`. |
| АКТ-03–04, 10–14 Движение и жизненный цикл | **Частично** | Константы этапов и типов движений в домене; переходы **«в ремонт» / при закрытии ремонта** реализованы в `TicketService` при тикете типа `repair`. Полный CRUD движений и все триггеры ТЗ — **нет**. |
| АКТ-02, 15–16 История по активу | **Частично** | Запись истории при ремонтных тикетах; **нет API** просмотра карточки/истории/движений актива. |
| АКТ-06–09, 17–19 Остальное | **Частично / Нет** | Валидации идентификатора/склада на уровне карточки актива без API не проверяются end-to-end. |

### 3. Service Desk (SD-*)

| Требование | Статус | Комментарий |
|---|---|---|
| SD-01–02 Регистрация и очередь | **Частично** | Backend: `GET/POST /api/tickets`, очередь для `admin,agent`. UI очереди — mock или несовместимый HTTP-клиент. |
| SD-03, 08–09 Типы заявок | **Частично** | Типы и seed в БД; встроенный `repair`. **Нет API** администрирования типов (`/api/ticket-types` только в OpenAPI). |
| SD-04, 11–12 Критичность | **Частично** | Поле `Priority` / `CriticalityLevelId`; эскалация меняет уровень. **Нет API** настройки шкалы критичности. |
| SD-06, 15–17 Статусы и переходы | **Частично** | `ChangeStatusAsync` проверяет `TicketStatusTransitions` и роли; закрытие ремонта с исходом. **Нет API** настройки статусов/переходов; портальная «проверка» (SD-17) — **нет** backend для `/portal/.../review`. |
| SD-05, 18–23 SLA | **Нет / Частично** | Поля SLA на `Ticket`, seed графиков в БД; **нет расчёта дедлайнов**, таймеров, pausing по статусу в бизнес-логике, **нет** API `sla-settings`, `working-schedules`, `calendar-exceptions`. `SlaBreachNotifier` — заголовок-комментарий. |
| SD-13–14 Очередь и назначение | **Частично** | Фильтры в UI (mock); назначение `POST .../assign` на backend. Автораспределение — **нет**. |
| SD-24 Отчёты | **Нет** (API) / **Частично** (UI) | Экран отчётов в admin-workspace на mock; `/api/reports` не реализован. |

### 4. Эскалация (ESC-*)

| Требование | Статус | Комментарий |
|---|---|---|
| ESC-01 Настройка | **Частично** | `GET/PUT /api/escalation-rules/{ticketTypeId}`; UI политик — mock. |
| ESC-02, 04–08 Выполнение | **Частично** | Ручная эскалация: повышение критичности, уведомления в `Notifications`, запись `TicketEscalation`. **Нет** автоматической эскалации по нарушению SLA (ESC-03). |
| ESC-05 Руководитель | **Частично** | `GET/PUT /api/ticket-configuration/service-desk-head`. |

### 5. Конструктор форм (FORM-*)

| Требование | Статус | Комментарий |
|---|---|---|
| FORM-01–12 | **Нет / Частично** | `FormTemplate`, поля, `jsonb` custom fields на заявке; UI «Формы» в admin — mock. **Нет API** `/api/form-templates`, нет движка условной логики и валидации полей по схеме. |

### 6. История заявок по активу (HIST-*)

| Требование | Статус | Комментарий |
|---|---|---|
| HIST-01–06 | **Частично** | Связь `Ticket.AssetId`; **`GET /api/assets/{id}/tickets`** с фильтрами в `TicketsController`. Карточка актива с вкладками — **нет** (нет API актива). |

### 7. Портал (PORT-*)

| Требование | Статус | Комментарий |
|---|---|---|
| PORT-01–11 | **Частично** | Экран `/portal` и сценарии на mock; backend: ограничения при `CreateAsync` (только portal-available типы, не `repair`). **Нет** `/api/portal/*`, уведомлений, настройки видимости SLA на портале. |
| PORT-04 Учётные записи админом | **Нет** | `/api/users`, `admin-create-user` в OpenAPI, UI `admin/users` — без backend. |

### 8. Справочники (SPR-*)

| Требование | Статус | Комментарий |
|---|---|---|
| SPR-01–06 | **Да** (ядро) / **Частично** (склады/места) | CRUD пользовательских справочников реализован. Отдельные seed-справочники складов/мест — проверять наполнение миграций; UI `/dictionaries`. |

### 9. Роли (РОЛ-*)

| Требование | Статус | Комментарий |
|---|---|---|
| РОЛ-02–05 | **Частично** | Роли в JWT и guards (`admin`, `agent`, `manager`, `portal_user`, `tenant_admin`). Seeder создаёт пользователей с ролями. **Нет** полного RBAC на все операции ТЗ; **tenant_admin** и SaaS-тенанты — UI без API. |
| РОЛ-01 Админ тенантов | **Частично** | Маршрут `/admin/tenants`, mock API. |

### 10. Нефункциональные (НФТ-*)

| Требование | Статус | Комментарий |
|---|---|---|
| НФТ-01–02 SaaS / изоляция тенантов | **Нет** | `TenantId` на пользователях; мультитенантность не доведена. |
| НФТ-07 Парольные политики, блокировка | **Нет** | Сущность `PasswordPolicy`; API `/api/security-settings` не реализован. |
| НФТ-08 Журнал действий | **Нет** | `/api/audit-log` не реализован; есть `TicketAuditLog` по заявкам. |
| НФТ-10 MFA обязательна для admin/agent | **Нет** | TOTP опционален (`/api/auth/2fa/*`); принудительного включения нет. |
| НФТ-11 Уведомления только в системе | **Частично** | Запись `Notification` при эскалации; **нет** API списка/прочтения уведомлений. |
| НФТ-09 HTTPS | **Частично** | `UseHttpsRedirection` в API; типичный dev — HTTP на 5062/4200. |

---

## Frontend

### Маршруты (основные)

| Путь | Назначение | Данные |
|---|---|---|
| `/login` | Вход + 2FA | Dev: `MockAuthApiService` (`useMockAuth: true`); prod-сборка: HTTP `/api/auth` |
| `/dashboard` | Обзор | Mock workspace (dev) |
| `/tickets` | Очередь Service Desk | Mock workspace (dev) |
| `/assets` | Учёт активов | Mock |
| `/escalations` | Эскалации | Mock |
| `/portal` | Портал самообслуживания | Mock |
| `/admin/settings`, `/admin/forms`, `/admin/escalation-policies`, `/admin/reports` | Админ-рабочие места | Mock |
| `/admin/users` | Создание пользователей | Mock |
| `/admin/tenants` | Управление тенантами (SaaS) | Mock |
| `/dictionaries`, `/asset-classes` | Справочники, классы активов | **HTTP** к реальному API (`DictionaryApiService` / `AssetClassApiService` или workspace — см. ниже) |
| `/profile/security` | TOTP 2FA | HTTP `/api/auth/2fa/*` при реальном auth |

Guards: `authGuard`, `internalAccessGuard`, `portalAccessGuard`, `adminAccessGuard`, `adminOnlyAccessGuard`, `reportAccessGuard`, `tenantAdminAccessGuard`.

### Интеграция с API

- **`environment.apiBaseUrl`**: `http://localhost:5062/api` (Compose: backend на 5062).
- **Auth:** в dev по умолчанию mock; для полного цикла с backend нужно `useMockAuth: false` и пересборка.
- **Workspace:** dev → `workspace-api.provider.ts` → **Mock**; production build заменяет на `HttpWorkspaceApiService`, который вызывает, например:
  - `PATCH .../tickets/{id}/status` — backend ожидает **`PUT .../status`** с `statusId`;
  - `.../comments` — backend: **`.../messages`**;
  - `/api/settings`, `/api/ticket-types`, `/api/assets`, `/api/portal/*` — **контроллеров нет**.

Страницы **справочников** и **классов активов** historically используют отдельные API-сервисы к реализованным endpoint’ам; остальной продуктовый UI ориентирован на mock/несогласованный HTTP-слой.

---

## HTTP API (фактически реализовано)

Регистрация только через **`MapControllers()`**. Папка `Endpoints/` содержит **заготовки** (комментарии), маршруты не создают.

### `/api/auth`

| Метод | Назначение |
|---|---|
| `POST register`, `confirm-email` | Регистрация и подтверждение email (письмо через `LoggingEmailService` → лог) |
| `POST login` | BCrypt по пользователям БД; TOTP или email-код 2FA |
| `POST verify-2fa` | JWT с ролями |
| `POST/GET 2fa/setup`, `verify-setup`, `status`, `disable` | TOTP для текущего пользователя |

Mock LDAP **снят**; login не использует внешний AD.

### `/api/dictionaries`, `/api/asset-classes`

Полный CRUD по прежней модели (роли `admin`/`agent` на чтение, изменения в основном `admin`).

### `/api/tickets`

Очередь, просмотр, создание, обновление, смена статуса, назначение, ручная эскалация, сообщения, загрузка/скачивание вложений (до 10 МБ, `PhysicalTicketAttachmentStorage`).

### `/api/escalation-rules`, `/api/ticket-configuration/service-desk-head`

Настройка эскалации по типу и назначение руководителя SD.

### Не реализовано (но есть в `openapi/index.json`)

Assets, form-templates, reports, users/roles/security-settings, tenants, audit-log, notifications, SLA/working-schedules/calendar-exceptions, ticket-types/statuses/transitions, assignment-settings, criticality-levels, portal-маршруты и др.

---

## Доменная модель и БД

`ItsmDbContext` покрывает области ТЗ: пользователи и роли, тенанты, каталог услуг и формы, заявки (статусы, переходы, согласования, сообщения, вложения, аудит), SLA-настройки, эскалации, ITAM, справочники, уведомления, политики паролей и т.д.

**Сервисы с бизнес-логикой в Infrastructure:** `UserService`, `DictionaryService`, `AssetClassService`, **`TicketService`**.

Seed в миграциях: роли, типы заявок, статусы, переходы, критичность, SLA-заготовки, рабочие графики, тип `repair` и др.

---

## Запуск и конфигурация

- Compose: PostgreSQL **5433**, API **5062**, UI **4200**; backend читает `backend/.env` (секреты не коммитить).
- JWT key обязателен; строка подключения через `ConnectionStrings__DefaultConnection` или `POSTGRES_*`.
- Email и 2FA по email — **только для dev** (логи); для production нужен реальный `IEmailService`.
- `initdb` volume отключён; схема — **миграции EF** при старте API.

---

## Тесты

Последний прогон: **2026-10-19**, Docker доступен, команда из корня backend:

```powershell
cd backend
dotnet test
```

**Итог: 33 пройдено, 4 не пройдено, 37 всего, ~49 с.**

### Backend (`backend/itsm.Tests`)

| Стек | xUnit, Moq, FluentAssertions, `Microsoft.AspNetCore.Mvc.Testing`, **Testcontainers.PostgreSql** (`postgres:16-alpine`) |
|---|---|
| Окружение API в интеграции | `ASPNETCORE_ENVIRONMENT=Testing` (Serilog bootstrap отключён; миграции в фабрике) |
| Коллекции xUnit | **`Integration`** — один `CustomWebApplicationFactory` на все Web API-тесты; **`TicketServiceTestCollection`** — отдельный контейнер и **отдельная БД на каждый scope** (`TicketServiceFixture`) |

#### Покрытие по файлам

| Файл | Тестов | Назначение | Результат (Docker) |
|---|---:|---|---|
| `IntegrationTests/TicketServiceIntegrationTests.cs` | 16 | Создание/очередь/статусы/назначение/ремонт↔актив, сообщения и вложения, видимость portal vs staff, ручная эскалация, правила эскалации, история заявок по активу, руководитель SD | **16/16** |
| `RegistrationTests.cs` | 12 | `POST /api/auth/register`: валидация, дубликаты, email, слабый пароль, запись в БД, `RecordingEmailService` | **12/12** |
| `AuthTests.cs` | 5 | Login + verify-2fa (JWT, claims), неверный пароль/код, «просроченный» код (mock `ITwoFactorService`) | **1/5** |
| `UnitTests/Infrastructure/PhysicalTicketAttachmentStorageTests.cs` | 2 | Файловое хранилище вложений, защита от path traversal | **2/2** |
| `UnitTests/Infrastructure/ItsmDbContextTests.cs` | 1 | Схема модели: таблицы, ключи, `jsonb` | **1/1** |
| `UnitTest1.cs` | 1 | Пустой placeholder | **1/1** |

#### Что проверяют интеграционные тесты `TicketService` (ядро Service Desk)

- Создание заявки, начальный статус, audit `created`.
- Тикет на ремонт: только инженер, смена этапа актива «на складе / в эксплуатации → в ремонте», записи `AssetHistoryLog`.
- Закрытие ремонта с исходом (эксплуатация / склад / списание) и обязательным текстом работ.
- Переходы статусов по `TicketStatusTransitions` и ролям; назначение только активным `agent`.
- Очередь (сортировка по дате), обновление priority / custom fields / снятие исполнителя.
- Вложения: одноразовое использование upload token, права portal vs staff, внутренние сообщения.
- Ручная эскалация: повышение критичности, `Notification`, учёт правила `OnManual`; фильтры истории заявок по активу.
- CRUD-логика руководителя SD через сервис (без HTTP-контроллера в тесте).

#### Не пройденные тесты (4) — `AuthTests`

| Тест | Ожидание | Факт | Вероятная причина |
|---|---|---|---|
| `Login_Then_Verify2Fa_ReturnsJwt` | Login 200 | **401** | В `CustomWebApplicationFactory` после `Migrate()` **нет пользователя `admin`/`admin123`**. `DatabaseSeeder` в Testing не вызывается; тесты рассчитывают на seeder dev, которого в контейнере нет. |
| `Verify2Fa_ReturnsJwtWithRequiredClaims` | JWT с claims | **ArgumentNullException** (пустой token) | Следствие неуспешного login/verify выше. |
| `Verify2Fa_WithWrongCode_Returns401_AndNoJwt` | Verify **401** | **400** | Login не создаёт пользователя → `UserId` некорректен → валидация `verify-2fa` (`UserId`/`Code`) отдаёт 400 до mock 2FA. |
| `Verify2Fa_WithExpiredCode_Returns401` | Verify **401** | **400** | То же. |

**Пройден:** `Login_WithWrongPassword_Returns401_AndNoSession` — не требует существующего пользователя с верным паролем.

**Рекомендация по починке:** в `ConfigureWebHost` фабрики после миграции вызвать `IDatabaseSeeder` или создать пользователя `admin` с BCrypt-хешем `admin123` и ролью `admin`; либо регистрировать пользователя в `@Before` каждого auth-теста. Дополнительно имеет смысл замокать `ITotpService`, если тесты должны идти только через email-код.

#### Пробелы тестового покрытия (относительно кода и ТЗ)

- **Нет** HTTP-тестов контроллеров `Dictionaries`, `AssetClasses`, `Tickets`, `EscalationRules`, `TicketConfiguration` (кроме register/auth через WebApplicationFactory).
- **Нет** тестов SLA (расчёт дедлайнов, pausing, breach), assets API, форм, portal API, TOTP (`/api/auth/2fa/*`).
- **`UnitTest1`** — заглушка без assertions; можно удалить или заменить осмысленным unit-тестом.

#### Запуск без Docker

Если Docker не запущен, **33+** тестов падают на создании `PostgreSqlContainer` (Testcontainers). Без контейнера стабильно проходят только unit-тесты, не требующие БД (`PhysicalTicketAttachmentStorageTests`, `UnitTest1`).

### Frontend

- Один файл **`src/app/app.spec.ts`** (smoke Angular). E2E (Cypress/Playwright) и тесты workspace/mock HTTP **отсутствуют**.

---

## Приоритетные пробелы (относительно ТЗ)

1. **Согласовать frontend с backend:** переключить dev на HTTP или единый клиент; привести `HttpWorkspaceApiService` к реальным маршрутам и DTO (`TicketResponse`, numeric id, `statusId`, `messages` и т.д.).
2. **Учёт активов (раздел 2):** `AssetsController` + сервис (карточка, движения, история, все триггеры жизненного цикла).
3. **Администрирование SD (раздел 3):** типы, статусы, переходы, критичность, SLA, графики, назначение (auto/manual), отчёты.
4. **SLA и эскалация по SLA (SD-18–23, ESC-03):** расчёт дедлайнов, pausing, фоновый мониторинг (`SlaBreachNotifier` / hosted service).
5. **Конструктор форм (раздел 5):** API + применение схемы при создании/редактировании заявки.
6. **Портал (раздел 7):** API «только свои заявки», переписка, review, вложения, in-app уведомления.
7. **Идентичность и SaaS (разделы 7, 9–10):** users API, политики паролей, audit log, обязательный TOTP для admin/agent, мультитенантность.
8. **Production hardening:** реальная почта, HTTPS, отказ от dev-only 2FA в логах.

---

## Изменения с предыдущего снимка (2026-10-08)

| Область | Было | Стало |
|---|---|---|
| Заявки | Только модель БД | `TicketService`, `TicketsController`, тесты, ремонт ↔ актив |
| Auth | Mock LDAP | BCrypt + TOTP + email 2FA |
| Миграции БД | Вручную | Авто при старте API |
| Frontend | 4 экрана, без guards | Полный каркас UI по ТЗ, guards, mock workspace |
| OpenAPI | Только Scalar с WebApi | Дополнительно модульный контракт в `openapi/` |
| Эскалация / SD head | Нет | API правил и руководителя |

---

*Источник требований: техническое задание «ITSM NC» (Nocode), файл `ТЗ_ITSM_NC (1).docx`.*
