import clsx from 'clsx';
import { useCallback, useEffect, useRef, useState } from 'react';
import { Api } from '../api/client';
import type { ClientProfile } from '../api/types';
import { FeedbackBanner } from '../components/FeedbackBanner';
import { InlineSpinner, LoadingView } from '../components/LoadingView';
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

type ProfileFormDraft = Pick<ClientProfile, 'fullName' | 'phone' | 'passportData' | 'driverLicense' | 'driverLicenseExpirationDate'>;
type ProfileFieldName = keyof ProfileFormDraft;
type ProfileFieldErrors = Partial<Record<ProfileFieldName, string>>;

const PROFILE_FIELD_ORDER: ProfileFieldName[] = ['fullName', 'phone', 'passportData', 'driverLicense', 'driverLicenseExpirationDate'];
const PROFILE_FIELD_EXAMPLES = {
  fullName: 'Коваленко Джонатан Вікторович',
  phone: '+380671234567',
  passportData: 'МК123456',
  driverLicense: 'ВХЕ123456',
  driverLicenseExpirationDate: '2030-12-31',
} as const;

function countPhoneDigits(value: string): number {
  return value.replace(/\D+/g, '').length;
}

function normalizeDateInputValue(value?: string | null): string {
  return typeof value === 'string' && value.trim().length >= 10
    ? value.trim().slice(0, 10)
    : '';
}

