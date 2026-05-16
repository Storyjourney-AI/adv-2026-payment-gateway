import type { Route } from "./+types/Page_ApplicationDetail";
import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { toast } from "sonner";
import { Button } from "~/components/ui/button";
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
import { ArrowLeft, Plus, Loader2, Copy, Key, Pencil, Trash2, CheckCircle, FlaskConical } from "lucide-react";
import { Badge } from "~/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "~/components/ui/tabs";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "~/components/ui/alert-dialog";
import { useApplications } from "@services/application";
import { getEnvironmentsByApplication, deleteEnvironment, regenerateApiKey, testPurchase } from "@services/application";
import type { Dto_ApplicationResponse, Dto_EnvironmentResponse } from "@services/application";
import { Compo_EnvironmentForm } from "./components/Compo_EnvironmentForm";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Application Details - Payment Gateway" },
    { name: "description", content: "View and manage application environments" },
  ];
}

export default function Page_ApplicationDetail() {
  const { id } = useParams<{ id: string }>();
  const [application, setApplication] = useState<Dto_ApplicationResponse | null>(null);
  const [environments, setEnvironments] = useState<Dto_EnvironmentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [editingEnv, setEditingEnv] = useState<Dto_EnvironmentResponse | null>(null);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [testPurchaseEnv, setTestPurchaseEnv] = useState<Dto_EnvironmentResponse | null>(null);
  const [testPurchaseLoading, setTestPurchaseLoading] = useState<string | null>(null);
  const [disabledEnvIds, setDisabledEnvIds] = useState<Set<string>>(new Set());
  const [copiedSnippet, setCopiedSnippet] = useState<string | null>(null);

  const { getApplicationById } = useApplications();

  const maskApiKey = (apiKey: string) => {
    if (apiKey.length <= 5) return apiKey;
    return apiKey.substring(0, 5) + '*'.repeat(apiKey.length - 5);
  };

  useEffect(() => {
    if (id) {
      loadData();
    }
  }, [id]);

  const loadData = async () => {
    if (!id) return;

    setLoading(true);
    setError(null);
    try {
      const [appResponse, envResponse] = await Promise.all([
        getApplicationById(id),
        getEnvironmentsByApplication(id),
      ]);

      if (appResponse.success && appResponse.data) {
        setApplication(appResponse.data);
      } else {
        setError(appResponse.message || "Failed to load application");
        return;
      }

      if (envResponse.success && envResponse.data) {
        setEnvironments(envResponse.data);
      } else {
        setError(envResponse.message || "Failed to load environments");
      }
    } catch (err) {
      setError("An unexpected error occurred");
    } finally {
      setLoading(false);
    }
  };

  const handleCopyApiKey = async (apiKey: string) => {
    await navigator.clipboard.writeText(apiKey);
    setCopiedKey(apiKey);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const handleDelete = async (envId: string) => {
    if (!confirm("Are you sure you want to delete this environment?")) {
      return;
    }

    try {
      const response = await deleteEnvironment(envId);
      if (response.success) {
        toast.success("Environment deleted successfully");
        await loadData();
      } else {
        toast.error(response.message || "Failed to delete environment");
      }
    } catch (err) {
      toast.error("An unexpected error occurred");
    }
  };

  const handleRegenerateKey = async (envId: string) => {
    if (!confirm("Are you sure you want to regenerate the API key? The old key will stop working immediately.")) {
      return;
    }

    try {
      const response = await regenerateApiKey(envId);
      if (response.success) {
        toast.success("API key regenerated successfully!");
        await loadData();
      } else {
        toast.error(response.message || "Failed to regenerate API key");
      }
    } catch (err) {
      toast.error("An unexpected error occurred");
    }
  };

  const handleEdit = (env: Dto_EnvironmentResponse) => {
    setEditingEnv(env);
    setEditDialogOpen(true);
  };

  const handleTestPurchase = async (env: Dto_EnvironmentResponse) => {
    setTestPurchaseEnv(null);
    setTestPurchaseLoading(env.id);
    try {
      const response = await testPurchase(env.id);
      if (response.success && response.data) {
        const newTab = window.open(response.data.redirectUrl, "_blank", "noopener,noreferrer");
        if (!newTab) {
          toast.warning("Pop-up blocked. Please allow pop-ups for this site and try again.");
        }
      } else {
        if (response.code === 503) {
          setDisabledEnvIds(prev => new Set(prev).add(env.id));
        }
        toast.error(response.message || "Test purchase failed");
      }
    } catch {
      toast.error("An unexpected error occurred");
    } finally {
      setTestPurchaseLoading(null);
    }
  };

  const handleCreateSuccess = () => {
    setCreateDialogOpen(false);
    loadData();
  };

  const handleEditSuccess = () => {
    setEditDialogOpen(false);
    setEditingEnv(null);
    loadData();
  };

  const handleCopySnippet = async (text: string, key: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedSnippet(key);
      setTimeout(() => setCopiedSnippet(null), 2000);
    } catch {
      // Clipboard API not available or permission denied
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center py-16">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (error || !application) {
    return (
      <div className="space-y-6">
        <Link to="/dashboard/applications">
          <Button variant="ghost">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Back to Applications
          </Button>
        </Link>
        <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg border-red-300">
          <p className="text-red-600 dark:text-red-400">{error}</p>
          <Button onClick={loadData} variant="outline" className="mt-4">
            Try Again
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Link to="/dashboard/applications">
            <Button variant="ghost" size="sm">
              <ArrowLeft className="h-4 w-4 mr-2" />
              Back
            </Button>
          </Link>
          <div>
            <h1 className="text-3xl font-bold">{application.name}</h1>
            {application.description && (
              <p className="text-gray-600 dark:text-gray-400 mt-1">
                {application.description}
              </p>
            )}
          </div>
        </div>
      </div>

      <div className="rounded-lg border bg-card p-6">
        <h3 className="font-semibold mb-4">Application Details</h3>
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <span className="text-muted-foreground">Application ID:</span>
            <p className="font-mono mt-1">{application.id}</p>
          </div>
          <div>
            <span className="text-muted-foreground">Created At:</span>
            <p className="mt-1">{new Date(application.createdAt).toLocaleString()}</p>
          </div>
          <div>
            <span className="text-muted-foreground">Last Updated:</span>
            <p className="mt-1">{new Date(application.updatedAt).toLocaleString()}</p>
          </div>
          <div>
            <span className="text-muted-foreground">Total Environments:</span>
            <p className="mt-1">{environments.length}</p>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <h2 className="text-2xl font-bold">Environments</h2>
          <Dialog open={createDialogOpen} onOpenChange={setCreateDialogOpen}>
            <DialogTrigger asChild>
              <Button>
                <Plus className="h-4 w-4 mr-2" />
                New Environment
              </Button>
            </DialogTrigger>
            <DialogContent className="max-w-2xl">
              <DialogHeader>
                <DialogTitle>Create New Environment</DialogTitle>
                <DialogDescription>
                  Add a new environment configuration for this application
                </DialogDescription>
              </DialogHeader>
              <Compo_EnvironmentForm
                applicationId={application.id}
                onSuccess={handleCreateSuccess}
              />
            </DialogContent>
          </Dialog>
        </div>

        {environments.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-16 border-2 border-dashed rounded-lg">
            <Plus className="h-12 w-12 text-muted-foreground mb-4" />
            <h3 className="text-lg font-semibold mb-2">No environments configured</h3>
            <p className="text-sm text-muted-foreground mb-4">
              Create your first environment to get API keys
            </p>
            <Button onClick={() => setCreateDialogOpen(true)}>
              <Plus className="h-4 w-4 mr-2" />
              Create Environment
            </Button>
          </div>
        ) : (
          <div className="space-y-4">
            {environments.map((env) => (
              <div key={env.id} className="rounded-lg border bg-card p-6">
                <div className="flex items-start justify-between mb-4">
                  <div>
                    <div className="flex items-center gap-2">
                      <h3 className="text-xl font-semibold">{env.name}</h3>
                      {env.isSandbox
                        ? <Badge variant="secondary">Sandbox</Badge>
                        : <Badge variant="destructive">Production</Badge>
                      }
                    </div>
                    <p className="text-sm text-muted-foreground">
                      Created {new Date(env.createdAt).toLocaleDateString()}
                    </p>
                  </div>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setTestPurchaseEnv(env)}
                      disabled={testPurchaseLoading === env.id || disabledEnvIds.has(env.id)}
                      title={disabledEnvIds.has(env.id) ? "Production payment environment is disabled" : undefined}
                    >
                      {testPurchaseLoading === env.id ? (
                        <Loader2 className="h-4 w-4 lg:mr-2 animate-spin" />
                      ) : (
                        <FlaskConical className="h-4 w-4 lg:mr-2" />
                      )}
                      <span className="hidden lg:inline">Test Purchase</span>
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleRegenerateKey(env.id)}
                    >
                      <Key className="h-4 w-4 lg:mr-2" />
                      <span className="hidden lg:inline">Regenerate Key</span>
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleEdit(env)}
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => handleDelete(env.id)}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </div>

                <div className="space-y-3 text-sm">
                  <div>
                    <span className="text-muted-foreground">API Key:</span>
                    <div className="flex items-center gap-2 mt-1">
                      <code className="flex-1 bg-muted px-3 py-2 rounded font-mono text-xs">
                        {maskApiKey(env.apiKey)}
                      </code>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => handleCopyApiKey(env.apiKey)}
                      >
                        {copiedKey === env.apiKey ? (
                          <CheckCircle className="h-4 w-4 text-green-500" />
                        ) : (
                          <Copy className="h-4 w-4" />
                        )}
                      </Button>
                    </div>
                  </div>

                  {env.allowedOrigins && (
                    <div>
                      <span className="text-muted-foreground">Allowed Origins:</span>
                      <p className="mt-1">{env.allowedOrigins}</p>
                    </div>
                  )}

                  <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
                    <div>
                      <span className="text-muted-foreground">Success URL:</span>
                      <p className="mt-1 break-all">{env.successResponseUrl}</p>
                    </div>
                    <div>
                      <span className="text-muted-foreground">Pending URL:</span>
                      {env.pendingResponseUrl ? (
                        <p className="mt-1 break-all">{env.pendingResponseUrl}</p>
                      ) : (
                        <div className="mt-1 space-y-1">
                          <p className="font-medium text-muted-foreground">Not configured</p>
                          <p className="text-xs text-muted-foreground">
                            Falls back to Failure URL for pending callbacks.
                          </p>
                        </div>
                      )}
                    </div>
                    <div>
                      <span className="text-muted-foreground">Failure URL:</span>
                      <p className="mt-1 break-all">{env.failureResponseUrl}</p>
                    </div>
                  </div>

                  {env.webhookUrl && (
                    <div>
                      <span className="text-muted-foreground">Webhook URL:</span>
                      <p className="mt-1 break-all">{env.webhookUrl}</p>
                    </div>
                  )}
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Quick Start Section */}
      {environments.length > 0 && (
        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-2xl font-bold">Quick Start</h2>
            <Link to="/dashboard/docs">
              <Button variant="link" className="gap-1">
                View full API docs <span aria-hidden="true">→</span>
              </Button>
            </Link>
          </div>

          <Tabs defaultValue={environments[0].id} className="rounded-lg border bg-card p-6">
            <TabsList className="mb-4">
              {environments.map((env) => (
                <TabsTrigger key={env.id} value={env.id} className="gap-2">
                  {env.name}
                  {env.isSandbox
                    ? <Badge variant="secondary" className="text-[10px] px-1.5 py-0">Sandbox</Badge>
                    : <Badge variant="destructive" className="text-[10px] px-1.5 py-0">Production</Badge>
                  }
                </TabsTrigger>
              ))}
            </TabsList>

            {environments.map((env) => {
              const baseUrl = window.location.origin;
              const curlCreateToken = `curl -X POST ${baseUrl}/api/snap/token \\
  -H "Content-Type: application/json" \\
  -H "X-Api-Key: ${env.apiKey}" \\
  -d '{
    "orderId": "order-001",
    "amount": 50000,
    "itemName": "Premium Subscription"
  }'`;

              const fetchCreateToken = `const response = await fetch("${baseUrl}/api/snap/token", {
  method: "POST",
  headers: {
    "Content-Type": "application/json",
    "X-Api-Key": "${env.apiKey}"
  },
  body: JSON.stringify({
    orderId: "order-001",
    amount: 50000,
    itemName: "Premium Subscription"
  })
});
const result = await response.json();
console.log(result.data.redirectUrl);`;

              const curlCheckStatus = `curl -X GET ${baseUrl}/api/snap/status/order-001 \\
  -H "X-Api-Key: ${env.apiKey}"`;

              const fetchCheckStatus = `const response = await fetch("${baseUrl}/api/snap/status/order-001", {
  headers: {
    "X-Api-Key": "${env.apiKey}"
  }
});
const result = await response.json();
console.log(result.data.transactionStatus);`;

              return (
                <TabsContent key={env.id} value={env.id} className="space-y-6">
                  {/* Create Payment Token */}
                  <div className="space-y-3">
                    <h3 className="text-lg font-semibold">1. Create a Payment Token</h3>
                    <p className="text-sm text-muted-foreground">
                      <code className="bg-muted px-1.5 py-0.5 rounded font-mono">POST /api/snap/token</code>
                    </p>

                    <div>
                      <p className="text-sm font-medium mb-2">cURL</p>
                      <div className="relative group">
                        <pre className="bg-muted rounded-lg p-4 overflow-x-auto text-sm">
                          <code>{curlCreateToken}</code>
                        </pre>
                        <Button
                          variant="outline"
                          size="sm"
                          className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity"
                          onClick={() => handleCopySnippet(curlCreateToken, `${env.id}-curl-create`)}
                        >
                          {copiedSnippet === `${env.id}-curl-create` ? (
                            <CheckCircle className="h-4 w-4 text-green-500" />
                          ) : (
                            <Copy className="h-4 w-4" />
                          )}
                        </Button>
                      </div>
                    </div>

                    <div>
                      <p className="text-sm font-medium mb-2">JavaScript (fetch)</p>
                      <div className="relative group">
                        <pre className="bg-muted rounded-lg p-4 overflow-x-auto text-sm">
                          <code>{fetchCreateToken}</code>
                        </pre>
                        <Button
                          variant="outline"
                          size="sm"
                          className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity"
                          onClick={() => handleCopySnippet(fetchCreateToken, `${env.id}-fetch-create`)}
                        >
                          {copiedSnippet === `${env.id}-fetch-create` ? (
                            <CheckCircle className="h-4 w-4 text-green-500" />
                          ) : (
                            <Copy className="h-4 w-4" />
                          )}
                        </Button>
                      </div>
                    </div>
                  </div>

                  {/* Check Payment Status */}
                  <div className="space-y-3">
                    <h3 className="text-lg font-semibold">2. Check Payment Status</h3>
                    <p className="text-sm text-muted-foreground">
                      <code className="bg-muted px-1.5 py-0.5 rounded font-mono">GET /api/snap/status/{"{orderId}"}</code>
                    </p>

                    <div>
                      <p className="text-sm font-medium mb-2">cURL</p>
                      <div className="relative group">
                        <pre className="bg-muted rounded-lg p-4 overflow-x-auto text-sm">
                          <code>{curlCheckStatus}</code>
                        </pre>
                        <Button
                          variant="outline"
                          size="sm"
                          className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity"
                          onClick={() => handleCopySnippet(curlCheckStatus, `${env.id}-curl-status`)}
                        >
                          {copiedSnippet === `${env.id}-curl-status` ? (
                            <CheckCircle className="h-4 w-4 text-green-500" />
                          ) : (
                            <Copy className="h-4 w-4" />
                          )}
                        </Button>
                      </div>
                    </div>

                    <div>
                      <p className="text-sm font-medium mb-2">JavaScript (fetch)</p>
                      <div className="relative group">
                        <pre className="bg-muted rounded-lg p-4 overflow-x-auto text-sm">
                          <code>{fetchCheckStatus}</code>
                        </pre>
                        <Button
                          variant="outline"
                          size="sm"
                          className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity"
                          onClick={() => handleCopySnippet(fetchCheckStatus, `${env.id}-fetch-status`)}
                        >
                          {copiedSnippet === `${env.id}-fetch-status` ? (
                            <CheckCircle className="h-4 w-4 text-green-500" />
                          ) : (
                            <Copy className="h-4 w-4" />
                          )}
                        </Button>
                      </div>
                    </div>
                  </div>
                </TabsContent>
              );
            })}
          </Tabs>
        </div>
      )}

      {/* Edit Dialog */}
      <Dialog open={editDialogOpen} onOpenChange={(open) => {
        setEditDialogOpen(open);
        if (!open) setEditingEnv(null);
      }}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Edit Environment</DialogTitle>
            <DialogDescription>
              Update environment configuration
            </DialogDescription>
          </DialogHeader>
          {editingEnv && (
            <Compo_EnvironmentForm
              applicationId={application.id}
              environment={editingEnv}
              onSuccess={handleEditSuccess}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Test Purchase Confirmation Dialog */}
      <AlertDialog open={testPurchaseEnv !== null} onOpenChange={(open) => { if (!open) setTestPurchaseEnv(null); }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {testPurchaseEnv?.isSandbox ? "Test Purchase" : "⚠ Real Money Warning"}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {testPurchaseEnv?.isSandbox
                ? "This will create a test transaction using Midtrans Sandbox. No real money will be charged."
                : <>This will create a LIVE Midtrans transaction for <strong>{testPurchaseEnv?.name}</strong>. Real money will be charged to the payment method. Are you sure?</>}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className={testPurchaseEnv?.isSandbox ? "" : "bg-destructive text-destructive-foreground hover:bg-destructive/90"}
              onClick={() => testPurchaseEnv && handleTestPurchase(testPurchaseEnv)}
            >
              {testPurchaseEnv?.isSandbox ? "Proceed" : "Yes, Proceed"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
