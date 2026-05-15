import type { Route } from "./+types/Page_Applications";
import { useEffect, useState } from "react";
import { Link } from "react-router";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "~/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "~/components/ui/dialog";
import { Plus, Loader2, Search, Pencil, Trash2 } from "lucide-react";
import { useApplications } from "@services/application";
import type { Dto_ApplicationListItem } from "@services/application";
import { Compo_ApplicationForm } from "./components/Compo_ApplicationForm";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Applications - Payment Gateway" },
    { name: "description", content: "Manage your applications" },
  ];
}

export default function Page_Applications() {
  const [applications, setApplications] = useState<Dto_ApplicationListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [search, setSearch] = useState("");
  const [searchDebounce, setSearchDebounce] = useState("");
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editingApp, setEditingApp] = useState<Dto_ApplicationListItem | null>(null);

  const { fetchApplications, deleteApplication } = useApplications();

  // Debounce search
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search);
      setPage(1); // Reset to first page on search
    }, 300);
    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    loadApplications();
  }, [page, searchDebounce]);

  const loadApplications = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetchApplications({
        page,
        pageSize: 10,
        search: searchDebounce || undefined,
      });
      if (response.success && response.data) {
        setApplications(response.data.items);
        setTotalPages(response.data.totalPages);
      } else {
        setError(response.message || "Failed to load applications");
      }
    } catch (err) {
      setError("An unexpected error occurred");
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!confirm("Are you sure you want to delete this application? This will also delete all associated environments.")) {
      return;
    }

    try {
      const response = await deleteApplication(id);
      if (response.success) {
        await loadApplications();
      } else {
        alert(response.message || "Failed to delete application");
      }
    } catch (err) {
      alert("An unexpected error occurred");
    }
  };

  const handleEdit = (app: Dto_ApplicationListItem) => {
    setEditingApp(app);
    setEditDialogOpen(true);
  };

  const handleCreateSuccess = () => {
    setCreateDialogOpen(false);
    loadApplications();
  };

  const handleEditSuccess = () => {
    setEditDialogOpen(false);
    setEditingApp(null);
    loadApplications();
  };

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Applications</h1>
          <p className="text-gray-600 dark:text-gray-400 mt-2">
            Manage your payment gateway applications
          </p>
        </div>
        <Dialog open={createDialogOpen} onOpenChange={setCreateDialogOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="h-4 w-4 mr-2" />
              New Application
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create New Application</DialogTitle>
              <DialogDescription>
                Create a new application. Staging and Production environments will be created automatically.
              </DialogDescription>
            </DialogHeader>
            <Compo_ApplicationForm onSuccess={handleCreateSuccess} />
          </DialogContent>
        </Dialog>
      </div>

      <div className="flex items-center space-x-2">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search applications..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="pl-9"
          />
        </div>
      </div>

      {loading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      ) : error ? (
        <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg border-red-300">
          <p className="text-red-600 dark:text-red-400">{error}</p>
          <Button onClick={loadApplications} variant="outline" className="mt-4">
            Try Again
          </Button>
        </div>
      ) : applications.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg">
          <Plus className="h-12 w-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-semibold mb-2">No applications yet</h3>
          <p className="text-sm text-muted-foreground mb-4">
            {search ? "No applications found matching your search" : "Create your first application to get started"}
          </p>
          {!search && (
            <Button onClick={() => setCreateDialogOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />
              Create your first application
            </Button>
          )}
        </div>
      ) : (
        <>
          <div className="rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Name</TableHead>
                  <TableHead>Description</TableHead>
                  <TableHead className="text-center">Environments</TableHead>
                  <TableHead>Created At</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {applications.map((app) => (
                  <TableRow key={app.id}>
                    <TableCell className="font-medium">
                      <Link
                        to={`/dashboard/applications/${app.id}`}
                        className="hover:underline text-primary"
                      >
                        {app.name}
                      </Link>
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {app.description || "-"}
                    </TableCell>
                    <TableCell className="text-center">
                      {app.environmentCount}
                    </TableCell>
                    <TableCell className="text-muted-foreground">
                      {new Date(app.createdAt).toLocaleDateString()}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEdit(app)}
                        >
                          <Pencil className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDelete(app.id)}
                        >
                          <Trash2 className="h-4 w-4 text-destructive" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {totalPages > 1 && (
            <div className="flex items-center justify-between">
              <div className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </div>
              <div className="flex gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.max(1, p - 1))}
                  disabled={page === 1}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                  disabled={page === totalPages}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </>
      )}

      {/* Edit Dialog */}
      <Dialog open={editDialogOpen} onOpenChange={(open) => {
        setEditDialogOpen(open);
        if (!open) setEditingApp(null);
      }}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Edit Application</DialogTitle>
            <DialogDescription>
              Update application details
            </DialogDescription>
          </DialogHeader>
          {editingApp && (
            <Compo_ApplicationForm
              application={editingApp}
              onSuccess={handleEditSuccess}
            />
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
