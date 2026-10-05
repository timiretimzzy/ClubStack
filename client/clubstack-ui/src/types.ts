export interface CurrentUser {
  userName: string;
  displayName: string;
  permissions: string[];
  roles: string[];
}

export interface Person {
  id: string;
  legalName: string;
  preferredName: string;
  memberId?: string | null;
  memberNumber?: string | null;
  idNumber?: string | null;
  dateOfBirth?: string | null;
  email?: string | null;
  phone?: string | null;
  isActive: boolean;
}

export interface PersonCreated {
  person: Person;
  duplicateWarnings: string[];
}

export interface PersonSearch {
  items: Person[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Member {
  id: string;
  personId: string;
  legalName: string;
  preferredName: string;
  memberNumber: string;
  status: string;
  phone?: string | null;
  email?: string | null;
}

export interface MemberSearch {
  items: Member[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Membership {
  id: string;
  memberId: string;
  categoryId: string;
  categoryName: string;
  status: string;
  startDateUtc?: string | null;
  endDateUtc?: string | null;
  renewalDateUtc?: string | null;
  activatedAtUtc?: string | null;
  createdAtUtc: string;
}

export interface MemberDetail {
  id: string;
  personId: string;
  legalName: string;
  preferredName: string;
  memberNumber: string;
  status: string;
  person: Person;
  memberships: Membership[];
  dependants: MembershipDependant[];
}

export interface MembershipDependant {
  id: string;
  memberId: string;
  dependantPersonId: string;
  dependantName: string;
  relationType: string;
  notes: string;
}

export interface MembershipStatusHistory {
  id: string;
  membershipId: string;
  previousState: string;
  newState: string;
  changedByUserId: string;
  reason: string;
  changedAtUtc: string;
}

export interface MembershipCategory {
  id: string;
  code: string;
  name: string;
  description: string;
  eligibilityRules: string;
  renewalPeriodMonths: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface MembershipFee {
  id: string;
  categoryId: string;
  feeType: string;
  description: string;
  amount: number;
  currencyCode: string;
  isProrationEnabled: boolean;
  isActive: boolean;
  updatedAtUtc: string;
}

export interface MembershipApplication {
  id: string;
  personId: string;
  personName: string;
  categoryId: string;
  categoryName: string;
  status: string;
  sponsorName?: string | null;
  referrerName?: string | null;
  notes: string;
  approvedByUserId?: string | null;
  requiredDocuments: string;
  submittedAtUtc?: string | null;
  reviewedAtUtc?: string | null;
  approvedAtUtc?: string | null;
  rejectionReason?: string | null;
  memberId?: string | null;
  memberNumber?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface MembershipApplicationSearch {
  items: MembershipApplication[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Household {
  id: string;
  name: string;
  reference: string;
  primaryMemberId?: string | null;
  primaryMemberName?: string | null;
  primaryMemberNumber?: string | null;
  isActive: boolean;
  members: HouseholdMember[];
}

export interface HouseholdMember {
  householdId: string;
  personId: string;
  personName: string;
  memberNumber?: string | null;
  role: string;
}

export interface FeeAssessment {
  id: string;
  membershipId: string;
  membershipNumber: string;
  memberName: string;
  feeType: string;
  description: string;
  amount: number;
  currencyCode: string;
  isProrated: boolean;
  assessmentDateUtc: string;
  dueDateUtc?: string | null;
  source: string;
  status: string;
  createdAtUtc: string;
}

export interface FeeAssessmentSearch {
  items: FeeAssessment[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface Credential {
  id: string;
  credentialNumber: string;
  memberId?: string | null;
  memberNumber?: string | null;
  memberName?: string | null;
  credentialType: string;
  credentialValue: string;
  status: string;
  activatedAtUtc?: string | null;
  suspendedAtUtc?: string | null;
  expiresAtUtc?: string | null;
  replacedByCredentialId?: string | null;
  replacedByCredentialNumber?: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CredentialSearch {
  items: Credential[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CredentialHistoryEntry {
  id: string;
  credentialId: string;
  previousState: string;
  newState: string;
  changedByUserId: string;
  reason: string;
  changedAtUtc: string;
}

export interface AccountBalance {
  currencyCode: string;
  amountDue: number;
  creditsBalance: number;
  unallocatedPayments: number;
  overdueBalance: number;
  asOfDateUtc: string;
}

export interface MemberAccount {
  id: string;
  memberId: string;
  memberNumber: string;
  memberName: string;
  accountNumber: string;
  status: string;
  currencyCode: string;
  openedAtUtc: string;
  closedAtUtc?: string | null;
  balance: AccountBalance;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface AccountTransaction {
  id: string;
  accountId: string;
  transactionType: string;
  status: string;
  currencyCode: string;
  amount: number;
  signedAmount: number;
  isDebit: boolean;
  description: string;
  reference: string;
  externalReference?: string | null;
  reason?: string | null;
  sourceType?: string | null;
  sourceId?: string | null;
  reverseOfTransactionId?: string | null;
  paymentMethodId?: string | null;
  paymentMethodCode?: string | null;
  paymentState?: string | null;
  transactionDateUtc: string;
  businessDateUtc: string;
  createdAtUtc: string;
}

export interface AccountTransactionSearch {
  items: AccountTransaction[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AgingBucket {
  label: string;
  daysFrom: number;
  daysTo?: number | null;
  outstandingAmount: number;
  documentCount?: number;
}

export interface AgingItem {
  chargeTransactionId: string;
  description: string;
  businessDateUtc: string;
  daysOutstanding: number;
  outstandingAmount: number;
}

export interface AgingReport {
  asOfDateUtc: string;
  currencyCode: string;
  daysOutstandingThreshold: number;
  totalOutstanding: number;
  openItemCount: number;
  buckets: AgingBucket[];
  items: AgingItem[];
}

export interface Statement {
  accountId: string;
  accountNumber: string;
  memberNumber: string;
  memberName: string;
  currencyCode: string;
  statementNumber: string;
  fromDateUtc: string;
  toDateUtc: string;
  openingBalance: number;
  closingBalance: number;
  currentBalance: number;
  generatedAtUtc: string;
  generatedByUserId: string;
  transactions: AccountTransaction[];
}

export interface Product {
  id: string;
  sku: string;
  name: string;
  categoryId: string;
  categoryName: string;
  unitOfMeasure: string;
  barcode?: string | null;
  stockManaged: boolean;
  cost: number;
  currencyCode: string;
  reorderLevel?: number | null;
  supplierId?: string | null;
  supplierName?: string | null;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface ProductSearch {
  items: Product[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ProductPrice {
  id: string;
  productId: string;
  priceType: string;
  currencyCode: string;
  amount: number;
  effectiveFromUtc: string;
  createdAtUtc: string;
}

export interface ProductCategory {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface InventoryLocation {
  id: string;
  code: string;
  name: string;
  type: string;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface Supplier {
  id: string;
  supplierCode: string;
  name: string;
  contactPerson?: string | null;
  email?: string | null;
  phone?: string | null;
  paymentTerms?: string | null;
  taxId?: string | null;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SupplierSearch {
  items: Supplier[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StockBalance {
  productId: string;
  productSku: string;
  productName: string;
  locationId: string;
  locationCode: string;
  locationName: string;
  onHand: number;
  reserved: number;
  available: number;
  reorderLevel?: number | null;
  status: string;
  updatedAtUtc: string;
}

export interface StockSearch {
  items: StockBalance[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StockMovement {
  id: string;
  reference: string;
  productId: string;
  productSku: string;
  productName: string;
  locationId: string;
  locationCode: string;
  quantity: number;
  movementType: string;
  unitCost: number;
  currencyCode: string;
  businessDateUtc: string;
  sourceType?: string | null;
  sourceId?: string | null;
  reason?: string | null;
  userId: string;
  createdAtUtc: string;
}

export interface MovementSearch {
  items: StockMovement[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StockTransferLine {
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  unitCost: number;
}

export interface StockTransfer {
  id: string;
  transferNumber: string;
  fromLocationId: string;
  fromLocationCode: string;
  toLocationId: string;
  toLocationCode: string;
  status: string;
  createdByUserId: string;
  completedAtUtc?: string | null;
  cancelledAtUtc?: string | null;
  createdAtUtc: string;
  lines: StockTransferLine[];
}

export interface StockTransferSearch {
  items: StockTransfer[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StockCountLine {
  productId: string;
  productSku: string;
  productName: string;
  expectedQuantity: number;
  countedQuantity?: number | null;
  variance: number;
}

export interface StockCount {
  id: string;
  countNumber: string;
  locationId: string;
  locationCode: string;
  status: string;
  createdByUserId: string;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  approvedAtUtc?: string | null;
  createdAtUtc: string;
  lines: StockCountLine[];
}

export interface StockCountSearch {
  items: StockCount[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface StockAdjustmentLine {
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  unitCost: number;
}

export interface StockAdjustment {
  id: string;
  adjustmentNumber: string;
  adjustmentType: string;
  locationId: string;
  locationCode: string;
  reason: string;
  businessDateUtc: string;
  createdByUserId: string;
  postedAtUtc: string;
  createdAtUtc: string;
  lines: StockAdjustmentLine[];
}

export interface StockAdjustmentSearch {
  items: StockAdjustment[];
  totalCount: number;
  page: number;
  pageSize: number;
}
// ---- D5 POS and Cashiering ----

export interface PosProduct {
  id: string;
  sku: string;
  name: string;
  barcode: string | null;
  unitOfMeasure: string;
  stockManaged: boolean;
  unitPrice: number;
  priceSourceType: string;
  isOverride: boolean;
  availableQuantity: number | null;
  currencyCode: string;
  taxTreatment: string;
  taxRateName: string | null;
  taxRate: number | null;
}

export interface PosProductSearch {
  items: PosProduct[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PosMember {
  memberId: string;
  memberNumber: string;
  memberName: string;
  status: string;
  credentialId: string | null;
  credentialNumber: string | null;
  credentialStatus: string | null;
}

export interface PosMemberSearch {
  items: PosMember[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface PosOutlet {
  id: string;
  code: string;
  name: string;
  notes: string | null;
  status: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PosRegister {
  id: string;
  posOutletId: string;
  posOutletCode: string;
  posOutletName: string;
  code: string;
  name: string;
  inventoryLocationId: string;
  inventoryLocationCode: string;
  inventoryLocationName: string;
  status: string;
  currentSessionId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface PosConfiguration {
  outlets: PosOutlet[];
  registers: PosRegister[];
}

export interface SaleLine {
  id: string;
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  originalUnitPrice: number;
  unitPrice: number;
  discountAmount: number;
  taxableBase: number;
  taxAmount: number;
  lineTotal: number;
  taxTreatment: string;
  taxRateName: string | null;
  taxRate: number | null;
  stockManaged: boolean;
}

export interface SalePayment {
  id: string;
  paymentMethodCode: string;
  paymentMethodName: string;
  kind: string;
  amount: number;
  externalReference: string | null;
  accountTransactionId: string | null;
  accountTransactionReference: string | null;
}

export interface SaleDiscount {
  id: string;
  level: string;
  lineId: string | null;
  discountType: string;
  value: number;
  reason: string | null;
  authorizedByUserId: string;
  appliedAtUtc: string;
}

export interface Receipt {
  id: string;
  receiptNumber: string;
  saleId: string;
  saleNumber: string;
  status: string;
  registerId: string;
  registerCode: string;
  posOutletId: string;
  posOutletCode: string;
  cashierUserId: string;
  memberId: string | null;
  memberNumber: string | null;
  memberName: string | null;
  currencyCode: string;
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  grandTotal: number;
  businessDateUtc: string;
  generatedAtUtc: string;
  lines: SaleLine[];
  payments: SalePayment[];
}

export interface Sale {
  id: string;
  saleNumber: string;
  status: string;
  registerId: string;
  registerCode: string;
  posOutletId: string;
  posOutletCode: string;
  locationId: string | null;
  locationCode: string | null;
  cashierUserId: string;
  memberId: string | null;
  memberNumber: string | null;
  memberName: string | null;
  currencyCode: string;
  subtotal: number;
  discountTotal: number;
  taxTotal: number;
  grandTotal: number;
  note: string | null;
  businessDateUtc: string;
  completedAtUtc: string | null;
  voidedAtUtc: string | null;
  voidReason: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  lines: SaleLine[];
  payments: SalePayment[];
  discounts: SaleDiscount[];
  receipt: Receipt | null;
}

export interface SaleSearch {
  items: Sale[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SaleRefundLine {
  productId: string;
  productSku: string;
  productName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface SaleRefundPayment {
  paymentMethodCode: string;
  paymentMethodName: string;
  kind: string;
  amount: number;
  externalReference: string | null;
}

export interface SaleRefund {
  id: string;
  refundNumber: string;
  saleId: string;
  saleNumber: string;
  status: string;
  currencyCode: string;
  amount: number;
  reason: string;
  refundedByUserId: string;
  refundedAtUtc: string;
  createdAtUtc: string;
  lines: SaleRefundLine[];
  payments: SaleRefundPayment[];
}

export interface CashMovement {
  id: string;
  sessionId: string;
  movementType: string;
  amount: number;
  signedAmount: number;
  reason: string | null;
  createdByUserId: string;
  createdAtUtc: string;
}

export interface CashierSession {
  id: string;
  sessionNumber: string;
  registerId: string;
  registerCode: string;
  cashierUserId: string;
  status: string;
  openingFloat: number;
  expectedCash: number;
  declaredCash: number | null;
  variance: number | null;
  varianceApproved: boolean;
  note: string | null;
  openedAtUtc: string;
  startedAtUtc: string | null;
  closingStartedAtUtc: string | null;
  closedAtUtc: string | null;
  reconciledAtUtc: string | null;
  reconciledByUserId: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  movements: CashMovement[];
}

export interface CashierSessionSearch {
  items: CashierSession[];
  totalCount: number;
  page: number;
  pageSize: number;
}
// ---- D5.5 ZIMRA fiscalisation ----

export type FiscalDayState = 'Opened' | 'Closed' | 'Unknown';

export interface FiscalDeviceStatus {
  available: boolean;
  deviceId: string | null;
  serialNumber: string | null;
  companyName: string | null;
  taxpayerName: string | null;
  taxpayerTaxNumber: string | null;
  vatNumber: string | null;
  licenseActive: boolean;
  licenseStatus: string | null;
  licenseStart: string | null;
  licenseExpiry: string | null;
  certificateCount: number;
  fiscalDayState: string;
  fiscalDayStatus: string | null;
  lastFiscalDayNo: string | null;
  lastReceiptGlobalNo: string | null;
  zimraReachable: boolean;
  zimraSource: string | null;
  lastError: string | null;
}

export interface FiscalDeviceConfiguration {
  deviceId: string | null;
  serialNumber: string | null;
  companyName: string | null;
  branchName: string | null;
  city: string | null;
  environment: string | null;
  standardRatedTaxId: number;
  zeroRatedTaxId: number;
  exemptTaxId: number;
  currencyCode: string | null;
  serverUrl: string | null;
  certificateFolder: string | null;
}

export interface FiscalDayStatus {
  state: string;
  fiscalDayStatus: string | null;
  lastFiscalDayNo: string | null;
  lastReceiptGlobalNo: string | null;
  zimraReachable: boolean;
  zimraSource: string | null;
}

export interface FiscalDayAction {
  success: boolean;
  code: string;
  message: string;
  fiscalDayNo: string | null;
  operationId: string | null;
  status: FiscalDayStatus | null;
}

export interface FiscalDeviceAction {
  success: boolean;
  code: string;
  message: string;
  status: FiscalDeviceStatus | null;
}

export interface FiscalTender {
  paymentMethodCode: string;
  paymentMethodName: string;
  kind: string;
  amount: number;
  externalReference: string | null;
}

export interface Fiscalisation {
  id: string;
  saleId: string;
  saleNumber: string;
  refundId: string | null;
  refundNumber: string | null;
  invoiceNumber: string;
  documentKind: string;
  status: string;
  deviceId: string | null;
  deviceSerialNumber: string | null;
  fiscalDayNo: string | null;
  receiptCounter: string | null;
  receiptGlobalNo: string | null;
  receiptId: string | null;
  operationId: string | null;
  deviceSignature: string | null;
  serverSignature: string | null;
  qrUrl: string | null;
  originalInvoiceNumber: string | null;
  paymentMapping: string | null;
  tenders: FiscalTender[];
  code: string | null;
  message: string | null;
  validationWarnings: string[];
  pendingSync: boolean;
  attemptCount: number;
  submittedAtUtc: string | null;
  fiscalisedAtUtc: string | null;
  lastAttemptAtUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface FiscalisationSearch {
  items: Fiscalisation[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FiscalisationRetry {
  success: boolean;
  code: string;
  message: string;
  fiscalisation: Fiscalisation | null;
}

export interface FiscalisationSubmitPending {
  stillPending: number;
}

export type FinancialAccountType = 0 | 1 | 2 | 3 | 4;
export type NormalBalance = 0 | 1;
export type FinancialAccountStatus = 0 | 1;
export type FinancialCashKind = 0 | 1 | 2;
export type JournalStatus = 0 | 1 | 2;
export type JournalDirection = 0 | 1;
export type FinancialPeriodStatus = 0 | 1;
export type ReceivableStatus = 0 | 1 | 2 | 3;
export type PayableStatus = 0 | 1 | 2 | 3;
export type PaymentDirection = 0 | 1;
export type FinancialReconciliationStatus = 0 | 1 | 2;
export type FinancialPostingStrategy = 0 | 1;

export interface FinancialAccount {
  id: string;
  reference: string;
  code: string;
  name: string;
  accountType: FinancialAccountType;
  normalBalance: NormalBalance;
  status: FinancialAccountStatus;
  parentAccountId: string | null;
  description: string | null;
  isSystem: boolean;
  cashKind: FinancialCashKind;
  isPostingAllowed: boolean;
  currencyCode: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface FinancialAccountSearch {
  items: FinancialAccount[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface FinancialPostingRuleMapping {
  id: string;
  ruleId: string;
  ruleCode: string;
  sequence: number;
  direction: JournalDirection;
  financialAccountId: string;
  financialAccountCode: string;
  financialAccountName: string;
  selector: string | null;
}

export interface FinancialPostingRule {
  id: string;
  code: string;
  name: string;
  sourceSection: string;
  strategy: FinancialPostingStrategy;
  description: string;
  mappings: FinancialPostingRuleMapping[];
}

export interface JournalLine {
  id: string;
  lineNumber: number;
  financialAccountId: string;
  financialAccountCode: string;
  financialAccountName: string;
  direction: JournalDirection;
  amount: number;
  currencyCode: string;
  baseAmount: number;
  description: string | null;
  sourceLineId: string | null;
}

export interface Journal {
  id: string;
  reference: string;
  status: JournalStatus;
  description: string;
  financialPeriodId: string;
  financialPeriodReference: string;
  transactionDate: string;
  businessDate: string;
  transactionCurrencyCode: string;
  baseCurrencyCode: string;
  exchangeRate: number;
  rateDateUtc: string;
  sourceType: string;
  sourceId: string;
  sourceReference: string | null;
  reversalOfJournalId: string | null;
  reversalOfJournalReference: string | null;
  reversedByJournalId: string | null;
  receivableId: string | null;
  payableId: string | null;
  totalDebit: number;
  totalCredit: number;
  postedAtUtc: string | null;
  postedByUserId: string | null;
  cancelledReason: string | null;
  createdByUserId: string;
  correlationId: string;
  createdAtUtc: string;
  lines: JournalLine[];
}

export interface JournalSearch {
  items: Journal[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface JournalValidation {
  isValid: boolean;
  errors: string[];
  totalDebit: number;
  totalCredit: number;
  lineCount: number;
}

export interface FinancialPeriod {
  id: string;
  reference: string;
  year: number;
  month: number;
  name: string;
  startUtc: string;
  endUtc: string;
  status: FinancialPeriodStatus;
  openedAtUtc: string;
  openedByUserId: string;
  closedAtUtc: string | null;
  closedByUserId: string | null;
  reopenedAtUtc: string | null;
  reopenedByUserId: string | null;
  reopenReason: string | null;
  postedJournalCount: number;
  postedDebitTotal: number;
  postedCreditTotal: number;
}

export interface FinancialPeriodSearch {
  items: FinancialPeriod[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReceivableAllocation {
  id: string;
  paymentId: string;
  paymentReference: string;
  amount: number;
  allocatedAtUtc: string;
  allocatedByUserId: string;
}

export interface Receivable {
  id: string;
  reference: string;
  memberId: string;
  memberNumber: string;
  memberName: string;
  memberAccountId: string | null;
  sourceType: string;
  sourceId: string;
  sourceReference: string | null;
  originalAmount: number;
  allocatedAmount: number;
  outstandingAmount: number;
  transactionDate: string;
  dueDate: string | null;
  currencyCode: string;
  status: ReceivableStatus;
  journalId: string | null;
  journalReference: string | null;
  description: string | null;
  cancelledReason: string;
  createdAtUtc: string;
  allocations: ReceivableAllocation[];
}

export interface ReceivableSearch {
  items: Receivable[];
  totalCount: number;
  totalOutstanding: number;
  page: number;
  pageSize: number;
}

export interface PayableAllocation {
  id: string;
  paymentId: string;
  paymentReference: string;
  amount: number;
  allocatedAtUtc: string;
  allocatedByUserId: string;
}

export interface Payable {
  id: string;
  reference: string;
  supplierId: string;
  supplierCode: string;
  supplierName: string;
  sourceType: string;
  sourceId: string;
  sourceReference: string | null;
  originalAmount: number;
  allocatedAmount: number;
  outstandingAmount: number;
  transactionDate: string;
  dueDate: string | null;
  currencyCode: string;
  status: PayableStatus;
  journalId: string | null;
  journalReference: string | null;
  description: string | null;
  cancelledReason: string;
  createdAtUtc: string;
  allocations: PayableAllocation[];
}

export interface PayableSearch {
  items: Payable[];
  totalCount: number;
  totalOutstanding: number;
  page: number;
  pageSize: number;
}

export interface FinancePaymentAllocation {
  id: string;
  paymentId: string;
  receivableId: string | null;
  payableId: string | null;
  receivableReference: string | null;
  payableReference: string | null;
  amount: number;
  createdAtUtc: string;
}

export interface FinancePayment {
  id: string;
  reference: string;
  direction: PaymentDirection;
  memberId: string | null;
  memberNumber: string | null;
  supplierId: string | null;
  supplierCode: string | null;
  memberAccountId: string | null;
  accountTransactionId: string | null;
  sourceType: string | null;
  sourceId: string | null;
  sourceReference: string | null;
  amount: number;
  unappliedAmount: number;
  currencyCode: string;
  transactionDate: string;
  valueDate: string | null;
  paymentMethodCode: string | null;
  externalReference: string | null;
  journalId: string | null;
  journalReference: string | null;
  description: string | null;
  idempotencyKey: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  allocations: FinancePaymentAllocation[];
}

export interface FinancePaymentSearch {
  items: FinancePayment[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface ReconciliationMatch {
  id: string;
  journalId: string;
  journalReference: string;
  transactionDate: string;
  ledgerAmount: number;
  note: string | null;
  matchedAtUtc: string;
  matchedByUserId: string;
}

export interface Reconciliation {
  id: string;
  reference: string;
  financialAccountId: string;
  financialAccountCode: string;
  financialAccountName: string;
  statementDateUtc: string;
  statementClosingBalance: number;
  ledgerClosingBalance: number | null;
  variance: number | null;
  status: FinancialReconciliationStatus;
  note: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  completedAtUtc: string | null;
  completedByUserId: string | null;
  cancelledReason: string | null;
  matches: ReconciliationMatch[];
  matchedLedgerTotal: number;
}

export interface ReconciliationSearch {
  items: Reconciliation[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface TrialBalanceRow {
  accountCode: string;
  accountName: string;
  accountType: FinancialAccountType;
  normalBalance: NormalBalance;
  debit: number;
  credit: number;
  balance: number;
}

export interface TrialBalance {
  currencyCode: string;
  asOf: string | null;
  rows: TrialBalanceRow[];
  totalDebit: number;
  totalCredit: number;
  difference: number;
}

export interface GeneralLedgerRow {
  transactionDate: string;
  journalReference: string;
  journalDescription: string;
  accountCode: string;
  accountName: string;
  direction: JournalDirection;
  amount: number;
  lineDescription: string | null;
  sourceReference: string | null;
}

export interface GeneralLedger {
  financialAccountId: string;
  accountCode: string;
  accountName: string;
  currencyCode: string;
  from: string | null;
  to: string | null;
  openingBalance: number;
  closingBalance: number;
  entries: GeneralLedgerRow[];
}

export interface AgingDocument {
  documentId: string;
  reference: string;
  counterpartyCode: string;
  counterpartyName: string;
  transactionDate: string;
  dueDate: string | null;
  daysOutstanding: number;
  bucketLabel: string;
  originalAmount: number;
  allocatedAmount: number;
  outstandingAmount: number;
  currencyCode: string;
}

export interface Aging {
  isReceivable: boolean;
  currencyCode: string;
  asOf: string;
  buckets: AgingBucket[];
  documents: AgingDocument[];
  totalOutstanding: number;
}

export interface CashPositionRow {
  accountCode: string;
  accountName: string;
  cashKind: FinancialCashKind;
  debitBalance: number;
  creditBalance: number;
  netBalance: number;
  currencyCode: string;
}

export interface CashPosition {
  baseCurrencyCode: string;
  asOf: string;
  rows: CashPositionRow[];
  totalCash: number;
  totalBank: number;
}

export interface FinancialPeriodsReportRow {
  reference: string;
  year: number;
  month: number;
  name: string;
  status: FinancialPeriodStatus;
  startUtc: string;
  endUtc: string;
  postedDebitTotal: number;
  postedCreditTotal: number;
  postedJournalCount: number;
}

export interface FinancialPeriodsReport {
  rows: FinancialPeriodsReportRow[];
  currentStatus: FinancialPeriodStatus | null;
  currentPeriod: FinancialPeriod | null;
}

export interface FinanceOverview {
  baseCurrencyCode: string;
  asOf: string;
  currentPeriod: FinancialPeriod | null;
  openReceivables: number;
  openPayables: number;
  cashAndBank: number;
  trialBalanceDebits: number;
  trialBalanceCredits: number;
  trialBalanceDifference: number;
  draftJournalCount: number;
  unreconciledCashAccountCount: number;
}

// ---------------------------------------------------------------------------
// D7 Golf operations
// ---------------------------------------------------------------------------

export type GolfCourseStatus = 'Active' | 'Inactive';
export type GolfTeeStatus = 'Active' | 'Inactive';
export type TeeSlotStatus = 'Open' | 'Blocked';
export type GolfBookingStatus =
  | 'Draft'
  | 'Confirmed'
  | 'CheckedIn'
  | 'Completed'
  | 'Cancelled'
  | 'NoShow';
export type GolfPlayerType = 'Member' | 'Guest';
export type GolfBookingPlayerStatus = 'Booked' | 'CheckedIn' | 'NoShow' | 'Cancelled';
export type GolfGuestStatus = 'Active' | 'Inactive';
export type GolfChargeType = 'GreenFee' | 'Additional';
export type GolfChargeStatus = 'Pending' | 'Charged' | 'Waived' | 'Cancelled';

export interface GolfHole {
  id: string;
  courseId: string;
  holeNumber: number;
  par: number;
  yardage: number | null;
  name: string | null;
}

export interface GolfTee {
  id: string;
  courseId: string;
  code: string;
  name: string;
  capacity: number;
  status: GolfTeeStatus;
  membersOnly: boolean;
  guestsAllowed: boolean;
  minimumPlayers: number | null;
  maximumPlayers: number | null;
  greenFeeProductId: string | null;
  guestFeeProductId: string | null;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface GolfCourse {
  id: string;
  code: string;
  name: string;
  numberOfHoles: number;
  status: GolfCourseStatus;
  currencyCode: string;
  description: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  holes: GolfHole[];
  tees: GolfTee[];
}

export interface GolfCourseSearch {
  items: GolfCourse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface GolfTeeSlot {
  id: string;
  courseId: string;
  teeId: string;
  courseCode: string;
  courseName: string;
  teeCode: string;
  teeName: string;
  date: string;
  startTime: string;
  durationMinutes: number;
  capacity: number;
  takenPlayers: number;
  openPlayers: number;
  status: TeeSlotStatus;
  maximumPlayers: number | null;
  membersOnly: boolean;
  guestsAllowed: boolean;
  currencyCode: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface GolfTeeSlotSearch {
  items: GolfTeeSlot[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface GolfGuest {
  id: string;
  reference: string;
  fullName: string;
  contactDetails: string | null;
  hostMemberId: string | null;
  status: GolfGuestStatus;
  visitCount: number;
  lastVisitedAtUtc: string | null;
  notes: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface GolfGuestSearch {
  items: GolfGuest[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface GolfBookingPlayer {
  id: string;
  bookingId: string;
  playerType: GolfPlayerType;
  memberId: string | null;
  memberNumber: string | null;
  memberName: string | null;
  golfGuestId: string | null;
  guestName: string | null;
  fee: number;
  currencyCode: string;
  accountTransactionId: string | null;
  receivableId: string | null;
  status: GolfBookingPlayerStatus;
  position: number;
}

export interface GolfBookingCharge {
  id: string;
  bookingId: string;
  bookingPlayerId: string | null;
  chargeType: GolfChargeType;
  description: string;
  amount: number;
  currencyCode: string;
  status: GolfChargeStatus;
  productId: string | null;
  accountTransactionId: string | null;
  receivableId: string | null;
  journalId: string | null;
  createdAtUtc: string;
}

export interface GolfBooking {
  id: string;
  reference: string;
  teeSlotId: string;
  courseId: string;
  teeId: string;
  courseName: string;
  teeName: string;
  teeDate: string;
  startTime: string;
  status: GolfBookingStatus;
  bookedByMemberId: string;
  playerCount: number;
  totalFee: number;
  currencyCode: string;
  cancellationReason: string | null;
  overrideReason: string | null;
  capacityOverridden: boolean;
  createdAtUtc: string;
  confirmedAtUtc: string | null;
  checkedInAtUtc: string | null;
  completedAtUtc: string | null;
  cancelledAtUtc: string | null;
  noShowAtUtc: string | null;
  players: GolfBookingPlayer[];
  charges: GolfBookingCharge[];
}

export interface GolfBookingSearch {
  items: GolfBooking[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface GolfChargeSearch {
  items: GolfBookingCharge[];
  totalCount: number;
  totalAmount: number;
  page: number;
  pageSize: number;
}
