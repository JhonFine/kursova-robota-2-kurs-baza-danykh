export const MIN_DAILY_RATE = 1000;
export const MAX_DAILY_RATE = 3500;
export const ECONOMY_UPPER_BOUND = 1500;
export const MID_UPPER_BOUND = 2000;
export const BUSINESS_UPPER_BOUND = 2500;

const UA_LICENSE_PLATE_REGEX = /^[ABCEHIKMOPTX]{2}\d{4}[ABCEHIKMOPTX]{2}$/;

export function normalizeLicensePlate(value: string): string {
  return value.trim().toUpperCase();
}

export function isValidUaLicensePlate(value: string): boolean {
  return UA_LICENSE_PLATE_REGEX.test(normalizeLicensePlate(value));
}

export function resolveVehicleClassLabel(dailyRate: number): string {
  if (dailyRate >= BUSINESS_UPPER_BOUND) {
    return 'Преміум';
  }

  if (dailyRate >= MID_UPPER_BOUND) {
    return 'Бізнес';
  }

  if (dailyRate >= ECONOMY_UPPER_BOUND) {
    return 'Середній';
  }

  return 'Економ';
}
