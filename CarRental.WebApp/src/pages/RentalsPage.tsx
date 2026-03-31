import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Api } from '../api/client';
import type { Client, Payment, Rental, Vehicle } from '../api/types';
import { useAuth } from '../auth/useAuth';
import { EmptyState } from '../components/EmptyState';
import { FeedbackBanner } from '../components/FeedbackBanner';
import { FilterField, FilterToolbar, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { InlineSpinner } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { Panel } from '../components/Panel';
import { StatCardSkeletons, TableSkeleton } from '../components/Skeleton';
import { StatCard } from '../components/StatCard';
import { formatCurrency, formatDate, formatShortDate } from '../utils/format';
import { STAFF_CANCELLATION_REASON } from '../utils/referenceData';
import { parseEnumParam, parsePositiveIntParam, withUpdatedSearchParams } from '../utils/searchParams';
import { getAvailableTimeOptionsForDate, LOCATION_OPTIONS, parseDateTime, toDateInputValue } from './prokatShared';

type RentalsTab = 'contracts' | 'payments';
type ScrollTarget = 'list' | 'close' | 'cancel' | 'payments';

const PAGE_SIZE = 20;
const rentalStatusFilterValues = ['all', 'Booked', 'Active', 'Closed', 'Canceled'] as const;
type RentalStatusFilter = (typeof rentalStatusFilterValues)[number];

const rentalStatusFilterLabels: Record<RentalStatusFilter, string> = {
  all: 'Усі статуси',
  Booked: 'Booked',
  Active: 'Active',
  Closed: 'Closed',
  Canceled: 'Canceled',
};

interface CreateForm {
  clientId: string;
  vehicleId: string;
  startDate: string;
  startTime: string;
  endDate: string;
  endTime: string;
  pickupLocation: string;
  returnLocation: string;
  createInitialPayment: boolean;
  paymentMethod: 'Cash' | 'Card';
  paymentDirection: 'Incoming' | 'Refund';
  notes: string;
}

function createTodayValue(): string {
  return toDateInputValue(new Date());
}

function toDateTimePayload(date: string, time: string): string {
  return `${date}T${time}:00`;
}

function statusClass(status: Rental['status']): 'ok' | 'bad' | 'wait' {
  if (status === 'Closed') {
    return 'ok';
  }

  if (status === 'Canceled') {
    return 'bad';
  }

  return 'wait';
}

function isWithinDayRange(value: string, dayStart: Date, dayEnd: Date): boolean {
  const date = new Date(value);
  return date >= dayStart && date <= dayEnd;
}

function compareByStartDate(left: Rental, right: Rental): number {
  return new Date(left.startDate).getTime() - new Date(right.startDate).getTime();
}

function compareByEndDate(left: Rental, right: Rental): number {
  return new Date(left.endDate).getTime() - new Date(right.endDate).getTime();
}

function resolveBoardActionTarget(rental: Rental, canManagePayments: boolean): ScrollTarget {
  if (canManagePayments && rental.balance > 0) {
    return 'payments';
  }

  if (rental.status === 'Booked') {
    return 'cancel';
  }

  return 'close';
}

function resolveTimelineMarker(rentals: Rental[], date: Date): string {
  const activeRental = rentals.find((item) => {
    if (item.status === 'Canceled') {
      return false;
    }

    const start = new Date(item.startDate);
    const end = new Date(item.endDate);
    return start <= date && date <= end;
  });

  if (!activeRental) {
    return '·';
  }

  if (activeRental.status === 'Active') {
    return 'A';
  }

  if (activeRental.status === 'Booked') {
    return 'B';
  }

  return 'C';
}

export function RentalsPage() {
  const { user } = useAuth();
  const [searchParams, setSearchParams] = useSearchParams();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [tab, setTab] = useState<RentalsTab>('contracts');
  const [rentals, setRentals] = useState<Rental[]>([]);
  const [scheduleRentals, setScheduleRentals] = useState<Rental[]>([]);
  const [clients, setClients] = useState<Client[]>([]);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [pickups, setPickups] = useState<Rental[]>([]);
  const [dueReturns, setDueReturns] = useState<Rental[]>([]);
  const [overdueReturns, setOverdueReturns] = useState<Rental[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [paymentAmount, setPaymentAmount] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<'Cash' | 'Card'>('Cash');
  const [paymentDirection, setPaymentDirection] = useState<'Incoming' | 'Refund'>('Incoming');
  const [closeDate, setCloseDate] = useState(createTodayValue());
  const [closeMileage, setCloseMileage] = useState('');
  const [pickupFuelPercent, setPickupFuelPercent] = useState('100');
  const [pickupInspectionNotes, setPickupInspectionNotes] = useState('');
  const [returnFuelPercent, setReturnFuelPercent] = useState('100');
  const [returnInspectionNotes, setReturnInspectionNotes] = useState('');
  const [cancelReason, setCancelReason] = useState(STAFF_CANCELLATION_REASON);
  const [pendingScrollTarget, setPendingScrollTarget] = useState<ScrollTarget | null>(null);
  const [createForm, setCreateForm] = useState<CreateForm>({
    clientId: '',
    vehicleId: '',
    startDate: createTodayValue(),
    startTime: '10:00',
    endDate: toDateInputValue(new Date(Date.now() + 24 * 3600 * 1000)),
    endTime: '10:00',
    pickupLocation: LOCATION_OPTIONS[0],
    returnLocation: LOCATION_OPTIONS[0],
    createInitialPayment: false,
    paymentMethod: 'Cash',
    paymentDirection: 'Incoming',
    notes: '',
  });
  const requestIdRef = useRef(0);
  const supportingRequestIdRef = useRef(0);
  const scheduleRequestIdRef = useRef(0);
  const paymentRequestIdRef = useRef(0);
  const selectedIdRef = useRef<number | null>(null);
  const contractsPanelRef = useRef<HTMLDivElement | null>(null);
  const closePanelRef = useRef<HTMLDivElement | null>(null);
  const cancelPanelRef = useRef<HTMLDivElement | null>(null);
  const paymentsPanelRef = useRef<HTMLDivElement | null>(null);

  const page = parsePositiveIntParam(searchParams.get('page'), 1);
  const search = searchParams.get('search') ?? '';
  const statusFilter = parseEnumParam(searchParams.get('status'), rentalStatusFilterValues, 'all');
  const selectedDate = searchParams.get('day') ?? createTodayValue();
  const canManagePayments = user?.role === 'Manager' || user?.role === 'Admin';

  const selectedRental = useMemo(
    () => rentals.find((item) => item.id === selectedId) ?? null,
    [rentals, selectedId],
  );
  // Усі праві панелі staff-екрана синхронізовані з одним вибраним договором,
  // тому блокування дій і стартові значення форм обчислюються від selectedRental.
  const pickupInspectionMissed = selectedRental?.status === 'Booked' && new Date(selectedRental.startDate) < new Date();
  const pickupInspectionDisabled = !selectedRental || selectedRental.status !== 'Booked' || pickupInspectionMissed;
  const currentMoment = new Date();
  const todayDateValue = toDateInputValue(currentMoment);
  const createStartTimeOptions = getAvailableTimeOptionsForDate(createForm.startDate, currentMoment);
  const createStartTimeValue = createStartTimeOptions.includes(createForm.startTime) ? createForm.startTime : '';
  const createStartDateTime = parseDateTime(createForm.startDate, createForm.startTime);
  const createEndMinimumMoment = createStartDateTime > currentMoment ? createStartDateTime : currentMoment;
  const createEndDateMin = createForm.startDate > todayDateValue ? createForm.startDate : todayDateValue;
  const createEndTimeOptions = getAvailableTimeOptionsForDate(createForm.endDate, createEndMinimumMoment);
  const createEndTimeValue = createEndTimeOptions.includes(createForm.endTime) ? createForm.endTime : '';
  const createEndDateTime = parseDateTime(createForm.endDate, createForm.endTime);
  // Створення нової оренди використовує ту саму часову дисципліну, що й self-service:
  // не можна створити видачу або повернення в минулому чи з "перевернутим" інтервалом.
  const createStartDateError = !createForm.startDate || Number.isNaN(createStartDateTime.getTime())
    ? 'Оберіть дату подачі.'
    : createStartTimeOptions.length === 0
      ? 'На обрану дату вже немає доступного часу подачі. Оберіть іншу дату.'
      : createStartDateTime < currentMoment
        ? 'Початок оренди не може бути в минулому.'
        : null;
  const createEndDateError = !createForm.endDate || Number.isNaN(createEndDateTime.getTime())
    ? 'Оберіть дату повернення.'
    : createForm.endDate < createForm.startDate
      ? 'Дата повернення не може бути раніше дати подачі.'
    : createForm.endDate < todayDateValue
      ? 'Дата повернення не може бути в минулому.'
      : createStartDateError
        ? null
        : createEndTimeOptions.length === 0
          ? 'На обрану дату вже немає доступного часу повернення. Оберіть іншу дату.'
          : createEndDateTime <= createStartDateTime
            ? 'Дата та час завершення мають бути пізнішими за початок оренди.'
            : null;
  const hasCreateDateSelectionError = Boolean(createStartDateError || createEndDateError);

  // Для Gantt-подібної сітки спочатку групуємо оренди за авто, а вже потім
  // розкладаємо горизонт на умовні маркери по днях.
  const rentalsByVehicleId = useMemo(() => {
    const grouped = new Map<number, Rental[]>();
    scheduleRentals.forEach((rental) => {
      const existing = grouped.get(rental.vehicleId);
      if (existing) {
        existing.push(rental);
        return;
      }

      grouped.set(rental.vehicleId, [rental]);
    });

    return grouped;
  }, [scheduleRentals]);

  const ganttHorizon = useMemo(
    () => Array.from({ length: 14 }, (_, index) => {
      const value = new Date();
      value.setHours(12, 0, 0, 0);
      value.setDate(value.getDate() + index);
      return value;
    }),
    [],
  );

  const ganttRows = useMemo(
    () => vehicles.map((vehicle) => ({
      id: vehicle.id,
      vehicle: `${vehicle.make} ${vehicle.model} [${vehicle.licensePlate}]`,
      cells: ganttHorizon.map((date) => resolveTimelineMarker(rentalsByVehicleId.get(vehicle.id) ?? [], date)),
    })),
    [ganttHorizon, rentalsByVehicleId, vehicles],
  );

  const updateListParams = (updates: {
    page?: number | null;
    search?: string | null;
    status?: RentalStatusFilter | null;
    day?: string | null;
  }): void => {
    setSearchParams((current) => withUpdatedSearchParams(current, updates));
  };

  useEffect(() => {
    selectedIdRef.current = selectedId;
  }, [selectedId]);

  const loadSupportingData = useCallback(async (): Promise<void> => {
    const requestId = ++supportingRequestIdRef.current;

    // Довідники клієнтів і авто живуть окремим запитом, щоб їх можна було
    // оновлювати незалежно від основної таблиці договорів.
    try {
      const [nextClients, nextVehicles] = await Promise.all([
        user?.role === 'User'
          ? Api.getOwnClient().then((client) => [client])
          : Api.getClients(),
        Api.getVehicles(),
      ]);

      if (requestId !== supportingRequestIdRef.current) {
        return;
      }

      setClients(nextClients);
      setVehicles(nextVehicles);
      setCreateForm((currentForm) => {
        const hasCurrentClient =
          currentForm.clientId !== '' &&
          nextClients.some((item) => String(item.id) === currentForm.clientId);
        const hasCurrentVehicle =
          currentForm.vehicleId !== '' &&
          nextVehicles.some((item) => String(item.id) === currentForm.vehicleId);

        return {
          ...currentForm,
          clientId: hasCurrentClient ? currentForm.clientId : String(nextClients[0]?.id ?? ''),
          vehicleId: hasCurrentVehicle ? currentForm.vehicleId : String(nextVehicles[0]?.id ?? ''),
        };
      });
    } catch (requestError) {
      if (requestId !== supportingRequestIdRef.current) {
        return;
      }

      setError(Api.errorMessage(requestError));
    }
  }, [user?.role]);

  const loadScheduleData = useCallback(async (): Promise<void> => {
    const requestId = ++scheduleRequestIdRef.current;
 
    try {
      const nextScheduleRentals = await Api.getRentals();
      if (requestId !== scheduleRequestIdRef.current) {
        return;
      }

      setScheduleRentals(nextScheduleRentals);
    } catch (requestError) {
      if (requestId !== scheduleRequestIdRef.current) {
        return;
      }

      setError(Api.errorMessage(requestError));
    }
  }, []);

  const loadPaymentsForSelection = useCallback(async (rentalId: number | null): Promise<void> => {
    const requestId = ++paymentRequestIdRef.current;

    if (!rentalId) {
      setPayments([]);
      return;
    }

    try {
      const rentalPayments = await Api.getRentalPayments(rentalId);
      if (requestId !== paymentRequestIdRef.current) {
        return;
      }

      setPayments(rentalPayments);
    } catch (requestError) {
      if (requestId !== paymentRequestIdRef.current) {
        return;
      }

      setError(Api.errorMessage(requestError));
    }
  }, []);

  const loadPageData = useCallback(async (preferredSelectedId?: number | null): Promise<void> => {
    const requestId = ++requestIdRef.current;
    const dayStart = new Date(`${selectedDate}T00:00:00`);
    const dayEnd = new Date(`${selectedDate}T23:59:59.999`);

    // Основна вибірка сторінки агрегує одразу кілька зрізів: пагіновану таблицю,
    // оперативні картки на день і "бажаний" selectedId після mutation-операцій.
    try {
      setLoading(true);
      setError(null);

      const [pagedRentals, upcomingBookedRentals, activeRentalsUntilSelectedDay] = await Promise.all([
        Api.getRentalsPage({
          page,
          pageSize: PAGE_SIZE,
          status: statusFilter === 'all' ? undefined : statusFilter,
          search: search || undefined,
        }),
        Api.getRentals({
          status: 'Booked',
          fromDate: dayStart.toISOString(),
        }),
        Api.getRentals({
          status: 'Active',
          toDate: dayEnd.toISOString(),
        }),
      ]);

      if (requestId !== requestIdRef.current) {
        return;
      }

      const desiredSelectedId = preferredSelectedId ?? selectedIdRef.current;
      const nextSelectedId =
        desiredSelectedId && pagedRentals.items.some((item) => item.id === desiredSelectedId)
          ? desiredSelectedId
          : pagedRentals.items[0]?.id ?? null;

      setRentals(pagedRentals.items);
      setTotalCount(pagedRentals.totalCount);
      setSelectedId(nextSelectedId);
      setPickups(
        upcomingBookedRentals
          .filter((item) => isWithinDayRange(item.startDate, dayStart, dayEnd))
          .sort(compareByStartDate),
      );
      setDueReturns(
        activeRentalsUntilSelectedDay
          .filter((item) => isWithinDayRange(item.endDate, dayStart, dayEnd))
          .sort(compareByEndDate),
      );
      setOverdueReturns(
        activeRentalsUntilSelectedDay
          .filter((item) => new Date(item.endDate) < dayStart)
          .sort(compareByEndDate),
      );
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
  }, [page, search, selectedDate, statusFilter]);

  const refreshAll = useCallback(async (options?: {
    preferredSelectedId?: number | null;
    reloadSupporting?: boolean;
    reloadSchedule?: boolean;
  }): Promise<void> => {
    const tasks: Array<Promise<unknown>> = [
      loadPageData(options?.preferredSelectedId ?? selectedIdRef.current),
    ];

    if (options?.reloadSupporting) {
      tasks.push(loadSupportingData());
    }

    if (options?.reloadSchedule) {
      tasks.push(loadScheduleData());
    }

    await Promise.all(tasks);
  }, [loadPageData, loadScheduleData, loadSupportingData]);

  useEffect(() => {
    void loadSupportingData();
  }, [loadSupportingData]);

  useEffect(() => {
    void loadScheduleData();
  }, [loadScheduleData]);

  useEffect(() => {
    void loadPageData(selectedIdRef.current);
  }, [loadPageData]);

  useEffect(() => {
    const frameId = window.requestAnimationFrame(() => {
      if (!selectedRental) {
        setCloseDate(createTodayValue());
        setCloseMileage('');
        return;
      }

      setCloseDate(createTodayValue());
      setCloseMileage(String(selectedRental.startMileage + 50));
    });

    return () => {
      window.cancelAnimationFrame(frameId);
    };
  }, [selectedRental]);

  useEffect(() => {
    if (createStartTimeOptions.length === 0 || createStartTimeOptions.includes(createForm.startTime)) {
      return;
    }

    setCreateForm((currentForm) => ({
      ...currentForm,
      startTime: createStartTimeOptions[0],
    }));
  }, [createForm.startTime, createStartTimeOptions]);

  useEffect(() => {
    if (createEndTimeOptions.length === 0 || createEndTimeOptions.includes(createForm.endTime)) {
      return;
    }

    setCreateForm((currentForm) => ({
      ...currentForm,
      endTime: createEndTimeOptions[0],
    }));
  }, [createEndTimeOptions, createForm.endTime]);

  useEffect(() => {
    void loadPaymentsForSelection(selectedId);
  }, [loadPaymentsForSelection, selectedId]);

  useEffect(() => {
    if (!pendingScrollTarget) {
      return undefined;
    }

    const targetRef =
      pendingScrollTarget === 'list'
        ? contractsPanelRef
        : pendingScrollTarget === 'close'
          ? closePanelRef
          : pendingScrollTarget === 'cancel'
            ? cancelPanelRef
            : paymentsPanelRef;

    const timeoutId = window.setTimeout(() => {
      targetRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      setPendingScrollTarget(null);
    }, 120);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, [pendingScrollTarget, tab]);

  const createRental = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (!createForm.clientId) {
      setError('Оберіть клієнта.');
      return;
    }

    if (!createForm.vehicleId) {
      setError('Оберіть авто.');
      return;
    }

    if (createStartDateError) {
      setError(createStartDateError);
      return;
    }

    if (createEndDateError) {
      setError(createEndDateError);
      return;
    }

    const startDateTime = createStartDateTime;
    const endDateTime = createEndDateTime;
    if (endDateTime <= startDateTime) {
      setError('Дата та час завершення мають бути пізнішими за початок оренди.');
      return;
    }

    if (startDateTime < new Date()) {
      setError('Початок оренди не може бути в минулому.');
      return;
    }

    try {
      setError(null);
      const createdRental = await Api.createRental({
        clientId: Number(createForm.clientId),
        vehicleId: Number(createForm.vehicleId),
        startDate: toDateTimePayload(createForm.startDate, createForm.startTime),
        endDate: toDateTimePayload(createForm.endDate, createForm.endTime),
        pickupLocation: createForm.pickupLocation,
        returnLocation: createForm.returnLocation,
        createInitialPayment: createForm.createInitialPayment,
        paymentMethod: createForm.paymentMethod,
        paymentDirection: createForm.paymentDirection,
        notes: createForm.notes,
      });

      setMessage('Оренду створено.');
      setTab('contracts');
      setCreateForm((currentForm) => ({
        ...currentForm,
        createInitialPayment: false,
        paymentDirection: 'Incoming',
        paymentMethod: 'Cash',
        notes: '',
      }));
      await refreshAll({
        preferredSelectedId: createdRental.id,
        reloadSupporting: true,
        reloadSchedule: true,
      });
      setPendingScrollTarget('list');
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const closeRental = async (): Promise<void> => {
    if (!selectedRental) {
      setError('Оберіть оренду.');
      return;
    }

    const mileage = Number(closeMileage);
    if (!Number.isFinite(mileage) || mileage <= 0) {
      setError('Некоректний кінцевий пробіг.');
      return;
    }

    try {
      setError(null);
      await Api.closeRental(
        selectedRental.id,
        `${closeDate}T10:00:00`,
        mileage,
        Number(returnFuelPercent),
        returnInspectionNotes,
      );
      setMessage('Оренду закрито.');
      await refreshAll({
        preferredSelectedId: selectedRental.id,
        reloadSupporting: true,
        reloadSchedule: true,
      });
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const completePickupInspection = async (): Promise<void> => {
    if (!selectedRental) {
      setError('Оберіть оренду.');
      return;
    }

    const fuelPercent = Number(pickupFuelPercent);
    if (!Number.isFinite(fuelPercent) || fuelPercent < 0 || fuelPercent > 100) {
      setError('Вкажіть коректний рівень пального для видачі.');
      return;
    }

    try {
      setError(null);
      await Api.completePickupInspection(selectedRental.id, fuelPercent, pickupInspectionNotes);
      setMessage('Огляд видачі збережено.');
      await refreshAll({
        preferredSelectedId: selectedRental.id,
        reloadSupporting: true,
        reloadSchedule: true,
      });
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const cancelRental = async (): Promise<void> => {
    if (!selectedRental) {
      setError('Оберіть оренду.');
      return;
    }

    if (!cancelReason.trim()) {
      setError('Вкажіть причину скасування.');
      return;
    }

    try {
      setError(null);
      await Api.cancelRental(selectedRental.id, cancelReason.trim());
      setMessage('Оренду скасовано.');
      await refreshAll({
        preferredSelectedId: selectedRental.id,
        reloadSupporting: true,
        reloadSchedule: true,
      });
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const addPayment = async (event: React.FormEvent<HTMLFormElement>): Promise<void> => {
    event.preventDefault();

    if (!selectedRental) {
      setError('Оберіть оренду.');
      return;
    }

    const amount = Number(paymentAmount);
    if (!Number.isFinite(amount) || amount <= 0) {
      setError('Некоректна сума платежу.');
      return;
    }

    try {
      setError(null);
      await Api.addPayment({
        rentalId: selectedRental.id,
        amount,
        method: paymentMethod,
        direction: paymentDirection,
        notes: '',
      });

      setPaymentAmount('');
      setMessage('Платіж додано.');
      await loadPageData(selectedRental.id);
      await loadPaymentsForSelection(selectedRental.id);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    }
  };

  const focusRentalFromBoard = (rental: Rental, target: ScrollTarget): void => {
    setMessage(null);
    setError(null);
    setSelectedId(rental.id);
    setTab(target === 'payments' ? 'payments' : 'contracts');
    updateListParams({
      page: 1,
      search: rental.contractNumber,
      status: null,
    });
    setPendingScrollTarget(target);
  };

  if (loading && rentals.length === 0 && clients.length === 0 && vehicles.length === 0) {
    return (
      <div className="staff-dashboard">
        <StatCardSkeletons count={4} />

        <Panel title="Staff-операції" subtitle="Готуємо фільтри, денну дошку та робочу дату.">
          <TableSkeleton rows={3} compact />
        </Panel>

        <section className="rentals-operations-grid">
          <Panel title="Видачі" subtitle="Готуємо дошку видач на вибрану дату.">
            <TableSkeleton rows={3} compact />
          </Panel>
          <Panel title="Повернення" subtitle="Готуємо контроль повернень.">
            <TableSkeleton rows={3} compact />
          </Panel>
          <Panel title="Прострочені" subtitle="Готуємо ризикові кейси.">
            <TableSkeleton rows={3} compact />
          </Panel>
        </section>

        <div className="staff-dashboard-grid">
          <Panel title="Договори" subtitle="Підготовка списку активних і майбутніх договорів.">
            <TableSkeleton rows={8} />
          </Panel>
          <Panel title="Операції з договором" subtitle="Підготовка панелей видачі, закриття та скасування.">
            <EmptyState
              icon="RENT"
              compact
              title="Завантажуємо оренди."
              description="Після першої вибірки тут з’явиться картка договору, огляд і всі пов’язані дії."
            />
          </Panel>
        </div>
      </div>
    );
  }

  const operationsSections = [
    {
      key: 'pickups',
      title: 'Видачі на вибрану дату',
      subtitle: `${formatShortDate(`${selectedDate}T00:00:00`)} • ${pickups.length} записів`,
      items: pickups,
      emptyText: 'На вибрану дату немає видач.',
    },
    {
      key: 'returns',
      title: 'Повернення на вибрану дату',
      subtitle: `${formatShortDate(`${selectedDate}T00:00:00`)} • ${dueReturns.length} записів`,
      items: dueReturns,
      emptyText: 'На вибрану дату немає повернень.',
    },
    {
      key: 'overdue',
      title: 'Прострочені повернення',
      subtitle: `До ${formatShortDate(`${selectedDate}T00:00:00`)} • ${overdueReturns.length} записів`,
      items: overdueReturns,
      emptyText: 'Прострочених повернень немає.',
    },
  ] as const;
  const activeFilters: ActiveFilterChipItem[] = [
    {
      key: 'day',
      label: `Робоча дата: ${formatShortDate(`${selectedDate}T00:00:00`)}`,
      tone: 'accent',
      onRemove: () => updateListParams({ day: null, page: 1 }),
    },
  ];

  if (search.trim()) {
    activeFilters.push({
      key: 'search',
      label: `Пошук: ${search.trim()}`,
      onRemove: () => updateListParams({ search: null, page: 1 }),
    });
  }

  if (statusFilter !== 'all') {
    activeFilters.push({
      key: 'status',
      label: `Статус: ${rentalStatusFilterLabels[statusFilter]}`,
      onRemove: () => updateListParams({ status: null, page: 1 }),
    });
  }

  return (
    <div className="staff-dashboard">
      <section className="stats-grid">
        <StatCard label="Усього оренд" value={scheduleRentals.length} accent="blue" />
        <StatCard label="Активні" value={scheduleRentals.filter((item) => item.status === 'Active').length} accent="mint" />
        <StatCard label="Очікують оплату" value={scheduleRentals.filter((item) => item.balance > 0).length} accent="amber" />
        <StatCard label="Прострочені" value={overdueReturns.length} accent="red" />
      </section>

      <Panel
        title="Staff-операції"
        subtitle="Server-side пошук по договорах і денна дошка для швидкого переходу до потрібної дії."
      >
        <FilterToolbar
          chips={activeFilters}
          footerNote="Денна дошка та список договорів працюють від одного набору фільтрів, тому зміни одразу відображаються в обох частинах екрана."
          actions={(
            <>
              <button
                type="button"
                className="btn ghost"
                onClick={() => updateListParams({ page: null, search: null, status: null, day: null })}
              >
                Скинути
              </button>
              <button
                type="button"
                className="btn"
                onClick={() => void refreshAll({
                  preferredSelectedId: selectedIdRef.current,
                  reloadSupporting: true,
                  reloadSchedule: true,
                })}
              >
                Оновити
              </button>
            </>
          )}
        >
          <FilterField label="Робоча дата">
            <input
              type="date"
              value={selectedDate}
              onChange={(event) => updateListParams({ day: event.target.value, page: 1 })}
            />
          </FilterField>

          <FilterField label="Пошук" hint="Договір, клієнт або авто">
            <input
              value={search}
              onChange={(event) => updateListParams({ search: event.target.value, page: 1 })}
              placeholder="Договір, клієнт або авто"
            />
          </FilterField>

          <FilterField label="Статус у таблиці">
            <select
              value={statusFilter}
              onChange={(event) => updateListParams({ status: event.target.value as RentalStatusFilter, page: 1 })}
            >
              <option value="all">Усі статуси</option>
              <option value="Booked">Booked</option>
              <option value="Active">Active</option>
              <option value="Closed">Closed</option>
              <option value="Canceled">Canceled</option>
            </select>
          </FilterField>
        </FilterToolbar>
      </Panel>

      <section className="rentals-operations-grid">
        {operationsSections.map((section) => (
          <Panel
            key={section.key}
            title={section.title}
            subtitle={section.subtitle}
            className="rentals-operations-column"
          >
            {section.items.length === 0 ? (
              <EmptyState
                icon="DAY"
                compact
                title="Нічого не заплановано."
                description={section.emptyText}
              />
            ) : (
              <div className="rentals-operations-list">
                {section.items.map((rental) => {
                  const nextActionTarget = resolveBoardActionTarget(rental, canManagePayments);

                  return (
                    <article key={rental.id} className="rentals-operations-card">
                      <div className="rentals-operations-card-top">
                        <div>
                          <strong>{rental.contractNumber}</strong>
                          <p>{rental.clientName}</p>
                        </div>
                        <span className={`status-pill ${statusClass(rental.status)}`}>{rental.status}</span>
                      </div>

                      <div className="kv-grid">
                        <strong>Авто</strong>
                        <span>{rental.vehicleName}</span>
                        <strong>Період</strong>
                        <span>{formatDate(rental.startDate)} - {formatDate(rental.endDate)}</span>
                        <strong>Баланс</strong>
                        <span>{formatCurrency(rental.balance)}</span>
                      </div>

                      <div className="rentals-operations-actions">
                        <button
                          type="button"
                          className="btn ghost"
                          onClick={() => focusRentalFromBoard(rental, 'list')}
                        >
                          Виділити в списку
                        </button>
                        <button
                          type="button"
                          className="btn"
                          onClick={() => focusRentalFromBoard(rental, nextActionTarget)}
                        >
                          До дії
                        </button>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </Panel>
        ))}
      </section>

      <div className="tab-strip">
        <button
          type="button"
          className={`tab-button ${tab === 'contracts' ? 'active' : ''}`}
          onClick={() => setTab('contracts')}
        >
          Договори
        </button>
        <button
          type="button"
          className={`tab-button ${tab === 'payments' ? 'active' : ''}`}
          onClick={() => setTab('payments')}
        >
          Платежі та графік
        </button>
      </div>

      {tab === 'contracts' ? (
        <div className="staff-dashboard-grid">
          <div className="staff-side-stack sticky-panel">
            <Panel
              title="Новий договір"
              subtitle="Швидке створення оренди з початковим платежем за потреби."
            >
              <form className="form-grid" onSubmit={(event) => void createRental(event)}>
                <label>
                  Клієнт
                  <select
                    value={createForm.clientId}
                    onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, clientId: event.target.value }))}
                  >
                    <option value="">Оберіть клієнта</option>
                    {clients.map((client) => (
                      <option key={client.id} value={client.id}>
                        {client.fullName}
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  Авто
                  <select
                    value={createForm.vehicleId}
                    onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, vehicleId: event.target.value }))}
                  >
                    <option value="">Оберіть авто</option>
                    {vehicles.map((vehicle) => (
                      <option key={vehicle.id} value={vehicle.id}>
                        {vehicle.make} {vehicle.model} [{vehicle.licensePlate}]
                      </option>
                    ))}
                  </select>
                </label>

                <label>
                  Початок
                  <div className="inline-form prokat-inline-datetime">
                    <input
                      type="date"
                      value={createForm.startDate}
                      min={todayDateValue}
                      aria-invalid={Boolean(createStartDateError)}
                      onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, startDate: event.target.value }))}
                    />
                    <select
                      value={createStartTimeValue}
                      disabled={createStartTimeOptions.length === 0}
                      onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, startTime: event.target.value }))}
                    >
                      {createStartTimeOptions.length === 0 ? (
                        <option value="">Немає доступного часу</option>
                      ) : createStartTimeOptions.map((time) => (
                        <option key={time} value={time}>{time}</option>
                      ))}
                    </select>
                  </div>
                  {createStartDateError ? <small className="prokat-field-hint warn">{createStartDateError}</small> : null}
                </label>

                <label>
                  Повернення
                  <div className="inline-form prokat-inline-datetime">
                    <input
                      type="date"
                      value={createForm.endDate}
                      min={createEndDateMin}
                      aria-invalid={Boolean(createEndDateError)}
                      onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, endDate: event.target.value }))}
                    />
                    <select
                      value={createEndTimeValue}
                      disabled={createEndTimeOptions.length === 0}
                      onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, endTime: event.target.value }))}
                    >
                      {createEndTimeOptions.length === 0 ? (
                        <option value="">Немає доступного часу</option>
                      ) : createEndTimeOptions.map((time) => (
                        <option key={time} value={time}>{time}</option>
                      ))}
                    </select>
                  </div>
                  {createEndDateError ? <small className="prokat-field-hint warn">{createEndDateError}</small> : null}
                </label>

                <label>
                  Локація отримання
                  <select
                    value={createForm.pickupLocation}
                    onChange={(event) => setCreateForm((currentForm) => ({
                      ...currentForm,
                      pickupLocation: event.target.value,
                      returnLocation: currentForm.returnLocation || event.target.value,
                    }))}
                  >
                    {LOCATION_OPTIONS.map((location) => (
                      <option key={location} value={location}>{location}</option>
                    ))}
                  </select>
                </label>

                <label>
                  Локація повернення
                  <select
                    value={createForm.returnLocation}
                    onChange={(event) => setCreateForm((currentForm) => ({
                      ...currentForm,
                      returnLocation: event.target.value,
                    }))}
                  >
                    {LOCATION_OPTIONS.map((location) => (
                      <option key={location} value={location}>{location}</option>
                    ))}
                  </select>
                </label>

                <label className="inline-form checkbox-row full-row">
                  <input
                    type="checkbox"
                    checked={createForm.createInitialPayment}
                    onChange={(event) => setCreateForm((currentForm) => ({
                      ...currentForm,
                      createInitialPayment: event.target.checked,
                    }))}
                  />
                  Створити початковий платіж разом з орендою
                </label>

                <label>
                  Метод платежу
                  <select
                    value={createForm.paymentMethod}
                    disabled={!createForm.createInitialPayment}
                    onChange={(event) => setCreateForm((currentForm) => ({
                      ...currentForm,
                      paymentMethod: event.target.value as 'Cash' | 'Card',
                    }))}
                  >
                    <option value="Cash">Готівка</option>
                    <option value="Card">Картка</option>
                  </select>
                </label>

                <label>
                  Тип платежу
                  <select
                    value={createForm.paymentDirection}
                    disabled={!createForm.createInitialPayment}
                    onChange={(event) => setCreateForm((currentForm) => ({
                      ...currentForm,
                      paymentDirection: event.target.value as 'Incoming' | 'Refund',
                    }))}
                  >
                    <option value="Incoming">Оплата</option>
                    <option value="Refund">Повернення</option>
                  </select>
                </label>

                <label className="full-row">
                  Нотатки
                  <textarea
                    value={createForm.notes}
                    onChange={(event) => setCreateForm((currentForm) => ({ ...currentForm, notes: event.target.value }))}
                    placeholder="Коментар для менеджера або фінансів"
                  />
                </label>

                <button type="submit" className="btn primary" disabled={hasCreateDateSelectionError}>
                  Створити договір
                </button>
              </form>
            </Panel>

            <div ref={contractsPanelRef}>
              <Panel
                title="Договори"
                subtitle={`Показано ${rentals.length} записів із ${totalCount}`}
              >
                <div className={`surface-refresh${loading ? ' is-refreshing' : ''}`}>
                  {rentals.length === 0 ? (
                    <EmptyState
                      icon="LIST"
                      title="У списку договорів поки нічого немає."
                      description="Змініть фільтри або створіть новий договір, щоб побачити записи в робочій таблиці."
                      actions={(
                        <>
                          <button
                            type="button"
                            className="btn ghost"
                            onClick={() => updateListParams({ page: null, search: null, status: null, day: null })}
                          >
                            Скинути фільтри
                          </button>
                          <button
                            type="button"
                            className="btn primary"
                            onClick={() => void refreshAll({
                              preferredSelectedId: selectedIdRef.current,
                              reloadSupporting: true,
                              reloadSchedule: true,
                            })}
                          >
                            Оновити список
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
                              <th>Договір</th>
                              <th>Клієнт</th>
                              <th>Авто</th>
                              <th>Період</th>
                              <th>Статус</th>
                              <th>Сума</th>
                              <th>Оплачено</th>
                              <th>Баланс</th>
                              <th>Менеджер</th>
                            </tr>
                          </thead>
                          <tbody>
                            {rentals.map((rental) => (
                              <tr
                                key={rental.id}
                                className={rental.id === selectedId ? 'selected-row' : undefined}
                                onClick={() => {
                                  setMessage(null);
                                  setError(null);
                                  setSelectedId(rental.id);
                                }}
                              >
                                <td>{rental.contractNumber}</td>
                                <td>{rental.clientName}</td>
                                <td>{rental.vehicleName}</td>
                                <td>{formatDate(rental.startDate)} - {formatDate(rental.endDate)}</td>
                                <td>
                                  <span className={`status-pill ${statusClass(rental.status)}`}>
                                    {rental.status}
                                  </span>
                                </td>
                                <td>{formatCurrency(rental.totalAmount)}</td>
                                <td>{formatCurrency(rental.paidAmount)}</td>
                                <td>{formatCurrency(rental.balance)}</td>
                                <td>{rental.employeeName}</td>
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
                  onPageChange={(nextPage) => updateListParams({ page: nextPage })}
                />
              </Panel>
            </div>
          </div>

          <div className="page-grid">
            <Panel
              title="Вибрана оренда"
              subtitle={selectedRental ? `Договір ${selectedRental.contractNumber}` : 'Оберіть рядок у таблиці'}
            >
              {selectedRental ? (
                <>
                  <div className="kv-grid">
                    <strong>Клієнт</strong>
                    <span>{selectedRental.clientName}</span>
                    <strong>Авто</strong>
                    <span>{selectedRental.vehicleName}</span>
                    <strong>Період</strong>
                    <span>{formatDate(selectedRental.startDate)} - {formatDate(selectedRental.endDate)}</span>
                    <strong>Статус</strong>
                    <span>{selectedRental.status}</span>
                    <strong>Баланс</strong>
                    <span>{formatCurrency(selectedRental.balance)}</span>
                    <strong>Створено</strong>
                    <span>{formatDate(selectedRental.createdAtUtc)}</span>
                    <strong>Локації</strong>
                    <span>{selectedRental.pickupLocation} → {selectedRental.returnLocation}</span>
                    {selectedRental.pickupInspectionCompletedAtUtc ? (
                      <>
                        <strong>Видача</strong>
                        <span>
                          {formatDate(selectedRental.pickupInspectionCompletedAtUtc)}
                          {typeof selectedRental.pickupFuelPercent === 'number' ? ` • ${selectedRental.pickupFuelPercent}% пального` : ''}
                        </span>
                      </>
                    ) : null}
                    {selectedRental.returnInspectionCompletedAtUtc ? (
                      <>
                        <strong>Повернення</strong>
                        <span>
                          {formatDate(selectedRental.returnInspectionCompletedAtUtc)}
                          {typeof selectedRental.returnFuelPercent === 'number' ? ` • ${selectedRental.returnFuelPercent}% пального` : ''}
                        </span>
                      </>
                    ) : null}
                    {selectedRental.cancellationReason ? (
                      <>
                        <strong>Причина скасування</strong>
                        <span>{selectedRental.cancellationReason}</span>
                      </>
                    ) : null}
                  </div>

                  {canManagePayments ? (
                    <div className="row-actions">
                      <button
                        type="button"
                        className="btn ghost"
                        onClick={() => {
                          setTab('payments');
                          setPendingScrollTarget('payments');
                        }}
                      >
                        До платежів
                      </button>
                    </div>
                  ) : null}
                </>
              ) : (
                <EmptyState
                  icon="RENT"
                  compact
                  title="Оренду ще не вибрано."
                  description="Клік по рядку таблиці або картці на денній дошці відкриє тут деталі договору, стан оглядів і швидкі переходи до платежів."
                />
              )}
            </Panel>

            <Panel
              title="Видача авто"
              subtitle="Заповніть мінімальний огляд перед видачею заброньованого авто."
            >
              {selectedRental ? (
                <>
                  <div className="form-grid">
                    <label>
                      Пальне, %
                      <input
                        value={pickupFuelPercent}
                        disabled={pickupInspectionDisabled}
                        onChange={(event) => setPickupFuelPercent(event.target.value)}
                      />
                    </label>

                    <label className="full-row">
                      Нотатки видачі
                      <textarea
                        value={pickupInspectionNotes}
                        disabled={pickupInspectionDisabled}
                        onChange={(event) => setPickupInspectionNotes(event.target.value)}
                        placeholder="Стан авто, пломби, дрібні зауваження"
                      />
                    </label>
                  </div>

                  <button
                    type="button"
                    className="btn primary"
                    disabled={pickupInspectionDisabled}
                    onClick={() => void completePickupInspection()}
                  >
                    Зафіксувати видачу
                  </button>
                  <p className="muted">
                    {selectedRental.status === 'Booked'
                      ? pickupInspectionMissed
                        ? 'Час видачі за цим бронюванням уже минув. Перенесіть або скасуйте бронювання.'
                        : 'Огляд видачі доступний лише до часу початку бронювання.'
                      : 'Щоб зафіксувати видачу, оберіть бронювання зі статусом Booked.'}
                  </p>
                </>
              ) : (
                <EmptyState
                  icon="PICK"
                  compact
                  title="Спочатку оберіть бронювання."
                  description="Після вибору договору тут з’явиться короткий огляд видачі з пальним і нотатками перед передачею авто клієнту."
                />
              )}
            </Panel>

            <div ref={closePanelRef}>
              <Panel
                title="Закриття оренди"
                subtitle="Працює лише для активного договору."
              >
                {selectedRental ? (
                  <>
                    <div className="form-grid">
                      <label>
                        Фактична дата
                        <input
                          type="date"
                          value={closeDate}
                          disabled={selectedRental.status !== 'Active'}
                          onChange={(event) => setCloseDate(event.target.value)}
                        />
                      </label>

                      <label>
                        Кінцевий пробіг
                        <input
                          value={closeMileage}
                          disabled={selectedRental.status !== 'Active'}
                          onChange={(event) => setCloseMileage(event.target.value)}
                          placeholder="Наприклад 45120"
                        />
                      </label>
                    </div>

                    <label>
                      Пальне при поверненні, %
                      <input
                        value={returnFuelPercent}
                        disabled={selectedRental.status !== 'Active'}
                        onChange={(event) => setReturnFuelPercent(event.target.value)}
                        placeholder="100"
                      />
                    </label>

                    <label className="full-row">
                      Нотатки повернення
                      <textarea
                        value={returnInspectionNotes}
                        disabled={selectedRental.status !== 'Active'}
                        onChange={(event) => setReturnInspectionNotes(event.target.value)}
                        placeholder="Пальне, пошкодження, салон, комплектність"
                      />
                    </label>

                    <p className="muted">
                      {selectedRental.status === 'Active'
                        ? 'Перевірте фактичну дату та пробіг перед закриттям.'
                        : 'Щоб закрити оренду, спочатку оберіть активний договір.'}
                    </p>

                    <button
                      type="button"
                      className="btn primary"
                      disabled={selectedRental.status !== 'Active'}
                      onClick={() => void closeRental()}
                    >
                      Закрити оренду
                    </button>
                  </>
                ) : (
                  <EmptyState
                    icon="CLOSE"
                    compact
                    title="Немає активного договору для закриття."
                    description="Оберіть оренду зі списку. Після цього тут з’являться фактична дата, пробіг та нотатки повернення."
                  />
                )}
              </Panel>
            </div>

            <div ref={cancelPanelRef}>
              <Panel
                title="Скасування оренди"
                subtitle="Працює лише для договорів зі статусом Booked."
              >
                {selectedRental ? (
                  <>
                    <label>
                      Причина
                      <textarea
                        value={cancelReason}
                        disabled={selectedRental.status !== 'Booked'}
                        onChange={(event) => setCancelReason(event.target.value)}
                        placeholder="Опишіть причину скасування"
                      />
                    </label>

                    <p className="muted">
                      {selectedRental.status === 'Booked'
                        ? 'Після скасування договір перейде в історію.'
                        : 'Щоб скасувати оренду, оберіть запис зі статусом Booked.'}
                    </p>

                    <button
                      type="button"
                      className="btn danger"
                      disabled={selectedRental.status !== 'Booked'}
                      onClick={() => void cancelRental()}
                    >
                      Скасувати оренду
                    </button>
                  </>
                ) : (
                  <EmptyState
                    icon="VOID"
                    compact
                    title="Спочатку виберіть бронювання."
                    description="Панель скасування відкривається для договору зі статусом Booked, щоб одразу зафіксувати причину."
                  />
                )}
              </Panel>
            </div>
          </div>
        </div>
      ) : (
        <div className="staff-dashboard-grid">
          <div ref={paymentsPanelRef}>
            <Panel
              title="Платежі по оренді"
              subtitle={selectedRental ? `Договір ${selectedRental.contractNumber}` : 'Оберіть оренду в таблиці'}
              actions={selectedRental ? (
                <button
                  type="button"
                  className="btn ghost"
                  onClick={() => void loadPaymentsForSelection(selectedRental.id)}
                >
                  Оновити
                </button>
              ) : null}
            >
              {selectedRental ? (
                <>
                  <div className="kv-grid">
                    <strong>Клієнт</strong>
                    <span>{selectedRental.clientName}</span>
                    <strong>Авто</strong>
                    <span>{selectedRental.vehicleName}</span>
                    <strong>Статус</strong>
                    <span>{selectedRental.status}</span>
                    <strong>Баланс</strong>
                    <span>{formatCurrency(selectedRental.balance)}</span>
                  </div>

                  {canManagePayments ? (
                    <form className="form-grid" onSubmit={(event) => void addPayment(event)}>
                      <label>
                        Сума
                        <input
                          value={paymentAmount}
                          onChange={(event) => setPaymentAmount(event.target.value)}
                          placeholder="0.00"
                        />
                      </label>

                      <label>
                        Метод
                        <select
                          value={paymentMethod}
                          onChange={(event) => setPaymentMethod(event.target.value as 'Cash' | 'Card')}
                        >
                          <option value="Cash">Готівка</option>
                          <option value="Card">Картка</option>
                        </select>
                      </label>

                      <label>
                        Напрям
                        <select
                          value={paymentDirection}
                          onChange={(event) => setPaymentDirection(event.target.value as 'Incoming' | 'Refund')}
                        >
                          <option value="Incoming">Оплата</option>
                          <option value="Refund">Повернення</option>
                        </select>
                      </label>

                      <button type="submit" className="btn primary" style={{ alignSelf: 'end' }}>
                        Додати платіж
                      </button>
                    </form>
                  ) : (
                    <p className="muted">Додавання платежів доступне лише менеджеру або адміну.</p>
                  )}

                  <div className="table-wrap compact">
                    <table>
                      <thead>
                        <tr>
                          <th>Дата</th>
                          <th>Метод</th>
                          <th>Напрям</th>
                          <th>Сума</th>
                          <th>Нотатки</th>
                        </tr>
                      </thead>
                      <tbody>
                        {payments.map((payment) => (
                          <tr key={payment.id}>
                            <td>{formatDate(payment.createdAtUtc)}</td>
                            <td>{payment.method}</td>
                            <td>{payment.direction}</td>
                            <td>{formatCurrency(payment.amount)}</td>
                            <td>{payment.notes || '-'}</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </>
              ) : (
                <EmptyState
                  icon="PAY"
                  compact
                  title="Платежі ще не відкриті."
                  description="Оберіть договір, щоб переглянути історію оплат, додати новий платіж або перевірити залишок."
                />
              )}
            </Panel>
          </div>

          <Panel
            title="14-денний горизонт автопарку"
            subtitle="A = active, B = booked, C = closed у межах горизонту, · = вільно."
          >
            <div className="table-wrap compact">
              <table className="rentals-gantt-table">
                <thead>
                  <tr>
                    <th>Авто</th>
                    {ganttHorizon.map((date) => (
                      <th key={date.toISOString()}>
                        {date.toLocaleDateString('uk-UA', { day: '2-digit', month: '2-digit' })}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {ganttRows.map((row) => (
                    <tr key={row.id}>
                      <td>{row.vehicle}</td>
                      {row.cells.map((cell, index) => (
                        <td
                          key={`${row.id}-${index}`}
                          className={`rentals-gantt-cell ${
                            cell === 'A'
                              ? 'is-active'
                              : cell === 'B'
                                ? 'is-booked'
                                : cell === 'C'
                                  ? 'is-closed'
                                  : 'is-free'
                          }`}
                        >
                          {cell}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Panel>
        </div>
      )}

      {message ? (
        <FeedbackBanner tone="success" title="Оренду оновлено" onDismiss={() => setMessage(null)} autoHideMs={4200}>
          {message}
        </FeedbackBanner>
      ) : null}
      {error ? (
        <FeedbackBanner tone="error" title="Не вдалося виконати дію" onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
    </div>
  );
}
