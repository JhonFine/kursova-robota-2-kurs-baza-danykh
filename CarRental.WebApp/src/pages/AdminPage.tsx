import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../auth/useAuth';
import { useCallback } from 'react';
import { Api } from '../api/client';
import type { Employee } from '../api/types';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { LoadingView } from '../components/LoadingView';
import { Panel } from '../components/Panel';
import { StatCard } from '../components/StatCard';
import { formatDate } from '../utils/format';

function roleDisplay(role: Employee['role']): string {
  if (role === 'Admin') {
    return 'Адміністратор';
  }

  if (role === 'Manager') {
    return 'Менеджер';
  }

  return 'Користувач';
}

type PendingAction = {
  title: string;
  description: string;
  confirmLabel: string;
  tone: 'default' | 'danger';
  action: () => Promise<void>;
} | null;

export function AdminPage() {
  const { user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [status, setStatus] = useState<string>('Оберіть працівника в таблиці для керування доступом.');
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [filterQuery, setFilterQuery] = useState('');
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [pendingAction, setPendingAction] = useState<PendingAction>(null);

  const filteredEmployees = useMemo(() => {
    const query = filterQuery.trim().toLowerCase();
    if (!query) {
      return employees;
    }

    return employees.filter((item) => {
      const text = `${item.fullName} ${item.login} ${roleDisplay(item.role)}`.toLowerCase();
      return text.includes(query);
    });
  }, [employees, filterQuery]);

  const selectedEmployee = useMemo(
    () => filteredEmployees.find((item) => item.id === selectedId) ?? employees.find((item) => item.id === selectedId) ?? null,
    [filteredEmployees, employees, selectedId],
  );
  const isSelfSelected = selectedEmployee?.id === user?.id;
  const canMutate = true;

  const lockedEmployees = useMemo(
    () => employees.filter((item) => item.lockoutUntilUtc && new Date(item.lockoutUntilUtc) > new Date()).length,
    [employees],
  );
  const accessModeSummary = 'Підтвердження змін виконується через confirmation modal.';
  const activeFilters = useMemo(() => {
    const items: ActiveFilterChipItem[] = [];

    if (filterQuery.trim()) {
      items.push({
        key: 'query',
        label: `Пошук: ${filterQuery.trim()}`,
        onRemove: () => setFilterQuery(''),
      });
    }

    return items;
  }, [filterQuery]);

  const load = useCallback(async (): Promise<void> => {
    try {
      setLoading(true);
      setError(null);
      const data = await Api.getEmployees();
      setEmployees(data);

      setSelectedId((currentSelectedId) => (
        currentSelectedId && !data.some((item) => item.id === currentSelectedId)
          ? null
          : currentSelectedId
      ));
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const runAction = async (callback: () => Promise<void>, successText: string): Promise<void> => {
    try {
      setError(null);
      await callback();
      setStatus(successText);
      setPendingAction(null);
      await load();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const promptAction = (
    title: string,
    description: string,
    confirmLabel: string,
    action: () => Promise<void>,
    tone: 'default' | 'danger' = 'default',
  ): void => {
    setPendingAction({
      title,
      description,
      confirmLabel,
      tone,
      action,
    });
  };

  const toggleActive = async (): Promise<void> => {
    if (!selectedEmployee) {
      return;
    }

    await runAction(
      () => Api.toggleEmployeeActive(selectedEmployee.id).then(() => undefined),
      `Стан працівника "${selectedEmployee.fullName}" оновлено.`,
    );
  };

  const toggleRole = async (): Promise<void> => {
    if (!selectedEmployee) {
      return;
    }

    await runAction(
      () => Api.toggleEmployeeManagerRole(selectedEmployee.id).then(() => undefined),
      `Роль працівника "${selectedEmployee.fullName}" оновлено.`,
    );
  };

  const unlock = async (): Promise<void> => {
    if (!selectedEmployee) {
      return;
    }

    await runAction(
      () => Api.unlockEmployee(selectedEmployee.id).then(() => undefined),
      `Працівника "${selectedEmployee.fullName}" розблоковано.`,
    );
  };

  const changePassword = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (newPassword.length < 8) {
      setError('Мінімальна довжина нового пароля: 8 символів.');
      return;
    }

    try {
      setError(null);
      await Api.changePassword(currentPassword, newPassword);
      setCurrentPassword('');
      setNewPassword('');
      setStatus('Пароль успішно змінено.');
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  if (loading) {
    return <LoadingView text="Завантаження адміністрування..." />;
  }

  return (
    <>
      <div className="page-grid">
        <Panel title="Панель адміністратора" subtitle="Керуйте працівниками, ролями та безпекою облікових записів.">
          <FilterToolbar
            chips={activeFilters}
            footerNote={accessModeSummary}
            actions={(
              <>
                <button type="button" className="btn ghost" onClick={() => setFilterQuery('')}>
                  Очистити
                </button>
                <button type="button" className="btn primary" onClick={() => void load()}>
                  Оновити список
                </button>
              </>
            )}
          >
            <FilterField label="Пошук" hint="ПІБ, логін або роль">
              <input
                value={filterQuery}
                onChange={(event) => setFilterQuery(event.target.value)}
                placeholder="ПІБ, логін або роль"
              />
            </FilterField>
          </FilterToolbar>
        </Panel>

        <section className="stats-grid">
          <StatCard label="Всього працівників" value={employees.length} accent="blue" />
          <StatCard label="Активні" value={employees.filter((item) => item.isActive).length} accent="mint" />
          <StatCard label="Менеджери" value={employees.filter((item) => item.role === 'Manager').length} accent="amber" />
          <StatCard label="Заблоковані" value={lockedEmployees} accent="red" />
        </section>

        <div className="admin-workspace">
          <Panel title="Працівники" subtitle={`Відображено: ${filteredEmployees.length}`}>
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>ПІБ</th>
                    <th>Логін</th>
                    <th>Роль</th>
                    <th>Стан</th>
                    <th>Блокування до</th>
                    <th>Останній вхід</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredEmployees.map((employee) => (
                    <tr
                      key={employee.id}
                      className={selectedId === employee.id ? 'selected-row' : ''}
                      onClick={() => setSelectedId(employee.id)}
                    >
                      <td>{employee.fullName}</td>
                      <td>{employee.login}</td>
                      <td>{roleDisplay(employee.role)}</td>
                      <td>
                        <span className={`status-pill ${employee.isActive ? 'ok' : 'bad'}`}>
                          {employee.isActive ? 'Активний' : 'Вимкнений'}
                        </span>
                      </td>
                      <td>{formatDate(employee.lockoutUntilUtc)}</td>
                      <td>{formatDate(employee.lastLoginUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>

          <div className="page-grid">
            <Panel
              title="Дії з працівником"
              subtitle={selectedEmployee ? 'Окремі зміни вимагають явного підтвердження.' : 'Оберіть працівника в таблиці'}
            >
              <div className="kv-grid">
                <strong>Працівник:</strong>
                <span>{selectedEmployee?.fullName ?? '—'}</span>
                <strong>Роль:</strong>
                <span>{selectedEmployee ? roleDisplay(selectedEmployee.role) : '—'}</span>
                <strong>Стан:</strong>
                <span>{selectedEmployee ? (selectedEmployee.isActive ? 'Активний' : 'Вимкнений') : '—'}</span>
                <strong>Блокування:</strong>
                <span>{selectedEmployee ? formatDate(selectedEmployee.lockoutUntilUtc) : '—'}</span>
              </div>

              {!canMutate ? (
                <p className="muted" style={{ marginTop: '12px' }}>
                  Зміни заблоковані для поточного режиму доступу.
                </p>
              ) : null}

              <div className="page-grid" style={{ marginTop: '10px' }}>
                <button
                  type="button"
                  className="btn ghost"
                  disabled={!canMutate || !selectedEmployee || (isSelfSelected && selectedEmployee.isActive)}
                  onClick={() => {
                    if (!selectedEmployee) {
                      return;
                    }

                    if (selectedEmployee.isActive) {
                      promptAction(
                        'Вимкнути працівника?',
                        `Працівник "${selectedEmployee.fullName}" втратить доступ до системи, доки його не буде активовано повторно.`,
                        'Вимкнути',
                        toggleActive,
                        'danger',
                      );
                      return;
                    }

                    void toggleActive();
                  }}
                >
                  {selectedEmployee?.isActive ? 'Вимкнути працівника' : 'Активувати працівника'}
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  disabled={!canMutate || !selectedEmployee || selectedEmployee.role === 'Admin'}
                  onClick={() => {
                    if (!selectedEmployee) {
                      return;
                    }

                    promptAction(
                      'Змінити роль працівника?',
                      selectedEmployee.role === 'Manager'
                        ? `Працівника "${selectedEmployee.fullName}" буде переведено в роль користувача.`
                        : `Працівника "${selectedEmployee.fullName}" буде переведено в роль менеджера.`,
                      selectedEmployee.role === 'Manager' ? 'Зробити користувачем' : 'Зробити менеджером',
                      toggleRole,
                    );
                  }}
                >
                  {selectedEmployee?.role === 'Admin'
                    ? 'Роль адміністратора не змінюється'
                    : selectedEmployee?.role === 'Manager'
                      ? 'Зробити користувачем'
                      : 'Зробити менеджером'}
                </button>
                <button
                  type="button"
                  className="btn primary"
                  disabled={!canMutate || !selectedEmployee}
                  onClick={() => void unlock()}
                >
                  Розблокувати / скинути спроби входу
                </button>
              </div>
            </Panel>

            <Panel title="Зміна мого пароля" subtitle="Використовуйте пароль, який не повторює попередній.">
              <form className="form-grid" onSubmit={(event) => void changePassword(event)}>
                <label className="full-row">
                  Поточний пароль
                  <input
                    type="password"
                    value={currentPassword}
                    onChange={(event) => setCurrentPassword(event.target.value)}
                  />
                </label>
                <label className="full-row">
                  Новий пароль
                  <input
                    type="password"
                    value={newPassword}
                    onChange={(event) => setNewPassword(event.target.value)}
                  />
                </label>
                <button
                  type="submit"
                  className="btn primary"
                  disabled={!canMutate || !currentPassword || !newPassword}
                >
                  Змінити пароль
                </button>
              </form>
            </Panel>
          </div>
        </div>

        <div className="status-panel">
          <strong>Статус:</strong> {status}
        </div>

        {error ? <p className="error-box">{error}</p> : null}
      </div>

      <ConfirmDialog
        open={pendingAction !== null}
        title={pendingAction?.title ?? ''}
        description={pendingAction?.description ?? ''}
        confirmLabel={pendingAction?.confirmLabel ?? 'Підтвердити'}
        tone={pendingAction?.tone ?? 'default'}
        onCancel={() => setPendingAction(null)}
        onConfirm={() => {
          if (!pendingAction) {
            return;
          }

          void pendingAction.action();
        }}
      />
    </>
  );
}
