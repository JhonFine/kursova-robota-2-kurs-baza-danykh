import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Api } from '../api/client';
import type { ClientProfile, Rental, RentalAvailabilitySlot, Vehicle } from '../api/types';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { EmptyState } from '../components/EmptyState';
import { FeedbackBanner } from '../components/FeedbackBanner';
import { ActiveFilterChips, type ActiveFilterChipItem } from '../components/FilterToolbar';
import { InlineSpinner } from '../components/LoadingView';
import { PaginationControls } from '../components/PaginationControls';
import { CardSkeletonGrid, TableSkeleton } from '../components/Skeleton';
import { formatCurrency, formatVehicleCargoCapacity, formatVehicleConsumption } from '../utils/format';
import {
  parseBooleanParam,
  parseCsvParam,
  parseEnumParam,
  parseNullablePositiveIntParam,
  parsePositiveIntParam,
  withUpdatedSearchParams,
} from '../utils/searchParams';
import {
  CARD_NUMBER_MAX_DIGITS,
  CARD_CVV_MAX_DIGITS,
  CARD_EXPIRY_INPUT_MAX_LENGTH,
  CARDHOLDER_NAME_MAX_LENGTH,
  CARD_NUMBER_INPUT_MAX_LENGTH,
  buildCatalogVehicleCards,
  buildAvailabilityMap,
  buildMaskedCardPaymentNote,
  classifyVehicleBySpec,
  DEFAULT_MAX_PRICE,
  DEFAULT_MIN_PRICE,
  LOCATION_OPTIONS,
  digitsOnly,
  estimateRentalAmount,
  formatCardCvvInput,
  formatBookingMoment,
  formatCardExpiryInput,
  formatCardNumberInput,
  formatDoors,
  formatDuration,
  getAvailableTimeOptionsForDate,
  getClientProfileCompletionMessage,
  isDateTimeInPast,
  localImage,
  parseCardExpiry,
  parseDateTime,
  pickBookableVehicleId,
  pickVehicleId,
  rentalStatusLabel,
  shouldWarnAboutCardNumber,
  sortVehiclesForSelection,
  sortOptionValues,
  timeOptions,
  toDateInputValue,
  type CatalogVehicleCard,
  type SortOption,
  VEHICLE_PAGE_SIZE,
} from './prokatShared';

function resolveDateParam(value: string | null, fallback: string): string {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return fallback;
  }

  return value;
}

