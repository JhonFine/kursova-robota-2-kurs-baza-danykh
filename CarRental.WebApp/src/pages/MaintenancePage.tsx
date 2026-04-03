import { useEffect, useState } from 'react';
import { Api } from '../api/client';
import { useCallback } from 'react';
import type { MaintenanceDue, MaintenanceRecord, Vehicle } from '../api/types';
import { LoadingView } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCard } from '../components/StatCard';
import { formatCurrency, formatShortDate } from '../utils/format';
import { DEFAULT_MAINTENANCE_DESCRIPTION } from '../utils/referenceData';

const PAGE_SIZE = 25;

export function MaintenancePage() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [records, setRecords] = useState<MaintenanceRecord[]>([]);
  const [dueItems, setDueItems] = useState<MaintenanceDue[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [form, setForm] = useState({
    vehicleId: '',
    serviceDate: new Date().toISOString().slice(0, 10),
    mileageAtService: '',
    description: DEFAULT_MAINTENANCE_DESCRIPTION,
    cost: '',
    nextServiceMileage: '',
    nextServiceDate: '',
  });

  const load = useCallback(async (pageToLoad: number): Promise<void> => {
    // Модуль ТО щоразу збирає і журнал записів, і прострочені авто, і довідник машин,
    // бо форма створення і праві статистики залежать від одного snapshot стану.
    try {
      setLoading(true);
      setError(null);
      const [recordsData, dueData, vehiclesData] = await Promise.all([
        Api.getMaintenanceRecordsPage({ page: pageToLoad, pageSize: PAGE_SIZE }),
        Api.getMaintenanceDue(),
        Api.getVehicles(),
      ]);
      setRecords(recordsData.items);
      setPage(recordsData.page);
      setTotalCount(recordsData.totalCount);
      setDueItems(dueData);
      setVehicles(vehiclesData);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(page);
  }, [load, page]);

  const addRecord = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (!form.nextServiceMileage && !form.nextServiceDate) {
      setError('Вкажіть пробіг або дату наступного ТО.');
      return;
    }

    // Після додавання запису оновлюємо весь модуль, щоб одразу перерахувати
    // overdue-список і поточну історію ТО без локальних "ручних" патчів стану.
    try {
      await Api.addMaintenanceRecord({
        vehicleId: Number(form.vehicleId),
        serviceDate: form.serviceDate,
        mileageAtService: Number(form.mileageAtService),
        description: form.description,
        cost: Number(form.cost),
        nextServiceMileage: form.nextServiceMileage ? Number(form.nextServiceMileage) : null,
        nextServiceDate: form.nextServiceDate || null,
      });

      setForm((prev) => ({
        ...prev,
        mileageAtService: '',
        cost: '',
        nextServiceMileage: '',
        nextServiceDate: '',
      }));
      await load(page);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  if (loading) {
    return <LoadingView text="Завантаження модуля ТО..." />;
  }

  return (
    <div className="page-grid">
      <section className="stats-grid">
        <StatCard label="Записи ТО" value={totalCount} accent="blue" />
        <StatCard label="Прострочені авто" value={dueItems.length} accent="red" />
      </section>

      <div className="two-col-grid">
        <Panel title="Додати запис ТО" subtitle="Фіксація виконаного техобслуговування.">
          <form className="form-grid" onSubmit={(event) => void addRecord(event)}>
            <label>
              Авто
              <select required value={form.vehicleId} onChange={(event) => setForm((prev) => ({ ...prev, vehicleId: event.target.value }))}>
                <option value="">Оберіть авто</option>
                {vehicles.map((vehicle) => (
                  <option key={vehicle.id} value={vehicle.id}>
                    {vehicle.makeName} {vehicle.modelName} [{vehicle.licensePlate}]
                  </option>
                ))}
              </select>
            </label>
            <label>
              Дата ТО
              <input required type="date" value={form.serviceDate} onChange={(event) => setForm((prev) => ({ ...prev, serviceDate: event.target.value }))} />
            </label>
            <label>
              Пробіг на ТО
              <input required type="number" min={0} value={form.mileageAtService} onChange={(event) => setForm((prev) => ({ ...prev, mileageAtService: event.target.value }))} />
            </label>
            <label>
              Наступне ТО (км)
              <input type="number" min={1} value={form.nextServiceMileage} onChange={(event) => setForm((prev) => ({ ...prev, nextServiceMileage: event.target.value }))} />
            </label>
            <label>
              Наступне ТО (дата)
              <input type="date" value={form.nextServiceDate} onChange={(event) => setForm((prev) => ({ ...prev, nextServiceDate: event.target.value }))} />
            </label>
            <label>
              Вартість
              <input required type="number" min={0} step="0.01" value={form.cost} onChange={(event) => setForm((prev) => ({ ...prev, cost: event.target.value }))} />
            </label>
            <label className="full-row">
              Опис
              <input required value={form.description} onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))} />
            </label>
            <button type="submit" className="btn primary">Додати запис</button>
          </form>
        </Panel>

        <Panel title="Прострочені ТО" subtitle="Авто, що потребують обслуговування.">
          <div className="table-wrap compact">
            <table>
              <thead>
                <tr>
                  <th>Авто</th>
                  <th>Пробіг</th>
                  <th>Потрібно до</th>
                  <th>Перевищення</th>
                </tr>
              </thead>
              <tbody>
                {dueItems.map((item) => (
                  <tr key={`${item.vehicleId}-${item.nextServiceMileage}`}>
                    <td>{item.vehicle}</td>
                    <td>{item.currentMileage.toLocaleString('uk-UA')}</td>
                    <td>
                      {item.nextServiceMileage ? `${item.nextServiceMileage.toLocaleString('uk-UA')} км` : '-'}
                      {item.nextServiceDate ? ` / ${formatShortDate(item.nextServiceDate)}` : ''}
                    </td>
                    <td>
                      {item.overdueByKm > 0 ? `${item.overdueByKm.toLocaleString('uk-UA')} км` : '-'}
                      {item.overdueByDays > 0 ? ` / ${item.overdueByDays} дн.` : ''}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      </div>

      <Panel title="Історія ТО" subtitle="Останні записи">
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Дата</th>
                <th>Авто</th>
                <th>Пробіг</th>
                <th>Наступне ТО</th>
                <th>Вартість</th>
                <th>Опис</th>
              </tr>
            </thead>
            <tbody>
              {records.map((record) => (
                <tr key={record.id}>
                  <td>{formatShortDate(record.serviceDate)}</td>
                  <td>{record.vehicleName}</td>
                  <td>{record.mileageAtService.toLocaleString('uk-UA')}</td>
                  <td>
                    {record.nextServiceMileage ? `${record.nextServiceMileage.toLocaleString('uk-UA')} км` : '-'}
                    {record.nextServiceDate ? ` / ${formatShortDate(record.nextServiceDate)}` : ''}
                  </td>
                  <td>{formatCurrency(record.cost)}</td>
                  <td>{record.description}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <PaginationControls
          page={page}
          pageSize={PAGE_SIZE}
          totalCount={totalCount}
          disabled={loading}
          onPageChange={setPage}
        />
      </Panel>

      {error ? <p className="error-box">{error}</p> : null}
    </div>
  );
}
