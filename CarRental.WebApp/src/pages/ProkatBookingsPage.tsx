import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { Api } from '../api/client';
import type { ClientProfile, Rental } from '../api/types';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState } from '../components/EmptyState';
import { FeedbackBanner } from '../components/FeedbackBanner';
import { CardSkeletonGrid } from '../components/Skeleton';
import { formatCurrency } from '../utils/format';
import {
  CARD_NUMBER_MAX_DIGITS,
  CARDHOLDER_NAME_MAX_LENGTH,
  CARD_NUMBER_INPUT_MAX_LENGTH,
  buildMaskedCardPaymentNote,
  compareAscByDate,
  compareDescByHistoryMoment,
  digitsOnly,
  formatCardNumberInput,
  formatRentalPeriod,
  rentalStatusClass,
  rentalStatusLabel,
  SELF_SERVICE_CANCEL_REASON,
  shouldWarnAboutCardNumber,
} from './prokatShared';

function toDateTimeInputValue(value: string): string {
  const date = new Date(value);
  const timezoneOffsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffsetMs).toISOString().slice(0, 16);
}

function toRequestDateTimeValue(value: string): string {
  return value.length === 16 ? `${value}:00` : value;
}

function getCurrentDateTimeInputValue(): string {
  const now = new Date();
  const timezoneOffsetMs = now.getTimezoneOffset() * 60_000;
  return new Date(now.getTime() - timezoneOffsetMs).toISOString().slice(0, 16);
}

function renderInspectionSummary(rental: Rental): ReactNode {
  const items: string[] = [];

  if (rental.pickupInspectionCompletedAtUtc) {
    items.push(`Видача: ${new Date(rental.pickupInspectionCompletedAtUtc).toLocaleString('uk-UA')}`);
  }

  if (typeof rental.pickupFuelPercent === 'number') {
    items.push(`Пальне при видачі: ${rental.pickupFuelPercent}%`);
  }

  if (rental.pickupInspectionNotes) {
    items.push(`Нотатки видачі: ${rental.pickupInspectionNotes}`);
  }

  if (rental.returnInspectionCompletedAtUtc) {
    items.push(`Повернення: ${new Date(rental.returnInspectionCompletedAtUtc).toLocaleString('uk-UA')}`);
  }

  if (typeof rental.returnFuelPercent === 'number') {
    items.push(`Пальне при поверненні: ${rental.returnFuelPercent}%`);
  }

  if (rental.returnInspectionNotes) {
    items.push(`Нотатки повернення: ${rental.returnInspectionNotes}`);
  }

  if (items.length === 0) {
    return null;
  }

  return (
    <div className="prokat-history-reason">
      <strong>Огляд авто:</strong>
      {' '}
      {items.join(' • ')}
    </div>
  );
}

interface RentalCardProps {
  rental: Rental;
  submitting: boolean;
  onCancel?: (rental: Rental) => void;
  onReschedule?: (rental: Rental) => void;
  onPayBalance?: (rental: Rental) => void;
  onBookAgain?: (rental: Rental) => void;
}

function RentalCard({
  rental,
  submitting,
  onCancel,
  onReschedule,
  onPayBalance,
  onBookAgain,
}: RentalCardProps) {
  return (
    <article className="prokat-history-card">
      <div className="prokat-history-card-top">
        <div>
          <h3>{rental.vehicleName}</h3>
          <p>{rental.contractNumber}</p>
        </div>
        <span className={`status-pill ${rentalStatusClass(rental.status)}`}>
          {rentalStatusLabel(rental.status)}
        </span>
      </div>

      <div className="kv-grid prokat-history-grid-data">
        <strong>Період</strong>
        <span>{formatRentalPeriod(rental)}</span>
        <strong>Локації</strong>
        <span>{rental.pickupLocation} → {rental.returnLocation}</span>
        <strong>Сума</strong>
        <span>{formatCurrency(rental.totalAmount)}</span>
        <strong>Сплачено</strong>
        <span>{formatCurrency(rental.paidAmount)}</span>
        <strong>Залишок</strong>
        <span>{formatCurrency(rental.balance)}</span>
      </div>

      {rental.cancellationReason ? (
        <p className="prokat-history-reason">
          <strong>Причина скасування:</strong>
          {' '}
          {rental.cancellationReason}
        </p>
      ) : null}

      {renderInspectionSummary(rental)}

      <div className="prokat-history-actions">
        {onReschedule ? (
          <button type="button" className="btn ghost" onClick={() => onReschedule(rental)} disabled={submitting}>
            Змінити дати
          </button>
        ) : null}

        {onCancel ? (
          <button type="button" className="btn danger" onClick={() => onCancel(rental)} disabled={submitting}>
            Скасувати
          </button>
        ) : null}

        {onPayBalance && rental.balance > 0 ? (
          <button type="button" className="btn primary" onClick={() => onPayBalance(rental)} disabled={submitting}>
            Оплатити залишок
          </button>
        ) : null}

        {onBookAgain ? (
          <button type="button" className="btn ghost" onClick={() => onBookAgain(rental)} disabled={submitting}>
            Забронювати знову
          </button>
        ) : null}
      </div>
    </article>
  );
}

