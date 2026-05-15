import type {
  Dto_TransactionListItem,
  TransactionFilterParams,
} from "../types/transaction.types";
import type { PaginationWrapper } from "@services/application";
import { authenticatedFetch, type DataWrapper } from "@services/auth/utils/api.helper";
import { authStore } from "@services/auth/store/auth.store";

const API_BASE = import.meta.env.VITE_API_BASE_URL || '';

export async function getTransactions(
  params: TransactionFilterParams
): Promise<DataWrapper<PaginationWrapper<Dto_TransactionListItem>>> {
  const queryParams = new URLSearchParams({
    page: params.page.toString(),
    pageSize: params.pageSize.toString(),
    ...(params.status && params.status !== "all" && { status: params.status }),
    ...(params.search && { search: params.search }),
    ...(params.dateFrom && { dateFrom: params.dateFrom }),
    ...(params.dateTo && { dateTo: params.dateTo }),
  });

  return authenticatedFetch<PaginationWrapper<Dto_TransactionListItem>>(
    `/api/transaction?${queryParams}`
  );
}

export async function exportTransactionsPdf(
  dateFrom: string,
  dateTo: string,
  status?: string
): Promise<Blob> {
  const queryParams = new URLSearchParams({
    dateFrom,
    dateTo,
    ...(status && status !== "all" && { status }),
  });

  const token = authStore.getAccessToken();

  const response = await fetch(`${API_BASE}/api/transaction/export/pdf?${queryParams}`, {
    headers: {
      ...(token && { Authorization: `Bearer ${token}` }),
    },
    credentials: "include",
  });

  if (!response.ok) {
    let errorMessage = "Failed to export PDF";
    try {
      const errorData = await response.json();
      errorMessage = errorData?.message || `HTTP ${response.status}`;
    } catch {
      errorMessage = `HTTP ${response.status}: ${response.statusText}`;
    }
    throw new Error(errorMessage);
  }

  return response.blob();
}