function getTodayDateInputValue(): string {
  const today = new Date();
  return `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;
}

function getLocalProfileFieldErrors(profile: ProfileFormDraft): ProfileFieldErrors {
  const errors: ProfileFieldErrors = {};

  if (!profile.fullName.trim()) {
    errors.fullName = `Вкажіть ПІБ. Приклад: ${PROFILE_FIELD_EXAMPLES.fullName}.`;
  }

  if (!profile.phone.trim()) {
    errors.phone = `Вкажіть номер телефону. Приклад: ${PROFILE_FIELD_EXAMPLES.phone}.`;
  } else {
    const digitsCount = countPhoneDigits(profile.phone);
    if (digitsCount < 10 || digitsCount > 15) {
      errors.phone = `Телефон має містити від 10 до 15 цифр. Приклад: ${PROFILE_FIELD_EXAMPLES.phone}.`;
    }
  }

  if (!profile.passportData.trim()) {
    errors.passportData = `Вкажіть номер паспорта. Приклад: ${PROFILE_FIELD_EXAMPLES.passportData}.`;
  } else if (isLegacyPassportData(profile.passportData)) {
    errors.passportData = `Службове значення EMP-... не підходить. Вкажіть реальний номер, наприклад ${PROFILE_FIELD_EXAMPLES.passportData}.`;
  }

  if (!profile.driverLicense.trim()) {
    errors.driverLicense = `Вкажіть номер посвідчення водія. Приклад: ${PROFILE_FIELD_EXAMPLES.driverLicense}.`;
  } else if (isLegacyDriverLicense(profile.driverLicense)) {
    errors.driverLicense = `Службове значення USR-... не підходить. Вкажіть реальний номер, наприклад ${PROFILE_FIELD_EXAMPLES.driverLicense}.`;
  }

  if (!profile.driverLicenseExpirationDate?.trim()) {
    errors.driverLicenseExpirationDate = 'Вкажіть дату, до якої чинне посвідчення водія. Без неї профіль залишиться неповним.';
  } else if (profile.driverLicenseExpirationDate < getTodayDateInputValue()) {
    errors.driverLicenseExpirationDate = 'Посвідчення водія має бути чинним на сьогодні або пізніше.';
  }

  return errors;
}

function getProfileFieldErrorsFromApiMessage(message: string): ProfileFieldErrors {
  const normalized = message.trim().toLowerCase();
  const errors: ProfileFieldErrors = {};
  const hasGenericDuplicateConflict = normalized.includes('номер документа')
    || normalized.includes('document number');

  if (normalized.includes('full name is required') || normalized.includes('вкажіть піб')) {
    errors.fullName = `Вкажіть ПІБ. Приклад: ${PROFILE_FIELD_EXAMPLES.fullName}.`;
  }

  if (
    normalized.includes('valid phone number')
    || normalized.includes('номер телефону')
    || normalized.includes('phone number')
    || normalized.includes('телефон')
  ) {
    errors.phone = normalized.includes('already exists') || normalized.includes('вже використовується')
      ? 'Цей номер телефону вже використовується іншим клієнтом.'
      : `Вкажіть коректний номер телефону. Приклад: ${PROFILE_FIELD_EXAMPLES.phone}.`;
  }

  if (normalized.includes('passport') || normalized.includes('паспорт')) {
    errors.passportData = normalized.includes('already exists') || normalized.includes('вже використовується')
      ? 'Такий номер паспорта вже використовується іншим клієнтом.'
      : `Перевірте номер паспорта. Приклад: ${PROFILE_FIELD_EXAMPLES.passportData}.`;
  }

  if (
    normalized.includes('driver license')
    || normalized.includes('посвідчення водія')
    || normalized.includes('водій')
  ) {
    errors.driverLicense = normalized.includes('already exists') || normalized.includes('вже використовується')
      ? 'Таке посвідчення водія вже використовується іншим клієнтом.'
      : `Перевірте номер посвідчення водія. Приклад: ${PROFILE_FIELD_EXAMPLES.driverLicense}.`;
  }

  if (hasGenericDuplicateConflict) {
    errors.phone ??= 'Перевірте номер телефону: він має бути унікальним у системі.';
    errors.passportData ??= 'Перевірте номер паспорта: він має бути унікальним у системі.';
    errors.driverLicense ??= 'Перевірте номер посвідчення водія: він має бути унікальним у системі.';
  }

  if (normalized.includes('термін дії') || normalized.includes('expiration') || normalized.includes('чинним')) {
    errors.driverLicenseExpirationDate = 'Вкажіть чинну дату дії посвідчення водія.';
  }

  return errors;
}

function collectProfileFieldIssues(errors: ProfileFieldErrors): string[] {
  return PROFILE_FIELD_ORDER
    .map((fieldName) => errors[fieldName])
    .filter((value): value is string => typeof value === 'string' && value.trim().length > 0);
}

export function ProkatProfilePage() {
  const [loading, setLoading] = useState(true);
  const [savingProfile, setSavingProfile] = useState(false);
  const [savingPassword, setSavingPassword] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorTitle, setErrorTitle] = useState('Не вдалося виконати дію');
  const [message, setMessage] = useState<string | null>(null);
  const [messageTitle, setMessageTitle] = useState('Операцію виконано');
  const [profile, setProfile] = useState<ClientProfile | null>(null);
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const [passportData, setPassportData] = useState('');
  const [driverLicense, setDriverLicense] = useState('');
  const [driverLicenseExpirationDate, setDriverLicenseExpirationDate] = useState('');
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [profileSubmitAttempted, setProfileSubmitAttempted] = useState(false);
  const [serverProfileFieldErrors, setServerProfileFieldErrors] = useState<ProfileFieldErrors>({});
  const requestIdRef = useRef(0);
  const profileDraft: ProfileFormDraft = {
    fullName,
    phone,
    passportData,
    driverLicense,
    driverLicenseExpirationDate,
  };
  const completionIssues = getClientProfileCompletionIssues(profileDraft);
  const completionMessage = getClientProfileCompletionMessage(profileDraft);
  const localProfileFieldErrors = getLocalProfileFieldErrors(profileDraft);
  // Після submit серверні помилки мають пріоритет над локальними підказками,
  // щоб користувач бачив реальну причину відмови API, а не лише frontend-checklist.
  const visibleProfileFieldErrors: ProfileFieldErrors = {
    fullName: serverProfileFieldErrors.fullName ?? (profileSubmitAttempted ? localProfileFieldErrors.fullName : undefined),
    phone: serverProfileFieldErrors.phone ?? (profileSubmitAttempted ? localProfileFieldErrors.phone : undefined),
    passportData: serverProfileFieldErrors.passportData ?? (profileSubmitAttempted ? localProfileFieldErrors.passportData : undefined),
    driverLicense: serverProfileFieldErrors.driverLicense ?? (profileSubmitAttempted ? localProfileFieldErrors.driverLicense : undefined),
    driverLicenseExpirationDate: serverProfileFieldErrors.driverLicenseExpirationDate
      ?? (profileSubmitAttempted ? localProfileFieldErrors.driverLicenseExpirationDate : undefined),
  };
  const profileSaveIssues = collectProfileFieldIssues(visibleProfileFieldErrors);

  const resetProfileServerErrors = () => {
    setServerProfileFieldErrors({});
    setError(null);
  };

  const loadProfile = useCallback(async (): Promise<void> => {
    const requestId = ++requestIdRef.current;

    // requestIdRef захищає форму від стану "старий профіль перезаписав новий",
    // якщо користувач встиг ініціювати повторне завантаження.
    try {
      setLoading(true);
      setError(null);
      setErrorTitle('Не вдалося завантажити профіль');

      const data = await Api.getOwnClient();
      if (requestId !== requestIdRef.current) {
        return;
      }

      setProfile(data);
      setFullName(data.fullName);
      setPhone(data.phone);
      setPassportData(data.passportData);
      setDriverLicense(data.driverLicense);
      setDriverLicenseExpirationDate(normalizeDateInputValue(data.driverLicenseExpirationDate));
      setProfileSubmitAttempted(false);
      setServerProfileFieldErrors({});
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
    setProfileSubmitAttempted(true);

    // Спочатку відсікаємо локально очевидні проблеми формату, а вже потім
    // показуємо точніші конфлікти унікальності та доменні помилки від API.
    try {
      setSavingProfile(true);
      setError(null);
      setErrorTitle('Не вдалося зберегти дані');
      setMessage(null);
      setServerProfileFieldErrors({});

      if (collectProfileFieldIssues(localProfileFieldErrors).length > 0) {
        setError('Форма не збережена. Перевірте підсвічені поля та приклади формату під ними.');
        return;
      }

      const updated = await Api.updateOwnClientProfile({
        fullName,
        phone,
        passportData,
        driverLicense,
        passportExpirationDate: profile?.passportExpirationDate ?? null,
        passportPhotoPath: profile?.passportPhotoPath ?? null,
        driverLicenseExpirationDate: driverLicenseExpirationDate || null,
        driverLicensePhotoPath: profile?.driverLicensePhotoPath ?? null,
      });

      setProfile(updated);
      setFullName(updated.fullName);
      setPhone(updated.phone);
      setPassportData(updated.passportData);
      setDriverLicense(updated.driverLicense);
      setDriverLicenseExpirationDate(normalizeDateInputValue(updated.driverLicenseExpirationDate));
      setProfileSubmitAttempted(false);
      setServerProfileFieldErrors({});

      const updatedCompletionMessage = getClientProfileCompletionMessage(updated);
      setMessageTitle('Профіль оновлено');
      setMessage(updated.isComplete
        ? 'Профіль клієнта оновлено.'
        : `Профіль оновлено. ${updatedCompletionMessage ?? 'Для бронювання ще потрібно завершити всі поля.'}`);
    } catch (requestError) {
      const errorMessage = Api.errorMessage(requestError);
      setServerProfileFieldErrors(getProfileFieldErrorsFromApiMessage(errorMessage));
      setError(errorMessage);
    } finally {
      setSavingProfile(false);
    }
  };

  const changePassword = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (newPassword.length < 8) {
      setErrorTitle('Не вдалося змінити пароль');
      setError('Мінімальна довжина нового пароля: 8 символів.');
      return;
    }

    try {
      setSavingPassword(true);
      setError(null);
      setErrorTitle('Не вдалося змінити пароль');
      setMessage(null);

      await Api.changePassword(currentPassword, newPassword);
      setCurrentPassword('');
      setNewPassword('');
      setMessageTitle('Пароль оновлено');
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
      {error ? (
        <FeedbackBanner tone="error" title={errorTitle} onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
      {message ? (
        <FeedbackBanner tone="success" title={messageTitle} onDismiss={() => setMessage(null)} autoHideMs={4200}>
          {message}
        </FeedbackBanner>
      ) : null}

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
            {profileSaveIssues.length > 0 ? (
              <div className="prokat-profile-hints full-row">
                <strong>Чому зараз не вдається зберегти профіль</strong>
                <ul>
                  {profileSaveIssues.map((issue) => (
                    <li key={issue}>{issue}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            {completionIssues.length > 0 && profileSaveIssues.length === 0 ? (
              <div className="prokat-profile-hints full-row">
                <strong>Що ще потрібно для повного профілю</strong>
                <ul>
                  {completionIssues.map((issue) => (
                    <li key={issue}>{issue}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            <label className={clsx('full-row', 'prokat-form-field', { invalid: Boolean(visibleProfileFieldErrors.fullName) })}>
              ПІБ
              <input
                value={fullName}
                onChange={(event) => {
                  resetProfileServerErrors();
                  setFullName(event.target.value.slice(0, CLIENT_FULL_NAME_MAX_LENGTH));
                }}
                maxLength={CLIENT_FULL_NAME_MAX_LENGTH}
                autoComplete="name"
                placeholder={PROFILE_FIELD_EXAMPLES.fullName}
                aria-invalid={Boolean(visibleProfileFieldErrors.fullName)}
              />
              <small className="prokat-field-hint">Приклад: {PROFILE_FIELD_EXAMPLES.fullName}</small>
              {visibleProfileFieldErrors.fullName ? (
                <small className="prokat-field-hint warn">{visibleProfileFieldErrors.fullName}</small>
              ) : null}
            </label>

            <label className={clsx('prokat-form-field', { invalid: Boolean(visibleProfileFieldErrors.phone) })}>
              Телефон
              <input
                value={phone}
                onChange={(event) => {
                  resetProfileServerErrors();
                  setPhone(event.target.value.slice(0, CLIENT_PHONE_MAX_LENGTH));
                }}
                maxLength={CLIENT_PHONE_MAX_LENGTH}
                inputMode="tel"
                autoComplete="tel"
                placeholder={PROFILE_FIELD_EXAMPLES.phone}
                aria-invalid={Boolean(visibleProfileFieldErrors.phone)}
              />
              <small className="prokat-field-hint">Приклад: {PROFILE_FIELD_EXAMPLES.phone} або +380 67 123 45 67</small>
              {visibleProfileFieldErrors.phone ? (
                <small className="prokat-field-hint warn">{visibleProfileFieldErrors.phone}</small>
              ) : null}
            </label>

            <label className={clsx('prokat-form-field', { invalid: Boolean(visibleProfileFieldErrors.passportData) })}>
              Паспорт
              <input
                value={passportData}
                onChange={(event) => {
                  resetProfileServerErrors();
                  setPassportData(event.target.value.slice(0, CLIENT_PASSPORT_MAX_LENGTH));
                }}
                maxLength={CLIENT_PASSPORT_MAX_LENGTH}
                spellCheck={false}
                placeholder={PROFILE_FIELD_EXAMPLES.passportData}
                aria-invalid={Boolean(visibleProfileFieldErrors.passportData)}
              />
              <small className="prokat-field-hint">Приклад: {PROFILE_FIELD_EXAMPLES.passportData} або 123456789 для ID-картки</small>
              {visibleProfileFieldErrors.passportData ? (
                <small className="prokat-field-hint warn">{visibleProfileFieldErrors.passportData}</small>
              ) : null}
              {isLegacyPassportData(passportData) ? (
                <small className="prokat-field-hint warn">EMP-... не рахується реальними паспортними даними.</small>
              ) : null}
            </label>

            <label className={clsx('full-row', 'prokat-form-field', { invalid: Boolean(visibleProfileFieldErrors.driverLicense) })}>
              Посвідчення водія
              <input
                value={driverLicense}
                onChange={(event) => {
                  resetProfileServerErrors();
                  setDriverLicense(event.target.value.slice(0, CLIENT_DRIVER_LICENSE_MAX_LENGTH));
                }}
                maxLength={CLIENT_DRIVER_LICENSE_MAX_LENGTH}
                spellCheck={false}
                placeholder={PROFILE_FIELD_EXAMPLES.driverLicense}
                aria-invalid={Boolean(visibleProfileFieldErrors.driverLicense)}
              />
              <small className="prokat-field-hint">Приклад: {PROFILE_FIELD_EXAMPLES.driverLicense}</small>
              {visibleProfileFieldErrors.driverLicense ? (
                <small className="prokat-field-hint warn">{visibleProfileFieldErrors.driverLicense}</small>
              ) : null}
              {isLegacyDriverLicense(driverLicense) ? (
                <small className="prokat-field-hint warn">USR-... не рахується реальним посвідченням водія.</small>
              ) : null}
            </label>

            <label className={clsx('full-row', 'prokat-form-field', { invalid: Boolean(visibleProfileFieldErrors.driverLicenseExpirationDate) })}>
              Посвідчення водія дійсне до
              <input
                type="date"
                value={driverLicenseExpirationDate}
                onChange={(event) => {
                  resetProfileServerErrors();
                  setDriverLicenseExpirationDate(event.target.value);
                }}
                min={getTodayDateInputValue()}
                aria-invalid={Boolean(visibleProfileFieldErrors.driverLicenseExpirationDate)}
              />
              <small className="prokat-field-hint">Оберіть дату в календарі. Приклад: 31.12.2030.</small>
              <small className="prokat-field-hint">Без чинної дати система не дасть завершити профіль, а бронювання залишаться заблокованими.</small>
              {visibleProfileFieldErrors.driverLicenseExpirationDate ? (
                <small className="prokat-field-hint warn">{visibleProfileFieldErrors.driverLicenseExpirationDate}</small>
              ) : null}
            </label>

            <button type="submit" className="btn primary" disabled={savingProfile}>
              {savingProfile ? (
                <>
                  <InlineSpinner />
                  {' '}Збереження...
                </>
              ) : 'Зберегти профіль'}
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
              {savingPassword ? (
                <>
                  <InlineSpinner />
                  {' '}Оновлення...
                </>
              ) : 'Оновити пароль'}
            </button>
          </form>
        </section>
      </div>
    </div>
  );
}
