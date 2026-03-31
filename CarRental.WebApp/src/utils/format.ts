export function formatCurrency(value: number): string {
  return new Intl.NumberFormat('uk-UA', {
    style: 'currency',
    currency: 'UAH',
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return new Date(value).toLocaleString('uk-UA');
}

export function formatShortDate(value: string | null | undefined): string {
  if (!value) {
    return '-';
  }

  return new Date(value).toLocaleDateString('uk-UA');
}

function formatSpecNumber(value: number): string {
  return new Intl.NumberFormat('uk-UA', {
    minimumFractionDigits: Number.isInteger(value) ? 0 : 1,
    maximumFractionDigits: 2,
  }).format(value);
}

export function formatVehiclePowertrain(value: number, unit: string): string {
  switch (unit) {
    case 'KWH':
      return `${formatSpecNumber(value)} кВт·год`;
    case 'L':
    default:
      return `${formatSpecNumber(value)} л`;
  }
}

export function formatVehicleCargoCapacity(value: number, unit: string): string {
  switch (unit) {
    case 'KG':
      return `${formatSpecNumber(value)} кг`;
    case 'SEATS':
      return `${formatSpecNumber(value)} місця`;
    case 'L':
    default:
      return `${formatSpecNumber(value)} л`;
  }
}

export function formatVehicleConsumption(value: number, unit: string): string {
  switch (unit) {
    case 'KWH_PER_100KM':
      return `${formatSpecNumber(value)} кВт·год/100 км`;
    case 'L_PER_100KM':
    default:
      return `${formatSpecNumber(value)} л/100 км`;
  }
}
