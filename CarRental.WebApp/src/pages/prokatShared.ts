import { Api } from '../api/client';
import type { ClientProfile, Rental, RentalAvailabilitySlot, RentalStatus, Vehicle } from '../api/types';

export const DEFAULT_MIN_PRICE = '20';
export const DEFAULT_MAX_PRICE = '290';
export const VEHICLE_PAGE_SIZE = 20;
export const SELF_SERVICE_CANCEL_REASON = 'Скасовано клієнтом через сайт';
export const LOCATION_OPTIONS = ['Київ', 'Львів', 'Одеса', 'Дніпро', 'Харків'] as const;
export const sortOptionValues = ['popular', 'priceAsc', 'priceDesc'] as const;
export const timeOptions = Array.from({ length: 13 }, (_, index) => `${String(index + 8).padStart(2, '0')}:00`);
export const CLIENT_FULL_NAME_MAX_LENGTH = 120;
export const CLIENT_PHONE_MAX_LENGTH = 40;
export const CLIENT_PASSPORT_MAX_LENGTH = 120;
export const CLIENT_DRIVER_LICENSE_MAX_LENGTH = 80;
export const PASSWORD_MAX_LENGTH = 128;
export const CARDHOLDER_NAME_MAX_LENGTH = 120;
export const CARD_NUMBER_MAX_DIGITS = 16;
export const CARD_NUMBER_INPUT_MAX_LENGTH = 19;
export const CARD_EXPIRY_MAX_DIGITS = 4;
export const CARD_EXPIRY_INPUT_MAX_LENGTH = 5;
export const CARD_CVV_MAX_DIGITS = 4;

export type SortOption = (typeof sortOptionValues)[number];
export type AvailabilityState = 'available' | 'busy';

export interface AvailabilityInfo {
  state: AvailabilityState;
  note: string;
}

export interface CatalogVehicleCard {
  key: string;
  representativeVehicle: Vehicle;
  vehicles: Vehicle[];
  vehicleIds: number[];
  vehicleCount: number;
  availableVehicleCount: number;
  minDailyRate: number;
  maxDailyRate: number;
}

export function classifyVehicle(vehicle: Vehicle): string {
  const model = `${vehicle.make} ${vehicle.model}`.toLowerCase();
  if (model.includes('tesla') || model.includes('ioniq') || model.includes('ev')) {
    return 'Електромобілі';
  }

  if (vehicle.dailyRate >= 95) {
    return 'Преміум';
  }

  if (vehicle.dailyRate >= 75) {
    return 'Бізнес';
  }

  if (vehicle.dailyRate >= 55) {
    return 'Середній';
  }

  return 'Економ';
}

export function classifyVehicleBySpec(make: string, model: string, dailyRate: number): string {
  return classifyVehicle({
    id: 0,
    make,
    model,
    engineDisplay: '',
    fuelType: '',
    transmissionType: '',
    doorsCount: 0,
    cargoCapacityDisplay: '',
    consumptionDisplay: '',
    hasAirConditioning: false,
    licensePlate: '',
    mileage: 0,
    dailyRate,
    isAvailable: false,
    serviceIntervalKm: 0,
    photoPath: null,
  });
}

function normalizeAlphaNumeric(value: string): string {
  return value.toLowerCase().replace(/[^a-z0-9]+/g, '');
}

function buildCatalogGroupKey(vehicle: Vehicle): string {
  return `${normalizeAlphaNumeric(vehicle.make)}|${normalizeAlphaNumeric(vehicle.model)}`;
}

function isVehicleAvailable(
  vehicleId: number,
  availabilityByVehicleId: Map<number, AvailabilityInfo>,
): boolean {
  return availabilityByVehicleId.get(vehicleId)?.state === 'available';
}

function pickRepresentativeVehicle(
  vehicles: Vehicle[],
  availabilityByVehicleId: Map<number, AvailabilityInfo>,
): Vehicle {
  return [...vehicles].sort((left, right) => {
    const leftAvailable = isVehicleAvailable(left.id, availabilityByVehicleId) ? 1 : 0;
    const rightAvailable = isVehicleAvailable(right.id, availabilityByVehicleId) ? 1 : 0;
    return rightAvailable - leftAvailable || left.mileage - right.mileage || left.dailyRate - right.dailyRate;
  })[0];
}

