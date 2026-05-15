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
}

export interface TransactionFilterParams {
  page: number;
  pageSize: number;
  status?: string;
  search?: string;
  dateFrom?: string;
  dateTo?: string;
}
