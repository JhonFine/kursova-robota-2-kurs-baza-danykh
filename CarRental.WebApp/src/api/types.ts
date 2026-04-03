export type UserRole = 'Admin' | 'Manager' | 'User';

export type RentalStatus = 'Booked' | 'Active' | 'Closed' | 'Canceled';

export type PaymentMethod = 'Cash' | 'Card';

export type PaymentDirection = 'Incoming' | 'Refund';

export type PaymentStatus = 'Pending' | 'Completed' | 'Canceled' | 'Refunded';

export type DamageStatus = 'Open' | 'Charged' | 'Resolved';

export interface Employee {
  id: number;
  fullName: string;
  roleId: UserRole;
  login: string;
  isActive: boolean;
  lastLoginUtc?: string | null;
  lockoutUntilUtc?: string | null;
  createdAtUtc?: string;
  updatedAtUtc?: string;
}

export interface AuthenticatedUser {
  accountId: number;
  employeeId: number | null;
  clientId: number | null;
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
  user: AuthenticatedUser;
  employee: Employee | null;
}

export interface Client {
  id: number;
  fullName: string;
  passportData: string;
  passportExpirationDate?: string | null;
  passportPhotoPath?: string | null;
  driverLicense: string;
  driverLicenseExpirationDate?: string | null;
  driverLicensePhotoPath?: string | null;
  phone: string;
  isBlacklisted: boolean;
  blacklistReason?: string | null;
  blacklistedAtUtc?: string | null;
  blacklistedByEmployeeId?: number | null;
  accountId?: number | null;
}

export interface ClientProfile extends Client {
  isComplete: boolean;
}

export interface MediaAsset {
  id: number;
  storedPath: string;
  sortOrder: number;
  isPrimary?: boolean;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
}

export interface Vehicle {
  id: number;
  makeId: number;
  makeName: string;
  modelId: number;
  modelName: string;
  powertrainCapacityValue: number;
  powertrainCapacityUnit: string;
  fuelTypeCode: string;
  transmissionTypeCode: string;
  vehicleStatusCode: string;
  doorsCount: number;
  cargoCapacityValue: number;
  cargoCapacityUnit: string;
  consumptionValue: number;
  consumptionUnit: string;
  hasAirConditioning: boolean;
  licensePlate: string;
  mileage: number;
  dailyRate: number;
  isAvailable: boolean;
  serviceIntervalKm: number;
  photos: MediaAsset[];
}

export interface VehicleUpsertPayload {
  makeId: number;
  modelId: number;
  powertrainCapacityValue: number;
  powertrainCapacityUnit: string;
  fuelTypeCode: string;
  transmissionTypeCode: string;
  doorsCount: number;
  cargoCapacityValue: number;
  cargoCapacityUnit: string;
  consumptionValue: number;
  consumptionUnit: string;
  hasAirConditioning: boolean;
  licensePlate: string;
  mileage: number;
  dailyRate: number;
  serviceIntervalKm: number;
  photoPath?: string | null;
}

export interface VehicleMake {
  id: number;
  name: string;
}

export interface VehicleModel {
  id: number;
  makeId: number;
  name: string;
}

export interface Rental {
  id: number;
  contractNumber: string;
  clientId: number;
  clientName: string;
  vehicleId: number;
  vehicleName: string;
  createdByEmployeeId: number | null;
  createdByEmployeeName: string | null;
  closedByEmployeeId: number | null;
  closedByEmployeeName: string | null;
  canceledByEmployeeId: number | null;
  canceledByEmployeeName: string | null;
  startDate: string;
  endDate: string;
  pickupLocation: string;
  returnLocation: string;
  startMileage: number;
  endMileage?: number | null;
  statusId: RentalStatus;
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
  pickupInspectionPerformedByEmployeeId?: number | null;
  pickupInspectionPerformedByEmployeeName?: string | null;
  returnInspectionCompletedAtUtc?: string | null;
  returnFuelPercent?: number | null;
  returnInspectionNotes?: string | null;
  returnInspectionPerformedByEmployeeId?: number | null;
  returnInspectionPerformedByEmployeeName?: string | null;
}

export interface Payment {
  id: number;
  rentalId: number;
  recordedByEmployeeId: number | null;
  recordedByEmployeeName: string | null;
  amount: number;
  methodId: PaymentMethod;
  directionId: PaymentDirection;
  statusId: PaymentStatus;
  externalTransactionId?: string | null;
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
  statusId: RentalStatus;
}

export interface Damage {
  id: number;
  vehicleId: number;
  vehicleName: string;
  rentalId?: number | null;
  contractNumber?: string | null;
  reportedByEmployeeId: number;
  reportedByEmployeeName: string;
  description: string;
  dateReported: string;
  repairCost: number;
  photos: MediaAsset[];
  damageActNumber: string;
  chargedAmount: number;
  statusId: DamageStatus;
}

export interface MaintenanceRecord {
  id: number;
  vehicleId: number;
  vehicleName: string;
  performedByEmployeeId?: number | null;
  performedByEmployeeName?: string | null;
  serviceDate: string;
  mileageAtService: number;
  description: string;
  cost: number;
  nextServiceMileage?: number | null;
  nextServiceDate?: string | null;
  maintenanceTypeCode: string;
  serviceProviderName?: string | null;
}

export interface MaintenanceDue {
  vehicleId: number;
  vehicle: string;
  currentMileage: number;
  nextServiceMileage?: number | null;
  nextServiceDate?: string | null;
  overdueByKm: number;
  overdueByDays: number;
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
