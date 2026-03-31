import axios, { type AxiosError } from 'axios';
import type {
  AuthenticatedUser,
  AuthTokenResponse,
  Client,
  ClientProfile,
  Damage,
  DamageStatus,
  Employee,
  HealthStatus,
  MaintenanceDue,
  MaintenanceRecord,
  MediaAsset,
  Payment,
  PaymentDirection,
  PaymentMethod,
  RentalAvailabilitySlot,
  Rental,
  RentalStatus,
  RentalBalance,
  ReportSummary,
  UserRole,
  Vehicle,
  VehicleUpsertPayload,
  PaginationParams,
  PagedResult,
} from './types';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5079';
const TOKEN_STORAGE_KEY = 'car_rental_token';

type WireEnum<T extends string> = T | number | `${number}`;

type EmployeeWire = Omit<Employee, 'role'> & { role: WireEnum<UserRole> };
type AccountWire = {
  id: number;
  login: string;
  isActive: boolean;
  lastLoginUtc?: string | null;
  lockoutUntilUtc?: string | null;
};
type ClientSummaryWire = {
  id: number;
  fullName: string;
  phone: string;
};
type AccountContextWire = {
  account: AccountWire;
  role: WireEnum<UserRole>;
  employee?: EmployeeWire | null;
  client?: ClientSummaryWire | null;
};
type AuthTokenResponseWire = Omit<AuthTokenResponse, 'user' | 'employee'> & {
  user: AccountContextWire;
  employee?: EmployeeWire | null;
};
type MediaAssetWire = MediaAsset;
type RentalWire = Omit<Rental, 'status'> & { status: WireEnum<RentalStatus> };
type RentalAvailabilitySlotWire = Omit<RentalAvailabilitySlot, 'status'> & { status: WireEnum<RentalStatus> };
type PaymentWire = Omit<Payment, 'method' | 'direction'> & {
  method: WireEnum<PaymentMethod>;
  direction: WireEnum<PaymentDirection>;
};
type DamageWire = Omit<Damage, 'status' | 'photos'> & {
  status: WireEnum<DamageStatus>;
  photos?: MediaAssetWire[] | null;
};

