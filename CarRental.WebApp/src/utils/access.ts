import type { UserRole } from '../api/types';

type NavigationSection = 'Операції' | 'Аналітика' | 'Клієнт' | 'Система';

export interface NavigationItem {
  path: string;
  label: string;
  section: NavigationSection;
  description: string;
  roles: UserRole[];
}

const clientNavigationItems: NavigationItem[] = [
  {
    path: '/prokat/search',
    label: 'Підібрати авто',
    section: 'Клієнт',
    description: 'Каталог авто, вибір екземпляра на період і оформлення оренди з карткою.',
    roles: ['User'],
  },
  {
    path: '/prokat/bookings',
    label: 'Мої бронювання',
    section: 'Клієнт',
    description: 'Майбутні, активні та завершені оренди з діями самообслуговування.',
    roles: ['User'],
  },
];

const staffNavigationItems: NavigationItem[] = [
  {
    path: '/rentals',
    label: 'Оренди',
    section: 'Операції',
    description: 'Договори, платежі та завантаженість автопарку.',
    roles: ['Admin', 'Manager'],
  },
  {
    path: '/fleet',
    label: 'Автопарк',
    section: 'Операції',
    description: 'Каталог авто, ставки та швидкі деталі по машинах.',
    roles: ['Admin', 'Manager'],
  },
  {
    path: '/clients',
    label: 'Клієнти',
    section: 'Операції',
    description: 'Реєстр клієнтів, статуси профілів і перевірка доступу до оренди.',
    roles: ['Admin', 'Manager'],
  },
  {
    path: '/maintenance',
    label: 'ТО',
    section: 'Операції',
    description: 'Планове обслуговування, прострочені роботи та контроль пробігу.',
    roles: ['Admin', 'Manager'],
  },
  {
    path: '/damages',
    label: 'Пошкодження',
    section: 'Операції',
    description: 'Акти, ремонти та нарахування по інцидентах.',
    roles: ['Admin', 'Manager'],
  },
  {
    path: '/reports',
    label: 'Звіти',
    section: 'Аналітика',
    description: 'KPI, фільтровані підсумки та експорт даних.',
    roles: ['Admin', 'Manager'],
  },
  {
    path: '/admin',
    label: 'Адміністрування',
    section: 'Система',
    description: 'Працівники, ролі, блокування та безпека облікових записів.',
    roles: ['Admin'],
  },
];

const fallbackPageMeta: NavigationItem = {
  path: '/',
  label: 'Робочий простір',
  section: 'Клієнт',
  description: 'Виберіть доступний модуль для продовження роботи.',
  roles: ['User', 'Admin', 'Manager'],
};

export function hasRole(userRole: UserRole | undefined, allowed: UserRole[]): boolean {
  if (!userRole) {
    return false;
  }

  return allowed.includes(userRole);
}

export function getDefaultPathByRole(role: UserRole | undefined): string {
  if (role === 'User') {
    return '/prokat/search';
  }

  if (role === 'Admin' || role === 'Manager') {
    return '/rentals';
  }

  return '/login';
}

export function getNavigationSections(role: UserRole | undefined): Array<{
  section: NavigationSection;
  items: NavigationItem[];
}> {
  const sourceItems = role === 'User'
    ? clientNavigationItems
    : staffNavigationItems;

  const items = sourceItems.filter((item) => hasRole(role, item.roles));
  const order: NavigationSection[] = ['Операції', 'Аналітика', 'Клієнт', 'Система'];

  return order
    .map((section) => ({
      section,
      items: items.filter((item) => item.section === section),
    }))
    .filter((group) => group.items.length > 0);
}

export function getPageMeta(pathname: string): NavigationItem {
  const sourceItems = [...clientNavigationItems, ...staffNavigationItems];
  const normalizedPath = pathname.replace(/\/+$/, '') || '/';

  return sourceItems.find((item) => normalizedPath === item.path || normalizedPath.startsWith(`${item.path}/`)) ?? fallbackPageMeta;
}

