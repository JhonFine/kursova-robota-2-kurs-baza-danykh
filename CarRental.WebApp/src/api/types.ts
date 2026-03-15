export type UserRole = 'Admin' | 'Manager' | 'User';

export type RentalStatus = 'Booked' | 'Active' | 'Closed' | 'Canceled';

export type PaymentMethod = 'Cash' | 'Card';

export type PaymentDirection = 'Incoming' | 'Refund';

export type DamageStatus = 'Open' | 'Charged' | 'Resolved';

export interface Employee {
  id: number;
  fullName: string;
  login: string;
  role: UserRole;
  isActive: boolean;
  lastLoginUtc?: string | null;
  lockoutUntilUtc?: string | null;
}

export interface AuthTokenResponse {
  accessToken: string;
  expiresAtUtc: string;
  employee: Employee;
}

export interface Client {
  id: number;
  fullName: string;
  passportData: string;
  driverLicense: string;
  phone: string;
  blacklisted: boolean;
}

export interface ClientProfile extends Client {
  isComplete: boolean;
}

export interface Vehicle {
  id: number;
  make: string;
  model: string;
  engineDisplay: string;
  fuelType: string;
  transmissionType: string;
  doorsCount: number;
  cargoCapacityDisplay: string;
  consumptionDisplay: string;
  hasAirConditioning: boolean;
  licensePlate: string;
  mileage: number;
  dailyRate: number;
  isAvailable: boolean;
  serviceIntervalKm: number;
  photoPath?: string | null;
}

export interface Rental {
  id: number;
  contractNumber: string;
  clientId: number;
  clientName: string;
  vehicleId: number;
  vehicleName: string;
  employeeId: number;
  employeeName: string;
  startDate: string;
  endDate: string;
  pickupLocation: string;
  returnLocation: string;
  startMileage: number;
  endMileage?: number | null;
  status: RentalStatus;
  totalAmount: number;
  overageFee: number;
  paidAmount: number;
  balance: number;
  createdAtUtc: string;
  closedAtUtc?: string | null;
  canceledAtUtc?: string | null;
  cancellationReason?: string | null;
  pickupInspectionCompletedAtUtc?: string | null;
  pickupFuelPercent?: number | null;
  pickupInspectionNotes?: string | null;
  returnInspectionCompletedAtUtc?: string | null;
  returnFuelPercent?: number | null;
  returnInspectionNotes?: string | null;
}

export interface Payment {
  id: number;
  rentalId: number;
  employeeId: number;
  amount: number;
  method: PaymentMethod;
  direction: PaymentDirection;
  createdAtUtc: string;
  notes: string;
}

export interface RentalBalance {
  rentalId: number;
  balance: number;
}

export interface RentalAvailabilitySlot {
  vehicleId: number;
  startDate: string;
  endDate: string;
  status: RentalStatus;
}

export interface Damage {
  id: number;
  vehicleId: number;
  vehicleName: string;
  rentalId?: number | null;
  contractNumber?: string | null;
  description: string;
  dateReported: string;
  repairCost: number;
  photoPath?: string | null;
  actNumber: string;
  chargedAmount: number;
  isChargedToClient: boolean;
  status: DamageStatus;
}

export interface MaintenanceRecord {
  id: number;
  vehicleId: number;
  vehicleName: string;
  serviceDate: string;
  mileageAtService: number;
  description: string;
  cost: number;
  nextServiceMileage: number;
}

export interface MaintenanceDue {
  vehicleId: number;
  vehicle: string;
  currentMileage: number;
  nextServiceMileage: number;
  overdueByKm: number;
}

export interface ReportSummary {
  totalRentals: number;
  activeRentals: number;
  totalRevenue: number;
  totalDamageCost: number;
}

export interface HealthStatus {
  status: string;
  database: string;
  utcNow?: string;
}

export interface PaginationParams {
  page: number;
  pageSize: number;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}
