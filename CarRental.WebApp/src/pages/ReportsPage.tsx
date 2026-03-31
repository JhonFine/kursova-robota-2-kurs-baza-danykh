import { useCallback, useEffect, useMemo, useState } from 'react';
import { Api } from '../api/client';
import type { Rental, ReportSummary, Vehicle } from '../api/types';
import { EmptyState } from '../components/EmptyState';
import { FeedbackBanner } from '../components/FeedbackBanner';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { InlineSpinner } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCardSkeletons, TableSkeleton } from '../components/Skeleton';
import { StatCard } from '../components/StatCard';
import { formatCurrency, formatShortDate } from '../utils/format';

const PAGE_SIZE = 25;

function createDefaultFromDate(): string {
  return toIsoDate(new Date(Date.now() - 30 * 24 * 3600 * 1000));
}

function createDefaultToDate(): string {
  return toIsoDate(new Date());
}

function toIsoDate(value: Date): string {
  return value.toISOString().slice(0, 10);
}

function toBarWidth(value: number, maxValue: number): string {
  if (value <= 0 || maxValue <= 0) {
    return '0%';
  }

  return `${Math.max(12, Math.round((value / maxValue) * 100))}%`;
}

function buildSummary(rows: Rental[]): ReportSummary {
  // Підсумок рахуємо на frontend від повної вибірки, щоб картки KPI і export
  // ґрунтувалися на однаковому наборі договорів незалежно від поточної сторінки.
  return rows.reduce<ReportSummary>(
    (acc, item) => ({
      totalRentals: acc.totalRentals + 1,
      activeRentals: acc.activeRentals + (item.status === 'Active' ? 1 : 0),
      totalRevenue: acc.totalRevenue + (item.status === 'Closed' ? item.totalAmount : 0),
      totalDamageCost: acc.totalDamageCost + item.overageFee,
    }),
    {
      totalRentals: 0,
      activeRentals: 0,
      totalRevenue: 0,
      totalDamageCost: 0,
    },
  );
}

