import { useEffect, useState } from 'react';
import { Api } from '../api/client';
import { useCallback } from 'react';
import type { Damage, Rental, Vehicle } from '../api/types';
import { LoadingView } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCard } from '../components/StatCard';
import { formatCurrency, formatShortDate } from '../utils/format';

const PAGE_SIZE = 25;

export function DamagesPage() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [damages, setDamages] = useState<Damage[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [rentals, setRentals] = useState<Rental[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [form, setForm] = useState({
    vehicleId: '',
    rentalId: '',
    description: 'Пошкодження кузова',
    repairCost: '',
    photoPath: '',
    autoChargeToRental: false,
  });

  const load = useCallback(async (pageToLoad: number): Promise<void> => {
    try {
      setLoading(true);
      setError(null);
      const [damageData, vehicleData, rentalData] = await Promise.all([
        Api.getDamagesPage({ page: pageToLoad, pageSize: PAGE_SIZE }),
        Api.getVehicles(),
        Api.getRentals(),
      ]);
      setDamages(damageData.items);
      setPage(damageData.page);
      setTotalCount(damageData.totalCount);
      setVehicles(vehicleData);
      setRentals(rentalData);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load(page);
  }, [load, page]);

  const addDamage = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    try {
      await Api.addDamage({
        vehicleId: Number(form.vehicleId),
        rentalId: form.rentalId ? Number(form.rentalId) : null,
        description: form.description,
        repairCost: Number(form.repairCost),
        photoPath: form.photoPath.trim() || null,
        autoChargeToRental: form.autoChargeToRental,
      });
      setForm((prev) => ({ ...prev, repairCost: '', photoPath: '' }));
      await load(page);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  if (loading) {
    return <LoadingView text="Завантаження пошкоджень..." />;
  }

  return (
    <div className="page-grid">
      <section className="stats-grid">
        <StatCard label="Записи пошкоджень" value={totalCount} accent="blue" />
        <StatCard
          label="Відкриті"
          value={damages.filter((item) => item.status !== 'Resolved').length}
          accent="red"
        />
        <StatCard
          label="Нараховано клієнтам"
          value={formatCurrency(damages.reduce((sum, item) => sum + item.chargedAmount, 0))}
          accent="amber"
        />
      </section>

      <div className="two-col-grid">
        <Panel title="Додати пошкодження" subtitle="Фіксація акту та опціональне нарахування на оренду.">
          <form className="form-grid" onSubmit={(event) => void addDamage(event)}>
            <label>
              Авто
              <select required value={form.vehicleId} onChange={(event) => setForm((prev) => ({ ...prev, vehicleId: event.target.value }))}>
                <option value="">Оберіть авто</option>
                {vehicles.map((vehicle) => (
                  <option key={vehicle.id} value={vehicle.id}>
                    {vehicle.make} {vehicle.model} [{vehicle.licensePlate}]
                  </option>
                ))}
              </select>
            </label>

            <label>
              Оренда (опційно)
              <select value={form.rentalId} onChange={(event) => setForm((prev) => ({ ...prev, rentalId: event.target.value }))}>
                <option value="">Без прив'язки</option>
                {rentals.map((rental) => (
                  <option key={rental.id} value={rental.id}>
                    {rental.contractNumber} ({rental.status})
                  </option>
                ))}
              </select>
            </label>

            <label>
              Вартість ремонту
              <input required type="number" min={0.01} step="0.01" value={form.repairCost} onChange={(event) => setForm((prev) => ({ ...prev, repairCost: event.target.value }))} />
            </label>

            <label className="full-row">
              Опис
              <input required value={form.description} onChange={(event) => setForm((prev) => ({ ...prev, description: event.target.value }))} />
            </label>

            <label className="full-row">
              Шлях до фото
              <input value={form.photoPath} onChange={(event) => setForm((prev) => ({ ...prev, photoPath: event.target.value }))} placeholder="C:\\photos\\damage.jpg" />
            </label>

            <label className="checkbox-row full-row">
              <input
                type="checkbox"
                checked={form.autoChargeToRental}
                onChange={(event) => setForm((prev) => ({ ...prev, autoChargeToRental: event.target.checked }))}
              />
              Автоматично нарахувати в суму оренди
            </label>

            <button type="submit" className="btn primary">Зберегти акт</button>
          </form>
        </Panel>

        <Panel title="Журнал пошкоджень" subtitle="Останні зареєстровані випадки">
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Дата</th>
                  <th>Акт</th>
                  <th>Авто</th>
                  <th>Договір</th>
                  <th>Ремонт</th>
                  <th>Нараховано</th>
                  <th>Статус</th>
                </tr>
              </thead>
              <tbody>
                {damages.map((item) => (
                  <tr key={item.id}>
                    <td>{formatShortDate(item.dateReported)}</td>
                    <td>{item.actNumber}</td>
                    <td>{item.vehicleName}</td>
                    <td>{item.contractNumber ?? '-'}</td>
                    <td>{formatCurrency(item.repairCost)}</td>
                    <td>{formatCurrency(item.chargedAmount)}</td>
                    <td>{item.status}</td>
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
      </div>

      {error ? <p className="error-box">{error}</p> : null}
    </div>
  );
}
