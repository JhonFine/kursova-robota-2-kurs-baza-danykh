export function formatCurrency(value: number): string {
  return new Intl.NumberFormat('uk-UA', {
    style: 'currency',
    currency: 'EUR',
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
