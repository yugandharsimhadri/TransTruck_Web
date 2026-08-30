// Mirrors TransTrack.Core entities/DTOs. Kept as plain interfaces (not
// generated) since the API is small and stable enough that hand-matching
// beats wiring up a codegen step for now.

export type UserRole = "Owner" | "CoOwner" | "Accountant";
export type VehicleOwnership = "Own" | "Other";
export type ApprovalStatus = "Pending" | "Approved" | "Rejected";
export type TripStatus = "Open" | "Closed";
export type PaymentMode = "Cash" | "Bank" | "Upi" | "Cheque";
// Not the same "Advance" as DriverLedgerEntryType.AdvanceGiven below — that
// one is a driver's wage advance; this is the party's money against freight.
export type ReceiptType = "Advance" | "Payment";
export type DriverLedgerEntryType = "SalaryPaid" | "AdvanceGiven" | "Deduction";

export interface State {
  id: string;
  name: string;
}

export interface City {
  id: string;
  name: string;
  stateId: string;
  state?: State;
  display: string;
}

export interface Owner {
  id: string;
  name: string;
  phone?: string | null;
  address?: string | null;
  bankAccountNo?: string | null;
  ifsc?: string | null;
  remarks?: string | null;
}

export interface Party {
  id: string;
  name: string;
  phone?: string | null;
  address?: string | null;
  gstin?: string | null;
}

export interface Driver {
  id: string;
  employeeCode: string;
  name: string;
  phone: string;
  salary: number;
  joiningDate?: string | null;
  isActive: boolean;
  display: string;
}

export interface Vehicle {
  id: string;
  regNo: string;
  ownership: VehicleOwnership;
  ownerId?: string | null;
  owner?: Owner | null;
  vehicleType?: string | null;
  capacity?: number | null;
  permitUpto?: string | null;
  nationalPermitUpto?: string | null;
  insuranceUpto?: string | null;
  fitnessUpto?: string | null;
  pollutionUpto?: string | null;
  isActive: boolean;
  display: string;
}

/** The one document held against a vehicle. The API returns null when none
 *  has been uploaded — a normal state, not an error. */
/** Document types, split by what they can be filed against. Mirrors
 *  DocumentTypes.For() on the server, which rejects a mismatch. */
export const VEHICLE_DOCUMENT_TYPES = [
  "Permit",
  "NationalPermit",
  "Insurance",
  "Fitness",
  "Pollution",
  "Others",
] as const;

export const DRIVER_DOCUMENT_TYPES = ["AadhaarCard", "DriverLicence", "Others"] as const;

export type DocumentType =
  | (typeof VEHICLE_DOCUMENT_TYPES)[number]
  | (typeof DRIVER_DOCUMENT_TYPES)[number];

/** How a type reads on screen. The server sends its own label too; this is
 *  for the picker, which lists types before anything has been uploaded. */
export const DOCUMENT_TYPE_LABELS: Record<DocumentType, string> = {
  Permit: "Permit",
  NationalPermit: "National permit",
  Insurance: "Insurance",
  Fitness: "Fitness",
  Pollution: "Pollution",
  AadhaarCard: "Aadhaar card",
  DriverLicence: "Driving licence",
  Others: "Others",
};

export interface DocumentInfo {
  id: string;
  documentType: DocumentType;
  documentTypeLabel: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedOn: string;
}

export interface ExpenseCategory {
  id: string;
  name: string;
}

export interface TripExpense {
  id: string;
  tripId: string;
  date: string;
  expenseCategoryId: string;
  expenseCategory?: ExpenseCategory;
  amount: number;
  remarks?: string | null;
}

export interface TripTransaction {
  id: string;
  tripId: string;
  trip?: Trip;
  date: string;
  amount: number;
  paymentMode: PaymentMode;
  receiptType: ReceiptType;
  remarks?: string | null;
  enteredByUserId?: string | null;
  approvalStatus: ApprovalStatus;
  approvedByUserId?: string | null;
  approvedOn?: string | null;
  approvalRemarks?: string | null;
}

export interface Trip {
  id: string;
  tripNo: string;
  date: string;
  vehicleId: string;
  vehicle?: Vehicle;
  driverId: string;
  driver?: Driver;
  partyId: string;
  party?: Party;
  fromCityId: string;
  fromCity?: City;
  fromAddress?: string | null;
  toCityId: string;
  toCity?: City;
  toAddress?: string | null;
  consignorName: string;
  consignorAddress?: string | null;
  consigneeName: string;
  consigneeAddress?: string | null;
  weight?: number | null;
  rate?: number | null;
  amount: number;
  startReading: number;
  endReading?: number | null;
  commissionAmount?: number | null;
  remarks?: string | null;
  lrNo?: string | null;
  wayBillNo?: string | null;
  billNo?: string | null;
  status: TripStatus;
  closedOn?: string | null;
  expenses: TripExpense[];
  transactions: TripTransaction[];
  totalExpenses: number;
  totalApprovedReceived: number;
  totalAdvanceReceived: number;
  totalPaymentReceived: number;
  balanceReceivable: number;
  netAfterExpenses: number;
}

/**
 * The trips list row. Deliberately flat and much smaller than `Trip`: the
 * list screen shows nine short fields, so the API sends exactly those rather
 * than the whole graph (vehicle, driver, party, cities, every expense and
 * amount) it sends for the detail screen.
 */