const api = axios.create({
  baseURL: API_BASE_URL,
  timeout: 20000,
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_STORAGE_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

export function setAuthToken(token: string | null): void {
  if (token) {
    localStorage.setItem(TOKEN_STORAGE_KEY, token);
    return;
  }

  localStorage.removeItem(TOKEN_STORAGE_KEY);
}

export function getStoredToken(): string | null {
  return localStorage.getItem(TOKEN_STORAGE_KEY);
}

type ApiErrorResponse = {
  message?: string;
  title?: string;
  detail?: string;
  errors?: Record<string, string[] | string | null | undefined>;
};

function toErrorMessage(error: unknown): string {
  const axiosError = error as AxiosError<ApiErrorResponse>;
  const message = axiosError.response?.data?.message;
  if (message && typeof message === 'string') {
    return message;
  }

  const validationMessages = Object
    .values(axiosError.response?.data?.errors ?? {})
    .flatMap((value) => {
      if (Array.isArray(value)) {
        return value;
      }

      return typeof value === 'string' ? [value] : [];
    })
    .map((value) => value.trim())
    .filter((value) => value.length > 0);
  if (validationMessages.length > 0) {
    return validationMessages.join(' ');
  }

  const detail = axiosError.response?.data?.detail;
  if (detail && typeof detail === 'string') {
    return detail;
  }

  const title = axiosError.response?.data?.title;
  if (title && typeof title === 'string') {
    return title;
  }

  if (!axiosError.response) {
    if (axiosError.code === 'ERR_NETWORK' || axiosError.message === 'Network Error') {
      return `Не вдалося підключитися до API (${API_BASE_URL}). Перевірте запуск Web API та PostgreSQL.`;
    }
  }

  return axiosError.message || 'Request failed';
}

function normalizeUserRole(role: WireEnum<UserRole> | undefined): UserRole {
  switch (role) {
    case 'Admin':
    case 1:
    case '1':
      return 'Admin';
    case 'Manager':
    case 2:
    case '2':
      return 'Manager';
    case 'User':
    case 3:
    case '3':
      return 'User';
    default:
      return 'User';
  }
}

function normalizeRentalStatus(status: WireEnum<RentalStatus> | undefined): RentalStatus {
  switch (status) {
    case 'Booked':
    case 1:
    case '1':
      return 'Booked';
    case 'Active':
    case 2:
    case '2':
      return 'Active';
    case 'Closed':
    case 3:
    case '3':
      return 'Closed';
    case 'Canceled':
    case 4:
    case '4':
      return 'Canceled';
    default:
      return 'Booked';
  }
}

function normalizePaymentMethod(method: WireEnum<PaymentMethod> | undefined): PaymentMethod {
  switch (method) {
    case 'Cash':
    case 1:
    case '1':
      return 'Cash';
    case 'Card':
    case 2:
    case '2':
      return 'Card';
    default:
      return 'Cash';
  }
}

function normalizePaymentDirection(direction: WireEnum<PaymentDirection> | undefined): PaymentDirection {
  switch (direction) {
    case 'Incoming':
    case 1:
    case '1':
      return 'Incoming';
    case 'Refund':
    case 2:
    case '2':
      return 'Refund';
    default:
      return 'Incoming';
  }
}

function normalizeDamageStatus(status: WireEnum<DamageStatus> | undefined): DamageStatus {
  switch (status) {
    case 'Open':
    case 1:
    case '1':
      return 'Open';
    case 'Charged':
    case 2:
    case '2':
      return 'Charged';
    case 'Resolved':
    case 3:
    case '3':
      return 'Resolved';
    default:
      return 'Open';
  }
}

function normalizeEmployee(employee: EmployeeWire): Employee {
  return {
    ...employee,
    role: normalizeUserRole(employee.role),
  };
}

function normalizeAuthenticatedUser(context: AccountContextWire): AuthenticatedUser {
  const normalizedEmployee = context.employee ? normalizeEmployee(context.employee) : null;
  const fullName = normalizedEmployee?.fullName ?? context.client?.fullName ?? context.account.login;

  return {
    accountId: context.account.id,
    employeeId: normalizedEmployee?.id ?? null,
    clientId: context.client?.id ?? null,
    fullName,
    login: context.account.login,
    role: normalizeUserRole(context.role),
    isActive: context.account.isActive,
    lastLoginUtc: context.account.lastLoginUtc ?? null,
    lockoutUntilUtc: context.account.lockoutUntilUtc ?? null,
  };
}

function normalizeAuthTokenResponse(response: AuthTokenResponseWire): AuthTokenResponse {
  return {
    accessToken: response.accessToken,
    expiresAtUtc: response.expiresAtUtc,
    user: normalizeAuthenticatedUser(response.user),
    employee: response.employee ? normalizeEmployee(response.employee) : null,
  };
}

function normalizeRental(rental: RentalWire): Rental {
  return {
    ...rental,
    status: normalizeRentalStatus(rental.status),
  };
}

function normalizeRentalAvailabilitySlot(slot: RentalAvailabilitySlotWire): RentalAvailabilitySlot {
  return {
    ...slot,
    status: normalizeRentalStatus(slot.status),
  };
}

function normalizePayment(payment: PaymentWire): Payment {
  return {
    ...payment,
    method: normalizePaymentMethod(payment.method),
    direction: normalizePaymentDirection(payment.direction),
  };
}

function normalizeDamage(damage: DamageWire): Damage {
  const photos = damage.photos ?? [];
  return {
    ...damage,
    photos,
    photoPath: damage.photoPath ?? photos
      .slice()
      .sort((left, right) => left.sortOrder - right.sortOrder)
      .map((item) => item.storedPath)[0] ?? null,
    status: normalizeDamageStatus(damage.status),
  };
}

function resolvePagedResult<T>(
  items: T[],
  headers: Record<string, unknown>,
  pagination: PaginationParams,
): PagedResult<T> {
  const parseHeader = (key: string, fallback: number): number => {
    const raw = headers[key];
    const value = Array.isArray(raw) ? raw[0] : raw;
    const parsed = Number.parseInt(value ?? '', 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback;
  };

  return {
    items,
    page: parseHeader('x-page', pagination.page),
    pageSize: parseHeader('x-page-size', pagination.pageSize),
    totalCount: parseHeader('x-total-count', items.length),
  };
}

export const Api = {
  async login(login: string, password: string): Promise<AuthTokenResponse> {
    const response = await api.post<AuthTokenResponseWire>('/api/auth/login', { login, password });
    return normalizeAuthTokenResponse(response.data);
  },

  async register(input: {
    fullName: string;
    login: string;
    phone: string;
    password: string;
  }): Promise<AuthTokenResponse> {
    const response = await api.post<AuthTokenResponseWire>('/api/auth/register', input);
    return normalizeAuthTokenResponse(response.data);
  },

  async me(): Promise<AuthenticatedUser> {
    const response = await api.get<AccountContextWire>('/api/auth/me');
    return normalizeAuthenticatedUser(response.data);
  },

  async changeOwnRole(role: UserRole): Promise<Employee> {
    const response = await api.patch<EmployeeWire>('/api/auth/me/role', { role });
    return normalizeEmployee(response.data);
  },

  async changePassword(currentPassword: string, newPassword: string): Promise<void> {
    await api.post('/api/auth/change-password', { currentPassword, newPassword });
  },

  async getOwnClient(): Promise<ClientProfile> {
    const response = await api.get<ClientProfile>('/api/profile/client');
    return response.data;
  },

  async updateOwnClientProfile(payload: {
    fullName: string;
    phone: string;
    passportData: string;
    passportExpirationDate?: string | null;
    passportPhotoPath?: string | null;
    driverLicense: string;
    driverLicenseExpirationDate?: string | null;
    driverLicensePhotoPath?: string | null;
  }): Promise<ClientProfile> {
    const response = await api.put<ClientProfile>('/api/profile/client', payload);
    return response.data;
  },

  async getClients(): Promise<Client[]> {
    const response = await api.get<Client[]>('/api/clients');
    return response.data;
  },

  async getClientsPage(params: PaginationParams & {
    search?: string;
    blacklisted?: boolean;
  }): Promise<PagedResult<Client>> {
    const response = await api.get<Client[]>('/api/clients', { params });
    return resolvePagedResult(response.data, response.headers, params);
  },

  async createClient(payload: Omit<Client, 'id'>): Promise<Client> {
    const response = await api.post<Client>('/api/clients', payload);
    return response.data;
  },

  async updateClient(id: number, payload: Omit<Client, 'id'>): Promise<Client> {
    const response = await api.put<Client>(`/api/clients/${id}`, payload);
    return response.data;
  },

  async setClientBlacklist(id: number, blacklisted: boolean): Promise<Client> {
    const response = await api.patch<Client>(`/api/clients/${id}/blacklist`, { blacklisted });
    return response.data;
  },

  async deleteClient(id: number): Promise<void> {
    await api.delete(`/api/clients/${id}`);
  },

  async getVehicles(): Promise<Vehicle[]> {
    const response = await api.get<Vehicle[]>('/api/vehicles');
    return response.data;
  },

  async getVehiclesPage(params: PaginationParams & {
    search?: string;
    availability?: boolean;
    vehicleClass?: string;
    sortBy?: string;
    sortDir?: 'asc' | 'desc';
  }): Promise<PagedResult<Vehicle>> {
    const response = await api.get<Vehicle[]>('/api/vehicles', { params });
    return resolvePagedResult(response.data, response.headers, params);
  },

  getVehiclePhotoUrl(vehicleId: number): string {
    return `${API_BASE_URL}/api/vehicles/${vehicleId}/photo`;
  },

  getDamagePhotoUrl(damageId: number, photoId: number): string {
    return `${API_BASE_URL}/api/damages/${damageId}/photos/${photoId}`;
  },

  async getDamagePhotoBlob(damageId: number, photoId: number): Promise<Blob> {
    const response = await api.get<Blob>(`/api/damages/${damageId}/photos/${photoId}`, {
      responseType: 'blob',
    });
    return response.data;
  },

  getAssetUrl(path: string): string {
    const normalizedPath = path.startsWith('/') ? path : `/${path}`;
    return `${API_BASE_URL}${normalizedPath}`;
  },

  async createVehicle(payload: VehicleUpsertPayload): Promise<Vehicle> {
    const response = await api.post<Vehicle>('/api/vehicles', payload);
    return response.data;
  },

  async updateVehicle(id: number, payload: VehicleUpsertPayload): Promise<Vehicle> {
    const response = await api.put<Vehicle>(`/api/vehicles/${id}`, payload);
    return response.data;
  },

  async updateVehicleRate(id: number, dailyRate: number): Promise<Vehicle> {
    const response = await api.patch<Vehicle>(`/api/vehicles/${id}/rate`, { dailyRate });
    return response.data;
  },

  async deleteVehicle(id: number): Promise<void> {
    await api.delete(`/api/vehicles/${id}`);
  },

  async getRentals(params?: {
    status?: string;
    vehicleId?: number;
    clientId?: number;
    fromDate?: string;
    toDate?: string;
    search?: string;
  }): Promise<Rental[]> {
    const response = await api.get<RentalWire[]>('/api/rentals', { params });
    return response.data.map(normalizeRental);
  },

  async getOwnRentals(params?: {
    status?: string;
    vehicleId?: number;
    fromDate?: string;
    toDate?: string;
  }): Promise<Rental[]> {
    const response = await api.get<RentalWire[]>('/api/rentals', { params });
    return response.data.map(normalizeRental);
  },

  async getRentalAvailabilitySlots(): Promise<RentalAvailabilitySlot[]> {
    const response = await api.get<RentalAvailabilitySlotWire[]>('/api/rentals/availability');
    return response.data.map(normalizeRentalAvailabilitySlot);
  },

  async getRentalsPage(params: {
    page: number;
    pageSize: number;
    status?: string;
    vehicleId?: number;
    clientId?: number;
    fromDate?: string;
    toDate?: string;
    search?: string;
  }): Promise<PagedResult<Rental>> {
    const response = await api.get<RentalWire[]>('/api/rentals', { params });
    return resolvePagedResult(response.data.map(normalizeRental), response.headers, params);
  },

  async createRental(payload: {
    clientId: number;
    vehicleId: number;
    startDate: string;
    endDate: string;
    pickupLocation: string;
    returnLocation?: string;
    createInitialPayment: boolean;
    paymentMethod?: PaymentMethod;
    paymentDirection?: PaymentDirection;
    notes?: string;
  }): Promise<Rental> {
    const response = await api.post<RentalWire>('/api/rentals', payload);
    return normalizeRental(response.data);
  },

  async createRentalWithCardPayment(payload: {
    clientId: number;
    vehicleId: number;
    startDate: string;
    endDate: string;
    pickupLocation: string;
    returnLocation?: string;
    notes: string;
  }): Promise<Rental> {
    const response = await api.post<RentalWire>('/api/rentals', {
      ...payload,
      createInitialPayment: true,
      paymentMethod: 'Card',
      paymentDirection: 'Incoming',
    });
    return normalizeRental(response.data);
  },

  async closeRental(
    id: number,
    actualEndDate: string,
    endMileage: number,
    returnFuelPercent?: number | null,
    returnInspectionNotes?: string,
  ): Promise<Rental> {
    const response = await api.post<RentalWire>(`/api/rentals/${id}/close`, {
      actualEndDate,
      endMileage,
      returnFuelPercent,
      returnInspectionNotes,
    });
    return normalizeRental(response.data);
  },

  async cancelRental(id: number, reason: string): Promise<Rental> {
    const response = await api.post<RentalWire>(`/api/rentals/${id}/cancel`, { reason });
    return normalizeRental(response.data);
  },

  async rescheduleRental(id: number, startDate: string, endDate: string): Promise<Rental> {
    const response = await api.post<RentalWire>(`/api/rentals/${id}/reschedule`, { startDate, endDate });
    return normalizeRental(response.data);
  },

  async settleRentalBalance(id: number, notes: string): Promise<Rental> {
    const response = await api.post<RentalWire>(`/api/rentals/${id}/settle-balance`, { notes });
    return normalizeRental(response.data);
  },

  async completePickupInspection(id: number, fuelPercent: number, notes: string): Promise<Rental> {
    const response = await api.post<RentalWire>(`/api/rentals/${id}/pickup-inspection`, { fuelPercent, notes });
    return normalizeRental(response.data);
  },

  async refreshRentalStatuses(): Promise<void> {
    await api.post('/api/rentals/refresh-statuses');
  },

  async getRentalPayments(rentalId: number): Promise<Payment[]> {
    const response = await api.get<PaymentWire[]>(`/api/payments/rentals/${rentalId}`);
    return response.data.map(normalizePayment);
  },

  async getRentalBalance(rentalId: number): Promise<RentalBalance> {
    const response = await api.get<RentalBalance>(`/api/payments/rentals/${rentalId}/balance`);
    return response.data;
  },

  async addPayment(payload: {
    rentalId: number;
    amount: number;
    method: string;
    direction: string;
    notes: string;
  }): Promise<Payment> {
    const response = await api.post<PaymentWire>('/api/payments', payload);
    return normalizePayment(response.data);
  },

  async getDamages(): Promise<Damage[]> {
    const response = await api.get<DamageWire[]>('/api/damages');
    return response.data.map(normalizeDamage);
  },

  async getDamagesPage(pagination: PaginationParams): Promise<PagedResult<Damage>> {
    const response = await api.get<DamageWire[]>('/api/damages', { params: pagination });
    return resolvePagedResult(response.data.map(normalizeDamage), response.headers, pagination);
  },

  async addDamage(payload: {
    vehicleId: number;
    rentalId?: number | null;
    description: string;
    repairCost: number;
    autoChargeToRental: boolean;
    photos?: File[];
  }): Promise<Damage> {
    const hasPhotos = (payload.photos?.length ?? 0) > 0;
    if (!hasPhotos) {
      const response = await api.post<DamageWire>('/api/damages', {
        vehicleId: payload.vehicleId,
        rentalId: payload.rentalId ?? null,
        description: payload.description,
        repairCost: payload.repairCost,
        autoChargeToRental: payload.autoChargeToRental,
      });
      return normalizeDamage(response.data);
    }

    const formData = new FormData();
    formData.append('vehicleId', String(payload.vehicleId));
    if (payload.rentalId) {
      formData.append('rentalId', String(payload.rentalId));
    }

    formData.append('description', payload.description);
    formData.append('repairCost', String(payload.repairCost));
    formData.append('autoChargeToRental', String(payload.autoChargeToRental));
    for (const photo of payload.photos ?? []) {
      formData.append('photos', photo);
    }

    const response = await api.post<DamageWire>('/api/damages', formData);
    return normalizeDamage(response.data);
  },

  async getMaintenanceRecords(): Promise<MaintenanceRecord[]> {
    const response = await api.get<MaintenanceRecord[]>('/api/maintenance/records');
    return response.data;
  },

  async getMaintenanceRecordsPage(pagination: PaginationParams): Promise<PagedResult<MaintenanceRecord>> {
    const response = await api.get<MaintenanceRecord[]>('/api/maintenance/records', { params: pagination });
    return resolvePagedResult(response.data, response.headers, pagination);
  },

  async getMaintenanceDue(): Promise<MaintenanceDue[]> {
    const response = await api.get<MaintenanceDue[]>('/api/maintenance/due');
    return response.data;
  },

  async addMaintenanceRecord(payload: {
    vehicleId: number;
    serviceDate: string;
    mileageAtService: number;
    description: string;
    cost: number;
    nextServiceMileage: number;
  }): Promise<void> {
    await api.post('/api/maintenance/records', payload);
  },

  async getEmployees(): Promise<Employee[]> {
    const response = await api.get<EmployeeWire[]>('/api/admin/employees');
    return response.data.map(normalizeEmployee);
  },

  async getEmployeesPage(pagination: PaginationParams): Promise<PagedResult<Employee>> {
    const response = await api.get<EmployeeWire[]>('/api/admin/employees', { params: pagination });
    return resolvePagedResult(response.data.map(normalizeEmployee), response.headers, pagination);
  },

  async toggleEmployeeActive(id: number): Promise<Employee> {
    const response = await api.patch<EmployeeWire>(`/api/admin/employees/${id}/toggle-active`);
    return normalizeEmployee(response.data);
  },

  async toggleEmployeeManagerRole(id: number): Promise<Employee> {
    const response = await api.patch<EmployeeWire>(`/api/admin/employees/${id}/toggle-manager-role`);
    return normalizeEmployee(response.data);
  },

  async unlockEmployee(id: number): Promise<Employee> {
    const response = await api.patch<EmployeeWire>(`/api/admin/employees/${id}/unlock`);
    return normalizeEmployee(response.data);
  },

  async resetEmployeePassword(id: number, newPassword: string): Promise<void> {
    await api.post(`/api/admin/employees/${id}/reset-password`, { newPassword });
  },

  async getReportSummary(): Promise<ReportSummary> {
    const response = await api.get<ReportSummary>('/api/reports/summary');
    return response.data;
  },

  async getReportRentals(params: {
    fromDate: string;
    toDate: string;
    vehicleId?: number;
    employeeId?: number;
  }): Promise<Rental[]> {
    const response = await api.get<RentalWire[]>('/api/reports/rentals', { params });
    return response.data.map(normalizeRental);
  },

  async getReportRentalsPage(params: {
    fromDate: string;
    toDate: string;
    vehicleId?: number;
    employeeId?: number;
    page: number;
    pageSize: number;
  }): Promise<PagedResult<Rental>> {
    const response = await api.get<RentalWire[]>('/api/reports/rentals', { params });
    return resolvePagedResult(response.data.map(normalizeRental), response.headers, params);
  },

  async getHealth(): Promise<HealthStatus> {
    const response = await api.get<HealthStatus>('/api/system/health');
    return response.data;
  },

  errorMessage: toErrorMessage,
};
