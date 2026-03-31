import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Api } from '../api/client';
import type { Vehicle } from '../api/types';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { EmptyState } from '../components/EmptyState';
import { FeedbackBanner } from '../components/FeedbackBanner';
import { InlineSpinner } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCardSkeletons, TableSkeleton } from '../components/Skeleton';
import { StatCard } from '../components/StatCard';
import { formatCurrency } from '../utils/format';
import { parseEnumParam, parsePositiveIntParam, withUpdatedSearchParams } from '../utils/searchParams';
import { isValidUaLicensePlate, MIN_DAILY_RATE, resolveVehicleClassLabel } from '../utils/vehicleRules';
import {
  CARGO_UNIT_OPTIONS,
  CONSUMPTION_UNIT_OPTIONS,
  FUEL_TYPE_OPTIONS,
  POWERTRAIN_UNIT_OPTIONS,
  TRANSMISSION_TYPE_OPTIONS,
} from '../utils/referenceData';

const PAGE_SIZE = 20;
const classFilterValues = ['all', 'Economy', 'Mid', 'Business', 'Premium'] as const;
const availabilityFilterValues = ['all', 'available', 'busy'] as const;
const sortByValues = ['name', 'dailyRate', 'mileage'] as const;
const sortDirValues = ['asc', 'desc'] as const;

type ClassFilter = (typeof classFilterValues)[number];
type AvailabilityFilter = (typeof availabilityFilterValues)[number];
type SortBy = (typeof sortByValues)[number];
type SortDir = (typeof sortDirValues)[number];

const classFilterLabels: Record<ClassFilter, string> = {
  all: 'Усі класи',
  Economy: 'Економ',
  Mid: 'Середній',
  Business: 'Бізнес',
  Premium: 'Преміум',
};

const availabilityFilterLabels: Record<AvailabilityFilter, string> = {
  all: 'Усі статуси',
  available: 'Доступне',
  busy: 'Зайняте',
};

const sortLabels: Record<`${SortBy}:${SortDir}`, string> = {
  'name:asc': 'Назва A-Z',
  'name:desc': 'Назва Z-A',
  'dailyRate:asc': 'Ціна зростання',
  'dailyRate:desc': 'Ціна спадання',
  'mileage:asc': 'Пробіг зростання',
  'mileage:desc': 'Пробіг спадання',
};

