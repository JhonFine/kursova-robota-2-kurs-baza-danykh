import { MAX_DAILY_RATE, MIN_DAILY_RATE, resolveVehicleClassLabel } from './vehicleRules';

export const DEFAULT_MIN_PRICE = String(MIN_DAILY_RATE);
export const DEFAULT_MAX_PRICE = String(MAX_DAILY_RATE);
export const SELF_SERVICE_CANCEL_REASON = 'Скасовано клієнтом через сайт';
export const STAFF_CANCELLATION_REASON = 'За запитом клієнта';
export const DEFAULT_MAINTENANCE_DESCRIPTION = 'Планове техобслуговування';
export const DEFAULT_DAMAGE_DESCRIPTION = 'Пошкодження кузова';
export const LOCATION_OPTIONS = ['Київ', 'Львів', 'Одеса', 'Дніпро', 'Харків'] as const;
export const TIME_OPTIONS = Array.from({ length: 13 }, (_, index) => `${String(index + 8).padStart(2, '0')}:00`);
export const FUEL_TYPE_OPTIONS = [
  { value: 'PETROL', label: 'Бензин' },
  { value: 'DIESEL', label: 'Дизель' },
  { value: 'EV', label: 'Електро' },
] as const;
export const TRANSMISSION_TYPE_OPTIONS = [
  { value: 'AUTO', label: 'Автомат' },
  { value: 'MANUAL', label: 'Механіка' },
] as const;
export const POWERTRAIN_UNIT_OPTIONS = [
  { value: 'L', label: 'л' },
  { value: 'KWH', label: 'кВт·год' },
] as const;
export const CARGO_UNIT_OPTIONS = [
  { value: 'L', label: 'л' },
  { value: 'KG', label: 'кг' },
  { value: 'SEATS', label: 'місця' },
] as const;
export const CONSUMPTION_UNIT_OPTIONS = [
  { value: 'L_PER_100KM', label: 'л/100 км' },
  { value: 'KWH_PER_100KM', label: 'кВт·год/100 км' },
] as const;

// Клієнтський каталог має окремий маркетинговий клас "Електромобілі",
// тому тут є невелика евристика поверх rate-based класифікації.
export function resolveCatalogVehicleClassLabel(make: string, model: string, dailyRate: number): string {
  const normalizedModel = `${make} ${model}`.toLowerCase();
  if (normalizedModel.includes('tesla') || normalizedModel.includes('ioniq') || normalizedModel.includes('ev')) {
    return 'Електромобілі';
  }

  return resolveVehicleClassLabel(dailyRate);
}

export function formatFuelTypeLabel(code: string): string {
  return FUEL_TYPE_OPTIONS.find((item) => item.value === code)?.label ?? code;
}

export function formatTransmissionTypeLabel(code: string): string {
  return TRANSMISSION_TYPE_OPTIONS.find((item) => item.value === code)?.label ?? code;
}
