import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Api } from '../api/client';
import type { Client } from '../api/types';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { LoadingView } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCard } from '../components/StatCard';
import { parseEnumParam, parsePositiveIntParam, withUpdatedSearchParams } from '../utils/searchParams';

const emptyForm = {
  fullName: '',
  passportData: '',
  driverLicense: '',
  phone: '',
  blacklisted: false,
};

const PAGE_SIZE = 25;
const blacklistFilterValues = ['all', 'active', 'blacklisted'] as const;
type BlacklistFilter = (typeof blacklistFilterValues)[number];

const blacklistFilterLabels: Record<BlacklistFilter, string> = {
  all: 'Усі клієнти',
  active: 'Лише активні',
  blacklisted: 'Лише blacklist',
};

export function ClientsPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [clients, setClients] = useState<Client[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const requestIdRef = useRef(0);

  const page = parsePositiveIntParam(searchParams.get('page'), 1);
  const search = searchParams.get('search') ?? '';
  const blacklistFilter = parseEnumParam(searchParams.get('blacklisted'), blacklistFilterValues, 'all');

  const updateListParams = useCallback((updates: {
    page?: number | null;
    search?: string | null;
    blacklisted?: BlacklistFilter | null;
  }): void => {
    setSearchParams((current) => withUpdatedSearchParams(current, updates));
  }, [setSearchParams]);

  const selected = useMemo(() => clients.find((item) => item.id === selectedId) ?? null, [clients, selectedId]);
  const activeFilters = useMemo(() => {
    const items: ActiveFilterChipItem[] = [];

    if (search.trim()) {
      items.push({
        key: 'search',
        label: `Пошук: ${search.trim()}`,
        onRemove: () => updateListParams({ search: null, page: 1 }),
      });
    }

    if (blacklistFilter !== 'all') {
      items.push({
        key: 'blacklist',
        label: `Статус: ${blacklistFilterLabels[blacklistFilter]}`,
        onRemove: () => updateListParams({ blacklisted: null, page: 1 }),
      });
    }

    return items;
  }, [blacklistFilter, search, updateListParams]);

  const loadClients = useCallback(async (): Promise<void> => {
    const requestId = ++requestIdRef.current;

    try {
      setLoading(true);
      setError(null);

      const response = await Api.getClientsPage({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        blacklisted:
          blacklistFilter === 'all'
            ? undefined
            : blacklistFilter === 'blacklisted',
      });

      if (requestId !== requestIdRef.current) {
        return;
      }

      setClients(response.items);
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
        setLoading(false);
      }
    }
  }, [blacklistFilter, page, search]);

  useEffect(() => {
    void loadClients();
  }, [loadClients]);

  useEffect(() => {
    if (!selected) {
      setForm(emptyForm);
      return;
    }

    setForm({
      fullName: selected.fullName,
      passportData: selected.passportData,
      driverLicense: selected.driverLicense,
      phone: selected.phone,
      blacklisted: selected.blacklisted,
    });
  }, [selected]);

  const submitCreate = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    try {
      setError(null);
      await Api.createClient(form);
      setForm(emptyForm);
      setMessage('Клієнта додано до реєстру.');
      await loadClients();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const submitUpdate = async (): Promise<void> => {
    if (!selected) {
      return;
    }

    try {
      setError(null);
      await Api.updateClient(selected.id, form);
      setMessage(`Профіль клієнта "${selected.fullName}" оновлено.`);
      await loadClients();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const toggleBlacklist = async (): Promise<void> => {
    if (!selected) {
      return;
    }

    try {
      setError(null);
      await Api.setClientBlacklist(selected.id, !selected.blacklisted);
      setMessage(
        selected.blacklisted
          ? `Клієнта "${selected.fullName}" повернуто до активного списку.`
          : `Клієнта "${selected.fullName}" додано до чорного списку.`,
      );
      await loadClients();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const removeClient = async (): Promise<void> => {
    if (!selected) {
      return;
    }

    try {
      setError(null);
      await Api.deleteClient(selected.id);
      setMessage(`Клієнта "${selected.fullName}" видалено.`);
      setSelectedId(null);
      setIsDeleteDialogOpen(false);
      await loadClients();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  if (loading) {
    return <LoadingView text="Завантаження клієнтів..." />;
  }

  return (
    <>
      <div className="page-grid">
        <section className="stats-grid">
          <StatCard label="Усього у вибірці" value={totalCount} accent="blue" />
          <StatCard
            label="У blacklist у вибірці"
            value={
              blacklistFilter === 'blacklisted'
                ? totalCount
                : clients.filter((item) => item.blacklisted).length
            }
            accent="red"
          />
        </section>

        <Panel
          title="Реєстр клієнтів"
          subtitle="CRM-реєстр з server-side пошуком, blacklist-фільтрами та URL-станом."
        >
          <FilterToolbar
            chips={activeFilters}
            footerNote="Вибрані параметри збережені в адресі сторінки, тому реєстр легко відкрити повторно з тією самою вибіркою."
            actions={(
              <>
                <button
                  type="button"
                  className="btn ghost"
                  onClick={() => updateListParams({
                    page: null,
                    search: null,
                    blacklisted: null,
                  })}
                >
                  Скинути
                </button>
                <button
                  type="button"
                  className="btn"
                  onClick={() => void loadClients()}
                >
                  Оновити
                </button>
              </>
            )}
          >
            <FilterField label="Пошук" hint="ПІБ, телефон або водійське">
              <input
                value={search}
                onChange={(event) => updateListParams({
                  search: event.target.value,
                  page: 1,
                })}
                placeholder="ПІБ, телефон або водійське"
              />
            </FilterField>

            <FilterField label="Статус">
              <select
                value={blacklistFilter}
                onChange={(event) => updateListParams({
                  blacklisted: event.target.value as BlacklistFilter,
                  page: 1,
                })}
              >
                <option value="all">Усі клієнти</option>
                <option value="active">Лише активні</option>
                <option value="blacklisted">Лише blacklist</option>
              </select>
            </FilterField>
          </FilterToolbar>

          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>ПІБ</th>
                  <th>Телефон</th>
                  <th>Водійське</th>
                  <th>Статус</th>
                </tr>
              </thead>
              <tbody>
                {clients.map((item) => (
                  <tr
                    key={item.id}
                    onClick={() => setSelectedId(item.id)}
                    className={selectedId === item.id ? 'selected-row' : ''}
                  >
                    <td>{item.id}</td>
                    <td>{item.fullName}</td>
                    <td>{item.phone}</td>
                    <td>{item.driverLicense}</td>
                    <td>
                      <span className={`status-pill ${item.blacklisted ? 'bad' : 'ok'}`}>
                        {item.blacklisted ? 'У чорному списку' : 'Активний'}
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
            disabled={loading}
            onPageChange={(nextPage) => updateListParams({ page: nextPage })}
          />
        </Panel>

        <div className="two-col-grid">
          <Panel title="Новий клієнт" subtitle="Створення нового запису">
            <form className="form-grid" onSubmit={(event) => void submitCreate(event)}>
              <label>
                ПІБ
                <input required value={form.fullName} onChange={(event) => setForm((prev) => ({ ...prev, fullName: event.target.value }))} />
              </label>
              <label>
                Паспортні дані
                <input required value={form.passportData} onChange={(event) => setForm((prev) => ({ ...prev, passportData: event.target.value }))} />
              </label>
              <label>
                Водійське посвідчення
                <input required value={form.driverLicense} onChange={(event) => setForm((prev) => ({ ...prev, driverLicense: event.target.value }))} />
              </label>
              <label>
                Телефон
                <input required value={form.phone} onChange={(event) => setForm((prev) => ({ ...prev, phone: event.target.value }))} />
              </label>
              <label className="checkbox-row">
                <input type="checkbox" checked={form.blacklisted} onChange={(event) => setForm((prev) => ({ ...prev, blacklisted: event.target.checked }))} />
                У чорному списку
              </label>
              <button type="submit" className="btn primary">Створити</button>
            </form>
          </Panel>

          <Panel title="Деталі клієнта" subtitle={selected ? `ID ${selected.id}` : 'Оберіть клієнта в таблиці'}>
            <form className="form-grid" onSubmit={(event) => event.preventDefault()}>
              <label>
                ПІБ
                <input value={form.fullName} onChange={(event) => setForm((prev) => ({ ...prev, fullName: event.target.value }))} disabled={!selected} />
              </label>
              <label>
                Паспортні дані
                <input value={form.passportData} onChange={(event) => setForm((prev) => ({ ...prev, passportData: event.target.value }))} disabled={!selected} />
              </label>
              <label>
                Водійське посвідчення
                <input value={form.driverLicense} onChange={(event) => setForm((prev) => ({ ...prev, driverLicense: event.target.value }))} disabled={!selected} />
              </label>
              <label>
                Телефон
                <input value={form.phone} onChange={(event) => setForm((prev) => ({ ...prev, phone: event.target.value }))} disabled={!selected} />
              </label>
              <label className="checkbox-row">
                <input type="checkbox" checked={form.blacklisted} onChange={(event) => setForm((prev) => ({ ...prev, blacklisted: event.target.checked }))} disabled={!selected} />
                У чорному списку
              </label>
              <div className="inline-form">
                <button type="button" className="btn primary" disabled={!selected} onClick={() => void submitUpdate()}>
                  Зберегти
                </button>
                <button type="button" className="btn warning" disabled={!selected} onClick={() => void toggleBlacklist()}>
                  Додати/прибрати зі списку
                </button>
                <button type="button" className="btn danger" disabled={!selected} onClick={() => setIsDeleteDialogOpen(true)}>
                  Видалити
                </button>
              </div>
            </form>
          </Panel>
        </div>

        {message ? <p className="success-box">{message}</p> : null}
        {error ? <p className="error-box">{error}</p> : null}
      </div>

      <ConfirmDialog
        open={isDeleteDialogOpen}
        title="Видалити клієнта?"
        description={selected ? `Запис для "${selected.fullName}" буде остаточно видалено зі списку на сайті.` : ''}
        confirmLabel="Видалити клієнта"
        tone="danger"
        onCancel={() => setIsDeleteDialogOpen(false)}
        onConfirm={() => void removeClient()}
      />
    </>
  );
}
