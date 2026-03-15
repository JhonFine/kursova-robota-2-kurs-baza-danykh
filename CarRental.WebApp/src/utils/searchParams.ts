export function parsePositiveIntParam(value: string | null, fallback: number): number {
  const parsed = Number.parseInt(value ?? '', 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
}

export function parseNullablePositiveIntParam(value: string | null): number | null {
  const parsed = Number.parseInt(value ?? '', 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
}

export function parseBooleanParam(value: string | null, fallback: boolean): boolean {
  if (value === null) {
    return fallback;
  }

  const normalized = value.trim().toLowerCase();
  if (normalized === 'true' || normalized === '1' || normalized === 'yes' || normalized === 'on') {
    return true;
  }

  if (normalized === 'false' || normalized === '0' || normalized === 'no' || normalized === 'off') {
    return false;
  }

  return fallback;
}

export function parseCsvParam(value: string | null): string[] {
  if (!value) {
    return [];
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean);
}

export function parseEnumParam<T extends string>(
  value: string | null,
  allowedValues: readonly T[],
  fallback: T,
): T {
  return allowedValues.includes(value as T) ? (value as T) : fallback;
}

export function withUpdatedSearchParams(
  current: URLSearchParams,
  updates: Record<string, string | number | boolean | null | undefined>,
): URLSearchParams {
  const next = new URLSearchParams(current);

  Object.entries(updates).forEach(([key, value]) => {
    if (value === null || value === undefined || value === '') {
      next.delete(key);
      return;
    }

    next.set(key, String(value));
  });

  return next;
}
