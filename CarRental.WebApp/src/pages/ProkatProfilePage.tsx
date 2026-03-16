import { useCallback, useEffect, useRef, useState } from 'react';
import { Api } from '../api/client';
import type { ClientProfile } from '../api/types';
import { LoadingView } from '../components/LoadingView';
import {
  CLIENT_DRIVER_LICENSE_MAX_LENGTH,
  CLIENT_FULL_NAME_MAX_LENGTH,
  CLIENT_PASSPORT_MAX_LENGTH,
  CLIENT_PHONE_MAX_LENGTH,
  getClientProfileCompletionIssues,
  getClientProfileCompletionMessage,
  isLegacyDriverLicense,
  isLegacyPassportData,
  PASSWORD_MAX_LENGTH,
} from './prokatShared';

export function ProkatProfilePage() {
  const [loading, setLoading] = useState(true);
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [profile, setProfile] = useState<ClientProfile | null>(null);
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [passportData, setPassportData] = useState('');
  const [driverLicense, setDriverLicense] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const requestIdRef = useRef(0);
  const profileDraft = { fullName, phone, passportData, driverLicense };
  const completionIssues = getClientProfileCompletionIssues(profileDraft);
  const completionMessage = getClientProfileCompletionMessage(profileDraft);

  const loadProfile = useCallback(async (): Promise<void> => {
    const requestId = ++requestIdRef.current;

    try {
      setLoading(true);
      setError(null);

      const data = await Api.getOwnClient();
      if (requestId !== requestIdRef.current) {
        return;
      }

      setProfile(data);
      setFullName(data.fullName);
      setPhone(data.phone);
      setPassportData(data.passportData);
      setDriverLicense(data.driverLicense);
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
  }, []);

  useEffect(() => {
    void loadProfile();
  }, [loadProfile]);

  const saveProfile = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    try {
      setSavingProfile(true);
      setError(null);
      setMessage(null);

      const updated = await Api.updateOwnClientProfile({
        fullName,
        phone,
        passportData,
        driverLicense,
      });

      setProfile(updated);
      setFullName(updated.fullName);
      setPhone(updated.phone);
      setPassportData(updated.passportData);
      setDriverLicense(updated.driverLicense);

      const updatedCompletionMessage = getClientProfileCompletionMessage(updated);
      setMessage(updated.isComplete
        ? 'Профіль клієнта оновлено.'
        : `Профіль оновлено. ${updatedCompletionMessage ?? 'Для бронювання ще потрібно завершити всі поля.'}`);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setSavingProfile(false);
    }
  };

  const changePassword = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (newPassword.length < 8) {
      setError('Мінімальна довжина нового пароля: 8 символів.');
      return;
    }

    try {
      setSavingPassword(true);
      setError(null);
      setMessage(null);

      await Api.changePassword(currentPassword, newPassword);
      setCurrentPassword('');
      setNewPassword('');
      setMessage('Пароль успішно змінено.');
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setSavingPassword(false);
    }
  };

  if (loading) {
    return <LoadingView text="Завантаження профілю клієнта..." />;
  }

  return (
    <div className="page-grid prokat-page">
      {error ? <p className="error-box">{error}</p> : null}
      {message ? <p className="success-box">{message}</p> : null}

      <section className="prokat-hero">
        <div className="prokat-hero-copy">
          <span className="topbar-kicker">Профіль</span>
          <h2>Контакти, документи та пароль</h2>
          <p>
            Перед першою орендою заповніть реальні паспортні дані, посвідчення водія та актуальний номер телефону.
          </p>
        </div>

        <div className="prokat-hero-side">
          <div className="prokat-hero-stats">
            <div>
              <span>Статус профілю</span>
              <strong>{profile?.isComplete ? 'Готовий' : 'Неповний'}</strong>
            </div>
          </div>
        </div>
      </section>

      {!profile?.isComplete ? (
        <section className="status-panel">
          <strong>Профіль потрібно завершити</strong>
          {completionMessage ? <p className="muted">{completionMessage}</p> : null}
          <p className="muted">
            Поки профіль неповний, оформлення нових бронювань у каталозі буде заблоковано.
          </p>
        </section>
      ) : null}

      <div className="two-col-grid">
        <section className="status-panel">
          <div className="prokat-review-heading">
            <span>Секція 1</span>
            <strong>Контакти і документи</strong>
          </div>

          <form className="form-grid" onSubmit={(event) => void saveProfile(event)}>
            {completionIssues.length > 0 ? (
              <div className="prokat-profile-hints full-row">
                <strong>Що ще потрібно для повного профілю</strong>
                <ul>
                  {completionIssues.map((issue) => (
                    <li key={issue}>{issue}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            <label className="full-row">
              ПІБ
              <input
                value={fullName}
                onChange={(event) => setFullName(event.target.value.slice(0, CLIENT_FULL_NAME_MAX_LENGTH))}
                maxLength={CLIENT_FULL_NAME_MAX_LENGTH}
                autoComplete="name"
                placeholder="Коваленко Іван Олександрович"
              />
            </label>

            <label>
              Телефон
              <input
                value={phone}
                onChange={(event) => setPhone(event.target.value.slice(0, CLIENT_PHONE_MAX_LENGTH))}
                maxLength={CLIENT_PHONE_MAX_LENGTH}
                inputMode="tel"
                autoComplete="tel"
                placeholder="+380671234567"
              />
            </label>

            <label>
              Паспорт
              <input
                value={passportData}
                onChange={(event) => setPassportData(event.target.value.slice(0, CLIENT_PASSPORT_MAX_LENGTH))}
                maxLength={CLIENT_PASSPORT_MAX_LENGTH}
                spellCheck={false}
                placeholder="МК123456"
              />
              {isLegacyPassportData(passportData) ? (
                <small className="prokat-field-hint warn">EMP-... не рахується реальними паспортними даними.</small>
              ) : null}
            </label>

            <label className="full-row">
              Посвідчення водія
              <input
                value={driverLicense}
                onChange={(event) => setDriverLicense(event.target.value.slice(0, CLIENT_DRIVER_LICENSE_MAX_LENGTH))}
                maxLength={CLIENT_DRIVER_LICENSE_MAX_LENGTH}
                spellCheck={false}
                placeholder="ВХЕ123456"
              />
              {isLegacyDriverLicense(driverLicense) ? (
                <small className="prokat-field-hint warn">USR-... не рахується реальним посвідченням водія.</small>
              ) : null}
            </label>

            <button type="submit" className="btn primary" disabled={savingProfile}>
              {savingProfile ? 'Збереження...' : 'Зберегти профіль'}
            </button>
          </form>
        </section>

        <section className="status-panel">
          <div className="prokat-review-heading">
            <span>Секція 2</span>
            <strong>Зміна пароля</strong>
          </div>

          <form className="form-grid" onSubmit={(event) => void changePassword(event)}>
            <label className="full-row">
              Поточний пароль
              <input
                type="password"
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value.slice(0, PASSWORD_MAX_LENGTH))}
                maxLength={PASSWORD_MAX_LENGTH}
                autoComplete="current-password"
              />
            </label>

            <label className="full-row">
              Новий пароль
              <input
                type="password"
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value.slice(0, PASSWORD_MAX_LENGTH))}
                maxLength={PASSWORD_MAX_LENGTH}
                autoComplete="new-password"
              />
            </label>

            <button type="submit" className="btn primary" disabled={savingPassword}>
              {savingPassword ? 'Оновлення...' : 'Оновити пароль'}
            </button>
          </form>
        </section>
      </div>
    </div>
  );
}