function downloadFile(filename: string, content: string, mimeType: string): void {
  const blob = new Blob(['\uFEFF', content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.click();
  URL.revokeObjectURL(url);
}

function toCsv(rows: Rental[]): string {
  const header = ['Дата', 'Договір', 'Клієнт', 'Авто', 'Менеджер', 'Статус', 'Сума', 'Оплачено', 'Баланс'];
  const lines = rows.map((item) => [
    formatShortDate(item.createdAtUtc),
    item.contractNumber,
    item.clientName,
    item.vehicleName,
    item.employeeName,
    item.status,
    item.totalAmount.toFixed(2),
    item.paidAmount.toFixed(2),
    item.balance.toFixed(2),
  ]);

  return [header, ...lines]
    .map((line) => line.map((cell) => `"${String(cell).replaceAll('"', '""')}"`).join(','))
    .join('\n');
}

function toExcelLike(rows: Rental[]): string {
  const header = ['Дата', 'Договір', 'Клієнт', 'Авто', 'Менеджер', 'Статус', 'Сума', 'Оплачено', 'Баланс'];
  const body = rows
    .map((item) => [
      formatShortDate(item.createdAtUtc),
      item.contractNumber,
      item.clientName,
      item.vehicleName,
      item.employeeName,
      item.status,
      item.totalAmount.toFixed(2),
      item.paidAmount.toFixed(2),
      item.balance.toFixed(2),
    ].join('\t'))
    .join('\n');

  return `${header.join('\t')}\n${body}`;
}

export function ReportsPage() {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [summary, setSummary] = useState<ReportSummary | null>(null);
  const [rentals, setRentals] = useState<Rental[]>([]);
  const [allRows, setAllRows] = useState<Rental[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [employees, setEmployees] = useState<Array<{ id: number; fullName: string }>>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [fromDate, setFromDate] = useState(createDefaultFromDate);
  const [toDate, setToDate] = useState(createDefaultToDate);
  const [vehicleId, setVehicleId] = useState<string>('');
  const [employeeId, setEmployeeId] = useState<string>('');

  const reportParams = useMemo(
    () => ({
      fromDate: `${fromDate}T00:00:00`,
      toDate: `${toDate}T23:59:59`,
      vehicleId: vehicleId ? Number(vehicleId) : undefined,
      employeeId: employeeId ? Number(employeeId) : undefined,
    }),
    [employeeId, fromDate, toDate, vehicleId],
  );

  const load = useCallback(async (pageToLoad: number): Promise<void> => {
    // Для звітів потрібні одночасно три джерела: paged table, повний набір рядків
    // для KPI/export та довідники для людських labels у фільтрах.
    try {
      setLoading(true);
      setError(null);
      const [vehiclesData, rentalsData, rows] = await Promise.all([
        Api.getVehicles(),
        Api.getReportRentalsPage({
          ...reportParams,
          page: pageToLoad,
          pageSize: PAGE_SIZE,
        }),
        Api.getReportRentals(reportParams),
      ]);

      setVehicles(vehiclesData);
      setRentals(rentalsData.items);
      setAllRows(rows);
      setSummary(buildSummary(rows));
      setPage(rentalsData.page);
      setTotalCount(rentalsData.totalCount);
      setEmployees(
        Array.from(
          rows.reduce((map, item) => {
            if (!map.has(item.employeeId)) {
              map.set(item.employeeId, { id: item.employeeId, fullName: item.employeeName });
            }

            return map;
          }, new Map<number, { id: number; fullName: string }>())
            .values(),
        ),
      );
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }, [reportParams]);

  useEffect(() => {
    void load(page);
  }, [load, page]);

  const exportCsv = (): void => {
    downloadFile(`rentals_${fromDate}_${toDate}.csv`, toCsv(allRows), 'text/csv;charset=utf-8');
  };

  const exportExcel = (): void => {
    downloadFile(
      `rentals_${fromDate}_${toDate}.xls`,
      toExcelLike(allRows),
      'application/vnd.ms-excel;charset=utf-8',
    );
  };
  const resetFilters = (): void => {
    setPage(1);
    setFromDate(createDefaultFromDate());
    setToDate(createDefaultToDate());
    setVehicleId('');
    setEmployeeId('');
  };
  const selectedVehicleLabel = useMemo(
    () => vehicles.find((item) => String(item.id) === vehicleId),
    [vehicleId, vehicles],
  );
  const selectedEmployeeLabel = useMemo(
    () => employees.find((item) => String(item.id) === employeeId),
    [employeeId, employees],
  );
  const activeFilters = useMemo(() => {
    // Діапазон завжди показуємо окремим chip, бо це базовий зріз для всіх звітів,
    // навіть якщо решта фільтрів не вибрані.
    const items: ActiveFilterChipItem[] = [
      {
        key: 'range',
        label: `${formatShortDate(`${fromDate}T00:00:00`)} - ${formatShortDate(`${toDate}T00:00:00`)}`,
        tone: 'accent',
      },
    ];

    if (selectedVehicleLabel) {
      items.push({
        key: 'vehicle',
        label: `Авто: ${selectedVehicleLabel.make} ${selectedVehicleLabel.model}`,
        onRemove: () => {
          setPage(1);
          setVehicleId('');
        },
      });
    }

    if (selectedEmployeeLabel) {
      items.push({
        key: 'employee',
        label: `Менеджер: ${selectedEmployeeLabel.fullName}`,
        onRemove: () => {
          setPage(1);
          setEmployeeId('');
        },
      });
    }

    return items;
  }, [fromDate, selectedEmployeeLabel, selectedVehicleLabel, toDate]);

  const volumeMax = Math.max(summary?.totalRentals ?? 0, summary?.activeRentals ?? 0, 1);
  const financeMax = Math.max(summary?.totalRevenue ?? 0, summary?.totalDamageCost ?? 0, 1);
  const activeShare = summary && summary.totalRentals > 0
    ? Math.round((summary.activeRentals / summary.totalRentals) * 100)
    : 0;

  if (loading && !summary) {
    return (
      <div className="staff-dashboard">
        <Panel title="Звіти та аналітика" subtitle="Готуємо KPI, зрізи та доступні фільтри.">
          <TableSkeleton rows={3} compact />
        </Panel>

        <StatCardSkeletons count={4} />

        <div className="reports-analytics">
          <Panel title="Ключові сигнали" subtitle="Підготовка оглядових індикаторів.">
            <TableSkeleton rows={4} compact />
          </Panel>
          <Panel title="Підсумок" subtitle="Фінансові й операційні акценти.">
            <TableSkeleton rows={4} compact />
          </Panel>
        </div>

        <Panel title="Результати" subtitle="Підготовка таблиці договорів.">
          <TableSkeleton rows={8} />
        </Panel>
      </div>
    );
  }

  return (
    <div className="staff-dashboard">
      <Panel
        title="Звіти та аналітика"
        subtitle="KPI і таблиця працюють у межах одного й того самого фільтра."
      >
        <FilterToolbar
          chips={activeFilters}
          footerNote={`У поточному зрізі: ${allRows.length} записів за вибраний період і набір фільтрів.`}
          actions={(
            <>
              <button type="button" className="btn ghost" onClick={resetFilters}>
                Скинути
              </button>
              <button type="button" className="btn primary" onClick={() => void load(page)}>
                Оновити
              </button>
              <button type="button" className="btn ghost" onClick={exportCsv}>
                Експорт CSV
              </button>
              <button type="button" className="btn ghost" onClick={exportExcel}>
                Експорт Excel
              </button>
            </>
          )}
        >
          <FilterField label="Період з">
            <input
              type="date"
              value={fromDate}
              onChange={(event) => {
                setPage(1);
                setFromDate(event.target.value);
              }}
            />
          </FilterField>
          <FilterField label="Період до">
            <input
              type="date"
              value={toDate}
              onChange={(event) => {
                setPage(1);
                setToDate(event.target.value);
              }}
            />
          </FilterField>
          <FilterField label="Авто">
            <select
              value={vehicleId}
              onChange={(event) => {
                setPage(1);
                setVehicleId(event.target.value);
              }}
            >
              <option value="">Усі авто</option>
              {vehicles.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.make} {item.model} [{item.licensePlate}]
                </option>
              ))}
            </select>
          </FilterField>
          <FilterField label="Менеджер">
            <select
              value={employeeId}
              onChange={(event) => {
                setPage(1);
                setEmployeeId(event.target.value);
              }}
            >
              <option value="">Усі менеджери</option>
              {employees.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.fullName}
                </option>
              ))}
            </select>
          </FilterField>
        </FilterToolbar>
      </Panel>

      <section className="stats-grid">
        <StatCard label="Усього оренд" value={summary?.totalRentals ?? 0} accent="blue" />
        <StatCard label="Активні зараз" value={summary?.activeRentals ?? 0} accent="mint" />
        <StatCard label="Дохід (закриті оренди)" value={formatCurrency(summary?.totalRevenue ?? 0)} accent="amber" />
        <StatCard label="Витрати на пошкодження" value={formatCurrency(summary?.totalDamageCost ?? 0)} accent="red" />
      </section>

      <div className="reports-analytics">
        <Panel title="Ключові сигнали" subtitle="Швидкий зріз по обсягу, активності та фінансах.">
          <div className="reports-bars">
            <article className="reports-bar-card">
              <div className="reports-bar-head">
                <span>Обсяг договорів</span>
                <strong>{summary?.totalRentals ?? 0}</strong>
              </div>
              <div className="reports-bar-track">
                <div className="reports-bar-fill" style={{ width: toBarWidth(summary?.totalRentals ?? 0, volumeMax) }} />
              </div>
            </article>

            <article className="reports-bar-card">
              <div className="reports-bar-head">
                <span>Активні зараз</span>
                <strong>{activeShare}% від усіх</strong>
              </div>
              <div className="reports-bar-track">
                <div className="reports-bar-fill" style={{ width: toBarWidth(summary?.activeRentals ?? 0, volumeMax) }} />
              </div>
            </article>

            <article className="reports-bar-card">
              <div className="reports-bar-head">
                <span>Пошкодження vs дохід</span>
                <strong>{formatCurrency(summary?.totalDamageCost ?? 0)}</strong>
              </div>
              <div className="reports-bar-track">
                <div className="reports-bar-fill is-danger" style={{ width: toBarWidth(summary?.totalDamageCost ?? 0, financeMax) }} />
              </div>
            </article>
          </div>
        </Panel>

        <Panel title="Підсумок" subtitle="Що відбулося у вибраному зрізі.">
          <div className="reports-summary-card">
            <strong>Звіт сформовано для періоду {formatShortDate(`${fromDate}T00:00:00`)} - {formatShortDate(`${toDate}T00:00:00`)}</strong>
            <p>
              У вибірці {allRows.length} записів. Активні оренди залишаються в роботі, а закриті договори формують дохідний зріз для звітності.
            </p>

            <div className="reports-summary-grid">
              <div>
                <span>Закриті договори</span>
                <strong>{(summary?.totalRentals ?? 0) - (summary?.activeRentals ?? 0)}</strong>
              </div>
              <div>
                <span>Орієнтовна частка активних</span>
                <strong>{activeShare}%</strong>
              </div>
              <div>
                <span>Дохід</span>
                <strong>{formatCurrency(summary?.totalRevenue ?? 0)}</strong>
              </div>
              <div>
                <span>Втрати/пошкодження</span>
                <strong>{formatCurrency(summary?.totalDamageCost ?? 0)}</strong>
              </div>
            </div>
          </div>
        </Panel>
      </div>

      <Panel title="Результати" subtitle={`Записів на сторінці: ${rentals.length} із ${totalCount}`}>
        <div className={`surface-refresh${loading ? ' is-refreshing' : ''}`}>
          {rentals.length === 0 ? (
            <EmptyState
              icon="RPT"
              title="У вибраному періоді немає записів."
              description="Змініть діапазон дат або приберіть додаткові фільтри, щоб побачити повний зріз договорів."
              actions={(
                <>
                  <button type="button" className="btn ghost" onClick={resetFilters}>
                    Скинути фільтри
                  </button>
                  <button type="button" className="btn primary" onClick={() => void load(page)}>
                    Оновити звіт
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
                      <th>Дата</th>
                      <th>Договір</th>
                      <th>Клієнт</th>
                      <th>Авто</th>
                      <th>Менеджер</th>
                      <th>Статус</th>
                      <th>Сума</th>
                      <th>Оплачено</th>
                      <th>Баланс</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rentals.map((item) => (
                      <tr key={item.id}>
                        <td>{formatShortDate(item.createdAtUtc)}</td>
                        <td>{item.contractNumber}</td>
                        <td>{item.clientName}</td>
                        <td>{item.vehicleName}</td>
                        <td>{item.employeeName}</td>
                        <td>{item.status}</td>
                        <td>{formatCurrency(item.totalAmount)}</td>
                        <td>{formatCurrency(item.paidAmount)}</td>
                        <td>{formatCurrency(item.balance)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              {loading ? (
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
          disabled={loading}
          onPageChange={setPage}
        />
      </Panel>

      {error ? (
        <FeedbackBanner tone="error" title="Не вдалося сформувати звіт" onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
    </div>
  );
}
