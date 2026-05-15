import type { Route } from "./+types/Page_Dashboard";
import { useCallback, useEffect, useState } from "react";
import { useAuth } from "@services/auth";
import { getApplications, type PaginationWrapper } from "@services/application";
import {
  getTransactions,
  type Dto_TransactionListItem,
  type TransactionFilterParams,
} from "@services/transaction";
import { Badge } from "~/components/ui/badge";
import { Button } from "~/components/ui/button";
import { Loader2, RefreshCw } from "lucide-react";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Dashboard - Payment Gateway" },
    { name: "description", content: "User Dashboard" },
  ];
}

const REFRESH_INTERVAL_MS = 30_000;

interface DashboardStats {
  totalApplications: number;
  totalTransactions: number;
  pendingTransactions: number;
  successTransactions: number;
}

function getStatusBadgeClass(status: string | null): string {
  switch (status?.toLowerCase()) {
    case "settlement":
    case "capture":
      return "bg-emerald-100 text-emerald-800 border-emerald-200";
    case "pending":
    case "authorize":
      return "bg-amber-100 text-amber-800 border-amber-200";
    case "deny":
    case "cancel":
    case "expire":
    case "failure":
    case "error":
      return "bg-red-100 text-red-800 border-red-200";
    default:
      return "bg-gray-100 text-gray-800 border-gray-200";
  }
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat("id-ID").format(value);
}

function formatLastUpdated(value: Date | null): string {
  if (!value) return "Belum pernah diperbarui";
  return `Update terakhir: ${value.toLocaleTimeString("id-ID")}`;
}

function formatTransactionStatus(status: string | null): string {
  if (!status) return "unknown";
  if (status === "partial_refund") return "partial refund";
  return status;
}

