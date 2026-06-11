// Transaction DTOs matching backend

export interface Dto_TransactionListItem {
  id: string;
  callerOrderId: string;
  midtransOrderId: string;
  grossAmount: number;
  transactionStatus: string | null;
  midtransEnv: string;
  midtransTransactionId: string | null;
  applicationName: string;
  environmentName: string;
  isSandbox: boolean;
  createdAt: string;
  updatedAt: string;
  // Webhook delivery fields — null when no webhook URL registered or no attempt made
  webhookForwardStatus: "Pending" | "Delivered" | "Failed" | "Exhausted" | null;
  webhookAttemptCount: number | null;
  webhookLastAttemptAt: string | null;
  webhookLastResponseCode: number | null;
}

export interface Dto_WebhookForwardStatus {
  snapTransactionId: string;
  status: "Pending" | "Delivered" | "Failed" | "Exhausted";
  attemptCount: number;
  maxAttempts: number;
  lastAttemptAt: string | null;
  nextAttemptAt: string | null;
  lastResponseCode: number | null;
  lastError: string | null;
}

export interface TransactionFilterParams {
  page: number;
  pageSize: number;
  status?: string;
  search?: string;
  dateFrom?: string;
  dateTo?: string;
}