interface RentalGroupSectionProps {
  title: string;
  description: string;
  rentals: Rental[];
  badgeTone?: 'ok' | 'wait' | 'bad';
  submitting: boolean;
  onCancel?: (rental: Rental) => void;
  onReschedule?: (rental: Rental) => void;
  onPayBalance?: (rental: Rental) => void;
  onBookAgain?: (rental: Rental) => void;
}

function RentalGroupSection(props: RentalGroupSectionProps) {
  const {
    title,
    description,
    rentals,
    badgeTone,
    submitting,
    onCancel,
    onReschedule,
    onPayBalance,
    onBookAgain,
  } = props;

  if (rentals.length === 0) {
    return null;
  }

  return (
    <section className="prokat-history-group">
      <div className="prokat-history-group-header">
        <div>
          <strong>{title}</strong>
          <span className="muted">{description}</span>
        </div>
        <span className={`status-pill${badgeTone ? ` ${badgeTone}` : ''}`}>{rentals.length}</span>
      </div>

      <div className="prokat-history-grid">
        {rentals.map((rental) => (
          <RentalCard
            key={rental.id}
            rental={rental}
            submitting={submitting}
            onCancel={onCancel}
            onReschedule={onReschedule}
            onPayBalance={onPayBalance}
            onBookAgain={onBookAgain}
          />
        ))}
      </div>
    </section>
  );
}