export default function Page_Dashboard() {
  const { user } = useAuth();
  const [stats, setStats] = useState<DashboardStats>({
    totalApplications: 0,
    totalTransactions: 0,
    pendingTransactions: 0,
    successTransactions: 0,
  });
  const [recentTransactions, setRecentTransactions] = useState<
    Dto_TransactionListItem[]
  >([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const loadDashboardData = useCallback(async (isManualRefresh = false) => {
    if (isManualRefresh) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }

    setError(null);

    try {
      const baseParams: TransactionFilterParams = {
        page: 1,
        pageSize: 1,
      };

      const [
        applicationsResponse,
        totalTransactionsResponse,
        pendingTransactionsResponse,
        successTransactionsResponse,
        recentTransactionsResponse,
      ] = await Promise.all([
        getApplications({ page: 1, pageSize: 1 }),
        getTransactions(baseParams),
        getTransactions({ ...baseParams, status: "pending" }),
        getTransactions({ ...baseParams, status: "settlement" }),
        getTransactions({ page: 1, pageSize: 5 }),
      ]);

      const responses = [
        applicationsResponse,
        totalTransactionsResponse,
        pendingTransactionsResponse,
        successTransactionsResponse,
        recentTransactionsResponse,
      ];

      const failedResponse = responses.find((response) => !response.success);
      if (failedResponse) {
        setError(failedResponse.message || "Gagal memuat data dashboard.");
        return;
      }

      const applicationData = applicationsResponse.data as
        | PaginationWrapper<unknown>
        | null;
      const totalData = totalTransactionsResponse.data as
        | PaginationWrapper<unknown>
        | null;
      const pendingData = pendingTransactionsResponse.data as
        | PaginationWrapper<unknown>
        | null;
      const successData = successTransactionsResponse.data as
        | PaginationWrapper<unknown>
        | null;
      const recentData = recentTransactionsResponse.data as
        | PaginationWrapper<Dto_TransactionListItem>
        | null;

      setStats({
        totalApplications: applicationData?.totalItems ?? 0,
        totalTransactions: totalData?.totalItems ?? 0,
        pendingTransactions: pendingData?.totalItems ?? 0,
        successTransactions: successData?.totalItems ?? 0,
      });
      setRecentTransactions(recentData?.items ?? []);
      setLastUpdated(new Date());
    } catch {
      setError("Terjadi kesalahan tidak terduga saat memuat dashboard.");
    } finally {
      if (isManualRefresh) {
        setRefreshing(false);
      } else {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    void loadDashboardData();

    const intervalId = window.setInterval(() => {
      void loadDashboardData(true);
    }, REFRESH_INTERVAL_MS);

    return () => window.clearInterval(intervalId);
  }, [loadDashboardData]);

  return (
    <div className="space-y-6">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Dashboard</h1>
          <p className="text-gray-600 dark:text-gray-400 mt-2">
            Welcome back, {user?.email}!
          </p>
          <p className="text-xs text-muted-foreground mt-1">
            {formatLastUpdated(lastUpdated)}
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          onClick={() => void loadDashboardData(true)}
          disabled={refreshing || loading}
        >
          <RefreshCw className={`h-4 w-4 mr-2 ${refreshing ? "animate-spin" : ""}`} />
          Refresh
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        <div className="rounded-lg border bg-card p-6">
          <h3 className="font-semibold mb-2">Profile</h3>
          <div className="space-y-1 text-sm">
            <p>
              <span className="text-muted-foreground">Email:</span> {user?.email}
            </p>
            <p>
              <span className="text-muted-foreground">User ID:</span>{" "}
              {user?.userId}
            </p>
            <p>
              <span className="text-muted-foreground">Roles:</span>{" "}
              {user?.roles?.join(", ") || "-"}
            </p>
          </div>
        </div>

        <div className="rounded-lg border bg-card p-6">
          <h3 className="font-semibold mb-2">Quick Stats</h3>
          {loading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Memuat statistik...
            </div>
          ) : (
            <div className="space-y-2 text-sm">
              <p>
                <span className="text-muted-foreground">Applications:</span>{" "}
                <span className="font-semibold">
                  {formatNumber(stats.totalApplications)}
                </span>
              </p>
              <p>
                <span className="text-muted-foreground">Transactions:</span>{" "}
                <span className="font-semibold">
                  {formatNumber(stats.totalTransactions)}
                </span>
              </p>
              <p>
                <span className="text-muted-foreground">Pending:</span>{" "}
                <span className="font-semibold">
                  {formatNumber(stats.pendingTransactions)}
                </span>
              </p>
              <p>
                <span className="text-muted-foreground">Settlement:</span>{" "}
                <span className="font-semibold">
                  {formatNumber(stats.successTransactions)}
                </span>
              </p>
            </div>
          )}
        </div>

        <div className="rounded-lg border bg-card p-6">
          <h3 className="font-semibold mb-2">Recent Transactions</h3>
          {loading ? (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin" />
              Memuat aktivitas...
            </div>
          ) : recentTransactions.length === 0 ? (
            <p className="text-sm text-muted-foreground">Belum ada transaksi terbaru.</p>
          ) : (
            <div className="space-y-2">
              {recentTransactions.map((transaction) => (
                <div
                  key={transaction.id}
                  className="rounded-md border p-2.5 space-y-2"
                >
                  <div className="flex items-center justify-between gap-2">
                    <p className="text-xs font-mono truncate">
                      {transaction.callerOrderId}
                    </p>
                    <Badge
                      variant="outline"
                      className={`${getStatusBadgeClass(transaction.transactionStatus)} text-[11px]`}
                    >
                      {formatTransactionStatus(transaction.transactionStatus)}
                    </Badge>
                  </div>
                  <div className="flex items-center justify-between gap-2 text-[11px] text-muted-foreground">
                    <p className="truncate">
                      App:{" "}
                      <span className="font-medium text-foreground">
                        {transaction.applicationName}
                      </span>
                    </p>
                    <div className="flex items-center gap-1">
                      <span>{transaction.environmentName}</span>
                      <Badge
                        variant="outline"
                        className={
                          transaction.isSandbox
                            ? "bg-amber-50 text-amber-700 border-amber-200 text-[10px]"
                            : "bg-emerald-50 text-emerald-700 border-emerald-200 text-[10px]"
                        }
                      >
                        {transaction.isSandbox ? "Sandbox" : "Production"}
                      </Badge>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>

      {error && (
        <div className="rounded-lg border border-red-300 bg-red-50 px-4 py-3 text-sm text-red-700">
          {error}
        </div>
      )}
    </div>
  );
}
