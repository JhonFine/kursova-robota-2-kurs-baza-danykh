# CarRentalSystem

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)
![React 19](https://img.shields.io/badge/React-19-61DAFB?logo=react&logoColor=black)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-14%2B-336791?logo=postgresql)
![WPF](https://img.shields.io/badge/WPF-Desktop-blue?logo=windows)
![PostgreSQL in Docker](https://img.shields.io/badge/PostgreSQL%20in%20Docker-16-2496ED?logo=docker&logoColor=white)

`CarRentalSystem` — монорепозиторій системи оренди автомобілів із трьома застосунками, спільною PostgreSQL-базою та єдиними seed/reference даними.

- `CarRental.Desktop` — WPF desktop-застосунок на .NET 8.
- `CarRental.WebApi` — ASP.NET Core 8 Web API з JWT-автентифікацією, EF Core migrations і Swagger у `Development`.
- `CarRental.WebApp` — React 19 + TypeScript + Vite 7 frontend для клієнтського self-service і staff/admin web-режиму.

Фактична архітектура:

- усі застосунки працюють поверх однієї PostgreSQL бази;
- джерело істини для схеми БД — EF Core migrations у `CarRental.WebApi/Data/Migrations`;
- `CarRental.WebApi` автоматично застосовує міграції та seed при старті;
- `CarRental.Desktop` міграції не виконує і потребує вже підготовленої схеми.

## Data Model Highlights

- `Accounts` isolates authentication from business profiles; `Employees` and `Clients` keep operational and portal data separately.
- `ClientDocuments`, `VehiclePhotos`, and `DamagePhotos` normalize documents and media instead of storing single-path scalar fields in parent tables.
- `RentalInspections` stores pickup and return inspection results separately from rental header and payment data.
- `FuelTypes`, `TransmissionTypes`, and `VehicleStatuses` normalize vehicle classifiers and state.
- `Clients` and `Vehicles` use audit fields plus soft-delete flags so historical rentals remain intact.

## Prerequisites

- Windows + PowerShell для локальних скриптів і WPF-клієнта.
- .NET SDK 8.
- Node.js 20+.
- PostgreSQL 14+ локально або Docker Desktop.
- `dotnet tool restore` для сценаріїв із `dotnet-ef`.

Локальний Docker Compose сценарій використовує строго `postgres:16` з `deploy/docker-compose.postgres.yml`, навіть якщо мінімальна підтримувана версія PostgreSQL для локального оточення — `14+`.

## Installation

Відновіть .NET-залежності та локальні tools:

```powershell
dotnet restore .\CarRentalSystem.sln
dotnet tool restore
```

Встановіть frontend-залежності у відтворюваному режимі через lock-файл:

```powershell
Set-Location .\CarRental.WebApp
npm ci
Set-Location ..
```

За потреби підніміть PostgreSQL через Docker:

```powershell
docker compose -f .\deploy\docker-compose.postgres.yml up -d
```

## Run

### Швидкий старт Web stack

```powershell
.\RunWeb.ps1
```

Скрипт:

- перевіряє `dotnet` і `npm`;
- за потреби піднімає PostgreSQL через Docker Compose;
- очікує доступності `127.0.0.1:5432`;
- виконує `dotnet restore`, якщо його не пропущено;
- виконує `npm install`, якщо `node_modules` ще немає;
- створює `CarRental.WebApp/.env.local` з `VITE_API_BASE_URL`;
- генерує тимчасовий JWT signing key, якщо `CAR_RENTAL_JWT_SIGNING_KEY` не задано;
- відкриває окремі PowerShell-вікна для Web API і WebApp.

URL за замовчуванням:

- Web API: `http://localhost:5079`
- Swagger: `http://localhost:5079/swagger`
- WebApp: `http://localhost:5173`

Корисні опції:

```powershell
.\RunWeb.ps1 -SkipDocker
.\RunWeb.ps1 -SkipNpmInstall -SkipDotnetRestore
.\RunWeb.ps1 -ApiUrl "http://localhost:5079" -FrontendPort 5173
```

### Швидкий старт Desktop

Desktop потрібно запускати лише після того, як PostgreSQL-схема вже створена. Найпростіший сценарій:

```powershell
.\RunWeb.ps1
```

або повний reset:

```powershell
.\FactoryReset.ps1
```

Після цього desktop можна запускати окремо:

```powershell
.\RunDesktopPostgres.ps1
```

Корисні опції:

```powershell
.\RunDesktopPostgres.ps1 -SkipDocker
.\RunDesktopPostgres.ps1 -PostgresConnection "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres"
```

### Ручний запуск

Підняти PostgreSQL:

```powershell
docker compose -f .\deploy\docker-compose.postgres.yml up -d
```

Запустити Web API:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5079"
$env:CAR_RENTAL_JWT_SIGNING_KEY = "your-strong-signing-key-at-least-32-characters"
dotnet run --project .\CarRental.WebApi\CarRental.WebApi.csproj
```

Запустити WebApp:

```powershell
Set-Location .\CarRental.WebApp
"VITE_API_BASE_URL=http://localhost:5079" | Set-Content .env.local -Encoding UTF8
npm run dev -- --host localhost --port 5173
```

Запустити Desktop:

```powershell
$env:CAR_RENTAL_POSTGRES_CONNECTION = "Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres"
dotnet run --project .\CarRental.Desktop\CarRental.Desktop.csproj
```

### Допоміжні скрипти

Повний rebuild і restart web stack:

```powershell
.\OneClickDeploy.ps1
```

Factory reset локального стану:

```powershell
.\FactoryReset.ps1
```

`OneClickDeploy.ps1` зупиняє старі локальні процеси, збирає solution і frontend, а потім повторно викликає `RunWeb.ps1`.

`FactoryReset.ps1`:

- очищає `%LocalAppData%\CarRentalSystem`;
- за потреби піднімає PostgreSQL;
- виконує `dotnet-ef database drop` і `dotnet-ef database update` для `RentalDbContext`;
- залишає базу у factory state, після чого seed-дані відтворюються на наступному запуску API або застосунку.

### Proof of Success (Перевірка успішного запуску)

Відкрийте `http://localhost:5173`. Якщо міграції та seed пройшли успішно, ви побачите сторінку входу. Введіть логін `admin` та пароль `admin123`. Після входу має відкритися дашборд адміністратора.

## Environment Variables

| Змінна / ключ | Призначення | Значення за замовчуванням |
| --- | --- | --- |
| `CAR_RENTAL_POSTGRES_CONNECTION` | connection string для `CarRental.Desktop` і design-time EF сценаріїв | `Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres` **[LOCAL ENVIRONMENT]** |
| `ConnectionStrings__Postgres` | connection string для `CarRental.WebApi` через стандартний ASP.NET Core environment override | значення з `CarRental.WebApi/appsettings*.json` |
| `CAR_RENTAL_JWT_SIGNING_KEY` | JWT signing key для Web API, мінімум 32 символи | задається вручну або **[DEV ONLY]** тимчасово генерується в `RunWeb.ps1` |
| `CAR_RENTAL_ADMIN_PASSWORD` | override seed-пароля для `admin` | `admin123` **[DEV ONLY]** |
| `CAR_RENTAL_MANAGER_PASSWORD` | override seed-пароля для `manager` | `manager123` **[DEV ONLY]** |
| `CAR_RENTAL_TEST_POSTGRES_CONNECTION` | connection string для .NET інтеграційних тестів | `Host=localhost;Port=5432;Database=car_rental;Username=postgres;Password=postgres` **[LOCAL ENVIRONMENT]** |
| `VITE_API_BASE_URL` | base URL для `CarRental.WebApp` | `http://localhost:5079` |

**[DEV ONLY] / [LOCAL ENVIRONMENT]** значення на кшталт `admin123`, `manager123`, `postgres/postgres` і тимчасово згенерованих JWT-ключів категорично заборонено використовувати в production-середовищі; вони існують виключно для зручності локального старту та онбордингу.

Додатково:

- `CarRental.WebApi/appsettings.json` і `appsettings.Development.json` містять placeholder `__SET_CAR_RENTAL_JWT_SIGNING_KEY__`, з яким API не стартує;
- design-time factory для `CarRental.WebApi` використовує `CAR_RENTAL_POSTGRES_CONNECTION`, а якщо змінну не задано, fallback — `Host=localhost;Port=5432;Database=car_rental_dev;Username=postgres;Password=postgres`;
- дозволені origins для CORS задаються через `Cors:AllowedOrigins` у `appsettings*.json`.

## Seed Data And Access

На порожній базі seed-логіка створює staff-акаунти:

- `admin` / `admin123` **[DEV ONLY]**
- `manager` / `manager123` **[DEV ONLY]**

**[DEV ONLY]** seed-креди призначені тільки для локального середовища та демонстраційного старту. У production вони не мають використовуватися за жодних умов.

Ролі:

- `User` — self-service оренди, власний профіль, власні бронювання, зміна власного пароля.
- `Manager` — staff-робота з орендами, автопарком, клієнтами, ТО, пошкодженнями та звітами.
- `Admin` — усі можливості `Manager` плюс керування працівниками.

Обмеження безпеки:

- після 5 невдалих спроб входу акаунт блокується на 10 хвилин;
- пароль для реєстрації або зміни має містити щонайменше 8 символів.

Desktop локально створює каталог:

```text
%LocalAppData%\CarRentalSystem
```

Типові підкаталоги:

- `Contracts`
- `Exports`
- `Logs`

## Testing

Збірка .NET-рішення:

```powershell
dotnet build .\CarRentalSystem.sln -c Release
```

Тести:

```powershell
dotnet test .\CarRentalSystem.sln
dotnet test .\CarRental.WebApi.Tests\CarRental.WebApi.Tests.csproj
dotnet test .\CarRental.Desktop.Tests\CarRental.Desktop.Tests.csproj
```

Frontend-перевірки:

```powershell
Set-Location .\CarRental.WebApp
npm run build
npm run lint
```

Важливо:

- `CarRental.WebApi.Tests` і `CarRental.Desktop.Tests` використовують PostgreSQL і створюють тимчасові тестові БД;
- окремих frontend unit/integration тестів у репозиторії зараз не налаштовано, тому базова перевірка frontend — це `npm run build` і `npm run lint`.

## Troubleshooting

- Порт `5432` або `5079` зайнятий. Для PostgreSQL змініть port mapping у `deploy/docker-compose.postgres.yml` або зупиніть локальний інстанс БД; для Web API змініть `-ApiUrl` у `RunWeb.ps1` або вручну задайте інше значення в `$env:ASPNETCORE_URLS` і відповідно оновіть `VITE_API_BASE_URL`.
- PowerShell блокує виконання скриптів. Виконайте `Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser`, а потім повторіть запуск `RunWeb.ps1`, `RunDesktopPostgres.ps1` або `FactoryReset.ps1`.
- БД не існує або міграції розсинхронізовані. Запустіть `.\FactoryReset.ps1`, щоб повністю пересоздати локальну схему й повернути базу в узгоджений factory state.

## Project Structure

```text
.
|-- CarRental.Desktop/             # WPF desktop client
|-- CarRental.Desktop.Tests/       # desktop tests
|-- CarRental.WebApi/              # ASP.NET Core Web API, EF Core migrations, static images
|-- CarRental.WebApi.Tests/        # API and DB integration tests
|-- CarRental.WebApp/              # React + TypeScript + Vite frontend
|-- Shared/                        # shared seed and security reference data
|-- deploy/                        # docker-compose for PostgreSQL
|-- docs/                          # additional technical documentation
|-- sql/                           # coursework/demo SQL package
|-- .config/                       # local dotnet tools manifest
|-- RunWeb.ps1                     # quick start for Web API + WebApp
|-- RunDesktopPostgres.ps1         # desktop start script
|-- OneClickDeploy.ps1             # rebuild + restart web stack
|-- FactoryReset.ps1               # local reset for app data and database
`-- CarRentalSystem.sln            # .NET solution (WebApp runs separately via npm)
```

Примітки:

- `CarRentalSystem.sln` містить лише .NET-проєкти;
- `CarRental.WebApp` запускається окремо через `npm`;
- SQL-файли з каталогу `sql` призначені для coursework/demo сценаріїв, але runtime-схемою застосунку керують EF Core migrations із `CarRental.WebApi`.

## Additional Documentation

- `docs/WEB_PRODUCTION_ARCHITECTURE.md` — production/deployment нотатки для web stack.
- `docs/DB_COURSEWORK_PACKAGE.md` — опис БД, нормалізації та integrity rules.
- `docs/MEMORY_QUERY_AUDIT.md` — аудит memory/query аспектів.
- `sql/README.md` — порядок запуску coursework/demo SQL-скриптів.