export function ProkatBookingsPage() {
  const navigate = useNavigate();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [myClient, setMyClient] = useState<ClientProfile | null>(null);
  const [myRentals, setMyRentals] = useState<Rental[]>([]);
  const [cancelTarget, setCancelTarget] = useState<Rental | null>(null);
  const [rescheduleTarget, setRescheduleTarget] = useState<Rental | null>(null);
  const [payBalanceTarget, setPayBalanceTarget] = useState<Rental | null>(null);
  const [cardWarningOpen, setCardWarningOpen] = useState(false);
  const [rescheduleStart, setRescheduleStart] = useState('');
  const [rescheduleEnd, setRescheduleEnd] = useState('');
  const [cardholderName, setCardholderName] = useState('');
  const [cardNumber, setCardNumber] = useState('');
  const requestIdRef = useRef(0);

  const loadBookings = useCallback(async (): Promise<void> => {
    const requestId = ++requestIdRef.current;

    try {
      setLoading(true);
      setError(null);

      const [clientData, ownRentalsData] = await Promise.all([
        Api.getOwnClient(),
        Api.getOwnRentals(),
      ]);

      if (requestId !== requestIdRef.current) {
        return;
      }

      setMyClient(clientData);
      setMyRentals(ownRentalsData.filter((item) => item.clientId === clientData.id));
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
    void loadBookings();
  }, [loadBookings]);

  const upcomingRentals = useMemo(() => (
    [...myRentals]
      .filter((rental) => rental.status === 'Booked')
      .sort((left, right) => compareAscByDate(left, right, (item) => item.startDate))
  ), [myRentals]);

  const activeRentals = useMemo(() => (
    [...myRentals]
      .filter((rental) => rental.status === 'Active')
      .sort((left, right) => compareAscByDate(left, right, (item) => item.endDate))
  ), [myRentals]);

  const historyRentals = useMemo(() => (
    [...myRentals]
      .filter((rental) => rental.status === 'Closed' || rental.status === 'Canceled')
      .sort(compareDescByHistoryMoment)
  ), [myRentals]);

  const rescheduleStartError = useMemo(() => {
    if (!rescheduleTarget) {
      return null;
    }

    if (!rescheduleStart) {
      return 'Оберіть нову дату та час початку.';
    }

    const start = new Date(rescheduleStart);
    if (Number.isNaN(start.getTime())) {
      return 'Оберіть нову дату та час початку.';
    }

    if (start < new Date()) {
      return 'Початок оренди не може бути в минулому.';
    }

    return null;
  }, [rescheduleStart, rescheduleTarget]);

  const rescheduleEndError = useMemo(() => {
    if (!rescheduleTarget) {
      return null;
    }

    if (!rescheduleEnd) {
      return 'Оберіть нову дату та час повернення.';
    }

    const start = new Date(rescheduleStart);
    const end = new Date(rescheduleEnd);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
      return 'Оберіть нову дату та час повернення.';
    }

    if (end <= start) {
      return 'Дата повернення має бути пізнішою за дату отримання.';
    }

    return null;
  }, [rescheduleEnd, rescheduleStart, rescheduleTarget]);

  const rescheduleValidationMessageLegacy = useMemo(() => {
    if (!rescheduleTarget) {
      return null;
    }

    if (!rescheduleStart || !rescheduleEnd) {
      return 'Оберіть нові дату та час.';
    }

    const start = new Date(rescheduleStart);
    const end = new Date(rescheduleEnd);
    if (Number.isNaN(start.getTime()) || Number.isNaN(end.getTime())) {
      return 'Оберіть нові дату та час.';
    }

    if (end <= start) {
      return 'Дата повернення має бути пізнішою за дату отримання.';
    }

    if (start < new Date()) {
      return 'Початок оренди не може бути в минулому.';
    }

    return null;
  }, [rescheduleEnd, rescheduleStart, rescheduleTarget]);
  const rescheduleValidationMessage = rescheduleStartError ?? rescheduleEndError ?? rescheduleValidationMessageLegacy;

  const openRescheduleDialog = (rental: Rental): void => {
    setMessage(null);
    setError(null);
    setRescheduleTarget(rental);
    setRescheduleStart(toDateTimeInputValue(rental.startDate));
    setRescheduleEnd(toDateTimeInputValue(rental.endDate));
  };

  const openPayBalanceDialog = (rental: Rental): void => {
    setMessage(null);
    setError(null);
    setCardWarningOpen(false);
    setPayBalanceTarget(rental);
    setCardholderName('');
    setCardNumber('');
  };

  const handleCardholderNameChange = (value: string): void => {
    setCardWarningOpen(false);
    setError(null);
    setCardholderName(value.slice(0, CARDHOLDER_NAME_MAX_LENGTH));
  };

  const handleCardNumberChange = (value: string): void => {
    setCardWarningOpen(false);
    setError(null);
    setCardNumber(formatCardNumberInput(value));
  };

  const confirmCancelRental = async (): Promise<void> => {
    if (!cancelTarget) {
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      const rental = cancelTarget;
      setCancelTarget(null);

      await Api.cancelRental(rental.id, SELF_SERVICE_CANCEL_REASON);
      setMessage(`Бронювання ${rental.contractNumber} скасовано. Якщо були кошти, система зафіксувала повернення.`);
      await loadBookings();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  };

  const confirmReschedule = async (): Promise<void> => {
    if (!rescheduleTarget) {
      return;
    }

    if (rescheduleValidationMessage) {
      return;
    }

    try {
      setSubmitting(true);
      setError(null);
      const rental = rescheduleTarget;
      setRescheduleTarget(null);

      const updated = await Api.rescheduleRental(
        rental.id,
        toRequestDateTimeValue(rescheduleStart),
        toRequestDateTimeValue(rescheduleEnd),
      );

      const amountDelta = updated.totalAmount - rental.totalAmount;
      const financeSummary = amountDelta > 0
        ? `Новий розрахунок збільшив суму на ${formatCurrency(amountDelta)}.`
        : amountDelta < 0
          ? `Новий розрахунок зменшив суму на ${formatCurrency(Math.abs(amountDelta))}; якщо було переплачено, зафіксовано auto-refund.`
          : 'Сума оренди не змінилася.';

      setMessage(`Дати бронювання ${rental.contractNumber} оновлено. ${financeSummary}`);
      await loadBookings();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  };

  const confirmPayBalance = async (): Promise<void> => {
    if (!payBalanceTarget) {
      return;
    }

    if (!cardholderName.trim()) {
      setError("Вкажіть ім'я власника картки.");
      return;
    }

    const cardDigits = digitsOnly(cardNumber);
    if (cardDigits.length !== CARD_NUMBER_MAX_DIGITS) {
      setError('Вкажіть коректний номер картки.');
      return;
    }

    if (shouldWarnAboutCardNumber(cardNumber)) {
      setCardWarningOpen(true);
      return;
    }

    await executePayment();
  };

  const executePayment = async (): Promise<void> => {
    if (!payBalanceTarget) return;

    try {
      setSubmitting(true);
      setError(null);
      const rental = payBalanceTarget;

      await Api.settleRentalBalance(rental.id, buildMaskedCardPaymentNote(cardholderName, cardNumber));
      setMessage(`Залишок по договору ${rental.contractNumber} сплачено.`);

      setPayBalanceTarget(null);
      setCardWarningOpen(false);
      await loadBookings();
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  };

  const startBookingAgain = (rental: Rental): void => {
    navigate(`/prokat/search?vehicle=${rental.vehicleId}`);
  };

  if (loading && myRentals.length === 0) {
    return (
      <div className="page-grid prokat-page prokat-bookings-page">
        <section className="prokat-hero prokat-bookings-hero">
          <div className="prokat-hero-copy">
            <span className="topbar-kicker">Мої бронювання</span>
            <h2>Завантажуємо ваші договори</h2>
            <p>Готуємо майбутні бронювання, активні оренди та історію, щоб можна було швидко перейти до потрібної дії.</p>
          </div>
        </section>

        <CardSkeletonGrid count={4} />
      </div>
    );
  }

  const rescheduleMinStart = getCurrentDateTimeInputValue();
  const rescheduleMinEnd = rescheduleStart && new Date(rescheduleStart) > new Date()
    ? rescheduleStart
    : rescheduleMinStart;

  return (
    <div className="page-grid prokat-page prokat-bookings-page">
      {error ? (
        <FeedbackBanner tone="error" title="Не вдалося виконати дію" onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
      {message ? (
        <FeedbackBanner tone="success" title="Бронювання оновлено" onDismiss={() => setMessage(null)} autoHideMs={4200}>
          {message}
        </FeedbackBanner>
      ) : null}

      <section className="prokat-hero prokat-bookings-hero">
        <div className="prokat-hero-copy">
          <span className="topbar-kicker">Мої бронювання</span>
          <h2>Майбутні, активні та завершені оренди</h2>
          <p>
            {myClient
              ? `${myClient.fullName}, тут зібрано ваші договори, локації видачі та повернення, а також поточний баланс.`
              : 'Тут зібрано ваші договори та поточний баланс.'}
          </p>
        </div>

        <div className="prokat-hero-side">
          <div className="prokat-hero-stats">
            <div>
              <span>Профіль</span>
              <strong>{myClient?.isComplete ? 'Готовий' : 'Неповний'}</strong>
            </div>
            <div>
              <span>Майбутні</span>
              <strong>{upcomingRentals.length}</strong>
            </div>
            <div>
              <span>Активні</span>
              <strong>{activeRentals.length}</strong>
            </div>
          </div>

          <div className="prokat-hero-actions">
            <button type="button" className="btn primary" onClick={() => navigate('/prokat/search')}>
              Підібрати авто
            </button>
            {!myClient?.isComplete ? (
              <button type="button" className="btn ghost" onClick={() => navigate('/prokat/profile')}>
                Завершити профіль
              </button>
            ) : null}
          </div>
        </div>
      </section>

      {myRentals.length === 0 ? (
        <EmptyState
          icon="BOOK"
          title="У вас ще немає оформлених бронювань."
          description="Почніть з підбору авто, а після оформлення договір одразу з’явиться тут разом зі статусом, балансом і подальшими діями."
          actions={(
            <>
              <button type="button" className="btn primary" onClick={() => navigate('/prokat/search')}>
                Перейти до каталогу
              </button>
              {!myClient?.isComplete ? (
                <button type="button" className="btn ghost" onClick={() => navigate('/prokat/profile')}>
                  Завершити профіль
                </button>
              ) : null}
            </>
          )}
        />
      ) : (
        <div className="prokat-history-sections">
          <RentalGroupSection
            title="Майбутні бронювання"
            description="Для статусу Booked доступні зміна дат, скасування та оплата залишку."
            badgeTone="wait"
            rentals={upcomingRentals}
            submitting={submitting}
            onCancel={(rental) => setCancelTarget(rental)}
            onReschedule={openRescheduleDialog}
            onPayBalance={openPayBalanceDialog}
          />

          <RentalGroupSection
            title="Активні оренди"
            description="Активну оренду не можна скасувати або перенести, але можна доплатити залишок."
            badgeTone="ok"
            rentals={activeRentals}
            submitting={submitting}
            onPayBalance={openPayBalanceDialog}
          />

          <RentalGroupSection
            title="Історія"
            description="Завершені та скасовані договори. Для них доступний повторний старт пошуку."
            rentals={historyRentals}
            submitting={submitting}
            onBookAgain={startBookingAgain}
          />
        </div>
      )}

      <ConfirmDialog
        open={cancelTarget !== null}
        title="Скасувати бронювання"
        description={cancelTarget ? (
          <>
            <p>
              Бронювання <strong>{cancelTarget.contractNumber}</strong> буде скасоване.
            </p>
            <div className="kv-grid prokat-confirm-grid">
              <strong>Авто</strong>
              <span>{cancelTarget.vehicleName}</span>
              <strong>Період</strong>
              <span>{formatRentalPeriod(cancelTarget)}</span>
              <strong>Причина</strong>
              <span>{SELF_SERVICE_CANCEL_REASON}</span>
            </div>
          </>
        ) : null}
        confirmLabel="Скасувати бронювання"
        cancelLabel="Назад"
        tone="danger"
        onConfirm={() => void confirmCancelRental()}
        onCancel={() => setCancelTarget(null)}
      />

      <ConfirmDialog
        open={rescheduleTarget !== null}
        title="Змінити дати бронювання"
        description={rescheduleTarget ? (
          <>
            <p>
              Після збереження система перерахує суму. Якщо нова вартість буде нижчою за вже сплачену,
              зафіксується auto-refund; якщо вищою, у договорі зʼявиться новий баланс до оплати.
            </p>
            <div className="form-grid">
              <label className="full-row">
                Новий початок
                <input
                  type="datetime-local"
                  value={rescheduleStart}
                  min={rescheduleMinStart}
                  aria-invalid={Boolean(rescheduleStartError)}
                  onChange={(event) => setRescheduleStart(event.target.value)}
                />
                {rescheduleStartError ? (
                  <small className="prokat-field-hint warn">{rescheduleStartError}</small>
                ) : null}
              </label>

              <label className="full-row">
                Нове повернення
                <input
                  type="datetime-local"
                  value={rescheduleEnd}
                  min={rescheduleMinEnd}
                  aria-invalid={Boolean(rescheduleEndError)}
                  onChange={(event) => setRescheduleEnd(event.target.value)}
                />
                {rescheduleEndError ? (
                  <small className="prokat-field-hint warn">{rescheduleEndError}</small>
                ) : null}
              </label>
            </div>
          </>
        ) : null}
        confirmLabel="Зберегти нові дати"
        cancelLabel="Назад"
        confirmDisabled={Boolean(rescheduleValidationMessage)}
        onConfirm={() => void confirmReschedule()}
        onCancel={() => setRescheduleTarget(null)}
      />

      <ConfirmDialog
        open={cardWarningOpen}
        title="Ймовірно, це не справжня картка"
        description={(
          <p>
            Номер картки не пройшов базову перевірку. Якщо це тестова або нестандартна картка, ви все одно можете продовжити оплату.
          </p>
        )}
        confirmLabel="Продовжити"
        cancelLabel="Змінити номер"
        onConfirm={() => void executePayment()}
        onCancel={() => setCardWarningOpen(false)}
      />

      <ConfirmDialog
        open={payBalanceTarget !== null}
        title="Оплатити залишок"
        description={payBalanceTarget ? (
          <>
            <p>
              Буде списано весь поточний залишок по договору <strong>{payBalanceTarget.contractNumber}</strong>:
              {' '}
              <strong>{formatCurrency(payBalanceTarget.balance)}</strong>.
            </p>
            <div className="form-grid">
              <label className="full-row">
                Власник картки
                <input
                  value={cardholderName}
                  onChange={(event) => handleCardholderNameChange(event.target.value)}
                  maxLength={CARDHOLDER_NAME_MAX_LENGTH}
                  autoComplete="cc-name"
                />
              </label>

              <label className="full-row">
                Номер картки
                <input
                  value={cardNumber}
                  onChange={(event) => handleCardNumberChange(event.target.value)}
                  inputMode="numeric"
                  autoComplete="cc-number"
                  maxLength={CARD_NUMBER_INPUT_MAX_LENGTH}
                  spellCheck={false}
                />
              </label>
            </div>
          </>
        ) : null}
        confirmLabel="Оплатити залишок"
        cancelLabel="Назад"
        onConfirm={() => void confirmPayBalance()}
        onCancel={() => {
          setCardWarningOpen(false);
          setPayBalanceTarget(null);
        }}
      />
    </div>
  );
}