export function buildCatalogVehicleCards(
  vehicles: Vehicle[],
  availabilityByVehicleId: Map<number, AvailabilityInfo>,
): CatalogVehicleCard[] {
  const groupedVehicles = new Map<string, Vehicle[]>();

  vehicles.forEach((vehicle) => {
    const key = buildCatalogGroupKey(vehicle);
    const currentGroup = groupedVehicles.get(key);
    if (currentGroup) {
      currentGroup.push(vehicle);
      return;
    }

    groupedVehicles.set(key, [vehicle]);
  });

  return Array.from(groupedVehicles.entries()).map(([key, groupedItems]) => {
    const representativeVehicle = pickRepresentativeVehicle(groupedItems, availabilityByVehicleId);
    const dailyRates = groupedItems.map((vehicle) => vehicle.dailyRate);

    return {
      key,
      representativeVehicle,
      vehicles: groupedItems,
      vehicleIds: groupedItems.map((vehicle) => vehicle.id),
      vehicleCount: groupedItems.length,
      availableVehicleCount: groupedItems.filter((vehicle) => (
        isVehicleAvailable(vehicle.id, availabilityByVehicleId)
      )).length,
      minDailyRate: Math.min(...dailyRates),
      maxDailyRate: Math.max(...dailyRates),
    };
  });
}

export function pickBookableVehicleId(
  vehicles: Vehicle[],
  availabilityByVehicleId: Map<number, AvailabilityInfo>,
  preferredVehicleId: number | null,
): number | null {
  if (
    preferredVehicleId
    && vehicles.some((vehicle) => (
      vehicle.id === preferredVehicleId && availabilityByVehicleId.get(vehicle.id)?.state === 'available'
    ))
  ) {
    return preferredVehicleId;
  }

  const firstAvailable = vehicles.find((vehicle) => availabilityByVehicleId.get(vehicle.id)?.state === 'available');
  return firstAvailable?.id ?? preferredVehicleId ?? vehicles[0]?.id ?? null;
}

export function sortVehiclesForSelection(
  vehicles: Vehicle[],
  availabilityByVehicleId: Map<number, AvailabilityInfo>,
): Vehicle[] {
  return [...vehicles].sort((left, right) => {
    const leftAvailable = isVehicleAvailable(left.id, availabilityByVehicleId) ? 1 : 0;
    const rightAvailable = isVehicleAvailable(right.id, availabilityByVehicleId) ? 1 : 0;
    return rightAvailable - leftAvailable || left.mileage - right.mileage || left.dailyRate - right.dailyRate;
  });
}

export function formatDoors(doorsCount: number): string {
  return doorsCount >= 5 ? `${doorsCount} дверей` : `${doorsCount} двері`;
}

export function localImage(vehicle: Vehicle): string | null {
  const path = vehicle.photoPath;
  if (!path) {
    return null;
  }

  if (path.startsWith('http://') || path.startsWith('https://') || path.startsWith('data:')) {
    return path;
  }

  if (path.startsWith('/images/')) {
    return Api.getAssetUrl(path);
  }

  return Api.getVehiclePhotoUrl(vehicle.id);
}

export function overlaps(requestStart: Date, requestEnd: Date, rentalStart: Date, rentalEnd: Date): boolean {
  return requestStart <= rentalEnd && rentalStart <= requestEnd;
}

export function parseDateTime(date: string, time: string): Date {
  return new Date(`${date}T${time}:00`);
}

export function toDateInputValue(date: Date): string {
  const timezoneOffsetMs = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - timezoneOffsetMs).toISOString().slice(0, 10);
}

function roundMoney(value: number): number {
  return Math.round((value + Number.EPSILON) * 100) / 100;
}

export function estimateRentalAmount(dailyRate: number, requestStart: Date, requestEnd: Date): number {
  const rentalHours = (requestEnd.getTime() - requestStart.getTime()) / 3_600_000;
  if (!Number.isFinite(rentalHours) || rentalHours <= 0) {
    return 0;
  }

  return roundMoney(dailyRate * (rentalHours / 24));
}

function pluralize(value: number, one: string, few: string, many: string): string {
  const normalized = Math.abs(value) % 100;
  const mod10 = normalized % 10;

  if (normalized > 10 && normalized < 20) {
    return many;
  }

  if (mod10 === 1) {
    return one;
  }

  if (mod10 >= 2 && mod10 <= 4) {
    return few;
  }

  return many;
}

