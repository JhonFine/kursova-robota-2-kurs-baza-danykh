import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Api } from '../api/client';
import type { Vehicle } from '../api/types';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { LoadingView } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCard } from '../components/StatCard';
import { formatCurrency } from '../utils/format';
import { parseEnumParam, parsePositiveIntParam, withUpdatedSearchParams } from '../utils/searchParams';

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

function resolveVehicleClassLabel(dailyRate: number): string {
  if (dailyRate >= 95) {
    return 'Преміум';
  }

  if (dailyRate >= 70) {
    return 'Бізнес';
  }

  if (dailyRate >= 45) {
    return 'Середній';
  }

  return 'Економ';
}

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
    engineDisplay: '',
    fuelType: '',
    transmissionType: '',
    doorsCount: '4',
    cargoCapacityDisplay: '',
    consumptionDisplay: '',
    hasAirConditioning: true,
    licensePlate: '',
    mileage: '0',
    dailyRate: '50',
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
  }, [search, selectedClass, selectedStatus, sortBy, sortDir]);

  const updateListParams = (updates: {
    page?: number | null;
    search?: string | null;
    class?: ClassFilter | null;
    status?: AvailabilityFilter | null;
    sortBy?: SortBy | null;
    sortDir?: SortDir | null;
  }): void => {
    setSearchParams((current) => withUpdatedSearchParams(current, updates));
  };

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

    try {
      setError(null);
      await Api.createVehicle({
        make: createForm.make.trim(),
        model: createForm.model.trim(),
        engineDisplay: createForm.engineDisplay.trim(),
        fuelType: createForm.fuelType.trim(),
        transmissionType: createForm.transmissionType.trim(),
        doorsCount: Number(createForm.doorsCount),
        cargoCapacityDisplay: createForm.cargoCapacityDisplay.trim(),
        consumptionDisplay: createForm.consumptionDisplay.trim(),
        hasAirConditioning: createForm.hasAirConditioning,
        licensePlate: createForm.licensePlate.trim(),
        mileage: Number(createForm.mileage),
        dailyRate: Number(createForm.dailyRate),
        isAvailable: true,
        serviceIntervalKm: Number(createForm.serviceIntervalKm),
        photoPath: createForm.photoPath.trim() || null,
      });

      setCreateForm({
        make: '',
        model: '',
        engineDisplay: '',
        fuelType: '',
        transmissionType: '',
        doorsCount: '4',
        cargoCapacityDisplay: '',
        consumptionDisplay: '',
        hasAirConditioning: true,
        licensePlate: '',
        mileage: '0',
        dailyRate: '50',
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

  if (isLoading) {
    return <LoadingView text="Завантаження автопарку..." />;
  }

  return (
    <div className="page-grid">
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
                    <span className={`status-pill ${vehicle.isAvailable ? 'ok' : 'bad'}`}>
                      {vehicle.isAvailable ? 'Доступне' : 'Зайняте'}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={page}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          disabled={isLoading}
          onPageChange={(nextPage) => updateListParams({ page: nextPage })}
        />
      </Panel>

      <div className="two-col-grid">
        <Panel title="Оновити ціну" subtitle={selectedVehicle ? `${selectedVehicle.make} ${selectedVehicle.model}` : 'Оберіть авто в таблиці'}>
          <div className="inline-form">
            <input
              value={newRate}
              onChange={(event) => setNewRate(event.target.value)}
              placeholder="Нова ціна"
              inputMode="decimal"
            />
            <button type="button" className="btn primary" onClick={() => void onUpdateRate()} disabled={!selectedVehicle}>
              Оновити ціну
            </button>
          </div>
        </Panel>

        <Panel title="Додавання авто" subtitle="Створення нового запису автопарку">
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
              <input required value={createForm.engineDisplay} onChange={(event) => setCreateForm((prev) => ({ ...prev, engineDisplay: event.target.value }))} />
            </label>
            <label>
              Тип пального
              <input required value={createForm.fuelType} onChange={(event) => setCreateForm((prev) => ({ ...prev, fuelType: event.target.value }))} />
            </label>
            <label>
              Коробка
              <input required value={createForm.transmissionType} onChange={(event) => setCreateForm((prev) => ({ ...prev, transmissionType: event.target.value }))} />
            </label>
            <label>
              Двері
              <input required type="number" min={1} max={8} value={createForm.doorsCount} onChange={(event) => setCreateForm((prev) => ({ ...prev, doorsCount: event.target.value }))} />
            </label>
            <label>
              Багажник / місткість
              <input required value={createForm.cargoCapacityDisplay} onChange={(event) => setCreateForm((prev) => ({ ...prev, cargoCapacityDisplay: event.target.value }))} />
            </label>
            <label>
              Витрата / споживання
              <input required value={createForm.consumptionDisplay} onChange={(event) => setCreateForm((prev) => ({ ...prev, consumptionDisplay: event.target.value }))} />
            </label>
            <label>
              Номер
              <input required value={createForm.licensePlate} onChange={(event) => setCreateForm((prev) => ({ ...prev, licensePlate: event.target.value }))} />
            </label>
            <label>
              Пробіг
              <input required type="number" min={0} value={createForm.mileage} onChange={(event) => setCreateForm((prev) => ({ ...prev, mileage: event.target.value }))} />
            </label>
            <label>
              Ціна/доба
              <input required type="number" min={1} step="0.01" value={createForm.dailyRate} onChange={(event) => setCreateForm((prev) => ({ ...prev, dailyRate: event.target.value }))} />
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

      {message ? <p className="success-box">{message}</p> : null}
      {error ? <p className="error-box">{error}</p> : null}
    </div>
  );
}
