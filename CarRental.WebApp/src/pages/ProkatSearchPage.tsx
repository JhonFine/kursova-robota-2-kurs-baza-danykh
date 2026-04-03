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
import { formatFuelTypeLabel, formatTransmissionTypeLabel } from '../utils/referenceData';
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
    label: 'РџРµСЂС–РѕРґ',
    panelId: 'client-period-section',
    heading: 'РџРµСЂС–РѕРґ РѕСЂРµРЅРґРё',
    kicker: 'РљСЂРѕРє 1',
  },
  {
    id: 'filters',
    label: 'Р¤С–Р»СЊС‚СЂРё',
    panelId: 'client-filters-section',
    heading: 'РџРѕС€СѓРє С– С„С–Р»СЊС‚СЂРё',
    kicker: 'РљСЂРѕРє 2',
  },
  {
    id: 'classes',
    label: 'РљР»Р°СЃ Р°РІС‚Рѕ',
    panelId: 'client-classes-section',
    heading: 'РљР»Р°СЃ Р°РІС‚Рѕ',
    kicker: 'РљСЂРѕРє 3',
  },
] as const;

type FilterSection = (typeof filterSectionTabs)[number]['id'];

const sortOptionLabels: Record<SortOption, string> = {
  popular: 'РЎРїРѕС‡Р°С‚РєСѓ РґРѕСЃС‚СѓРїРЅС–',
  priceAsc: 'Р”РµС€РµРІС€С– СЃРїРѕС‡Р°С‚РєСѓ',
  priceDesc: 'Р”РѕСЂРѕР¶С‡С– СЃРїРѕС‡Р°С‚РєСѓ',
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

  // РЈ URL Р¶РёРІРµ РІРµСЃСЊ СЃС‚Р°РЅ РєР°С‚Р°Р»РѕРіСѓ: РїРµСЂС–РѕРґ, СЃРѕСЂС‚СѓРІР°РЅРЅСЏ, С„С–Р»СЊС‚СЂРё Р№ РІРёР±СЂР°РЅРёР№ РµРєР·РµРјРїР»СЏСЂ.
  // Р¦Рµ РґРѕР·РІРѕР»СЏС” РѕРЅРѕРІР»СЋРІР°С‚Рё СЃС‚РѕСЂС–РЅРєСѓ Р±РµР· РІС‚СЂР°С‚Рё РєРѕРЅС‚РµРєСЃС‚Сѓ С– РґС–Р»РёС‚РёСЃСЏ РіРѕС‚РѕРІРёРј РїРѕС€СѓРєРѕРј.
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
  // Р’РµСЃСЊ derived state СЃС‚РѕСЂС–РЅРєРё СЃРїРёСЂР°С”С‚СЊСЃСЏ РЅР° РѕРґРёРЅ РѕР±СЂР°РЅРёР№ С‡Р°СЃРѕРІРёР№ РїСЂРѕРјС–Р¶РѕРє:
  // РІС–Рґ РЅСЊРѕРіРѕ Р·Р°Р»РµР¶Р°С‚СЊ РґРѕСЃС‚СѓРїРЅС– РіРѕРґРёРЅРё, РїРѕРїРµСЂРµРґР¶РµРЅРЅСЏ С– С„Р°РєС‚РёС‡РЅР° РґРѕСЃС‚СѓРїРЅС–СЃС‚СЊ Р°РІС‚Рѕ.
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
    ? 'Р”Р°С‚Р° С‚Р° С‡Р°СЃ РїРѕРІРµСЂРЅРµРЅРЅСЏ РјР°СЋС‚СЊ Р±СѓС‚Рё РїС–Р·РЅС–С€РёРјРё Р·Р° РїРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё.'
    : pickupTimeOptions.length === 0
      ? 'РќР° РѕР±СЂР°РЅСѓ РґР°С‚Сѓ РІР¶Рµ РЅРµРјР°С” РґРѕСЃС‚СѓРїРЅРѕРіРѕ С‡Р°СЃСѓ РїРѕС‡Р°С‚РєСѓ. РћР±РµСЂС–С‚СЊ С–РЅС€Сѓ РґР°С‚Сѓ.'
      : returnTimeOptions.length === 0
        ? 'РќР° РѕР±СЂР°РЅСѓ РґР°С‚Сѓ РІР¶Рµ РЅРµРјР°С” РґРѕСЃС‚СѓРїРЅРѕРіРѕ С‡Р°СЃСѓ РїРѕРІРµСЂРЅРµРЅРЅСЏ. РћР±РµСЂС–С‚СЊ С–РЅС€Сѓ РґР°С‚Сѓ.'
        : requestStartInPast
          ? 'РџРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РІ РјРёРЅСѓР»РѕРјСѓ.'
          : null;
  const startDateError = pickupTimeOptions.length === 0 || requestStartInPast
    ? periodError
    : null;
  const endDateError = endDate < startDate
    ? 'Р”Р°С‚Р° РїРѕРІРµСЂРЅРµРЅРЅСЏ РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё СЂР°РЅС–С€Рµ РґР°С‚Рё РїРѕРґР°С‡С–.'
    : endDate < todayDateValue
      ? 'Р”Р°С‚Р° РїРѕРІРµСЂРЅРµРЅРЅСЏ РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РІ РјРёРЅСѓР»РѕРјСѓ.'
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

    // requestIdRef РІС–РґСЃС–РєР°С” Р·Р°СЃС‚Р°СЂС–Р»С– РІС–РґРїРѕРІС–РґС–, СЏРєС‰Рѕ РєРѕСЂРёСЃС‚СѓРІР°С‡ С€РІРёРґРєРѕ Р·РјС–РЅСЋС”
    // РїР°СЂР°РјРµС‚СЂРё РїРѕС€СѓРєСѓ С– РїРѕРїРµСЂРµРґРЅС–Р№ Р·Р°РїРёС‚ Р·Р°РІРµСЂС€СѓС”С‚СЊСЃСЏ РїС–Р·РЅС–С€Рµ Р·Р° РЅРѕРІРёР№.
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

  // РљР°СЂС‚Р° РґРѕСЃС‚СѓРїРЅРѕСЃС‚С– С– РєР°СЂС‚РєРё РєР°С‚Р°Р»РѕРіСѓ Р±СѓРґСѓСЋС‚СЊСЃСЏ РѕРєСЂРµРјРѕ, С‰РѕР± UI РјС–Рі РѕРґРЅРѕС‡Р°СЃРЅРѕ
  // РїРѕРєР°Р·Р°С‚Рё РіСЂСѓРїСѓ РјРѕРґРµР»РµР№ С– С‚РѕС‡РЅСѓ РїСЂРёС‡РёРЅСѓ, С‡РѕРјСѓ РєРѕРЅРєСЂРµС‚РЅРёР№ РµРєР·РµРјРїР»СЏСЂ РЅРµРґРѕСЃС‚СѓРїРЅРёР№.
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
        card.representativeVehicle.makeName,
        card.representativeVehicle.modelName,
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
        card.representativeVehicle.makeName,
        card.representativeVehicle.modelName,
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
        card.representativeVehicle.makeName,
        card.representativeVehicle.modelName,
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
        .map((vehicle) => `${vehicle.makeName} ${vehicle.modelName} ${vehicle.licensePlate}`)
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
      label: `РџРµСЂС–РѕРґ: ${periodLabel}`,
      tone: 'accent',
    },
    {
      key: 'duration',
      label: `РўСЂРёРІР°Р»С–СЃС‚СЊ: ${durationLabel}`,
      tone: 'accent',
    },
    {
      key: 'models',
      label: `РњРѕРґРµР»С–: ${filteredCards.length}`,
    },
  ]), [durationLabel, filteredCards.length, periodLabel]);
  const activeFilterChips = useMemo(() => {
    const items: ActiveFilterChipItem[] = [];

    if (search.trim()) {
      items.push({
        key: 'search',
        label: `РџРѕС€СѓРє: ${search.trim()}`,
        onRemove: () => {
          beginInteraction();
          updateSearchState({ q: null, page: 1 });
        },
      });
    }

    if (showAvailableOnly) {
      items.push({
        key: 'availability',
        label: 'Р›РёС€Рµ РґРѕСЃС‚СѓРїРЅС–',
        onRemove: () => {
          beginInteraction();
          updateSearchState({ available: false, page: 1 });
        },
      });
    }

    if (sort !== 'popular') {
      items.push({
        key: 'sort',
        label: `РЎРѕСЂС‚СѓРІР°РЅРЅСЏ: ${sortOptionLabels[sort]}`,
        onRemove: () => {
          beginInteraction();
          updateSearchState({ sort: 'popular', page: 1 });
        },
      });
    }

    if (minPrice !== DEFAULT_MIN_PRICE || maxPrice !== DEFAULT_MAX_PRICE) {
      items.push({
        key: 'price',
        label: `Р¦С–РЅР°: ${minPrice} - ${maxPrice} РіСЂРЅ`,
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
      return 'РљР»С–С”РЅС‚СЃСЊРєРёР№ РїСЂРѕС„С–Р»СЊ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.';
    }

    if (!selectedVehicle) {
      return 'РћР±РµСЂС–С‚СЊ РІР°СЂС–Р°РЅС‚ Р°РІС‚Рѕ РїРµСЂРµРґ РѕС„РѕСЂРјР»РµРЅРЅСЏРј.';
    }

    if (!requestWindowValid) {
      return 'Р”Р°С‚Р° С‚Р° С‡Р°СЃ РїРѕРІРµСЂРЅРµРЅРЅСЏ РјР°СЋС‚СЊ Р±СѓС‚Рё РїС–Р·РЅС–С€РёРјРё Р·Р° РїРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё.';
    }

    if (selectedVehicleAvailability?.state !== 'available') {
      return selectedVehicleAvailability?.note ?? 'РћР±СЂР°РЅРµ Р°РІС‚Рѕ РЅРµРґРѕСЃС‚СѓРїРЅРµ РЅР° С†РµР№ РїРµСЂС–РѕРґ.';
    }

    if (!cardholderName.trim()) {
      return "Р’РєР°Р¶С–С‚СЊ С–Рј'СЏ РІР»Р°СЃРЅРёРєР° РєР°СЂС‚РєРё.";
    }

    const cardDigits = digitsOnly(cardNumber);
    if (cardDigits.length !== CARD_NUMBER_MAX_DIGITS) {
      return 'РќРµРєРѕСЂРµРєС‚РЅРёР№ РЅРѕРјРµСЂ РєР°СЂС‚РєРё.';
    }

    const expiryDate = parseCardExpiry(cardExpiry);
    if (!expiryDate) {
      return 'Р’РєР°Р¶С–С‚СЊ С‚РµСЂРјС–РЅ РґС–С— РєР°СЂС‚РєРё Сѓ С„РѕСЂРјР°С‚С– MM/YY.';
    }

    const today = new Date();
    const todayStart = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    if (expiryDate < todayStart) {
      return 'РўРµСЂРјС–РЅ РґС–С— РєР°СЂС‚РєРё РјРёРЅСѓРІ.';
    }

    const cvvDigits = digitsOnly(cardCvv);
    if (cvvDigits.length < 3 || cvvDigits.length > 4) {
      return 'CVV РјР°С” РјС–СЃС‚РёС‚Рё 3 Р°Р±Рѕ 4 С†РёС„СЂРё.';
    }

    return null;
  };

  const getCheckoutValidationMessageEnhanced = (): string | null => {
    if (!myClient) {
      return 'РљР»С–С”РЅС‚СЃСЊРєРёР№ РїСЂРѕС„С–Р»СЊ РЅРµ Р·РЅР°Р№РґРµРЅРѕ.';
    }

    if (!myClient.isComplete) {
      return 'Р—Р°РІРµСЂС€С–С‚СЊ РїСЂРѕС„С–Р»СЊ РєР»С–С”РЅС‚Р° РїРµСЂРµРґ РѕС„РѕСЂРјР»РµРЅРЅСЏРј РѕСЂРµРЅРґРё.';
    }

    if (!selectedVehicle) {
      return 'РћР±РµСЂС–С‚СЊ РІР°СЂС–Р°РЅС‚ Р°РІС‚Рѕ РїРµСЂРµРґ РѕС„РѕСЂРјР»РµРЅРЅСЏРј.';
    }

    if (!requestWindowValid) {
      return 'Р”Р°С‚Р° С‚Р° С‡Р°СЃ РїРѕРІРµСЂРЅРµРЅРЅСЏ РјР°СЋС‚СЊ Р±СѓС‚Рё РїС–Р·РЅС–С€РёРјРё Р·Р° РїРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё.';
    }

    if (requestStart < new Date()) {
      return 'РџРѕС‡Р°С‚РѕРє РѕСЂРµРЅРґРё РЅРµ РјРѕР¶Рµ Р±СѓС‚Рё РІ РјРёРЅСѓР»РѕРјСѓ.';
    }

    if (!pickupLocation.trim() || !returnLocation.trim()) {
      return 'РћР±РµСЂС–С‚СЊ Р»РѕРєР°С†С–С— РѕС‚СЂРёРјР°РЅРЅСЏ С‚Р° РїРѕРІРµСЂРЅРµРЅРЅСЏ.';
    }

    if (selectedVehicleAvailability?.state !== 'available') {
      return selectedVehicleAvailability?.note ?? 'РћР±СЂР°РЅРµ Р°РІС‚Рѕ РЅРµРґРѕСЃС‚СѓРїРЅРµ РЅР° С†РµР№ РїРµСЂС–РѕРґ.';
    }

    if (!cardholderName.trim()) {
      return "Р’РєР°Р¶С–С‚СЊ С–Рј'СЏ РІР»Р°СЃРЅРёРєР° РєР°СЂС‚РєРё.";
    }

    const cardDigits = digitsOnly(cardNumber);
    if (cardDigits.length !== CARD_NUMBER_MAX_DIGITS) {
      return 'РќРµРєРѕСЂРµРєС‚РЅРёР№ РЅРѕРјРµСЂ РєР°СЂС‚РєРё.';
    }

    const expiryDate = parseCardExpiry(cardExpiry);
    if (!expiryDate) {
      return 'Р’РєР°Р¶С–С‚СЊ С‚РµСЂРјС–РЅ РґС–С— РєР°СЂС‚РєРё Сѓ С„РѕСЂРјР°С‚С– MM/YY.';
    }

    const today = new Date();
    const todayStart = new Date(today.getFullYear(), today.getMonth(), today.getDate());
    if (expiryDate < todayStart) {
      return 'РўРµСЂРјС–РЅ РґС–С— РєР°СЂС‚РєРё РјРёРЅСѓРІ.';
    }

    const cvvDigits = digitsOnly(cardCvv);
    if (cvvDigits.length < 3 || cvvDigits.length > 4) {
      return 'CVV РјР°С” РјС–СЃС‚РёС‚Рё 3 Р°Р±Рѕ 4 С†РёС„СЂРё.';
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
            <span className="topbar-kicker">РџРѕС€СѓРє Р°РІС‚Рѕ</span>
            <h2>Р—Р°РІР°РЅС‚Р°Р¶СѓС”РјРѕ РєР°С‚Р°Р»РѕРі РѕСЂРµРЅРґРё</h2>
            <p>Р“РѕС‚СѓС”РјРѕ РїРµСЂС–РѕРґ, РґРѕСЃС‚СѓРїРЅС–СЃС‚СЊ, РєР°СЂС‚РєРё Р°РІС‚Рѕ С‚Р° С„С–РЅР°Р»СЊРЅРёР№ Р±Р»РѕРє РѕС„РѕСЂРјР»РµРЅРЅСЏ.</p>
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
                <span>РљСЂРѕРє 4</span>
                <strong>РџС–РґРіРѕС‚РѕРІРєР° РѕС„РѕСЂРјР»РµРЅРЅСЏ</strong>
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
        <FeedbackBanner tone="error" title="РќРµ РІРґР°Р»РѕСЃСЏ РѕРЅРѕРІРёС‚Рё РєР°С‚Р°Р»РѕРі" onDismiss={() => setError(null)}>
          {error}
        </FeedbackBanner>
      ) : null}
      <button
        type="button"
        className="btn primary prokat-mobile-filter-toggle"
        aria-expanded={isMobileFiltersOpen}
        onClick={() => setIsMobileFiltersOpen((current) => !current)}
      >
        {isMobileFiltersOpen ? 'Р—Р°РєСЂРёС‚Рё С„С–Р»СЊС‚СЂРё' : 'Р¤С–Р»СЊС‚СЂРё С‚Р° РїРµСЂС–РѕРґ'}
      </button>
      <button
        type="button"
        aria-label="Р—Р°РєСЂРёС‚Рё РїР°РЅРµР»СЊ С„С–Р»СЊС‚СЂС–РІ"
        className={`prokat-mobile-filter-overlay${isMobileFiltersOpen ? ' open' : ''}`}
        onClick={() => setIsMobileFiltersOpen(false)}
      />

      <div className="prokat-layout">
        <aside className={`prokat-filters${isMobileFiltersOpen ? ' is-mobile-open' : ''}`}>
          <section className="prokat-filter-workbench" aria-label="РџРµСЂС–РѕРґ РѕСЂРµРЅРґРё С‚Р° С„С–Р»СЊС‚СЂРё">
            <div className="prokat-mobile-filter-header">
              <div>
                <strong>Р¤С–Р»СЊС‚СЂРё С‚Р° РїРµСЂС–РѕРґ</strong>
                <span>РЈСЃС– Р·РјС–РЅРё Р·Р°СЃС‚РѕСЃРѕРІСѓСЋС‚СЊСЃСЏ РѕРґСЂР°Р·Сѓ РґРѕ РєР°С‚Р°Р»РѕРіСѓ.</span>
              </div>
              <button
                type="button"
                className="btn ghost"
                onClick={() => setIsMobileFiltersOpen(false)}
              >
                Р—Р°РєСЂРёС‚Рё
              </button>
            </div>
            <div className="prokat-filter-tabs" role="tablist" aria-label="Р РѕР·РґС–Р»Рё С„С–Р»СЊС‚СЂС–РІ">
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
                  РџРѕС‡Р°С‚РѕРє
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
                        <option value="">РќРµРјР°С” РґРѕСЃС‚СѓРїРЅРѕРіРѕ С‡Р°СЃСѓ</option>
                      ) : pickupTimeOptions.map((time) => (
                        <option key={time} value={time}>{time}</option>
                      ))}
                    </select>
                  </div>
                  {startDateError ? <small className="prokat-field-hint warn">{startDateError}</small> : null}
                </label>

                <label>
                  РџРѕРІРµСЂРЅРµРЅРЅСЏ
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
                        <option value="">РќРµРјР°С” РґРѕСЃС‚СѓРїРЅРѕРіРѕ С‡Р°СЃСѓ</option>
                      ) : returnTimeOptions.map((time) => (
                        <option key={time} value={time}>{time}</option>
                      ))}
                    </select>
                  </div>
                  {endDateError ? <small className="prokat-field-hint warn">{endDateError}</small> : null}
                </label>

                <label>
                  Р›РѕРєР°С†С–СЏ РѕС‚СЂРёРјР°РЅРЅСЏ
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
                  Р›РѕРєР°С†С–СЏ РїРѕРІРµСЂРЅРµРЅРЅСЏ
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
                  РџРѕС€СѓРє Р°РІС‚Рѕ
                  <input
                    value={search}
                    onChange={(event) => {
                      beginInteraction();
                      updateSearchState({ q: event.target.value || null, page: 1 });
                    }}
                    placeholder="РњР°СЂРєР°, РјРѕРґРµР»СЊ Р°Р±Рѕ РЅРѕРјРµСЂ"
                  />
                </label>

                <label>
                  РЎРѕСЂС‚СѓРІР°РЅРЅСЏ
                  <select
                    value={sort}
                    onChange={(event) => {
                      beginInteraction();
                      updateSearchState({ sort: event.target.value as SortOption, page: 1 });
                    }}
                  >
                    <option value="popular">РЎРїРѕС‡Р°С‚РєСѓ РґРѕСЃС‚СѓРїРЅС–</option>
                    <option value="priceAsc">Р”РµС€РµРІС€С– СЃРїРѕС‡Р°С‚РєСѓ</option>
                    <option value="priceDesc">Р”РѕСЂРѕР¶С‡С– СЃРїРѕС‡Р°С‚РєСѓ</option>
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
                  <span>Р›РёС€Рµ РґРѕСЃС‚СѓРїРЅС– РЅР° С†РµР№ РїРµСЂС–РѕРґ</span>
                  <small>{availableFilteredCount} РјРѕРґРµР»РµР№ РјРѕР¶РЅР° РѕС„РѕСЂРјРёС‚Рё Р·Р°СЂР°Р·</small>
                </label>

                <div className="prokat-price-card">
                  <div className="prokat-price-card-head">
                    <div>
                      <strong>Р”С–Р°РїР°Р·РѕРЅ С†С–РЅРё</strong>
                      <span>РљР°С‚Р°Р»РѕРі РѕРЅРѕРІР»СЋС”С‚СЊСЃСЏ РІС–Рґ {minPrice} РіСЂРЅ РґРѕ {maxPrice} РіСЂРЅ Р·Р° РґРѕР±Сѓ.</span>
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
                        РћС‡РёСЃС‚РёС‚Рё С†С–РЅСѓ
                      </button>
                    ) : null}
                  </div>

                  <div className="inline-form prokat-inline-price">
                    <label>
                      Р’С–Рґ, РіСЂРЅ
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
                      Р”Рѕ, РіСЂРЅ
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
                  РЎРєРёРЅСѓС‚Рё С„С–Р»СЊС‚СЂРё
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
                <strong>{selectedClasses.length > 0 ? `${selectedClasses.length} РѕР±СЂР°РЅРѕ` : 'РЈСЃС– РєР»Р°СЃРё'}</strong>
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
                      {classStats.get(className)?.available ?? 0} Р· {classStats.get(className)?.total ?? 0} РґРѕСЃС‚СѓРїРЅС–
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
              <span className="topbar-kicker">РџРѕС€СѓРє Р°РІС‚Рѕ</span>
              <h2>РџС–РґР±С–СЂ Р°РІС‚Рѕ С‚Р° РѕС„РѕСЂРјР»РµРЅРЅСЏ РІ РѕРґРЅРѕРјСѓ СЃС†РµРЅР°СЂС–С—</h2>
              <p>
                РЎРїРѕС‡Р°С‚РєСѓ Р·Р°РґР°Р№С‚Рµ РїРµСЂС–РѕРґ, РїРѕС‚С–Рј РІС–РґС„С–Р»СЊС‚СЂСѓР№С‚Рµ РєР°С‚Р°Р»РѕРі, РѕР±РµСЂС–С‚СЊ РєРѕРЅРєСЂРµС‚РЅРёР№ РµРєР·РµРјРїР»СЏСЂ Р°РІС‚Рѕ
                С– Р·Р°РІРµСЂС€С–С‚СЊ РѕС„РѕСЂРјР»РµРЅРЅСЏ Р· РѕРїР»Р°С‚РѕСЋ РєР°СЂС‚РєРѕСЋ РІ Р±Р»РѕС†С– РїСЂР°РІРѕСЂСѓС‡.
              </p>
            </div>

            <div className="prokat-hero-side">
              <div className="prokat-hero-stats">
                <div>
                  <span>Р—РЅР°Р№РґРµРЅРѕ</span>
                  <strong>{filteredCards.length}</strong>
                </div>
                <div>
                  <span>Р”РѕСЃС‚СѓРїРЅРѕ</span>
                  <strong>{availableFilteredCount}</strong>
                </div>
              </div>

              <div className="prokat-hero-actions">
                <button
                  type="button"
                  className="btn primary prokat-hero-filter-btn"
                  onClick={() => setIsMobileFiltersOpen(true)}
                >
                  Р’С–РґРєСЂРёС‚Рё С„С–Р»СЊС‚СЂРё
                </button>
                <button type="button" className="btn ghost" onClick={() => navigate('/prokat/bookings')}>
                  РњРѕС— Р±СЂРѕРЅСЋРІР°РЅРЅСЏ С‚Р° РѕСЂРµРЅРґРё
                </button>
              </div>
            </div>
          </section>

          <div className="prokat-summary-strip">
            <ActiveFilterChips items={summaryChips} className="prokat-summary-chips" />
            {activeFilterChips.length > 0 ? (
              <div className="prokat-active-filter-strip">
                <span>РђРєС‚РёРІРЅС– С„С–Р»СЊС‚СЂРё</span>
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
                title="РќРµРјР°С” Р°РІС‚Рѕ РїС–Рґ РїРѕС‚РѕС‡РЅРёР№ Р·Р°РїРёС‚."
                description="Р—РјС–РЅС–С‚СЊ РїРµСЂС–РѕРґ Р°Р±Рѕ СЃРєРёРЅСЊС‚Рµ С„С–Р»СЊС‚СЂРё, С‰РѕР± РїРѕРІРµСЂРЅСѓС‚РёСЃСЊ РґРѕ РїРѕРІРЅРѕРіРѕ РєР°С‚Р°Р»РѕРіСѓ Р№ Р·РЅРѕРІСѓ РїРѕР±Р°С‡РёС‚Рё РґРѕСЃС‚СѓРїРЅС– РјРѕРґРµР»С–."
                actions={(
                  <>
                    <button type="button" className="btn ghost" onClick={resetFilters}>
                      РЎРєРёРЅСѓС‚Рё С„С–Р»СЊС‚СЂРё
                    </button>
                    {selectedVehicle ? (
                      <button type="button" className="btn primary" onClick={revealSelectedVehicle}>
                        РџРѕРєР°Р·Р°С‚Рё РѕР±СЂР°РЅРµ Р°РІС‚Рѕ
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
                  const cardClass = classifyVehicleBySpec(vehicle.makeName, vehicle.modelName, card.minDailyRate);
                  const fuelTypeLabel = formatFuelTypeLabel(vehicle.fuelTypeCode);
                  const transmissionTypeLabel = formatTransmissionTypeLabel(vehicle.transmissionTypeCode);
                  const cardSummary = card.vehicleCount === 1
                    ? `${vehicle.licensePlate} вЂў ${fuelTypeLabel} вЂў ${transmissionTypeLabel}`
                    : `${card.vehicleCount} Р°РІС‚Рѕ вЂў РґРѕСЃС‚СѓРїРЅРѕ ${card.availableVehicleCount} вЂў ${fuelTypeLabel} вЂў ${transmissionTypeLabel}`;
                  const cardNote = card.vehicleCount === 1
                    ? availabilityByVehicleId.get(vehicle.id)?.note
                    : isAvailable
                      ? `Р”РѕСЃС‚СѓРїРЅРѕ ${card.availableVehicleCount} Р· ${card.vehicleCount} Р°РІС‚Рѕ С†С–С”С— РјРѕРґРµР»С– РЅР° РІРёР±СЂР°РЅРёР№ РїРµСЂС–РѕРґ.`
                      : `РЈСЃС– ${card.vehicleCount} Р°РІС‚Рѕ С†С–С”С— РјРѕРґРµР»С– РЅРµРґРѕСЃС‚СѓРїРЅС– РЅР° РІРёР±СЂР°РЅРёР№ РїРµСЂС–РѕРґ.`;

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
                          alt={`${vehicle.makeName} ${vehicle.modelName}`}
                          onError={() => markImageFailed(vehicle.id)}
                        />
                      ) : (
                        <div className="prokat-card-no-photo">Р¤РѕС‚Рѕ РЅРµРґРѕСЃС‚СѓРїРЅРµ</div>
                      )}

                      <div className="prokat-card-body">
                        <div className="prokat-card-headline">
                          <div>
                            <span className="prokat-card-class">{cardClass}</span>
                            <h3>{vehicle.makeName} {vehicle.modelName}</h3>
                            <p>{cardSummary}</p>
                          </div>
                          <span className={`status-pill ${isAvailable ? 'ok' : 'bad'}`}>
                            {isAvailable ? 'Р”РѕСЃС‚СѓРїРЅРµ' : 'РќРµРґРѕСЃС‚СѓРїРЅРµ'}
                          </span>
                        </div>

                        <div className="inline-form prokat-card-meta">
                          <span>{vehicle.hasAirConditioning ? 'A/C' : 'Р‘РµР· A/C'}</span>
                          <span>{formatDoors(vehicle.doorsCount)}</span>
                          <span>{formatVehicleCargoCapacity(vehicle.cargoCapacityValue, vehicle.cargoCapacityUnit)}</span>
                          <span>{formatVehicleConsumption(vehicle.consumptionValue, vehicle.consumptionUnit)}</span>
                        </div>

                        <p className={`prokat-card-note ${isAvailable ? 'ok' : 'bad'}`}>
                          {cardNote}
                        </p>

                        <div className="prokat-card-price">
                          <div>
                            <span>Р—Р° РґРѕР±Сѓ</span>
                            <strong>{priceDisplay}</strong>
                          </div>
                          <div>
                            <span>Р—Р° РІРёР±СЂР°РЅРёР№ РїРµСЂС–РѕРґ</span>
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
                              ? 'РћР±СЂР°РЅРѕ РґР»СЏ РѕС„РѕСЂРјР»РµРЅРЅСЏ'
                              : card.vehicleCount > 1
                                ? 'Р’РёР±СЂР°С‚Рё РІР°СЂС–Р°РЅС‚'
                                : 'РџРµСЂРµР№С‚Рё РґРѕ РѕС„РѕСЂРјР»РµРЅРЅСЏ'}
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
            <FeedbackBanner tone="success" title="РћСЂРµРЅРґСѓ РѕС„РѕСЂРјР»РµРЅРѕ" className="prokat-success-card">
              <div className="prokat-success-grid">
                <span>Р”РѕРіРѕРІС–СЂ</span>
                <strong>{createdRental.contractNumber}</strong>
                <span>РЎС‚Р°С‚СѓСЃ</span>
                <strong>{rentalStatusLabel(createdRental.statusId)}</strong>
                <span>РЎРїР»Р°С‡РµРЅРѕ</span>
                <strong>{formatCurrency(createdRental.paidAmount)}</strong>
                <span>Р—Р°Р»РёС€РѕРє</span>
                <strong>{formatCurrency(createdRental.balance)}</strong>
              </div>
              <p className="muted">
                РџР»Р°С‚С–Р¶ РєР°СЂС‚РєРѕСЋ Р·Р°С„С–РєСЃРѕРІР°РЅРѕ СЂР°Р·РѕРј С–Р· РґРѕРіРѕРІРѕСЂРѕРј. РџРµСЂРµРІС–СЂРёС‚Рё СЃС‚Р°С‚СѓСЃ, С–СЃС‚РѕСЂС–СЋ Р°Р±Рѕ РїРѕРІС‚РѕСЂРёС‚Рё
                РѕС„РѕСЂРјР»РµРЅРЅСЏ РјРѕР¶РЅР° Сѓ РІРєР»Р°РґС†С– Р· РІР°С€РёРјРё РѕСЂРµРЅРґР°РјРё.
              </p>
              <div className="prokat-success-actions">
                <button type="button" className="btn primary" onClick={() => navigate('/prokat/bookings')}>
                  РџРµСЂРµР№С‚Рё РґРѕ РјРѕС—С… РѕСЂРµРЅРґ
                </button>
                <button type="button" className="btn ghost" onClick={clearFeedback}>
                  РћС„РѕСЂРјРёС‚Рё С‰Рµ
                </button>
              </div>
            </FeedbackBanner>
          ) : null}

          <section id="client-review-section" className="status-panel prokat-review-card">
            <div className="prokat-review-heading">
              <span>РљСЂРѕРє 4</span>
              <strong>Р’РёР±С–СЂ РІР°СЂС–Р°РЅС‚Р° С‚Р° РѕС„РѕСЂРјР»РµРЅРЅСЏ</strong>
            </div>
            {selectedVehicle ? (
              <div className="prokat-review-content">
                <div className="prokat-review-vehicle">
                  <div>
                    <h3>{selectedVehicle.makeName} {selectedVehicle.modelName}</h3>
                    <p>{selectedVehicle.licensePlate} вЂў {classifyVehicleBySpec(selectedVehicle.makeName, selectedVehicle.modelName, selectedVehicle.dailyRate)}</p>
                  </div>
                  <span className={`status-pill ${selectedVehicleAvailability?.state === 'available' ? 'ok' : 'bad'}`}>
                    {selectedVehicleAvailability?.state === 'available' ? 'Р“РѕС‚РѕРІРµ РґРѕ РѕС„РѕСЂРјР»РµРЅРЅСЏ' : 'РџРѕС‚СЂРµР±СѓС” Р·РјС–РЅРё'}
                  </span>
                </div>

                {!selectedVehicleInFilteredList ? (
                  <div className="prokat-selection-alert">
                    <strong>РћР±СЂР°РЅРµ Р°РІС‚Рѕ РЅРµ РІС…РѕРґРёС‚СЊ Сѓ РїРѕС‚РѕС‡РЅРёР№ СЃРїРёСЃРѕРє.</strong>
                    <p>Р’Рё РІСЃРµ С‰Рµ Р±Р°С‡РёС‚Рµ Р№РѕРіРѕ РІ Р±Р»РѕС†С– РїРµСЂРµРІС–СЂРєРё, Р°Р»Рµ РІ РєР°С‚Р°Р»РѕР·С– РІРѕРЅРѕ РїСЂРёС…РѕРІР°РЅРµ РїРѕС‚РѕС‡РЅРёРјРё С„С–Р»СЊС‚СЂР°РјРё.</p>
                    <div className="row-actions">
                      <button type="button" className="btn primary" onClick={revealSelectedVehicle}>
                        РџРѕРєР°Р·Р°С‚Рё РѕР±СЂР°РЅРµ
                      </button>
                      <button type="button" className="btn ghost" onClick={resetFilters}>
                        РЎРєРёРЅСѓС‚Рё С„С–Р»СЊС‚СЂРё
                      </button>
                    </div>
                  </div>
                ) : null}

                <div className="prokat-variant-picker">
                  <div className="prokat-variant-picker-head">
                    <div>
                      <strong>{selectedVariants.length > 1 ? 'РћР±РµСЂС–С‚СЊ РµРєР·РµРјРїР»СЏСЂ Р°РІС‚РѕРїР°СЂРєСѓ' : 'Р•РєР·РµРјРїР»СЏСЂ Р°РІС‚РѕРїР°СЂРєСѓ'}</strong>
                      <span>
                        {selectedCard?.vehicleCount === 1
                          ? 'Р”Р»СЏ С†С–С”С— РјРѕРґРµР»С– РґРѕСЃС‚СѓРїРЅРёР№ РѕРґРёРЅ РµРєР·РµРјРїР»СЏСЂ.'
                          : `${selectedVariants.length} РІР°СЂС–Р°РЅС‚С–РІ РґР»СЏ С†С–С”С— РјРѕРґРµР»С–.`}
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
                                {vehicle.mileage.toLocaleString('uk-UA')} РєРј вЂў {formatCurrency(vehicle.dailyRate)} / РґРѕР±Р°
                              </span>
                            </div>
                            <span className={`status-pill ${isAvailable ? 'ok' : 'bad'}`}>
                              {isAvailable ? 'Р”РѕСЃС‚СѓРїРЅРёР№' : 'Р—Р°Р№РЅСЏС‚РёР№'}
                            </span>
                          </div>
                          <small>{availability?.note ?? 'РЎС‚Р°С‚СѓСЃ СѓС‚РѕС‡РЅСЋС”С‚СЊСЃСЏ.'}</small>
                        </button>
                      );
                    })}
                  </div>
                </div>

                {!myClient?.isComplete ? (
                  <div className="prokat-selection-alert">
                    <strong>РџСЂРѕС„С–Р»СЊ РєР»С–С”РЅС‚Р° С‰Рµ РЅРµ Р·Р°РІРµСЂС€РµРЅРёР№.</strong>
                    <p>Р—Р°РїРѕРІРЅС–С‚СЊ СЂРµР°Р»СЊРЅС– РґРѕРєСѓРјРµРЅС‚Рё С‚Р° РєРѕРЅС‚Р°РєС‚Рё, РїС–СЃР»СЏ С‡РѕРіРѕ РїРѕРІРµСЂРЅС–С‚СЊСЃСЏ РґРѕ РѕС„РѕСЂРјР»РµРЅРЅСЏ.</p>
                    {profileCompletionMessage ? <p>{profileCompletionMessage}</p> : null}
                    <div className="row-actions">
                      <button type="button" className="btn primary" onClick={() => navigate('/prokat/profile')}>
                        РџРµСЂРµР№С‚Рё РґРѕ РїСЂРѕС„С–Р»СЋ
                      </button>
                    </div>
                  </div>
                ) : null}

                <div className="kv-grid prokat-review-grid">
                  <strong>РњРѕРґРµР»СЊ</strong>
                  <span>{selectedVehicle.makeName} {selectedVehicle.modelName}</span>
                  <strong>Р’Р°СЂС–Р°РЅС‚</strong>
                  <span>{selectedVehicle.licensePlate}</span>
                  <strong>РџРµСЂС–РѕРґ</strong>
                  <span>{periodLabel}</span>
                  <strong>РўСЂРёРІР°Р»С–СЃС‚СЊ</strong>
                  <span>{durationLabel}</span>
                  <strong>Р”РѕР±РѕРІР° СЃС‚Р°РІРєР°</strong>
                  <span>{formatCurrency(selectedVehicle.dailyRate)}</span>
                  <strong>РћСЂС–С”РЅС‚РѕРІРЅРѕ РґРѕ СЃРїР»Р°С‚Рё</strong>
                  <span>{formatCurrency(selectedVehicleAmount)}</span>
                  <strong>РћСЂРµРЅРґР°СЂ</strong>
                  <span>{myClient?.fullName ?? 'РџСЂРѕС„С–Р»СЊ РЅРµ Р·РЅР°Р№РґРµРЅРѕ'}</span>
                  <strong>РљРѕРЅС‚Р°РєС‚</strong>
                  <span>{myClient?.phone || 'РўРµР»РµС„РѕРЅ РЅРµ РІРєР°Р·Р°РЅРѕ'}</span>
                </div>

                <div className="prokat-filter-note">
                  <strong>Р›РѕРєР°С†С–С—</strong>
                  <span>{pickupLocation} в†’ {returnLocation}</span>
                </div>

                <p className={`prokat-card-note ${selectedVehicleAvailability?.state === 'available' ? 'ok' : 'bad'}`}>
                  {selectedVehicleAvailability?.note ?? 'РћР±РµСЂС–С‚СЊ Р°РІС‚Рѕ РґР»СЏ РѕС„РѕСЂРјР»РµРЅРЅСЏ.'}
                </p>

                <div className="prokat-payment-card">
                  <div className="prokat-payment-card-head">
                    <div>
                      <strong>РћРїР»Р°С‚Р° РєР°СЂС‚РєРѕСЋ</strong>
                      <span>РЈ СЃРёСЃС‚РµРјСѓ РїРѕС‚СЂР°РїР»СЏС” Р»РёС€Рµ РјР°СЃРєРѕРІР°РЅРёР№ РЅРѕРјРµСЂ РєР°СЂС‚РєРё С‚Р° С–РјвЂ™СЏ РІР»Р°СЃРЅРёРєР°.</span>
                    </div>
                  </div>

                  <div className="prokat-payment-grid">
                    <label className="prokat-payment-field prokat-payment-field-wide">
                      <span>Р’Р»Р°СЃРЅРёРє РєР°СЂС‚РєРё</span>
                      <input
                        value={cardholderName}
                        onChange={(event) => handleCardholderNameChange(event.target.value)}
                        maxLength={CARDHOLDER_NAME_MAX_LENGTH}
                        autoComplete="cc-name"
                        placeholder="Р†Рј'СЏ С‚Р° РїСЂС–Р·РІРёС‰Рµ"
                      />
                    </label>

                    <label className="prokat-payment-field prokat-payment-field-wide">
                      <span>РќРѕРјРµСЂ РєР°СЂС‚РєРё</span>
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
                      <span>РўРµСЂРјС–РЅ РґС–С— (MM/YY)</span>
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
                  РџС–СЃР»СЏ РїС–РґС‚РІРµСЂРґР¶РµРЅРЅСЏ СЃРёСЃС‚РµРјР° СЃС‚РІРѕСЂРёС‚СЊ РѕСЂРµРЅРґСѓ, РґРѕРґР°СЃС‚СЊ РїРѕС‡Р°С‚РєРѕРІРёР№ РїР»Р°С‚С–Р¶ РєР°СЂС‚РєРѕСЋ
                  С– Р·Р±РµСЂРµР¶Рµ РґРѕРіРѕРІС–СЂ Сѓ РІР°С€РёС… Р±СЂРѕРЅСЋРІР°РЅРЅСЏС… С‚Р° РѕСЂРµРЅРґР°С….
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
                      {' '}РћС„РѕСЂРјР»РµРЅРЅСЏ...
                    </>
                  ) : 'РћРїР»Р°С‚РёС‚Рё С‚Р° РѕС„РѕСЂРјРёС‚Рё'}
                </button>
              </div>
            ) : (
              <EmptyState
                icon="STEP"
                className="prokat-review-empty"
                title="РџРѕС‡РЅС–С‚СЊ Р· РІРёР±РѕСЂСѓ Р°РІС‚Рѕ."
                description="Р’РєР°Р¶С–С‚СЊ РїРµСЂС–РѕРґ, Р·РЅР°Р№РґС–С‚СЊ РґРѕСЃС‚СѓРїРЅСѓ РјРѕРґРµР»СЊ Сѓ РєР°С‚Р°Р»РѕР·С– С‚Р° РѕР±РµСЂС–С‚СЊ РєРѕРЅРєСЂРµС‚РЅРёР№ РµРєР·РµРјРїР»СЏСЂ РґР»СЏ РѕС„РѕСЂРјР»РµРЅРЅСЏ."
              />
            )}
          </section>
        </aside>
      </div>

      <ConfirmDialog
        open={cardWarningOpen}
        title="Р™РјРѕРІС–СЂРЅРѕ, С†Рµ РЅРµ СЃРїСЂР°РІР¶РЅСЏ РєР°СЂС‚РєР°"
        description={(
          <p>
            РќРѕРјРµСЂ РєР°СЂС‚РєРё РЅРµ РїСЂРѕР№С€РѕРІ Р±Р°Р·РѕРІСѓ РїРµСЂРµРІС–СЂРєСѓ. РЇРєС‰Рѕ С†Рµ С‚РµСЃС‚РѕРІР° Р°Р±Рѕ РЅРµСЃС‚Р°РЅРґР°СЂС‚РЅР° РєР°СЂС‚РєР°, РІРё РІСЃРµ РѕРґРЅРѕ РјРѕР¶РµС‚Рµ РїСЂРѕРґРѕРІР¶РёС‚Рё РѕС„РѕСЂРјР»РµРЅРЅСЏ.
          </p>
        )}
        confirmLabel="РџСЂРѕРґРѕРІР¶РёС‚Рё"
        cancelLabel="Р—РјС–РЅРёС‚Рё РЅРѕРјРµСЂ"
        onConfirm={confirmSuspiciousCardCheckout}
        onCancel={() => setCardWarningOpen(false)}
      />

      <ConfirmDialog
        open={confirmOpen}
        title="РџС–РґС‚РІРµСЂРґРёС‚Рё РѕРїР»Р°С‚Сѓ С‚Р° РѕС„РѕСЂРјР»РµРЅРЅСЏ"
        description={selectedVehicle ? (
          <>
            <p>
              РњРё СЃС‚РІРѕСЂРёРјРѕ РѕСЂРµРЅРґСѓ Р· РїРѕС‡Р°С‚РєРѕРІРёРј РїР»Р°С‚РµР¶РµРј РєР°СЂС‚РєРѕСЋ. Р¤С–РЅР°Р»СЊРЅР° РґРѕСЃС‚СѓРїРЅС–СЃС‚СЊ Р°РІС‚Рѕ С‰Рµ СЂР°Р· РїРµСЂРµРІС–СЂРёС‚СЊСЃСЏ
              РїС–Рґ С‡Р°СЃ Р·Р±РµСЂРµР¶РµРЅРЅСЏ.
            </p>
            <div className="kv-grid prokat-confirm-grid">
              <strong>РђРІС‚Рѕ</strong>
              <span>{selectedVehicle.makeName} {selectedVehicle.modelName}</span>
              <strong>Р’Р°СЂС–Р°РЅС‚</strong>
              <span>{selectedVehicle.licensePlate}</span>
              <strong>РџРµСЂС–РѕРґ</strong>
              <span>{periodLabel}</span>
              <strong>Р”Рѕ СЃРїР»Р°С‚Рё</strong>
              <span>{formatCurrency(selectedVehicleAmount)}</span>
              <strong>РћРїР»Р°С‚Р°</strong>
              <span>{buildMaskedCardPaymentNote(cardholderName, cardNumber)}</span>
            </div>
          </>
        ) : null}
        confirmLabel="РћРїР»Р°С‚РёС‚Рё С‚Р° РѕС„РѕСЂРјРёС‚Рё"
        cancelLabel="РџРѕРІРµСЂРЅСѓС‚РёСЃСЏ"
        onConfirm={() => void confirmCheckout()}
        onCancel={() => setConfirmOpen(false)}
      />
    </div>
  );
}