export function formatDuration(rentalHours: number): string {
  if (!Number.isFinite(rentalHours) || rentalHours <= 0) {
    return '-';
  }

  const days = Math.floor(rentalHours / 24);
  const hours = rentalHours % 24;

  if (days > 0 && hours > 0) {
    return `${days} ${pluralize(days, 'доба', 'доби', 'діб')} ${hours} ${pluralize(hours, 'година', 'години', 'годин')}`;
  }

  if (days > 0) {
    return `${days} ${pluralize(days, 'доба', 'доби', 'діб')}`;
  }

  return `${rentalHours} ${pluralize(rentalHours, 'година', 'години', 'годин')}`;
}

export function formatBookingMoment(date: string, time: string): string {
  return new Intl.DateTimeFormat('uk-UA', {
    day: '2-digit',
    month: 'long',
    hour: '2-digit',
    minute: '2-digit',
  }).format(parseDateTime(date, time));
}

export function formatDateTime(value: string): string {
  return new Intl.DateTimeFormat('uk-UA', {
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(value));
}

export function formatRentalPeriod(rental: Rental): string {
  return `${formatDateTime(rental.startDate)} - ${formatDateTime(rental.endDate)}`;
}

export function compareAscByDate(left: Rental, right: Rental, selector: (rental: Rental) => string): number {
  return new Date(selector(left)).getTime() - new Date(selector(right)).getTime();
}

export function compareDescByHistoryMoment(left: Rental, right: Rental): number {
  const leftMoment = left.closedAtUtc ?? left.canceledAtUtc ?? left.createdAtUtc;
  const rightMoment = right.closedAtUtc ?? right.canceledAtUtc ?? right.createdAtUtc;
  return new Date(rightMoment).getTime() - new Date(leftMoment).getTime();
}

export function rentalStatusLabel(status: RentalStatus): string {
  switch (status) {
    case 'Booked':
      return 'Заброньовано';
    case 'Active':
      return 'Активна';
    case 'Closed':
      return 'Завершена';
    case 'Canceled':
      return 'Скасована';
    default:
      return status;
  }
}

export function rentalStatusClass(status: RentalStatus): 'ok' | 'bad' | 'wait' {
  switch (status) {
    case 'Canceled':
      return 'bad';
    case 'Booked':
      return 'wait';
    case 'Active':
    case 'Closed':
    default:
      return 'ok';
  }
}

export function digitsOnly(value: string): string {
  return value.replace(/\D+/g, '');
}

export function formatCardNumberInput(value: string): string {
  const digits = digitsOnly(value).slice(0, CARD_NUMBER_MAX_DIGITS);
  const groups = digits.match(/.{1,4}/g);
  return groups ? groups.join(' ') : '';
}

export function formatCardExpiryInput(value: string): string {
  const digits = digitsOnly(value).slice(0, CARD_EXPIRY_MAX_DIGITS);
  if (digits.length <= 2) {
    return digits;
  }

  return `${digits.slice(0, 2)}/${digits.slice(2)}`;
}

export function formatCardCvvInput(value: string): string {
  return digitsOnly(value).slice(0, CARD_CVV_MAX_DIGITS);
}

export function shouldWarnAboutCardNumber(cardNumber: string): boolean {
  const digits = digitsOnly(cardNumber);
  return digits.length === CARD_NUMBER_MAX_DIGITS && !passesLuhnCheck(digits);
}

export function isLegacyPassportData(value: string): boolean {
  return value.trim().toUpperCase().startsWith('EMP-');
}

export function isLegacyDriverLicense(value: string): boolean {
  return value.trim().toUpperCase().startsWith('USR-');
}

function isClientPhoneComplete(value: string): boolean {
  const digits = digitsOnly(value);
  return digits.length >= 10 && digits.length <= 15;
}

export function getClientProfileCompletionIssues(
  profile: Pick<ClientProfile, 'fullName' | 'phone' | 'passportData' | 'driverLicense'> | null,
): string[] {
  if (!profile) {
    return [];
  }

  const issues: string[] = [];

  if (!profile.fullName.trim()) {
    issues.push('вкажіть ПІБ');
  }

  if (!profile.phone.trim()) {
    issues.push('вкажіть телефон');
  } else if (!isClientPhoneComplete(profile.phone)) {
    issues.push('вкажіть телефон у форматі 10-15 цифр');
  }

  if (!profile.passportData.trim()) {
    issues.push('заповніть паспортні дані');
  } else if (isLegacyPassportData(profile.passportData)) {
    issues.push('замініть службове значення паспорта EMP-... на реальні дані');
  }

  if (!profile.driverLicense.trim()) {
    issues.push('заповніть посвідчення водія');
  } else if (isLegacyDriverLicense(profile.driverLicense)) {
    issues.push('замініть службове значення посвідчення USR-... на реальні дані');
  }

  return issues;
}

export function getClientProfileCompletionMessage(
  profile: Pick<ClientProfile, 'fullName' | 'phone' | 'passportData' | 'driverLicense'> | null,
): string | null {
  const issues = getClientProfileCompletionIssues(profile);
  return issues.length > 0 ? `Щоб завершити профіль, ${issues.join(', ')}.` : null;
}

export function passesLuhnCheck(digits: string): boolean {
  let sum = 0;
  let alternate = false;

  for (let index = digits.length - 1; index >= 0; index -= 1) {
    let digit = digits.charCodeAt(index) - 48;
    if (digit < 0 || digit > 9) {
      return false;
    }

    if (alternate) {
      digit *= 2;
      if (digit > 9) {
        digit -= 9;
      }
    }

    sum += digit;
    alternate = !alternate;
  }

  return sum % 10 === 0;
}

export function parseCardExpiry(value: string): Date | null {
  if (!value.trim()) {
    return null;
  }

  const parts = value.trim().split('/').map((part) => part.trim());
  if (parts.length !== 2) {
    return null;
  }

  const month = Number.parseInt(parts[0] ?? '', 10);
  let year = Number.parseInt(parts[1] ?? '', 10);
  if (!Number.isFinite(month) || !Number.isFinite(year)) {
    return null;
  }

  if (year < 100) {
    year += 2000;
  }

  if (month < 1 || month > 12 || year < 2000 || year > 2099) {
    return null;
  }

  return new Date(year, month, 0);
}

export function buildMaskedCardPaymentNote(cardholderName: string, cardNumber: string): string {
  const tail = digitsOnly(cardNumber).slice(-4);
  const owner = cardholderName.trim();
  let result = 'Оплата карткою';

  if (tail) {
    result += ` ****${tail}`;
  }

  if (owner) {
    result += `. Власник: ${owner}`;
  }

  return result;
}

export function buildAvailabilityMap(
  vehicles: Vehicle[],
  slots: RentalAvailabilitySlot[],
  requestStart: Date,
  requestEnd: Date,
): Map<number, AvailabilityInfo> {
  const map = new Map<number, AvailabilityInfo>();
  const requestWindowValid = requestEnd > requestStart;

  vehicles.forEach((vehicle) => {
    if (!requestWindowValid) {
      map.set(vehicle.id, {
        state: 'busy',
        note: 'Вкажіть коректний період, щоб побачити доступність авто.',
      });
      return;
    }

    const conflict = slots.find((slot) => (
      slot.vehicleId === vehicle.id
      && (slot.status === 'Booked' || slot.status === 'Active')
      && overlaps(requestStart, requestEnd, new Date(slot.startDate), new Date(slot.endDate))
    ));

    if (conflict) {
      map.set(vehicle.id, {
        state: 'busy',
        note: `Зайнято до ${formatDateTime(conflict.endDate)}.`,
      });
      return;
    }

    if (!vehicle.isAvailable) {
      map.set(vehicle.id, {
        state: 'busy',
        note: 'Авто тимчасово недоступне для видачі в системі.',
      });
      return;
    }

    map.set(vehicle.id, {
      state: 'available',
      note: 'Доступне для оформлення на вибраний період.',
    });
  });

  return map;
}

export function pickVehicleId(
  vehicles: Vehicle[],
  availabilityByVehicleId: Map<number, AvailabilityInfo>,
  preferredVehicleId: number | null,
): number | null {
  if (preferredVehicleId && vehicles.some((vehicle) => vehicle.id === preferredVehicleId)) {
    return preferredVehicleId;
  }

  const firstAvailable = vehicles.find((vehicle) => availabilityByVehicleId.get(vehicle.id)?.state === 'available');
  return firstAvailable?.id ?? vehicles[0]?.id ?? null;
}