export function FleetPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [newRate, setNewRate] = useState('');
  const [createForm, setCreateForm] = useState({
    make: '',
    model: '',
    powertrainCapacityValue: '',
    powertrainCapacityUnit: 'L',
    fuelType: '',
    transmissionType: '',
    doorsCount: '4',
    cargoCapacityValue: '',
    cargoCapacityUnit: 'L',
    consumptionValue: '',
    consumptionUnit: 'L_PER_100KM',
    hasAirConditioning: true,
    licensePlate: '',
    mileage: '0',
    dailyRate: String(MIN_DAILY_RATE),
    serviceIntervalKm: '10000',
    photoPath: '',
  });
  const requestIdRef = useRef(0);

  const page = parsePositiveIntParam(searchParams.get('page'), 1);
  const search = searchParams.get('search') ?? '';
  const selectedClass = parseEnumParam(searchParams.get('class'), classFilterValues, 'all');
  const selectedStatus = parseEnumParam(searchParams.get('status'), availabilityFilterValues, 'all');
  const sortBy = parseEnumParam(searchParams.get('sortBy'), sortByValues, 'name');
  const sortDir = parseEnumParam(searchParams.get('sortDir'), sortDirValues, 'asc');

  const updateListParams = useCallback((updates: {
    page?: number | null;
    search?: string | null;
    class?: ClassFilter | null;
    status?: AvailabilityFilter | null;
    sortBy?: SortBy | null;
    sortDir?: SortDir | null;
  }): void => {
    setSearchParams((current) => withUpdatedSearchParams(current, updates));
  }, [setSearchParams]);

  const selectedVehicle = useMemo(
    () => vehicles.find((item) => item.id === selectedId) ?? null,
    [vehicles, selectedId],
  );
  const activeFilters = useMemo(() => {
    const items: ActiveFilterChipItem[] = [];

    if (search.trim()) {
      items.push({
        key: 'search',
        label: `Пошук: ${search.trim()}`,
        onRemove: () => updateListParams({ search: null, page: 1 }),
      });
    }

    if (selectedClass !== 'all') {
      items.push({
        key: 'class',
        label: `Клас: ${classFilterLabels[selectedClass]}`,
        onRemove: () => updateListParams({ class: null, page: 1 }),
      });
    }

    if (selectedStatus !== 'all') {
      items.push({
        key: 'status',
        label: `Статус: ${availabilityFilterLabels[selectedStatus]}`,
        onRemove: () => updateListParams({ status: null, page: 1 }),
      });
    }

    if (sortBy !== 'name' || sortDir !== 'asc') {
      items.push({
        key: 'sort',
        label: `Сортування: ${sortLabels[`${sortBy}:${sortDir}`]}`,
        onRemove: () => updateListParams({ sortBy: null, sortDir: null, page: 1 }),
      });
    }

    return items;
  }, [search, selectedClass, selectedStatus, sortBy, sortDir, updateListParams]);

  const loadVehicles = useCallback(async (): Promise<void> => {
    const requestId = ++requestIdRef.current;

    try {
      setIsLoading(true);
      setError(null);

      const response = await Api.getVehiclesPage({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        availability:
          selectedStatus === 'all'
            ? undefined
            : selectedStatus === 'available',
        vehicleClass: selectedClass === 'all' ? undefined : selectedClass,
        sortBy,
        sortDir,
      });

      if (requestId !== requestIdRef.current) {
        return;
      }

      setVehicles(response.items);
      setTotalCount(response.totalCount);
      setSelectedId((currentSelectedId) => {
        if (currentSelectedId && response.items.some((item) => item.id === currentSelectedId)) {
          return currentSelectedId;
        }

        return response.items[0]?.id ?? null;
      });
    } catch (requestError) {
      if (requestId !== requestIdRef.current) {
        return;
      }

      setError(Api.errorMessage(requestError));
    } finally {
      if (requestId === requestIdRef.current) {
        setIsLoading(false);
      }
    }
  }, [page, search, selectedClass, selectedStatus, sortBy, sortDir]);

  useEffect(() => {
    void loadVehicles();
  }, [loadVehicles]);

  const onCreate = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    const normalizedPlate = createForm.licensePlate.trim().toUpperCase();
    if (!isValidUaLicensePlate(normalizedPlate)) {
      setError('Некоректний формат номера. Використовуйте шаблон AA1234BB.');
      return;
    }

    try {
      setError(null);
      await Api.createVehicle({
        make: createForm.make.trim(),
        model: createForm.model.trim(),
        powertrainCapacityValue: Number(createForm.powertrainCapacityValue),
        powertrainCapacityUnit: createForm.powertrainCapacityUnit,
        fuelType: createForm.fuelType.trim(),
        transmissionType: createForm.transmissionType.trim(),
        doorsCount: Number(createForm.doorsCount),
        cargoCapacityValue: Number(createForm.cargoCapacityValue),
        cargoCapacityUnit: createForm.cargoCapacityUnit,
        consumptionValue: Number(createForm.consumptionValue),
        consumptionUnit: createForm.consumptionUnit,
        hasAirConditioning: createForm.hasAirConditioning,
        licensePlate: normalizedPlate,
        mileage: Number(createForm.mileage),
        dailyRate: Number(createForm.dailyRate),
        isBookable: true,
        serviceIntervalKm: Number(createForm.serviceIntervalKm),
        photoPath: createForm.photoPath.trim() || null,
      });

      setCreateForm({
        make: '',
        model: '',
        powertrainCapacityValue: '',
        powertrainCapacityUnit: 'L',
        fuelType: '',
        transmissionType: '',
        doorsCount: '4',
        cargoCapacityValue: '',
        cargoCapacityUnit: 'L',
        consumptionValue: '',
        consumptionUnit: 'L_PER_100KM',
        hasAirConditioning: true,
        licensePlate: '',
        mileage: '0',
        dailyRate: String(MIN_DAILY_RATE),
        serviceIntervalKm: '10000',
        photoPath: '',
      });
      setMessage('Авто додано до автопарку.');
      await loadVehicles();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const onUpdateRate = async (): Promise<void> => {
    if (!selectedVehicle) {
      return;
    }

    const parsed = Number(newRate);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      setError('Некоректна ціна за добу.');
      return;
    }

    try {
      setError(null);
      await Api.updateVehicleRate(selectedVehicle.id, parsed);
      setNewRate('');
      setMessage(`Ставку для ${selectedVehicle.make} ${selectedVehicle.model} оновлено.`);
      await loadVehicles();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  if (isLoading && vehicles.length === 0) {
    return (
      <div className="staff-dashboard">
        <StatCardSkeletons count={3} />

        <Panel title="Автопарк" subtitle="Готуємо список авто, фільтри та навігацію по вибірці.">
          <TableSkeleton rows={7} />
        </Panel>

        <div className="staff-dashboard-grid">
          <Panel title="Оновити ціну" subtitle="Панель швидкого редагування ставки.">
            <EmptyState
              icon="RATE"
              compact
              title="Завантажуємо вибране авто."
              description="Щойно таблиця буде готова, тут з’явиться коротка картка автомобіля та поле для зміни тарифу."
            />
          </Panel>

          <Panel title="Додавання авто" subtitle="Форма поповнення автопарку.">
            <TableSkeleton rows={4} compact />
          </Panel>
        </div>
      </div>
    );
  }

  return (
    <div className="staff-dashboard">
      <section className="stats-grid">
        <StatCard label="Усього у вибірці" value={totalCount} accent="blue" />
        <StatCard
          label="Доступні у вибірці"
          value={selectedStatus === 'available' ? totalCount : vehicles.filter((item) => item.isAvailable).length}
          accent="mint"
        />
        <StatCard
          label="Середня ціна у вибірці"
          value={formatCurrency(
            vehicles.length > 0
              ? vehicles.reduce((sum, item) => sum + item.dailyRate, 0) / vehicles.length
              : 0,
          )}
          accent="amber"
        />
      </section>

      <Panel title="Автопарк" subtitle="Server-side фільтри, сортування та швидке редагування ставки.">
        <FilterToolbar
          chips={activeFilters}
          footerNote="Стан фільтрів зберігається в URL, тому сторінку можна оновлювати або ділитися посиланням без втрати вибірки."
          actions={(
            <>
              <button
                type="button"
                className="btn ghost"
                onClick={() => updateListParams({
                  page: null,
                  search: null,
                  class: null,
                  status: null,
                  sortBy: null,
                  sortDir: null,
                })}
              >
                Скинути
              </button>
              <button
                type="button"
                className="btn"
                onClick={() => void loadVehicles()}
              >
                Оновити
              </button>
            </>
          )}
        >
          <FilterField label="Пошук" hint="Марка, модель або номер">
            <input
              value={search}
              onChange={(event) => updateListParams({
                search: event.target.value,
                page: 1,
              })}
              placeholder="Марка, модель або номер"
            />
          </FilterField>

          <FilterField label="Клас авто">
            <select
              value={selectedClass}
              onChange={(event) => updateListParams({
                class: event.target.value as ClassFilter,
                page: 1,
              })}
            >
              <option value="all">Усі класи</option>
              <option value="Economy">Економ</option>
              <option value="Mid">Середній</option>
              <option value="Business">Бізнес</option>
              <option value="Premium">Преміум</option>
            </select>
          </FilterField>

          <FilterField label="Статус">
            <select
              value={selectedStatus}
              onChange={(event) => updateListParams({
                status: event.target.value as AvailabilityFilter,
                page: 1,
              })}
            >
              <option value="all">Усі статуси</option>
              <option value="available">Доступне</option>
              <option value="busy">Зайняте</option>
            </select>
          </FilterField>

          <FilterField label="Сортування">
            <select
              value={`${sortBy}:${sortDir}`}
              onChange={(event) => {
                const [nextSortBy, nextSortDir] = event.target.value.split(':') as [SortBy, SortDir];
                updateListParams({
                  sortBy: nextSortBy,
                  sortDir: nextSortDir,
                  page: 1,
                });
              }}
            >
              <option value="name:asc">Назва A-Z</option>
              <option value="name:desc">Назва Z-A</option>
              <option value="dailyRate:asc">Ціна зростання</option>
              <option value="dailyRate:desc">Ціна спадання</option>
              <option value="mileage:asc">Пробіг зростання</option>
              <option value="mileage:desc">Пробіг спадання</option>
            </select>
          </FilterField>
        </FilterToolbar>

        <div className={`surface-refresh${isLoading ? ' is-refreshing' : ''}`}>
          {vehicles.length === 0 ? (
            <EmptyState
              icon="CAR"
              title="Авто під поточні фільтри не знайдено."
              description="Скиньте частину обмежень або змініть пошук, щоб повернутись до повного списку автопарку."
              actions={(
                <>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => updateListParams({
                      page: null,
                      search: null,
                      class: null,
                      status: null,
                      sortBy: null,
                      sortDir: null,
                    })}
                  >
                    Скинути фільтри
                  </button>
                  <button type="button" className="btn primary" onClick={() => void loadVehicles()}>
                    Оновити список
                  </button>
                </>
              )}
            />
          ) : (
            <>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>ID</th>
                      <th>Авто</th>
                      <th>Клас</th>
                      <th>Номер</th>
                      <th>Пробіг</th>
                      <th>Ціна/доба</th>
                      <th>Статус</th>
                    </tr>
                  </thead>
                  <tbody>
                    {vehicles.map((vehicle) => (
                      <tr
                        key={vehicle.id}
                        className={selectedId === vehicle.id ? 'selected-row' : ''}
                        onClick={() => setSelectedId(vehicle.id)}
                      >
                        <td>{vehicle.id}</td>
                        <td>{vehicle.make} {vehicle.model}</td>
                        <td>{resolveVehicleClassLabel(vehicle.dailyRate)}</td>
                        <td>{vehicle.licensePlate}</td>
                        <td>{vehicle.mileage.toLocaleString('uk-UA')} км</td>
                        <td>{formatCurrency(vehicle.dailyRate)}</td>
                        <td>
                          <span className={`status-pill ${vehicle.isAvailable ? 'ok' : vehicle.isBookable ? 'wait' : 'bad'}`}>
                            {vehicle.isAvailable ? 'Доступне' : vehicle.isBookable ? 'Зайняте' : 'Недоступне'}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {isLoading ? (
                <div className="refresh-overlay">
                  <InlineSpinner />
                </div>
              ) : null}
            </>
          )}
        </div>
        <PaginationControls
          page={page}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          disabled={isLoading}
          onPageChange={(nextPage) => updateListParams({ page: nextPage })}
        />
      </Panel>

      <div className="staff-dashboard-grid">
        <Panel title="Оновити ціну" subtitle={selectedVehicle ? `${selectedVehicle.make} ${selectedVehicle.model}` : 'Оберіть авто в таблиці'}>
          {selectedVehicle ? (
            <div className="panel-stack">
              <div className="panel-intro">
                <strong>{selectedVehicle.make} {selectedVehicle.model}</strong>
                <p>
                  {selectedVehicle.licensePlate} • {formatCurrency(selectedVehicle.dailyRate)} за добу • {selectedVehicle.mileage.toLocaleString('uk-UA')} км
                </p>
              </div>

              <div className="inline-form">
                <input
                  value={newRate}
                  onChange={(event) => setNewRate(event.target.value)}
                  placeholder="Нова ціна"
                  inputMode="decimal"
                />
                <button type="button" className="btn primary" onClick={() => void onUpdateRate()}>
                  Оновити ціну
                </button>
              </div>
            </div>
          ) : (
            <EmptyState
              icon="RATE"
              compact
              title="Оберіть авто з таблиці."
              description="Клік по рядку відкриє швидке редагування ставки без переходу на окремий екран."
            />
          )}
        </Panel>

        <Panel title="Додавання авто" subtitle="Створення нового запису автопарку">
          <div className="panel-intro">
            <strong>Нове авто для автопарку.</strong>
            <p>Заповніть основні характеристики, а фото додайте одразу або пізніше, коли воно буде готове.</p>
          </div>
          <form className="form-grid" onSubmit={(event) => void onCreate(event)}>
            <label>
              Марка
              <input required value={createForm.make} onChange={(event) => setCreateForm((prev) => ({ ...prev, make: event.target.value }))} />
            </label>
            <label>
              Модель
              <input required value={createForm.model} onChange={(event) => setCreateForm((prev) => ({ ...prev, model: event.target.value }))} />
            </label>
            <label>
              Двигун / батарея
              <input required type="number" min={0.01} step="0.01" value={createForm.powertrainCapacityValue} onChange={(event) => setCreateForm((prev) => ({ ...prev, powertrainCapacityValue: event.target.value }))} />
            </label>
            <label>
              Одиниця силової установки
              <select value={createForm.powertrainCapacityUnit} onChange={(event) => setCreateForm((prev) => ({ ...prev, powertrainCapacityUnit: event.target.value }))}>
                {POWERTRAIN_UNIT_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Тип пального
              <select required value={createForm.fuelType} onChange={(event) => setCreateForm((prev) => ({ ...prev, fuelType: event.target.value }))}>
                <option value="">Оберіть тип</option>
                {FUEL_TYPE_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Коробка
              <select required value={createForm.transmissionType} onChange={(event) => setCreateForm((prev) => ({ ...prev, transmissionType: event.target.value }))}>
                <option value="">Оберіть коробку</option>
                {TRANSMISSION_TYPE_OPTIONS.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Двері
              <input required type="number" min={1} max={8} value={createForm.doorsCount} onChange={(event) => setCreateForm((prev) => ({ ...prev, doorsCount: event.target.value }))} />
            </label>
            <label>
              Багажник / місткість
              <input required type="number" min={0.01} step="0.01" value={createForm.cargoCapacityValue} onChange={(event) => setCreateForm((prev) => ({ ...prev, cargoCapacityValue: event.target.value }))} />
            </label>
            <label>
              Одиниця місткості / вантажу
              <select value={createForm.cargoCapacityUnit} onChange={(event) => setCreateForm((prev) => ({ ...prev, cargoCapacityUnit: event.target.value }))}>
                {CARGO_UNIT_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Витрата / споживання
              <input required type="number" min={0.01} step="0.01" value={createForm.consumptionValue} onChange={(event) => setCreateForm((prev) => ({ ...prev, consumptionValue: event.target.value }))} />
            </label>
            <label>
              Одиниця споживання
              <select value={createForm.consumptionUnit} onChange={(event) => setCreateForm((prev) => ({ ...prev, consumptionUnit: event.target.value }))}>
                {CONSUMPTION_UNIT_OPTIONS.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Номер
              <input required value={createForm.licensePlate} placeholder="AA1234BB" onChange={(event) => setCreateForm((prev) => ({ ...prev, licensePlate: event.target.value.toUpperCase() }))} />
            </label>
            <label>
              Пробіг
              <input required type="number" min={0} value={createForm.mileage} onChange={(event) => setCreateForm((prev) => ({ ...prev, mileage: event.target.value }))} />
            </label>
            <label>
              Ціна/доба
              <input required type="number" min={MIN_DAILY_RATE} step="50" value={createForm.dailyRate} onChange={(event) => setCreateForm((prev) => ({ ...prev, dailyRate: event.target.value }))} />
            </label>
            <label>
              Інтервал ТО (км)
              <input required type="number" min={1000} value={createForm.serviceIntervalKm} onChange={(event) => setCreateForm((prev) => ({ ...prev, serviceIntervalKm: event.target.value }))} />
            </label>
            <label className="full-row">
              Шлях до фото (опц.)
              <input
                value={createForm.photoPath}
                onChange={(event) => setCreateForm((prev) => ({ ...prev, photoPath: event.target.value }))}
                placeholder="/images/vehicles/toyota-camry-xv70.jpg"
              />
            </label>
            <label className="full-row checkbox-row">
              <input
                type="checkbox"
                checked={createForm.hasAirConditioning}
                onChange={(event) => setCreateForm((prev) => ({ ...prev, hasAirConditioning: event.target.checked }))}
              />
              У комплектації є A/C
            </label>
            <button type="submit" className="btn primary">Додати авто</button>
          </form>
        </Panel>
      </div>

      {message ? (
        <FeedbackBanner tone="success" title="Автопарк оновлено" onDismiss={() => setMessage(null)} autoHideMs={4200}>
          {message}
        </FeedbackBanner>
      ) : null}
      {error ? (
        <FeedbackBanner tone="error" title="Не вдалося виконати дію" onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
    </div>
  );
}