function resolveMoneyParam(value: string | null, fallback: string): string {
  if (!value) {
    return fallback;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? value : fallback;
}

const filterSectionTabs = [
  {
    id: 'period',
    label: 'Період',
    panelId: 'client-period-section',
    heading: 'Період оренди',
    kicker: 'Крок 1',
  },
  {
    id: 'filters',
    label: 'Фільтри',
    panelId: 'client-filters-section',
    heading: 'Пошук і фільтри',
    kicker: 'Крок 2',
  },
  {
    id: 'classes',
    label: 'Клас авто',
    panelId: 'client-classes-section',
    heading: 'Клас авто',
    kicker: 'Крок 3',
  },
] as const;

type FilterSection = (typeof filterSectionTabs)[number]['id'];

const sortOptionLabels: Record<SortOption, string> = {
  popular: 'Спочатку доступні',
  priceAsc: 'Дешевші спочатку',
  priceDesc: 'Дорожчі спочатку',
};

export function ProkatSearchPage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [cardWarningOpen, setCardWarningOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [createdRental, setCreatedRental] = useState<Rental | null>(null);
  const [vehicles, setVehicles] = useState<Vehicle[]>([]);
  const [availabilitySlots, setAvailabilitySlots] = useState<RentalAvailabilitySlot[]>([]);
  const [myClient, setMyClient] = useState<ClientProfile | null>(null);
  const [pickupLocation, setPickupLocation] = useState<string>(LOCATION_OPTIONS[0]);
  const [returnLocation, setReturnLocation] = useState<string>(LOCATION_OPTIONS[0]);
  const [cardholderName, setCardholderName] = useState('');
  const [cardNumber, setCardNumber] = useState('');
  const [cardExpiry, setCardExpiry] = useState('');
  const [cardCvv, setCardCvv] = useState('');
  const [failedImageVehicleIds, setFailedImageVehicleIds] = useState<number[]>([]);
  const [activeFilterSection, setActiveFilterSection] = useState<FilterSection>('period');
  const [isMobileFiltersOpen, setIsMobileFiltersOpen] = useState(false);
  const [isCatalogTransitioning, setIsCatalogTransitioning] = useState(false);
  const requestIdRef = useRef(0);
  const catalogResultsRef = useRef<HTMLDivElement | null>(null);
  const hasPaginationMountedRef = useRef(false);
  const filterTabRefs = useRef<Array<HTMLButtonElement | null>>([]);

  const defaultStartDate = useMemo(() => toDateInputValue(new Date()), []);
  const defaultEndDate = useMemo(() => toDateInputValue(new Date(Date.now() + 24 * 3_600_000)), []);

  const startDate = resolveDateParam(searchParams.get('start'), defaultStartDate);
  const endDate = resolveDateParam(searchParams.get('end'), defaultEndDate);
  const pickupTime = parseEnumParam(searchParams.get('pickup'), timeOptions, '10:00');
  const returnTime = parseEnumParam(searchParams.get('return'), timeOptions, '10:00');
  const search = searchParams.get('q') ?? '';
  const selectedClasses = parseCsvParam(searchParams.get('classes'));
  const showAvailableOnly = parseBooleanParam(searchParams.get('available'), true);
  const sort = parseEnumParam(searchParams.get('sort'), sortOptionValues, 'popular');
  const minPrice = resolveMoneyParam(searchParams.get('min'), DEFAULT_MIN_PRICE);
  const maxPrice = resolveMoneyParam(searchParams.get('max'), DEFAULT_MAX_PRICE);
  const catalogPage = parsePositiveIntParam(searchParams.get('page'), 1);
  const selectedVehicleId = parseNullablePositiveIntParam(searchParams.get('vehicle'));

  const currentMoment = new Date();
  const todayDateValue = toDateInputValue(currentMoment);
  const requestStart = useMemo(() => parseDateTime(startDate, pickupTime), [pickupTime, startDate]);
  const requestEnd = useMemo(() => parseDateTime(endDate, returnTime), [endDate, returnTime]);
  const pickupTimeOptions = getAvailableTimeOptionsForDate(startDate, currentMoment);
  const minimumReturnMoment = requestStart > currentMoment ? requestStart : currentMoment;
  const returnDateMin = startDate > todayDateValue ? startDate : todayDateValue;
  const returnTimeOptions = getAvailableTimeOptionsForDate(endDate, minimumReturnMoment);
  const pickupTimeValue = pickupTimeOptions.includes(pickupTime) ? pickupTime : '';
  const returnTimeValue = returnTimeOptions.includes(returnTime) ? returnTime : '';
  const requestStartInPast = isDateTimeInPast(startDate, pickupTime, currentMoment);
  const rentalHours = useMemo(() => {
    if (requestEnd <= requestStart) {
      return 0;
    }

    return Math.round((requestEnd.getTime() - requestStart.getTime()) / 3_600_000);
  }, [requestEnd, requestStart]);

  const requestWindowValid = requestEnd > requestStart;
  const periodError = !requestWindowValid
    ? 'Дата та час повернення мають бути пізнішими за початок оренди.'
    : pickupTimeOptions.length === 0
      ? 'На обрану дату вже немає доступного часу початку. Оберіть іншу дату.'
      : returnTimeOptions.length === 0
        ? 'На обрану дату вже немає доступного часу повернення. Оберіть іншу дату.'
        : requestStartInPast
          ? 'Початок оренди не може бути в минулому.'
          : null;
  const startDateError = pickupTimeOptions.length === 0 || requestStartInPast
    ? periodError
    : null;
  const endDateError = endDate < startDate
    ? 'Дата повернення не може бути раніше дати подачі.'
    : endDate < todayDateValue
      ? 'Дата повернення не може бути в минулому.'
      : startDateError
        ? null
        : periodError;
  const periodLabel = `${formatBookingMoment(startDate, pickupTime)} - ${formatBookingMoment(endDate, returnTime)}`;
  const durationLabel = formatDuration(rentalHours);

  const updateSearchState = useCallback((updates: Record<string, string | number | boolean | null | undefined>, replace = false) => {
    setSearchParams((current) => withUpdatedSearchParams(current, updates), { replace });
  }, [setSearchParams]);

  const loadCatalog = useCallback(async (softRefresh = false): Promise<void> => {
    const requestId = ++requestIdRef.current;

    try {
      if (!softRefresh) {
        setLoading(true);
      }
      setError(null);

      const [vehicleData, clientData, rentalAvailabilityData] = await Promise.all([
        Api.getVehicles(),
        Api.getOwnClient(),
        Api.getRentalAvailabilitySlots(),
      ]);

      if (requestId !== requestIdRef.current) {
        return;
      }

      setVehicles(vehicleData);
      setMyClient(clientData);
      setAvailabilitySlots(rentalAvailabilityData);
    } catch (requestError) {
      if (requestId !== requestIdRef.current) {
        return;
      }

      setError(Api.errorMessage(requestError));
    } finally {
      if (requestId === requestIdRef.current && !softRefresh) {
        setLoading(false);
      }
    }
  }, []);

  const clearFeedback = useCallback((): void => {
    setCreatedRental(null);
  }, []);

  const resetCardInputs = (): void => {
    setCardholderName('');
    setCardNumber('');
    setCardExpiry('');
    setCardCvv('');
  };

  const beginInteraction = useCallback((): void => {
    clearFeedback();
    setCardWarningOpen(false);
    setError(null);
  }, [clearFeedback]);

  const handleCardholderNameChange = (value: string): void => {
    beginInteraction();
    setCardholderName(value.slice(0, CARDHOLDER_NAME_MAX_LENGTH));
  };

  const handleCardNumberChange = (value: string): void => {
    beginInteraction();
    setCardNumber(formatCardNumberInput(value));
  };

  const handleCardExpiryChange = (value: string): void => {
    beginInteraction();
    setCardExpiry(formatCardExpiryInput(value));
  };

  const handleCardCvvChange = (value: string): void => {
    beginInteraction();
    setCardCvv(formatCardCvvInput(value));
  };

  const availabilityByVehicleId = useMemo(
    () => buildAvailabilityMap(vehicles, availabilitySlots, requestStart, requestEnd),
    [availabilitySlots, requestEnd, requestStart, vehicles],
  );

  const catalogCards = useMemo(
    () => buildCatalogVehicleCards(vehicles, availabilityByVehicleId),
    [availabilityByVehicleId, vehicles],
  );

  const classes = useMemo(() => {
    const values = Array.from(new Set(catalogCards.map((card) => (
      classifyVehicleBySpec(
        card.representativeVehicle.make,
        card.representativeVehicle.model,
        card.minDailyRate,
      )
    ))));
    values.sort((left, right) => left.localeCompare(right, 'uk-UA'));
    return values;
  }, [catalogCards]);
  const classStats = useMemo(() => {
    const stats = new Map<string, { total: number; available: number }>();

    catalogCards.forEach((card) => {
      const className = classifyVehicleBySpec(
        card.representativeVehicle.make,
        card.representativeVehicle.model,
        card.minDailyRate,
      );
      const current = stats.get(className) ?? { total: 0, available: 0 };
      current.total += 1;
      if (card.availableVehicleCount > 0) {
        current.available += 1;
      }

      stats.set(className, current);
    });

    return stats;
  }, [catalogCards]);

  const filteredCards = useMemo(() => {
    const query = search.trim().toLowerCase();
    const min = Number(minPrice);
    const max = Number(maxPrice);

    const result = catalogCards.filter((card) => {
      const cardClass = classifyVehicleBySpec(
        card.representativeVehicle.make,
        card.representativeVehicle.model,
        card.minDailyRate,
      );
      if (showAvailableOnly && card.availableVehicleCount === 0) {
        return false;
      }

      if (selectedClasses.length > 0 && !selectedClasses.includes(cardClass)) {
        return false;
      }

      if (Number.isFinite(min) && card.maxDailyRate < min) {
        return false;
      }

      if (Number.isFinite(max) && card.minDailyRate > max) {
        return false;
      }

      if (!query) {
        return true;
      }

      const text = card.vehicles
        .map((vehicle) => `${vehicle.make} ${vehicle.model} ${vehicle.licensePlate}`)
        .join(' ')
        .toLowerCase();
      return text.includes(query);
    });

    if (sort === 'priceAsc') {
      return result.sort((left, right) => left.minDailyRate - right.minDailyRate);
    }

    if (sort === 'priceDesc') {
      return result.sort((left, right) => right.maxDailyRate - left.maxDailyRate);
    }

    return result.sort((left, right) => {
      const leftAvailable = left.availableVehicleCount > 0 ? 1 : 0;
      const rightAvailable = right.availableVehicleCount > 0 ? 1 : 0;
      return rightAvailable - leftAvailable || left.minDailyRate - right.minDailyRate;
    });
  }, [catalogCards, maxPrice, minPrice, search, selectedClasses, showAvailableOnly, sort]);

  const totalCatalogPages = Math.max(1, Math.ceil(filteredCards.length / VEHICLE_PAGE_SIZE));
  const currentCatalogPage = Math.min(Math.max(catalogPage, 1), totalCatalogPages);
  const selectedClassesKey = selectedClasses.join(',');
  const pagedCards = useMemo(() => {
    const startIndex = (currentCatalogPage - 1) * VEHICLE_PAGE_SIZE;
    return filteredCards.slice(startIndex, startIndex + VEHICLE_PAGE_SIZE);
  }, [currentCatalogPage, filteredCards]);

  const selectedVehicle = useMemo(
    () => vehicles.find((vehicle) => vehicle.id === selectedVehicleId) ?? null,
    [selectedVehicleId, vehicles],
  );
  const selectedCard = useMemo(
    () => selectedVehicle
      ? catalogCards.find((card) => card.vehicleIds.includes(selectedVehicle.id)) ?? null
      : null,
    [catalogCards, selectedVehicle],
  );
  const selectedVariants = useMemo(
    () => selectedCard ? sortVehiclesForSelection(selectedCard.vehicles, availabilityByVehicleId) : [],
    [availabilityByVehicleId, selectedCard],
  );
  const selectedVehicleAvailability = selectedVehicle
    ? availabilityByVehicleId.get(selectedVehicle.id) ?? null
    : null;
  const selectedVehicleAmount = selectedVehicle
    ? estimateRentalAmount(selectedVehicle.dailyRate, requestStart, requestEnd)
    : 0;
  const selectedVehicleInFilteredList = selectedVehicle
    ? filteredCards.some((card) => card.vehicleIds.includes(selectedVehicle.id))
    : false;
  const availableFilteredCount = filteredCards.filter((card) => card.availableVehicleCount > 0).length;
  const summaryChips = useMemo<ActiveFilterChipItem[]>(() => ([
    {
      key: 'period',
      label: `Період: ${periodLabel}`,
      tone: 'accent',
    },
    {
      key: 'duration',
      label: `Тривалість: ${durationLabel}`,
      tone: 'accent',
    },
    {
      key: 'models',
      label: `Моделі: ${filteredCards.length}`,
    },
  ]), [durationLabel, filteredCards.length, periodLabel]);
  const activeFilterChips = useMemo(() => {
    const items: ActiveFilterChipItem[] = [];

    if (search.trim()) {
      items.push({
        key: 'search',
        label: `Пошук: ${search.trim()}`,
        onRemove: () => {
          beginInteraction();
          updateSearchState({ q: null, page: 1 });
        },
      });
    }

    if (showAvailableOnly) {
      items.push({
        key: 'availability',
        label: 'Лише доступні',
        onRemove: () => {
          beginInteraction();
          updateSearchState({ available: false, page: 1 });
        },
      });
    }

    if (sort !== 'popular') {
      items.push({
        key: 'sort',
        label: `Сортування: ${sortOptionLabels[sort]}`,
        onRemove: () => {
          beginInteraction();
          updateSearchState({ sort: 'popular', page: 1 });
        },
      });
    }

    if (minPrice !== DEFAULT_MIN_PRICE || maxPrice !== DEFAULT_MAX_PRICE) {
      items.push({
        key: 'price',
        label: `Ціна: ${minPrice} - ${maxPrice} грн`,
        onRemove: () => {
          beginInteraction();
          updateSearchState({ min: DEFAULT_MIN_PRICE, max: DEFAULT_MAX_PRICE, page: 1 });
        },
      });
    }

    selectedClasses.forEach((className) => {
      items.push({
        key: `class-${className}`,
        label: className,
        onRemove: () => {
          beginInteraction();
          const nextClasses = selectedClasses.filter((item) => item !== className);
          updateSearchState({
            classes: nextClasses.length > 0 ? nextClasses.join(',') : null,
            page: 1,
          });
        },
      });
    });

    return items;
  }, [beginInteraction, maxPrice, minPrice, search, selectedClasses, showAvailableOnly, sort, updateSearchState]);

  useEffect(() => {
    void loadCatalog();
  }, [loadCatalog]);

  useEffect(() => {
    if (pickupTimeOptions.length === 0 || pickupTimeOptions.includes(pickupTime)) {
      return;
    }

    updateSearchState({ pickup: pickupTimeOptions[0], page: 1 }, true);
  }, [pickupTime, pickupTimeOptions, updateSearchState]);

  useEffect(() => {
    if (returnTimeOptions.length === 0 || returnTimeOptions.includes(returnTime)) {
      return;
    }

    updateSearchState({ return: returnTimeOptions[0], page: 1 }, true);
  }, [returnTime, returnTimeOptions, updateSearchState]);

  useEffect(() => {
    setFailedImageVehicleIds([]);
  }, [vehicles]);

  useEffect(() => {
    if (loading) {
      return undefined;
    }

    setIsCatalogTransitioning(true);
    const timeoutId = window.setTimeout(() => setIsCatalogTransitioning(false), 180);
    return () => window.clearTimeout(timeoutId);
  }, [
    catalogPage,
    endDate,
    loading,
    maxPrice,
    minPrice,
    pickupTime,
    returnTime,
    search,
    selectedClassesKey,
    showAvailableOnly,
    sort,
    startDate,
  ]);

  useEffect(() => {
    const needsSync =
      searchParams.get('start') !== startDate
      || searchParams.get('end') !== endDate
      || searchParams.get('pickup') !== pickupTime
      || searchParams.get('return') !== returnTime
      || searchParams.get('available') !== String(showAvailableOnly)
      || searchParams.get('sort') !== sort
      || searchParams.get('min') !== minPrice
      || searchParams.get('max') !== maxPrice
      || searchParams.get('page') !== String(catalogPage);

    if (needsSync) {
      updateSearchState({
        start: startDate,
        end: endDate,
        pickup: pickupTime,
        return: returnTime,
        available: showAvailableOnly,
        sort,
        min: minPrice,
        max: maxPrice,
        page: catalogPage,
      }, true);
    }
  }, [
    catalogPage,
    endDate,
    maxPrice,
    minPrice,
    pickupTime,
    returnTime,
    searchParams,
    showAvailableOnly,
    sort,
    startDate,
    updateSearchState,
  ]);

  useEffect(() => {
    if (currentCatalogPage !== catalogPage) {
      updateSearchState({ page: currentCatalogPage }, true);
    }
  }, [catalogPage, currentCatalogPage, updateSearchState]);

  useEffect(() => {
    if (loading) {
      return;
    }

    if (vehicles.length === 0) {
      if (selectedVehicleId !== null) {
        updateSearchState({ vehicle: null }, true);
      }
      return;
    }

    if (selectedVehicleId && vehicles.some((vehicle) => vehicle.id === selectedVehicleId)) {
      return;
    }

    const fallbackVehicleId = pickVehicleId(vehicles, availabilityByVehicleId, selectedVehicleId);
    if (fallbackVehicleId !== selectedVehicleId) {
      updateSearchState({ vehicle: fallbackVehicleId }, true);
    }
  }, [availabilityByVehicleId, loading, selectedVehicleId, updateSearchState, vehicles]);

  useEffect(() => {
    if (!hasPaginationMountedRef.current) {
      hasPaginationMountedRef.current = true;
      return;
    }

    catalogResultsRef.current?.scrollIntoView({
      behavior: 'smooth',
      block: 'start',
    });
  }, [currentCatalogPage]);

  const selectVehicle = (card: CatalogVehicleCard): void => {
    beginInteraction();

    const preferredVehicleId = selectedVehicleId && card.vehicleIds.includes(selectedVehicleId)
      ? selectedVehicleId
      : null;
    const nextVehicleId = pickBookableVehicleId(card.vehicles, availabilityByVehicleId, preferredVehicleId);
    if (nextVehicleId !== null) {
      updateSearchState({ vehicle: nextVehicleId }, false);
    }
  };

  const selectVehicleVariant = (vehicleId: number): void => {
    beginInteraction();
    updateSearchState({ vehicle: vehicleId }, false);
  };

  const toggleClass = (className: string): void => {
    beginInteraction();
    const nextClasses = selectedClasses.includes(className)
      ? selectedClasses.filter((item) => item !== className)
      : [...selectedClasses, className];

    updateSearchState({
      classes: nextClasses.length > 0 ? nextClasses.join(',') : null,
      page: 1,
    });
  };

  const resetFilters = (): void => {
    beginInteraction();
    updateSearchState({
      q: null,
      classes: null,
      available: true,
      sort: 'popular',
      min: DEFAULT_MIN_PRICE,
      max: DEFAULT_MAX_PRICE,
      page: 1,
    });
  };

  const revealSelectedVehicle = (): void => {
    beginInteraction();
    updateSearchState({
      q: null,
      classes: null,
      available: false,
      sort: 'popular',
      min: DEFAULT_MIN_PRICE,
      max: DEFAULT_MAX_PRICE,
      page: 1,
      vehicle: selectedVehicleId,
    });
  };

  const markImageFailed = (vehicleId: number): void => {
    setFailedImageVehicleIds((currentIds) => (
      currentIds.includes(vehicleId) ? currentIds : [...currentIds, vehicleId]
    ));
  };

  const focusFilterTab = (nextSection: FilterSection): void => {
    const tabIndex = filterSectionTabs.findIndex((item) => item.id === nextSection);
    if (tabIndex < 0) {
      return;
    }

    filterTabRefs.current[tabIndex]?.focus();
  };

  const handleFilterTabKeyDown = (
    event: KeyboardEvent<HTMLButtonElement>,
    currentSection: FilterSection,
  ): void => {
    if (!['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) {
      return;
    }

    event.preventDefault();
    const currentIndex = filterSectionTabs.findIndex((item) => item.id === currentSection);
    if (currentIndex < 0) {
      return;
    }

    if (event.key === 'Home') {
      const firstSection = filterSectionTabs[0].id;
      setActiveFilterSection(firstSection);
      focusFilterTab(firstSection);
      return;
    }

    if (event.key === 'End') {
      const lastSection = filterSectionTabs[filterSectionTabs.length - 1].id;
      setActiveFilterSection(lastSection);
      focusFilterTab(lastSection);
      return;
    }

    const direction = event.key === 'ArrowLeft' || event.key === 'ArrowUp' ? -1 : 1;
    const nextIndex = (currentIndex + direction + filterSectionTabs.length) % filterSectionTabs.length;
    const nextSection = filterSectionTabs[nextIndex].id;
    setActiveFilterSection(nextSection);
    focusFilterTab(nextSection);
  };

  const getCheckoutValidationMessage = (): string | null => {
    if (!myClient) {
      return 'Клієнтський профіль не знайдено.';
    }

    if (!selectedVehicle) {
      return 'Оберіть варіант авто перед оформленням.';
    }

    if (!requestWindowValid) {
      return 'Дата та час повернення мають бути пізнішими за початок оренди.';
    }

    if (selectedVehicleAvailability?.state !== 'available') {
      return selectedVehicleAvailability?.note ?? 'Обране авто недоступне на цей період.';
    }

    if (!cardholderName.trim()) {
      return "Вкажіть ім'я власника картки.";
    }

    const cardDigits = digitsOnly(cardNumber);
    if (cardDigits.length !== CARD_NUMBER_MAX_DIGITS) {
      return 'Некоректний номер картки.';
    }

    const expiryDate = parseCardExpiry(cardExpiry);
    if (!expiryDate) {
      return 'Вкажіть термін дії картки у форматі MM/YY.';
    }

    const today = new Date();
    const todayStart = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    if (expiryDate < todayStart) {
      return 'Термін дії картки минув.';
    }

    const cvvDigits = digitsOnly(cardCvv);
    if (cvvDigits.length < 3 || cvvDigits.length > 4) {
      return 'CVV має містити 3 або 4 цифри.';
    }

    return null;
  };

  const getCheckoutValidationMessageEnhanced = (): string | null => {
    if (!myClient) {
      return 'Клієнтський профіль не знайдено.';
    }

    if (!myClient.isComplete) {
      return 'Завершіть профіль клієнта перед оформленням оренди.';
    }

    if (!selectedVehicle) {
      return 'Оберіть варіант авто перед оформленням.';
    }

    if (!requestWindowValid) {
      return 'Дата та час повернення мають бути пізнішими за початок оренди.';
    }

    if (requestStart < new Date()) {
      return 'Початок оренди не може бути в минулому.';
    }

    if (!pickupLocation.trim() || !returnLocation.trim()) {
      return 'Оберіть локації отримання та повернення.';
    }

    if (selectedVehicleAvailability?.state !== 'available') {
      return selectedVehicleAvailability?.note ?? 'Обране авто недоступне на цей період.';
    }

    if (!cardholderName.trim()) {
      return "Вкажіть ім'я власника картки.";
    }

    const cardDigits = digitsOnly(cardNumber);
    if (cardDigits.length !== CARD_NUMBER_MAX_DIGITS) {
      return 'Некоректний номер картки.';
    }

    const expiryDate = parseCardExpiry(cardExpiry);
    if (!expiryDate) {
      return 'Вкажіть термін дії картки у форматі MM/YY.';
    }

    const today = new Date();
    const todayStart = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    if (expiryDate < todayStart) {
      return 'Термін дії картки минув.';
    }

    const cvvDigits = digitsOnly(cardCvv);
    if (cvvDigits.length < 3 || cvvDigits.length > 4) {
      return 'CVV має містити 3 або 4 цифри.';
    }

    const legacyValidationMessage = getCheckoutValidationMessage();
    if (legacyValidationMessage) {
      return legacyValidationMessage;
    }

    return null;
  };

  const profileCompletionMessage = getClientProfileCompletionMessage(myClient);
  const checkoutValidationMessage = getCheckoutValidationMessageEnhanced();
  const cardNumberNeedsConfirmation = shouldWarnAboutCardNumber(cardNumber);

  const openConfirmDialog = (): void => {
    const validationMessage = checkoutValidationMessage;
    if (validationMessage) {
      if (myClient && !myClient.isComplete) {
        navigate('/prokat/profile');
      }
      setError(validationMessage);
      return;
    }

    clearFeedback();
    setError(null);
    if (cardNumberNeedsConfirmation) {
      setCardWarningOpen(true);
      return;
    }

    setConfirmOpen(true);
  };

  const confirmSuspiciousCardCheckout = (): void => {
    setCardWarningOpen(false);
    setConfirmOpen(true);
  };

  const confirmCheckout = async (): Promise<void> => {
    const validationMessage = getCheckoutValidationMessageEnhanced();
    if (validationMessage) {
      setConfirmOpen(false);
      setError(validationMessage);
      return;
    }

    if (!myClient || !selectedVehicle) {
      return;
    }

    try {
      setConfirmOpen(false);
      setSubmitting(true);
      setError(null);

      const created = await Api.createRentalWithCardPayment({
        clientId: myClient.id,
        vehicleId: selectedVehicle.id,
        startDate: `${startDate}T${pickupTime}:00`,
        endDate: `${endDate}T${returnTime}:00`,
        pickupLocation,
        returnLocation,
        notes: buildMaskedCardPaymentNote(cardholderName, cardNumber),
      });

      setCreatedRental(created);
      resetCardInputs();
      await loadCatalog(true);
      updateSearchState({ vehicle: created.vehicleId }, true);
    } catch (requestError) {
      setError(Api.errorMessage(requestError));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading && vehicles.length === 0) {
    return (
      <div className="page-grid prokat-page">
        <section className="prokat-hero">
          <div className="prokat-hero-copy">
            <span className="topbar-kicker">Пошук авто</span>
            <h2>Завантажуємо каталог оренди</h2>
            <p>Готуємо період, доступність, картки авто та фінальний блок оформлення.</p>
          </div>
        </section>

        <div className="prokat-layout">
          <aside className="prokat-filters">
            <section className="prokat-filter-workbench">
              <TableSkeleton rows={5} compact />
            </section>
          </aside>

          <section className="prokat-main">
            <CardSkeletonGrid count={4} />
          </section>

          <aside className="prokat-summary-panel">
            <section className="status-panel prokat-review-card">
              <div className="prokat-review-heading">
                <span>Крок 4</span>
                <strong>Підготовка оформлення</strong>
              </div>
              <div className="prokat-review-content">
                <TableSkeleton rows={5} compact />
              </div>
            </section>
          </aside>
        </div>
      </div>
    );
  }

  return (
    <div className="page-grid prokat-page">
      {error ? (
        <FeedbackBanner tone="error" title="Не вдалося оновити каталог" onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
      <button
        type="button"
        className="btn primary prokat-mobile-filter-toggle"
        aria-expanded={isMobileFiltersOpen}
        onClick={() => setIsMobileFiltersOpen((current) => !current)}
      >
        {isMobileFiltersOpen ? 'Закрити фільтри' : 'Фільтри та період'}
      </button>
      <button
        type="button"
        aria-label="Закрити панель фільтрів"
        className={`prokat-mobile-filter-overlay${isMobileFiltersOpen ? ' open' : ''}`}
        onClick={() => setIsMobileFiltersOpen(false)}
      />

      <div className="prokat-layout">
        <aside className={`prokat-filters${isMobileFiltersOpen ? ' is-mobile-open' : ''}`}>
          <section className="prokat-filter-workbench" aria-label="Період оренди та фільтри">
            <div className="prokat-mobile-filter-header">
              <div>
                <strong>Фільтри та період</strong>
                <span>Усі зміни застосовуються одразу до каталогу.</span>
              </div>
              <button
                type="button"
                className="btn ghost"
                onClick={() => setIsMobileFiltersOpen(false)}
              >
                Закрити
              </button>
            </div>
            <div className="prokat-filter-tabs" role="tablist" aria-label="Розділи фільтрів">
              {filterSectionTabs.map((tab, index) => (
                <button
                  key={tab.id}
                  ref={(element) => {
                    filterTabRefs.current[index] = element;
                  }}
                  type="button"
                  role="tab"
                  id={`prokat-filter-tab-${tab.id}`}
                  aria-selected={activeFilterSection === tab.id}
                  aria-controls={tab.panelId}
                  tabIndex={activeFilterSection === tab.id ? 0 : -1}
                  className={`prokat-filter-tab${activeFilterSection === tab.id ? ' active' : ''}`}
                  onClick={() => setActiveFilterSection(tab.id)}
                  onKeyDown={(event) => handleFilterTabKeyDown(event, tab.id)}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            <section
              id="client-period-section"
              role="tabpanel"
              aria-labelledby="prokat-filter-tab-period"
              className="prokat-filter-panel"
              hidden={activeFilterSection !== 'period'}
            >
              <div className="prokat-filter-heading">
                <span>{filterSectionTabs[0].kicker}</span>
                <strong>{filterSectionTabs[0].heading}</strong>
              </div>
              <div className="prokat-filter-panel-body">
                <label>
                  Початок
                  <div className="inline-form prokat-inline-datetime">
                    <input
                      type="date"
                      value={startDate}
                      min={todayDateValue}
                      aria-invalid={Boolean(startDateError)}
                      onChange={(event) => {
                        beginInteraction();
                        updateSearchState({ start: event.target.value, page: 1 });
                      }}
                    />
                    <select
                      value={pickupTimeValue}
                      disabled={pickupTimeOptions.length === 0}
                      onChange={(event) => {
                        beginInteraction();
                        updateSearchState({ pickup: event.target.value, page: 1 });
                      }}
                    >
                      {pickupTimeOptions.length === 0 ? (
                        <option value="">Немає доступного часу</option>
                      ) : pickupTimeOptions.map((time) => (
                        <option key={time} value={time}>{time}</option>
                      ))}
                    </select>
                  </div>
                  {startDateError ? <small className="prokat-field-hint warn">{startDateError}</small> : null}
                </label>

                <label>
                  Повернення
                  <div className="inline-form prokat-inline-datetime">
                    <input
                      type="date"
                      value={endDate}
                      min={returnDateMin}
                      aria-invalid={Boolean(endDateError)}
                      onChange={(event) => {
                        beginInteraction();
                        updateSearchState({ end: event.target.value, page: 1 });
                      }}
                    />
                    <select
                      value={returnTimeValue}
                      disabled={returnTimeOptions.length === 0}
                      onChange={(event) => {
                        beginInteraction();
                        updateSearchState({ return: event.target.value, page: 1 });
                      }}
                    >
                      {returnTimeOptions.length === 0 ? (
                        <option value="">Немає доступного часу</option>
                      ) : returnTimeOptions.map((time) => (
                        <option key={time} value={time}>{time}</option>
                      ))}
                    </select>
                  </div>
                  {endDateError ? <small className="prokat-field-hint warn">{endDateError}</small> : null}
                </label>

                <label>
                  Локація отримання
                  <select
                    value={pickupLocation}
                    onChange={(event) => {
                      beginInteraction();
                      setPickupLocation(event.target.value);
                    }}
                  >
                    {LOCATION_OPTIONS.map((location) => (
                      <option key={location} value={location}>{location}</option>
                    ))}
                  </select>
                </label>

                <label>
                  Локація повернення
                  <select
                    value={returnLocation}
                    onChange={(event) => {
                      beginInteraction();
                      setReturnLocation(event.target.value);
                    }}
                  >
                    {LOCATION_OPTIONS.map((location) => (
                      <option key={location} value={location}>{location}</option>
                    ))}
                  </select>
                </label>

                <div className="prokat-filter-note">
                  <strong>{durationLabel}</strong>
                  <span>{periodLabel}</span>
                </div>

              </div>
            </section>

            <section
              id="client-filters-section"
              role="tabpanel"
              aria-labelledby="prokat-filter-tab-filters"
              className="prokat-filter-panel"
              hidden={activeFilterSection !== 'filters'}
            >
              <div className="prokat-filter-heading">
                <span>{filterSectionTabs[1].kicker}</span>
                <strong>{filterSectionTabs[1].heading}</strong>
              </div>
              <div className="prokat-filter-panel-body">
                <label>
                  Пошук авто
                  <input
                    value={search}
                    onChange={(event) => {
                      beginInteraction();
                      updateSearchState({ q: event.target.value || null, page: 1 });
                    }}
                    placeholder="Марка, модель або номер"
                  />
                </label>

                <label>
                  Сортування
                  <select
                    value={sort}
                    onChange={(event) => {
                      beginInteraction();
                      updateSearchState({ sort: event.target.value as SortOption, page: 1 });
                    }}
                  >
                    <option value="popular">Спочатку доступні</option>
                    <option value="priceAsc">Дешевші спочатку</option>
                    <option value="priceDesc">Дорожчі спочатку</option>
                  </select>
                </label>

                <label className={`prokat-toggle-pill${showAvailableOnly ? ' active' : ''}`}>
                  <input
                    type="checkbox"
                    checked={showAvailableOnly}
                    onChange={(event) => {
                      beginInteraction();
                      updateSearchState({ available: event.target.checked, page: 1 });
                    }}
                  />
                  <span>Лише доступні на цей період</span>
                  <small>{availableFilteredCount} моделей можна оформити зараз</small>
                </label>

                <div className="prokat-price-card">
                  <div className="prokat-price-card-head">
                    <div>
                      <strong>Діапазон ціни</strong>
                      <span>Каталог оновлюється від {minPrice} грн до {maxPrice} грн за добу.</span>
                    </div>
                    {minPrice !== DEFAULT_MIN_PRICE || maxPrice !== DEFAULT_MAX_PRICE ? (
                      <button
                        type="button"
                        className="btn ghost btn tiny"
                        onClick={() => {
                          beginInteraction();
                          updateSearchState({ min: DEFAULT_MIN_PRICE, max: DEFAULT_MAX_PRICE, page: 1 });
                        }}
                      >
                        Очистити ціну
                      </button>
                    ) : null}
                  </div>

                  <div className="inline-form prokat-inline-price">
                    <label>
                      Від, грн
                      <input
                        type="number"
                        min="1000"
                        max="3500"
                        step="50"
                        value={minPrice}
                        onChange={(event) => {
                          beginInteraction();
                          updateSearchState({ min: event.target.value, page: 1 });
                        }}
                      />
                    </label>

                    <label>
                      До, грн
                      <input
                        type="number"
                        min="1000"
                        max="3500"
                        step="50"
                        value={maxPrice}
                        onChange={(event) => {
                          beginInteraction();
                          updateSearchState({ max: event.target.value, page: 1 });
                        }}
                      />
                    </label>
                  </div>
                </div>

                <button type="button" className="btn ghost prokat-search-btn" onClick={resetFilters}>
                  Скинути фільтри
                </button>
              </div>
            </section>

            <section
              id="client-classes-section"
              role="tabpanel"
              aria-labelledby="prokat-filter-tab-classes"
              className="prokat-filter-panel"
              hidden={activeFilterSection !== 'classes'}
            >
              <div className="prokat-filter-heading">
                <span>{filterSectionTabs[2].kicker}</span>
                <strong>{selectedClasses.length > 0 ? `${selectedClasses.length} обрано` : 'Усі класи'}</strong>
              </div>
              <div className="prokat-filter-panel-body prokat-class-list">
                {classes.map((className) => (
                  <label
                    key={className}
                    className={`prokat-class-chip${selectedClasses.includes(className) ? ' active' : ''}`}
                  >
                    <input
                      type="checkbox"
                      checked={selectedClasses.includes(className)}
                      onChange={() => toggleClass(className)}
                    />
                    <span>{className}</span>
                    <small>
                      {classStats.get(className)?.available ?? 0} з {classStats.get(className)?.total ?? 0} доступні
                    </small>
                  </label>
                ))}
              </div>
            </section>
          </section>
        </aside>

        <section className="prokat-main">
          <section className="prokat-hero">
            <div className="prokat-hero-copy">
              <span className="topbar-kicker">Пошук авто</span>
              <h2>Підбір авто та оформлення в одному сценарії</h2>
              <p>
                Спочатку задайте період, потім відфільтруйте каталог, оберіть конкретний екземпляр авто
                і завершіть оформлення з оплатою карткою в блоці праворуч.
              </p>
            </div>

            <div className="prokat-hero-side">
              <div className="prokat-hero-stats">
                <div>
                  <span>Знайдено</span>
                  <strong>{filteredCards.length}</strong>
                </div>
                <div>
                  <span>Доступно</span>
                  <strong>{availableFilteredCount}</strong>
                </div>
              </div>

              <div className="prokat-hero-actions">
                <button
                  type="button"
                  className="btn primary prokat-hero-filter-btn"
                  onClick={() => setIsMobileFiltersOpen(true)}
                >
                  Відкрити фільтри
                </button>
                <button type="button" className="btn ghost" onClick={() => navigate('/prokat/bookings')}>
                  Мої бронювання та оренди
                </button>
              </div>
            </div>
          </section>

          <div className="prokat-summary-strip">
            <ActiveFilterChips items={summaryChips} className="prokat-summary-chips" />
            {activeFilterChips.length > 0 ? (
              <div className="prokat-active-filter-strip">
                <span>Активні фільтри</span>
                <ActiveFilterChips items={activeFilterChips} className="prokat-active-filter-chips" />
              </div>
            ) : null}
          </div>

          <div
            id="client-catalog-section"
            ref={catalogResultsRef}
            className={`prokat-cards-wrap surface-refresh${isCatalogTransitioning ? ' is-refreshing' : ''}`}
          >
            {filteredCards.length === 0 ? (
              <EmptyState
                icon="AUTO"
                title="Немає авто під поточний запит."
                description="Змініть період або скиньте фільтри, щоб повернутись до повного каталогу й знову побачити доступні моделі."
                actions={(
                  <>
                    <button type="button" className="btn ghost" onClick={resetFilters}>
                      Скинути фільтри
                    </button>
                    {selectedVehicle ? (
                      <button type="button" className="btn primary" onClick={revealSelectedVehicle}>
                        Показати обране авто
                      </button>
                    ) : null}
                  </>
                )}
              />
            ) : (
              <div className="prokat-cards">
                {pagedCards.map((card) => {
                  const vehicle = card.representativeVehicle;
                  const image = failedImageVehicleIds.includes(vehicle.id) ? null : localImage(vehicle);
                  const isSelected = selectedVehicleId !== null && card.vehicleIds.includes(selectedVehicleId);
                  const isAvailable = card.availableVehicleCount > 0;
                  const vehicleAmountMin = estimateRentalAmount(card.minDailyRate, requestStart, requestEnd);
                  const vehicleAmountMax = estimateRentalAmount(card.maxDailyRate, requestStart, requestEnd);
                  const priceDisplay = card.minDailyRate === card.maxDailyRate
                    ? formatCurrency(card.minDailyRate)
                    : `${formatCurrency(card.minDailyRate)} - ${formatCurrency(card.maxDailyRate)}`;
                  const amountDisplay = vehicleAmountMin === vehicleAmountMax
                    ? formatCurrency(vehicleAmountMin)
                    : `${formatCurrency(vehicleAmountMin)} - ${formatCurrency(vehicleAmountMax)}`;
                  const cardClass = classifyVehicleBySpec(vehicle.make, vehicle.model, card.minDailyRate);
                  const cardSummary = card.vehicleCount === 1
                    ? `${vehicle.licensePlate} • ${vehicle.fuelType} • ${vehicle.transmissionType}`
                    : `${card.vehicleCount} авто • доступно ${card.availableVehicleCount} • ${vehicle.fuelType} • ${vehicle.transmissionType}`;
                  const cardNote = card.vehicleCount === 1
                    ? availabilityByVehicleId.get(vehicle.id)?.note
                    : isAvailable
                      ? `Доступно ${card.availableVehicleCount} з ${card.vehicleCount} авто цієї моделі на вибраний період.`
                      : `Усі ${card.vehicleCount} авто цієї моделі недоступні на вибраний період.`;

                  return (
                    <article
                      key={card.key}
                      className={`prokat-card${isSelected ? ' selected' : ''}${isAvailable ? '' : ' unavailable'}`}
                      role={isAvailable ? 'button' : undefined}
                      tabIndex={isAvailable ? 0 : -1}
                      aria-pressed={isAvailable ? isSelected : undefined}
                      aria-label={cardSummary}
                      onClick={() => {
                        if (isAvailable) {
                          selectVehicle(card);
                        }
                      }}
                      onKeyDown={(event) => {
                        if (!isAvailable) {
                          return;
                        }

                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          selectVehicle(card);
                        }
                      }}
                    >
                      {image ? (
                        <img
                          src={image}
                          alt={`${vehicle.make} ${vehicle.model}`}
                          onError={() => markImageFailed(vehicle.id)}
                        />
                      ) : (
                        <div className="prokat-card-no-photo">Фото недоступне</div>
                      )}

                      <div className="prokat-card-body">
                        <div className="prokat-card-headline">
                          <div>
                            <span className="prokat-card-class">{cardClass}</span>
                            <h3>{vehicle.make} {vehicle.model}</h3>
                            <p>{cardSummary}</p>
                          </div>
                          <span className={`status-pill ${isAvailable ? 'ok' : 'bad'}`}>
                            {isAvailable ? 'Доступне' : 'Недоступне'}
                          </span>
                        </div>

                        <div className="inline-form prokat-card-meta">
                          <span>{vehicle.hasAirConditioning ? 'A/C' : 'Без A/C'}</span>
                          <span>{formatDoors(vehicle.doorsCount)}</span>
                          <span>{formatVehicleCargoCapacity(vehicle.cargoCapacityValue, vehicle.cargoCapacityUnit)}</span>
                          <span>{formatVehicleConsumption(vehicle.consumptionValue, vehicle.consumptionUnit)}</span>
                        </div>

                        <p className={`prokat-card-note ${isAvailable ? 'ok' : 'bad'}`}>
                          {cardNote}
                        </p>

                        <div className="prokat-card-price">
                          <div>
                            <span>За добу</span>
                            <strong>{priceDisplay}</strong>
                          </div>
                          <div>
                            <span>За вибраний період</span>
                            <strong>{amountDisplay}</strong>
                          </div>
                        </div>

                        <div className="prokat-card-action">
                          <button
                            type="button"
                            className={`btn ${isSelected ? 'primary' : 'ghost'} prokat-card-btn`}
                            disabled={!isAvailable}
                            onClick={(event) => {
                              event.stopPropagation();
                              selectVehicle(card);
                            }}
                          >
                            {isSelected
                              ? 'Обрано для оформлення'
                              : card.vehicleCount > 1
                                ? 'Вибрати варіант'
                                : 'Перейти до оформлення'}
                          </button>
                        </div>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}

            {isCatalogTransitioning ? (
              <div className="refresh-overlay">
                <InlineSpinner />
              </div>
            ) : null}
          </div>

          {filteredCards.length > VEHICLE_PAGE_SIZE ? (
            <div className="prokat-pagination">
              <PaginationControls
                page={currentCatalogPage}
                pageSize={VEHICLE_PAGE_SIZE}
                totalCount={filteredCards.length}
                onPageChange={(page) => updateSearchState({ page })}
                disabled={loading || submitting}
              />
            </div>
          ) : null}
        </section>

        <aside className="prokat-summary-panel">
          {createdRental ? (
            <FeedbackBanner tone="success" title="Оренду оформлено" className="prokat-success-card">
              <div className="prokat-success-grid">
                <span>Договір</span>
                <strong>{createdRental.contractNumber}</strong>
                <span>Статус</span>
                <strong>{rentalStatusLabel(createdRental.status)}</strong>
                <span>Сплачено</span>
                <strong>{formatCurrency(createdRental.paidAmount)}</strong>
                <span>Залишок</span>
                <strong>{formatCurrency(createdRental.balance)}</strong>
              </div>
              <p className="muted">
                Платіж карткою зафіксовано разом із договором. Перевірити статус, історію або повторити
                оформлення можна у вкладці з вашими орендами.
              </p>
              <div className="prokat-success-actions">
                <button type="button" className="btn primary" onClick={() => navigate('/prokat/bookings')}>
                  Перейти до моїх оренд
                </button>
                <button type="button" className="btn ghost" onClick={clearFeedback}>
                  Оформити ще
                </button>
              </div>
            </FeedbackBanner>
          ) : null}

          <section id="client-review-section" className="status-panel prokat-review-card">
            <div className="prokat-review-heading">
              <span>Крок 4</span>
              <strong>Вибір варіанта та оформлення</strong>
            </div>
            {selectedVehicle ? (
              <div className="prokat-review-content">
                <div className="prokat-review-vehicle">
                  <div>
                    <h3>{selectedVehicle.make} {selectedVehicle.model}</h3>
                    <p>{selectedVehicle.licensePlate} • {classifyVehicleBySpec(selectedVehicle.make, selectedVehicle.model, selectedVehicle.dailyRate)}</p>
                  </div>
                  <span className={`status-pill ${selectedVehicleAvailability?.state === 'available' ? 'ok' : 'bad'}`}>
                    {selectedVehicleAvailability?.state === 'available' ? 'Готове до оформлення' : 'Потребує зміни'}
                  </span>
                </div>

                {!selectedVehicleInFilteredList ? (
                  <div className="prokat-selection-alert">
                    <strong>Обране авто не входить у поточний список.</strong>
                    <p>Ви все ще бачите його в блоці перевірки, але в каталозі воно приховане поточними фільтрами.</p>
                    <div className="row-actions">
                      <button type="button" className="btn primary" onClick={revealSelectedVehicle}>
                        Показати обране
                      </button>
                      <button type="button" className="btn ghost" onClick={resetFilters}>
                        Скинути фільтри
                      </button>
                    </div>
                  </div>
                ) : null}

                <div className="prokat-variant-picker">
                  <div className="prokat-variant-picker-head">
                    <div>
                      <strong>{selectedVariants.length > 1 ? 'Оберіть екземпляр автопарку' : 'Екземпляр автопарку'}</strong>
                      <span>
                        {selectedCard?.vehicleCount === 1
                          ? 'Для цієї моделі доступний один екземпляр.'
                          : `${selectedVariants.length} варіантів для цієї моделі.`}
                      </span>
                    </div>
                  </div>

                  <div className="prokat-variant-list">
                    {selectedVariants.map((vehicle) => {
                      const availability = availabilityByVehicleId.get(vehicle.id) ?? null;
                      const isAvailable = availability?.state === 'available';
                      const isCurrent = vehicle.id === selectedVehicle.id;

                      return (
                        <button
                          key={vehicle.id}
                          type="button"
                          className={`prokat-variant-card${isCurrent ? ' selected' : ''}${isAvailable ? '' : ' unavailable'}`}
                          onClick={() => selectVehicleVariant(vehicle.id)}
                          disabled={!isAvailable}
                        >
                          <div className="prokat-variant-card-top">
                            <div>
                              <strong>{vehicle.licensePlate}</strong>
                              <span>
                                {vehicle.mileage.toLocaleString('uk-UA')} км • {formatCurrency(vehicle.dailyRate)} / доба
                              </span>
                            </div>
                            <span className={`status-pill ${isAvailable ? 'ok' : 'bad'}`}>
                              {isAvailable ? 'Доступний' : 'Зайнятий'}
                            </span>
                          </div>
                          <small>{availability?.note ?? 'Статус уточнюється.'}</small>
                        </button>
                      );
                    })}
                  </div>
                </div>

                {!myClient?.isComplete ? (
                  <div className="prokat-selection-alert">
                    <strong>Профіль клієнта ще не завершений.</strong>
                    <p>Заповніть реальні документи та контакти, після чого поверніться до оформлення.</p>
                    {profileCompletionMessage ? <p>{profileCompletionMessage}</p> : null}
                    <div className="row-actions">
                      <button type="button" className="btn primary" onClick={() => navigate('/prokat/profile')}>
                        Перейти до профілю
                      </button>
                    </div>
                  </div>
                ) : null}

                <div className="kv-grid prokat-review-grid">
                  <strong>Модель</strong>
                  <span>{selectedVehicle.make} {selectedVehicle.model}</span>
                  <strong>Варіант</strong>
                  <span>{selectedVehicle.licensePlate}</span>
                  <strong>Період</strong>
                  <span>{periodLabel}</span>
                  <strong>Тривалість</strong>
                  <span>{durationLabel}</span>
                  <strong>Добова ставка</strong>
                  <span>{formatCurrency(selectedVehicle.dailyRate)}</span>
                  <strong>Орієнтовно до сплати</strong>
                  <span>{formatCurrency(selectedVehicleAmount)}</span>
                  <strong>Орендар</strong>
                  <span>{myClient?.fullName ?? 'Профіль не знайдено'}</span>
                  <strong>Контакт</strong>
                  <span>{myClient?.phone || 'Телефон не вказано'}</span>
                </div>

                <div className="prokat-filter-note">
                  <strong>Локації</strong>
                  <span>{pickupLocation} → {returnLocation}</span>
                </div>

                <p className={`prokat-card-note ${selectedVehicleAvailability?.state === 'available' ? 'ok' : 'bad'}`}>
                  {selectedVehicleAvailability?.note ?? 'Оберіть авто для оформлення.'}
                </p>

                <div className="prokat-payment-card">
                  <div className="prokat-payment-card-head">
                    <div>
                      <strong>Оплата карткою</strong>
                      <span>У систему потрапляє лише маскований номер картки та ім’я власника.</span>
                    </div>
                  </div>

                  <div className="prokat-payment-grid">
                    <label className="prokat-payment-field prokat-payment-field-wide">
                      <span>Власник картки</span>
                      <input
                        value={cardholderName}
                        onChange={(event) => handleCardholderNameChange(event.target.value)}
                        maxLength={CARDHOLDER_NAME_MAX_LENGTH}
                        autoComplete="cc-name"
                        placeholder="Ім'я та прізвище"
                      />
                    </label>

                    <label className="prokat-payment-field prokat-payment-field-wide">
                      <span>Номер картки</span>
                      <input
                        value={cardNumber}
                        onChange={(event) => handleCardNumberChange(event.target.value)}
                        inputMode="numeric"
                        autoComplete="cc-number"
                        maxLength={CARD_NUMBER_INPUT_MAX_LENGTH}
                        spellCheck={false}
                        placeholder="0000 0000 0000 0000"
                      />
                    </label>

                    <label className="prokat-payment-field">
                      <span>Термін дії (MM/YY)</span>
                      <input
                        value={cardExpiry}
                        onChange={(event) => handleCardExpiryChange(event.target.value)}
                        inputMode="numeric"
                        autoComplete="cc-exp"
                        maxLength={CARD_EXPIRY_INPUT_MAX_LENGTH}
                        spellCheck={false}
                        placeholder="08/29"
                      />
                    </label>

                    <label className="prokat-payment-field">
                      <span>CVV</span>
                      <input
                        value={cardCvv}
                        onChange={(event) => handleCardCvvChange(event.target.value)}
                        inputMode="numeric"
                        autoComplete="cc-csc"
                        maxLength={CARD_CVV_MAX_DIGITS}
                        spellCheck={false}
                        placeholder="123"
                      />
                    </label>
                  </div>
                </div>

                <div className="prokat-summary-note">
                  Після підтвердження система створить оренду, додасть початковий платіж карткою
                  і збереже договір у ваших бронюваннях та орендах.
                </div>

                {checkoutValidationMessage ? (
                  <p className="prokat-checkout-hint warn">{checkoutValidationMessage}</p>
                ) : null}

                <button
                  type="button"
                  className="btn primary prokat-rent-btn"
                  onClick={openConfirmDialog}
                  disabled={submitting || Boolean(checkoutValidationMessage)}
                >
                  {submitting ? (
                    <>
                      <InlineSpinner />
                      {' '}Оформлення...
                    </>
                  ) : 'Оплатити та оформити'}
                </button>
              </div>
            ) : (
              <EmptyState
                icon="STEP"
                className="prokat-review-empty"
                title="Почніть з вибору авто."
                description="Вкажіть період, знайдіть доступну модель у каталозі та оберіть конкретний екземпляр для оформлення."
              />
            )}
          </section>
        </aside>
      </div>

      <ConfirmDialog
        open={cardWarningOpen}
        title="Ймовірно, це не справжня картка"
        description={(
          <p>
            Номер картки не пройшов базову перевірку. Якщо це тестова або нестандартна картка, ви все одно можете продовжити оформлення.
          </p>
        )}
        confirmLabel="Продовжити"
        cancelLabel="Змінити номер"
        onConfirm={confirmSuspiciousCardCheckout}
        onCancel={() => setCardWarningOpen(false)}
      />

      <ConfirmDialog
        open={confirmOpen}
        title="Підтвердити оплату та оформлення"
        description={selectedVehicle ? (
          <>
            <p>
              Ми створимо оренду з початковим платежем карткою. Фінальна доступність авто ще раз перевіриться
              під час збереження.
            </p>
            <div className="kv-grid prokat-confirm-grid">
              <strong>Авто</strong>
              <span>{selectedVehicle.make} {selectedVehicle.model}</span>
              <strong>Варіант</strong>
              <span>{selectedVehicle.licensePlate}</span>
              <strong>Період</strong>
              <span>{periodLabel}</span>
              <strong>До сплати</strong>
              <span>{formatCurrency(selectedVehicleAmount)}</span>
              <strong>Оплата</strong>
              <span>{buildMaskedCardPaymentNote(cardholderName, cardNumber)}</span>
            </div>
          </>
        ) : null}
        confirmLabel="Оплатити та оформити"
        cancelLabel="Повернутися"
        onConfirm={() => void confirmCheckout()}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  );
}
