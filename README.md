# CarRentalSystem

![.NET 8](https://img.shields.io/badge/.NET%208-512BD4?style=flat&logo=dotnet&logoColor=white)
![React 19](https://img.shields.io/badge/React%2019-20232A?style=flat&logo=react&logoColor=61DAFB)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=flat&logo=postgresql&logoColor=white)

Технічний master-document для репозиторію системи оренди автомобілів.

## Зміст

- [1. Що це за система](#1-що-це-за-система)
- [2. Актуальний стан проєкту](#2-актуальний-стан-проєкту)
- [3. Склад рішення](#3-склад-рішення)
- [4. Функціональність по підсистемах](#4-функціональність-по-підсистемах)
- [5. Ролі й доступ](#5-ролі-й-доступ)
- [6. Дані, БД і файлові ресурси](#6-дані-бд-і-файлові-ресурси)
- [7. Запуск проєкту](#7-запуск-проєкту)
- [8. Конфігурація](#8-конфігурація)
- [9. HTTP API surface](#9-http-api-surface)
- [10. Тести](#10-тести)
- [11. Додаткова документація](#11-додаткова-документація)

## 1. Що це за система

`CarRentalSystem` — інформаційна система для прокату автомобілів з трьома точками входу:

- `CarRental.Desktop` — WPF desktop-застосунок для операційної роботи і desktop self-service режиму;
- `CarRental.WebApi` — ASP.NET Core 8 Web API;
- `CarRental.WebApp` — React + TypeScript frontend для клієнтського self-service і staff/admin web mode.

Система покриває:

- авторизацію і реєстрацію;
- каталог автопарку;
- клієнтські профілі;
- бронювання, активні оренди, закриття, скасування і перенесення;
- платежі та баланс договору;
- пошкодження і техобслуговування;
- звіти;
- адміністрування співробітників.

## 2. Актуальний стан проєкту

Поточна архітектура репозиторію:

- `Desktop + Web API + React WebApp`;
- спільна PostgreSQL база даних для desktop і web;
- авторитетне джерело схеми БД — EF Core migrations у [CarRental.WebApi/Data/Migrations](./CarRental.WebApi/Data/Migrations);
- `CarRental.WebApi` застосовує міграції на старті;
- desktop більше не ініціалізує схему самостійно і очікує, що БД уже підготовлена.

## 3. Склад рішення

Ключові частини репозиторію:

- `CarRental.Desktop` — WPF-клієнт на .NET 8;
- `CarRental.Desktop.Tests` — desktop-тести;
- `CarRental.WebApi` — ASP.NET Core 8 API, EF Core контекст, migrations, auth;
- `CarRental.WebApi.Tests` — API та інтеграційні тести;
- `CarRental.WebApp` — окремий Vite/npm-проєкт;
- `sql` — coursework/demo SQL scripts;
- `docs` — додаткова технічна документація;
- `deploy` — локальна docker-compose конфігурація для PostgreSQL.

`CarRentalSystem.sln` містить тільки .NET-проєкти:

- `CarRental.Desktop`
- `CarRental.Desktop.Tests`
- `CarRental.WebApi`
- `CarRental.WebApi.Tests`

`CarRental.WebApp` запускається окремо через `npm`.

## 4. Функціональність по підсистемах

### 4.1 Desktop

Desktop має два фактичні режими роботи.

`User`-режим:

- `Прокат`;
- `Мої бронювання та оренди`.

`Manager` / `Admin`-режим:

- `Автопарк`;
- `Клієнти`;
- `Оренди`;
- `Техобслуговування`;
- `Пошкодження`;
- `Звіти`;
- `Адміністрування`.

Фактичні desktop-сценарії:

- вхід за логіном і паролем;
- реєстрація клієнтського акаунта;
- блокування акаунта після 5 невдалих входів на 10 хвилин;
- пошук, створення та супровід оренд;
- керування автопарком, клієнтами, платежами, ТО та пошкодженнями;
- генерація договорів у `TXT`, `DOCX`, `PDF`;
- експорт у `CSV` і `XLSX`;
- друк документів через shell-команду ОС.

### 4.2 Web frontend: self-service

Клієнтський web-режим (`User`) у `CarRental.WebApp` надає сторінки:

- `/prokat/search`
- `/prokat/bookings`
- `/prokat/profile`

Основні сценарії:

- перегляд каталогу авто;
- перевірка доступності авто;
- створення бронювання з початковою оплатою;
- перегляд власних майбутніх, активних і завершених оренд;
- перенесення і скасування власного бронювання;
- погашення балансу;
- редагування клієнтського профілю;
- зміна власного пароля.

### 4.3 Web frontend: staff/admin mode

Внутрішній web-інтерфейс визначається роллю користувача в JWT:

- `Manager` / `Admin` бачать `/rentals`, `/fleet`, `/clients`, `/maintenance`, `/damages`, `/reports`;
- лише `Admin` бачить `/admin`.

Фактичні web staff-сценарії:

- денні списки видач, повернень і прострочень;
- створення, закриття, скасування і перенесення договорів;
- платежі;
- керування клієнтами й автопарком;
- облік ТО і пошкоджень;
- звіти;
- адміністрування співробітників через окрему сторінку для `Admin`.

### 4.4 Web API

`CarRental.WebApi` надає:

- JWT authentication;
- policy-based authorization;
- EF Core migrations;
- health endpoint;
- pagination headers (`X-Page`, `X-Page-Size`, `X-Total-Count`);
- обробку помилок через `application/problem+json`.

## 5. Ролі й доступ

У системі використовуються ролі:

- `User`
- `Manager`
- `Admin`

Фактична модель доступу:

| Можливість | User | Manager | Admin |
| --- | --- | --- | --- |
| Self-service оренди і профіль | ✅ | ❌ | ❌ |
| Desktop staff-модулі | ❌ | ✅ | ✅ |
| Staff web mode | ❌ | ✅ | ✅ |
| Керування орендами | ✅* | ✅ | ✅ |
| Платежі | ❌ | ✅ | ✅ |
| Клієнти | ❌ | ✅ | ✅ |
| Автопарк | ❌ | ✅ | ✅ |
| ТО і пошкодження | ❌ | ✅ | ✅ |
| Звіти | ❌ | ✅ | ✅ |
| Керування співробітниками | ❌ | ❌ | ✅ |
| Delete-операції в API | ❌ | ❌ | ✅ |
| Оновлення тарифів через окремий pricing endpoint | ❌ | ❌ | ✅ |

\* тільки у власних self-service сценаріях

Seed-акаунти співробітників:

| Логін | Роль | Пароль за замовчуванням | Override |
| --- | --- | --- | --- |
| `admin` | `Admin` | `admin123` | `CAR_RENTAL_ADMIN_PASSWORD` |
| `manager` | `Manager` | `manager123` | `CAR_RENTAL_MANAGER_PASSWORD` |

`User`-акаунти створюються через `POST /api/auth/register` або desktop-реєстрацію.

## 6. Дані, БД і файлові ресурси

### 6.1 PostgreSQL

Runtime БД — тільки PostgreSQL.

Connection string за замовчуванням:

```text
Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres
```

Джерела конфігурації:

- desktop: `CAR_RENTAL_POSTGRES_CONNECTION`;
- Web API: `ConnectionStrings:Postgres`;
- design-time factory: `CAR_RENTAL_POSTGRES_CONNECTION` або fallback `car_rental_dev`.

### 6.2 Джерело схеми

Реальна runtime-схема керується міграціями у [CarRental.WebApi/Data/Migrations](./CarRental.WebApi/Data/Migrations).

Основні сутності:

- `Employees`
- `Clients`
- `Vehicles`
- `Rentals`
- `Payments`
- `Damages`
- `MaintenanceRecords`
- `ContractSequences`

Нумерація договорів використовує формат `CR-YYYY-000001` і зберігається в `ContractSequences`.

### 6.3 Seed-дані

Seed-логіка підтримує:

- співробітників `admin` і `manager`;
- базових клієнтів;
- стартовий автопарк;
- `ContractSequences` для поточного року;
- синхронізацію каталогу авто з `VehicleCatalogSeeds`.

### 6.4 Резервні копії

Резервне копіювання БД виконується зовнішніми засобами PostgreSQL (`pg_dump`, snapshots, PITR); вбудованого UI для цього немає.

### 6.5 Фото каталогу

Каталогові фото автомобілів є статичними ресурсами і зберігаються в [CarRental.WebApi/wwwroot/images/vehicles](./CarRental.WebApi/wwwroot/images/vehicles).

### 6.6 Локальні desktop-файли

Desktop використовує директорію:

```text
%LocalAppData%\CarRentalSystem
```

Типові підкаталоги:

- `Contracts`
- `Exports`
- `Logs`

## 7. Запуск проєкту

### 7.1 Prerequisites

Потрібні:

- Windows + PowerShell;
- .NET SDK 8;
- Node.js + npm;
- PostgreSQL;
- Docker Desktop — опційно, але це основний локальний спосіб підняти PostgreSQL через `deploy/docker-compose.postgres.yml`.

Якщо на машині немає Docker, використовуйте `.\RunWeb.ps1 -SkipDocker` або `.\RunDesktopPostgres.ps1 -SkipDocker` і заздалегідь запустіть PostgreSQL локально.

### 7.2 Швидкий старт web stack

```powershell
.\RunWeb.ps1
```

**Якщо на машині немає Docker, використовуйте `.\RunWeb.ps1 -SkipDocker`**. PostgreSQL у такому сценарії має бути вже запущений локально.

Скрипт:

- перевіряє `dotnet` і `npm`;
- за можливості піднімає PostgreSQL через Docker;
- чекає `127.0.0.1:5432`;
- виконує `dotnet restore`;
- виконує `npm install`, якщо треба;
- створює `CarRental.WebApp/.env.local` з `VITE_API_BASE_URL`;
- генерує тимчасовий `CAR_RENTAL_JWT_SIGNING_KEY`, якщо змінна не задана;
- запускає окремі PowerShell-вікна для API і frontend.

Адреси за замовчуванням:

- API: `http://localhost:5079`
- frontend: `http://localhost:5173`

Корисні опції:

```powershell
.\RunWeb.ps1 -SkipDocker
.\RunWeb.ps1 -SkipNpmInstall -SkipDotnetRestore
.\RunWeb.ps1 -ApiUrl "http://localhost:5079" -FrontendPort 5173
```

### 7.3 Запуск desktop

```powershell
.\RunDesktopPostgres.ps1
```

Важливо: desktop не застосовує EF migrations. Для першого запуску спочатку потрібно підготувати PostgreSQL-схему через `RunWeb.ps1` або `FactoryReset.ps1`, і лише потім запускати desktop.

Типовий порядок:

1. Підняти БД і схему:

```powershell
.\RunWeb.ps1
```

або

```powershell
.\FactoryReset.ps1
```

2. Запустити desktop:

```powershell
.\RunDesktopPostgres.ps1
```

Корисні опції:

```powershell
.\RunDesktopPostgres.ps1 -SkipDocker
.\RunDesktopPostgres.ps1 -PostgresConnection "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres"
```

### 7.4 One-click rebuild/restart

```powershell
.\OneClickDeploy.ps1
```

Скрипт:

- зупиняє локальні процеси на `5079` і `5173`;
- піднімає PostgreSQL через Docker;
- виконує `dotnet restore` і `dotnet build -c Release`;
- виконує `npm run build`;
- перевикликає `RunWeb.ps1`;
- за замовчуванням відкриває браузер.

### 7.5 Factory reset

```powershell
.\FactoryReset.ps1
```

Скрипт:

- зупиняє `CarRental.Desktop`, якщо він запущений;
- очищає `%LocalAppData%\CarRentalSystem`;
- за замовчуванням піднімає PostgreSQL через Docker;
- виконує `dotnet tool restore`;
- через `dotnet-ef` робить `database drop` і `database update` для `RentalDbContext`.

Після reset seed-логіка знову створює:

- `admin`
- `manager`

### 7.6 Ручний запуск

Підняти PostgreSQL:

```powershell
docker compose -f .\deploy\docker-compose.postgres.yml up -d
```

Запустити API:

```powershell
dotnet restore .\CarRentalSystem.sln
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5079"
$env:CAR_RENTAL_JWT_SIGNING_KEY = "your-strong-signing-key-at-least-32-characters"
dotnet run --project .\CarRental.WebApi\CarRental.WebApi.csproj
```

Запустити frontend:

```powershell
Set-Location .\CarRental.WebApp
"VITE_API_BASE_URL=http://localhost:5079" | Set-Content .env.local -Encoding UTF8
npm install
npm run dev -- --host localhost --port 5173
```

Запустити desktop:

```powershell
$env:CAR_RENTAL_POSTGRES_CONNECTION = "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres"
dotnet run --project .\CarRental.Desktop\CarRental.Desktop.csproj
```

## 8. Конфігурація

### 8.1 Порти

| Компонент | Значення за замовчуванням |
| --- | --- |
| PostgreSQL | `5432` |
| Web API | `5079` |
| Frontend | `5173` |

### 8.2 Environment variables

| Змінна | Призначення |
| --- | --- |
| `CAR_RENTAL_POSTGRES_CONNECTION` | connection string для desktop і design-time EF сценаріїв |
| `CAR_RENTAL_JWT_SIGNING_KEY` | signing key для Web API, мінімум 32 символи |
| `CAR_RENTAL_ADMIN_PASSWORD` | override seed-пароля для `admin` |
| `CAR_RENTAL_MANAGER_PASSWORD` | override seed-пароля для `manager` |
| `CAR_RENTAL_TEST_POSTGRES_CONNECTION` | connection string для тестів |
| `VITE_API_BASE_URL` | base URL для `CarRental.WebApp` |

### 8.3 JWT і appsettings

У [CarRental.WebApi/appsettings.json](./CarRental.WebApi/appsettings.json) і [CarRental.WebApi/appsettings.Development.json](./CarRental.WebApi/appsettings.Development.json) `Jwt:SigningKey` містить placeholder:

```text
__SET_CAR_RENTAL_JWT_SIGNING_KEY__
```

Web API не стартує з порожнім, коротким або явно небезпечним ключем. `RunWeb.ps1` автоматично підставляє тимчасовий strong key, якщо `CAR_RENTAL_JWT_SIGNING_KEY` не задана.

### 8.4 `.env.local` і frontend

`RunWeb.ps1` створює [CarRental.WebApp/.env.local](./CarRental.WebApp/.env.local) з `VITE_API_BASE_URL`.

Якщо змінна не задана, frontend fallback у коді — `http://localhost:5079`.

### 8.5 CORS

`appsettings.json` дозволяє:

- `http://localhost:5173`
- `https://localhost:5173`

`appsettings.Development.json` додатково дозволяє:

- `http://localhost:3000`

## 9. HTTP API surface

Усі групи нижче реально присутні у [CarRental.WebApi/Controllers](./CarRental.WebApi/Controllers).

### 9.1 `auth`

- `POST /api/auth/login`
- `POST /api/auth/register`
- `GET /api/auth/me`
- `POST /api/auth/change-password`
- `PATCH /api/auth/me/role`

Сценарії: login/register, отримання поточного користувача, зміна власного пароля, технічна зміна власної ролі для користувача з `ManageEmployees`.

### 9.2 `profile`

- `GET /api/profile/client`
- `PUT /api/profile/client`

Сценарії: читання і оновлення пов'язаного клієнтського профілю.

### 9.3 `clients`

- `GET /api/clients`
- `GET /api/clients/{id}`
- `POST /api/clients`
- `PUT /api/clients/{id}`
- `PATCH /api/clients/{id}/blacklist`
- `DELETE /api/clients/{id}`

Сценарії: список, пошук, CRUD, blacklist, delete з валідаціями залежностей.

### 9.4 `vehicles`

- `GET /api/vehicles`
- `GET /api/vehicles/{id}`
- `GET /api/vehicles/{id}/photo`
- `POST /api/vehicles`
- `PUT /api/vehicles/{id}`
- `PATCH /api/vehicles/{id}/rate`
- `DELETE /api/vehicles/{id}`

Сценарії: каталог, фото, CRUD, зміна тарифу, delete з перевіркою історії оренд.

### 9.5 `rentals`

- `GET /api/rentals`
- `GET /api/rentals/availability`
- `GET /api/rentals/{id}`
- `POST /api/rentals`
- `POST /api/rentals/{id}/close`
- `POST /api/rentals/{id}/cancel`
- `POST /api/rentals/{id}/reschedule`
- `POST /api/rentals/{id}/settle-balance`
- `POST /api/rentals/{id}/pickup-inspection`
- `POST /api/rentals/refresh-statuses`

Сценарії: життєвий цикл договору, availability, self-service доступ до власних оренд, закриття, скасування, перенесення, баланс і фіксація видачі.

### 9.6 `payments`

- `GET /api/payments/rentals/{rentalId}`
- `GET /api/payments/rentals/{rentalId}/balance`
- `POST /api/payments`

Сценарії: історія платежів, баланс, внесення `Incoming` і `Refund` платежів.

### 9.7 `damages`

- `GET /api/damages`
- `POST /api/damages`

Сценарії: список пошкоджень і створення акта з опційним донарахуванням.

### 9.8 `maintenance`

- `GET /api/maintenance/records`
- `GET /api/maintenance/due`
- `POST /api/maintenance/records`

Сценарії: історія ТО, due-список, додавання нового запису.

### 9.9 `reports`

- `GET /api/reports/summary`
- `GET /api/reports/rentals`

Сценарії: KPI summary і звіт по орендах з фільтрами та пагінацією.

### 9.10 `admin`

- `GET /api/admin/employees`
- `PATCH /api/admin/employees/{id}/toggle-active`
- `PATCH /api/admin/employees/{id}/toggle-manager-role`
- `PATCH /api/admin/employees/{id}/unlock`
- `POST /api/admin/employees/{id}/reset-password`

Сценарії: адміністрування співробітників, блокування, розблокування, зміна ролі `User`/`Manager`, reset password.

### 9.11 `system`

- `GET /api/system/health`

Сценарії: health check і перевірка доступності PostgreSQL.

## 10. Тести

Backend і desktop:

```powershell
dotnet test .\CarRentalSystem.sln
```

Окремо:

```powershell
dotnet test .\CarRental.WebApi.Tests\CarRental.WebApi.Tests.csproj
dotnet test .\CarRental.Desktop.Tests\CarRental.Desktop.Tests.csproj
```

Frontend:

```powershell
Set-Location .\CarRental.WebApp
npm run build
```

Особливості тестування:
У проєкті (`CarRental.WebApi.Tests` та `CarRental.Desktop.Tests`) активно використовуються інтеграційні тести. Тому для успішного виконання `dotnet test` обов'язковою умовою є запущений екземпляр PostgreSQL; використовується connection string `CAR_RENTAL_TEST_POSTGRES_CONNECTION` або локальний `localhost:5432` за замовчуванням.

## 11. Додаткова документація

- [docs/WEB_PRODUCTION_ARCHITECTURE.md](./docs/WEB_PRODUCTION_ARCHITECTURE.md) — production/deployment архітектура і базові operational notes;
- [docs/DB_COURSEWORK_PACKAGE.md](./docs/DB_COURSEWORK_PACKAGE.md) — ER-модель, 3NF, integrity rules, coursework package;
- [docs/MEMORY_QUERY_AUDIT.md](./docs/MEMORY_QUERY_AUDIT.md) — аудит memory/query аспектів;
- [sql/README.md](./sql/README.md) — порядок запуску coursework/demo SQL scripts.

SQL package у [sql](./sql) містить:

1. `01_schema_postgres.sql`
2. `02_seed_postgres.sql`
3. `03_views_and_reports.sql`
4. `04_integrity_checks.sql`

Ці SQL-файли призначені для coursework/demo сценаріїв. Runtime-схемою реального застосунку керують EF Core migrations із `CarRental.WebApi`.
