import type { Route } from "./+types/Page_Transactions";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Badge } from "~/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "~/components/ui/table";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "~/components/ui/select";
import { Loader2, Search, FileDown, Receipt } from "lucide-react";
import { getTransactions, exportTransactionsPdf } from "@services/transaction";
import type {
  Dto_TransactionListItem,
  TransactionFilterParams,
} from "@services/transaction";
import type { PaginationWrapper } from "@services/application";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Transactions - Payment Gateway" },
    { name: "description", content: "View transaction history and export reports" },
  ];
}

const STATUS_OPTIONS = [
  { value: "all", label: "All Statuses" },
  { value: "pending", label: "Pending" },
  { value: "capture", label: "Success (Capture)" },
  { value: "settlement", label: "Settlement" },
  { value: "authorize", label: "Authorize" },
  { value: "deny", label: "Deny" },
  { value: "cancel", label: "Cancel" },
  { value: "expire", label: "Expire" },
  { value: "failure", label: "Failure" },
  { value: "refund", label: "Refund" },
  { value: "partial_refund", label: "Partial Refund" },
  { value: "error", label: "Error" },
];

function getStatusBadgeClass(status: string | null): string {
  switch (status?.toLowerCase()) {
    case "settlement":
    case "capture":
      return "bg-emerald-100 text-emerald-800 border-emerald-200";
    case "pending":
    case "authorize":
      return "bg-amber-100 text-amber-800 border-amber-200";
    case "refund":
    case "partial_refund":
      return "bg-blue-100 text-blue-800 border-blue-200";
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

function getStatusLabel(status: string | null): string {
  switch (status?.toLowerCase()) {
    case "settlement":
    case "capture":
      return "Success";
    case "partial_refund":
      return "Partial Refund";
    case "authorize":
      return "Authorized";
    default:
      return status || "unknown";
  }
}

function formatCurrency(amount: number): string {
  return new Intl.NumberFormat("id-ID", {
    style: "currency",
    currency: "IDR",
    minimumFractionDigits: 0,
  }).format(amount);
}

function getDefaultDateFrom(): string {
  const date = new Date();
  date.setDate(date.getDate() - 30);
  return date.toISOString().split("T")[0];
}

function getDefaultDateTo(): string {
  return new Date().toISOString().split("T")[0];
}

export default function Page_Transactions() {
  const [transactions, setTransactions] = useState<Dto_TransactionListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);

  const [search, setSearch] = useState("");
  const [searchDebounce, setSearchDebounce] = useState("");
  const [status, setStatus] = useState("all");
  const [dateFrom, setDateFrom] = useState(getDefaultDateFrom());
  const [dateTo, setDateTo] = useState(getDefaultDateTo());

  const [exporting, setExporting] = useState(false);

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    loadTransactions();
  }, [page, searchDebounce, status, dateFrom, dateTo]);

  const loadTransactions = async () => {
    setLoading(true);
    setError(null);
    try {
      const params: TransactionFilterParams = {
        page,
        pageSize: 10,
        status: status !== "all" ? status : undefined,
        search: searchDebounce || undefined,
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
      };

      const response = await getTransactions(params);
      if (response.success && response.data) {
        setTransactions(response.data.items);
        setTotalPages(response.data.totalPages);
        setTotalItems(response.data.totalItems);
      } else {
        setError(response.message || "Failed to load transactions");
      }
    } catch {
      setError("An unexpected error occurred");
    } finally {
      setLoading(false);
    }
  };

  const handleExportPdf = async () => {
    if (!dateFrom || !dateTo) {
      toast.error("Please select both a start and end date for the export.");
      return;
    }

    setExporting(true);
    try {
      const blob = await exportTransactionsPdf(dateFrom, dateTo, status);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `transactions_${dateFrom}_to_${dateTo}.pdf`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Failed to export PDF");
    } finally {
      setExporting(false);
    }
  };

  const handleStatusChange = (value: string) => {
    setStatus(value);
    setPage(1);
  };

  const handleDateFromChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setDateFrom(e.target.value);
    setPage(1);
  };

  const handleDateToChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setDateTo(e.target.value);
    setPage(1);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Transactions</h1>
          <p className="text-gray-600 dark:text-gray-400 mt-2">
            View transaction history and export reports
          </p>
        </div>
        <Button onClick={handleExportPdf} disabled={exporting}>
          {exporting ? (
            <Loader2 className="h-4 w-4 mr-2 animate-spin" />
          ) : (
            <FileDown className="h-4 w-4 mr-2" />
          )}
          Export PDF
        </Button>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-muted-foreground">From</label>
          <Input
            type="date"
            value={dateFrom}
            onChange={handleDateFromChange}
            className="w-40"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-muted-foreground">To</label>
          <Input
            type="date"
            value={dateTo}
            onChange={handleDateToChange}
            className="w-40"
          />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-muted-foreground">Status</label>
          <Select value={status} onValueChange={handleStatusChange}>
            <SelectTrigger className="w-40">
              <SelectValue placeholder="All Statuses" />
            </SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map((opt) => (
                <SelectItem key={opt.value} value={opt.value}>
                  {opt.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1 flex-1 min-w-[200px]">
          <label className="text-xs font-medium text-muted-foreground">Search</label>
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search by order ID..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pl-9"
            />
          </div>
        </div>
      </div>

      {/* Content */}
      {loading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : error ? (
        <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg border-red-300">
          <p className="text-red-600 dark:text-red-400">{error}</p>
          <Button onClick={loadTransactions} variant="outline" className="mt-4">
            Try Again
          </Button>
        </div>
      ) : transactions.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg">
          <Receipt className="h-12 w-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-semibold mb-2">No transactions found</h3>
          <p className="text-sm text-muted-foreground mb-4 text-center max-w-md">
            {search || status !== "all"
              ? "No transactions found matching your filters. Try adjusting the date range or status filter."
              : "Transactions will appear here once your applications start processing payments."}
          </p>
        </div>
      ) : (
        <>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Order ID</TableHead>
                  <TableHead>Application</TableHead>
                  <TableHead>Environment</TableHead>
                  <TableHead className="text-right">Amount</TableHead>
                  <TableHead>Status</TableHead>
                  <TableHead>Type</TableHead>
                  <TableHead>Created At</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {transactions.map((tx) => (
                  <TableRow key={tx.id}>
                    <TableCell className="font-mono text-sm">
                      {tx.callerOrderId}
                    </TableCell>
                    <TableCell>{tx.applicationName}</TableCell>
                    <TableCell>
                      <div className="flex items-center gap-2">
                        <span>{tx.environmentName}</span>
                        <Badge
                          variant="outline"
                          className={
                            tx.isSandbox
                              ? "bg-amber-50 text-amber-700 border-amber-200 text-xs"
                              : "bg-emerald-50 text-emerald-700 border-emerald-200 text-xs"
                          }
                        >
                          {tx.isSandbox ? "Sandbox" : "Production"}
                        </Badge>
                      </div>
                    </TableCell>
                    <TableCell className="text-right font-mono">
                      {formatCurrency(tx.grossAmount)}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={getStatusBadgeClass(tx.transactionStatus)}
                      >
                        {getStatusLabel(tx.transactionStatus)}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {tx.midtransEnv}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {new Date(tx.createdAt).toLocaleString()}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between">
            <div className="text-sm text-muted-foreground">
              Page {page} of {totalPages} ({totalItems} total)
            </div>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page === totalPages}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  );
}
