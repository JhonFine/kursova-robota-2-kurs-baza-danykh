import { useEffect, useMemo, useState } from 'react';
import { Api } from '../api/client';
import type { Rental, ReportSummary, Vehicle } from '../api/types';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { LoadingView } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
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

function buildSummary(rows: Rental[]): ReportSummary {
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
  const blob = new Blob([content], { type: mimeType });
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

  const load = async (pageToLoad = page): Promise<void> => {
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
  };

  useEffect(() => {
    void load(page);
  }, [page, reportParams]);

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
  }, [employeeId, employees, fromDate, selectedEmployeeLabel, selectedVehicleLabel, toDate, vehicleId]);

  if (loading && !summary) {
    return <LoadingView text="Формування звітів..." />;
  }

  return (
    <div className="page-grid">
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

      <Panel title="Результати" subtitle={`Записів на сторінці: ${rentals.length} із ${totalCount}`}>
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