export type TripListSort = "DateDesc" | "DateAsc" | "BalanceDesc" | "AmountDesc";

/** One page of the trips list. `total` counts everything the current filters
 *  match, not what came back in `items` — it is what lets the list say how
 *  much is still behind the page you are looking at. */
export interface TripListPage {
  items: TripListItem[];
  total: number;
}

export interface TripListItem {
  id: string;
  tripNo: string;
  date: string;
  vehicleRegNo: string;
  driverName: string;
  partyName: string;
  fromCity: string;
  toCity: string;
  amount: number;
  totalExpenses: number;
  totalApprovedReceived: number;
  balanceReceivable: number;
  status: TripStatus;
}

export interface Company {
  id: string;
  companyName: string;
  tagline?: string | null;
  addressLine?: string | null;
  phone?: string | null;
  cell?: string | null;
  pan?: string | null;
  gstin?: string | null;
  jurisdictionNote?: string | null;
  logoBase64?: string | null;
  logoFileName?: string | null;
  bankAccountNo?: string | null;
  ifsc?: string | null;
  showBankDetailsOnBill?: boolean;
  // Written by the API on every row; the settings form uses them to tell a
  // freshly-fetched company apart from the copy it already hydrated from.
  createdAt?: string;
  updatedAt?: string | null;
}

export interface DashboardSummary {
  tripsThisMonth: number;
  revenueThisMonth: number;
  expensesThisMonth: number;
  pendingApprovals: number;
  outstandingBalance: number;
  vehiclesExpiringSoon: number;
}

export interface MonthlyFigure {
  month: string;
  revenue: number;
  expense: number;
}

export interface CategoryFigure {
  category: string;
  amount: number;
}

export interface ComplianceAlert {
  vehicleRegNo: string;
  documentName: string;
  upto: string;
  isExpired: boolean;
}

// ── Auth ────────────────────────────────────────────────────────────────

export type LoginStatus = "Success" | "MustChangePassword" | "Recovery" | "Failed" | "LicenseExpired";

export interface LoginResponse {
  status: LoginStatus;
  token: string | null;
  message: string | null;
  mustChangePassword: boolean;
  displayName: string | null;
  role: UserRole | null;
}

export interface MeResponse {
  userId: string;
  username: string;
  displayName: string;
  role: UserRole;
  companyName: string;
}

// ── Enterprise ──────────────────────────────────────────────────────────

export interface OnboardResult {
  companyId: string;
  companyName: string;
  ownerUsername: string;
  temporaryPassword: string;
  licenseExpiresOn: string;
}

export interface CompanySummary {
  id: string;
  companyName: string;
  ownerName: string;
  ownerPhone: string;
  isActive: boolean;
  licenseExpiresOn: string;
  isLicenseValid: boolean;
  createdAt: string;
}

export interface CompanyUserSummary {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
}

// ── Users (a company's own team) ───────────────────────────────────────

export interface UserSummary {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  lastLoginOn?: string | null;
}

// ── Maintenance ─────────────────────────────────────────────────────────

export interface MaintenanceCategory {
  id: string;
  name: string;
}

export interface VehicleMaintenance {
  id: string;
  vehicleId: string;
  vehicle?: Vehicle;
  date: string;
  maintenanceCategoryId: string;
  maintenanceCategory?: MaintenanceCategory;
  odometerReading?: number | null;
  vendorName?: string | null;
  amount: number;
  nextDueDate?: string | null;
  nextDueOdometer?: number | null;
  remarks?: string | null;
}

// ── Driver ledger ───────────────────────────────────────────────────────

export interface DriverLedgerEntry {
  id: string;
  driverId: string;
  driver?: Driver;
  date: string;
  type: DriverLedgerEntryType;
  amount: number;
  forMonth?: string | null;
  remarks?: string | null;
}

// ── Audit trail ─────────────────────────────────────────────────────────

export type AuditAction = "Created" | "Updated" | "Deleted";

/** One field that moved, as stored in AuditEntry.changes (JSON). */
export interface AuditFieldChange {
  field: string;
  from: string | null;
  to: string | null;
}

export interface AuditEntry {
  id: string;
  entityType: string;
  entityId: string;
  tripId?: string | null;
  action: AuditAction;
  summary: string;
  /** JSON-encoded AuditFieldChange[]; null for a creation. */
  changes?: string | null;
  changedBy: string;
  changedOn: string;
}

// ── Reports ─────────────────────────────────────────────────────────────

export interface LedgerRow {
  date: string;
  tripNo: string;
  vehicleRegNo: string;
  driverName: string;
  kind: "Income" | "Expense";
  detail: string;
  amount: number;
  countsInCompanyAccounts: boolean;
  /** Null for an Expense row — the Advance/Payment split only applies to
   *  money coming in. */
  receiptType: ReceiptType | null;
}

export interface PartyTripRow {
  serialNo: number;
  date: string;
  vehicleRegNo: string;
  fromCity: string;
  toCity: string;
  weight?: number | null;
  rate?: number | null;
  amount: number;
}

export interface PartyReport {
  partyName: string;
  periodLabel: string;
  rows: PartyTripRow[];
  total: number;
}

export interface VehicleMonthlySaving {
  vehicleRegNo: string;
  monthLabel: string;
  trips: number;
  revenue: number;
  tripExpenses: number;
  maintenanceCost: number;
  saving: number;
  savingPerTrip: number;
}
