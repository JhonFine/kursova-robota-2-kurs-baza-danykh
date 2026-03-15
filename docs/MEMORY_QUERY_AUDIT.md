# Memory / Query Audit

## Critical

- `none`
  - У коді не знайдено mutable `static List<T>`, `static Dictionary<TKey, TValue>` або `static ObservableCollection<T>`, які безконтрольно накопичують session data.

## High

- `fixed` `CarRental.Desktop/Views/LoginWindow.xaml.cs`
  - Додана фінальна відписка `RequestClose` і `DataContextChanged` на `Closed`, щоб `LoginWindow` не утримував `ViewModel` після закриття.

- `fixed` `CarRental.Desktop/Converters/VehiclePhotoSourceConverter.cs`
  - Замість прямого file-path binding додано контрольоване завантаження `BitmapImage` з `CacheOption=OnLoad`, `DecodePixelWidth` і `Freeze()`, щоб файли фото не лишались залоченими і не тягнули повнорозмірні bitmap-и довше, ніж треба.

- `fixed` `CarRental.Desktop/Services/Maintenance/MaintenanceService.cs`
- `fixed` `CarRental.WebApi/Services/Maintenance/MaintenanceService.cs`
  - Найважчий eager-loading `Vehicles + MaintenanceRecords` переписано на проєкцію з `LastNextServiceMileage`, без materialize всієї колекції `MaintenanceRecords` для кожного авто.

- `fixed` `CarRental.WebApi/Controllers/RentalsController.cs`
- `fixed` `CarRental.WebApi/Controllers/ReportsController.cs`
- `fixed` `CarRental.WebApi/Controllers/DamagesController.cs`
- `fixed` `CarRental.WebApi/Controllers/MaintenanceController.cs`
- `fixed` `CarRental.WebApi/Controllers/VehiclesController.cs`
- `fixed` `CarRental.WebApi/Controllers/ClientsController.cs`
- `fixed` `CarRental.WebApi/Controllers/AdminController.cs`
  - Для великих list endpoint-ів додано optional `page/pageSize` з headers `X-Page`, `X-Page-Size`, `X-Total-Count`, без зміни body shape DTO.

## Medium

- `fixed` `CarRental.Desktop/ViewModels/RentalsPageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/ProkatPageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/FleetPageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/ClientsPageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/MaintenancePageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/DamagesPageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/AdminPageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/ReportsPageViewModel.cs`
  - Для heavy page/viewmodel lifecycle додано `ITransientStateOwner` і cleanup на `Unloaded`, щоб detail state, payments, dialog selections і card-payment input-и не висіли без потреби протягом усієї сесії.

- `fixed` `CarRental.Desktop/Data/DatabaseInitializer.cs`
- `fixed` `CarRental.WebApi/Data/DatabaseInitializer.cs`
  - При seed/catalog sync прибрано зайве повторне читання `Vehicles` і повторний прохід по `DbSet`, використовується вже завантажений список.

- `fixed` `CarRental.Desktop/Services/Analytics/AnalyticsExportService.cs`
- `fixed` `CarRental.Desktop/ViewModels/MaintenancePageViewModel.cs`
- `fixed` `CarRental.Desktop/ViewModels/DamagesPageViewModel.cs`
  - Зайві `Include(...)` замінені на прямі проєкції.

- `fixed` `CarRental.WebApp/src/pages/FleetPage.tsx`
- `fixed` `CarRental.WebApp/src/pages/ClientsPage.tsx`
- `fixed` `CarRental.WebApp/src/pages/RentalsPage.tsx`
- `fixed` `CarRental.WebApp/src/pages/MaintenancePage.tsx`
- `fixed` `CarRental.WebApp/src/pages/DamagesPage.tsx`
- `fixed` `CarRental.WebApp/src/pages/ReportsPage.tsx`
- `fixed` `CarRental.WebApp/src/api/client.ts`
  - Web UI більше не тягне повні списки для великих таблиць за замовчуванням; підключені paged API methods і current-slice refresh після mutation.

## Low

- `residual` `CarRental.Desktop/ViewModels/AdminPageViewModel.cs`
  - Desktop admin-grid все ще не є реальним performance hotspot через дуже малу кардинальність записів `Employees`, тому окремий paging/debounce refactor відкладено.

- `residual` `CarRental.WebApp/src/pages/FleetPage.tsx`
- `residual` `CarRental.WebApp/src/pages/AdminPage.tsx`
  - Частина локальних фільтрів працює по поточному slice сторінки, а не по всьому датасету. Для повного search-across-pages потрібні server-side filter params у наступній фазі.
