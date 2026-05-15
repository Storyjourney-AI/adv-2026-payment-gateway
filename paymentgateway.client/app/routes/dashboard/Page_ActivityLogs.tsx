import type { Route } from "./+types/Page_ActivityLogs";
import { useEffect, useState } from "react";
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
import { Loader2, Search, ScrollText } from "lucide-react";
import { getActivityLogs } from "@services/activity-log";
import type {
  Dto_ActivityLogItem,
  ActivityLogFilterParams,
} from "@services/activity-log";
import type { PaginationWrapper } from "@services/application";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Activity Logs - Payment Gateway" },
    { name: "description", content: "View system activity logs" },
  ];
}

const CATEGORY_OPTIONS = [
  { value: "all", label: "All Categories" },
  { value: "login", label: "Login" },
  { value: "creation", label: "Creation" },
  { value: "modification", label: "Modification" },
  { value: "deletion", label: "Deletion" },
];

function getCategoryBadgeClass(category: string): string {
  switch (category) {
    case "login":
      return "bg-blue-100 text-blue-800 border-blue-200";
    case "creation":
      return "bg-emerald-100 text-emerald-800 border-emerald-200";
    case "modification":
      return "bg-amber-100 text-amber-800 border-amber-200";
    case "deletion":
      return "bg-red-100 text-red-800 border-red-200";
    default:
      return "bg-gray-100 text-gray-800 border-gray-200";
  }
}

export default function Page_ActivityLogs() {
  const [logs, setLogs] = useState<Dto_ActivityLogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);

  const [search, setSearch] = useState("");
  const [searchDebounce, setSearchDebounce] = useState("");
  const [category, setCategory] = useState("all");

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search);
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    loadLogs();
  }, [page, searchDebounce, category]);

  const loadLogs = async () => {
    setLoading(true);
    setError(null);
    try {
      const params: ActivityLogFilterParams = {
        page,
        pageSize: 20,
        search: searchDebounce || undefined,
        category: category !== "all" ? category : undefined,
      };

      const response = await getActivityLogs(params);
      if (response.success && response.data) {
        setLogs(response.data.items ?? []);
        setTotalPages(response.data.totalPages);
        setTotalItems(response.data.totalItems);
      } else {
        setError(response.message || "Failed to load activity logs");
      }
    } catch {
      setError("An unexpected error occurred");
    } finally {
      setLoading(false);
    }
  };

  const handleCategoryChange = (value: string) => {
    setCategory(value);
    setPage(1);
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold">Activity Logs</h1>
        <p className="text-gray-600 dark:text-gray-400 mt-2">
          View system activity history. Logs are retained for 30 days.
        </p>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1">
          <label className="text-xs font-medium text-muted-foreground">Category</label>
          <Select value={category} onValueChange={handleCategoryChange}>
            <SelectTrigger className="w-44">
              <SelectValue placeholder="All Categories" />
            </SelectTrigger>
            <SelectContent>
              {CATEGORY_OPTIONS.map((opt) => (
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
              placeholder="Search by email, action, or session..."
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
          <Button onClick={loadLogs} variant="outline" className="mt-4">
            Try Again
          </Button>
        </div>
      ) : logs.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg">
          <ScrollText className="h-12 w-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-semibold mb-2">No activity logs found</h3>
          <p className="text-sm text-muted-foreground mb-4 text-center max-w-md">
            {search || category !== "all"
              ? "No logs matching your filters. Try adjusting the category or search term."
              : "Activity logs will appear here as users interact with the system."}
          </p>
        </div>
      ) : (
        <>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Timestamp</TableHead>
                  <TableHead>User</TableHead>
                  <TableHead>Session</TableHead>
                  <TableHead>Category</TableHead>
                  <TableHead>Action</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {logs.map((log) => (
                  <TableRow key={log.id}>
                    <TableCell className="text-muted-foreground whitespace-nowrap">
                      {new Date(log.timestamp).toLocaleString()}
                    </TableCell>
                    <TableCell>
                      {log.userEmail || "—"}
                    </TableCell>
                    <TableCell className="font-mono text-sm">
                      {log.sessionToken || "—"}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={getCategoryBadgeClass(log.category)}
                      >
                        {log.category}
                      </Badge>
                    </TableCell>
                    <TableCell>{log.action}</TableCell>
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
