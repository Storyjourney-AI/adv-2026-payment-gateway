import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useState } from "react";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { Badge } from "~/components/ui/badge";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import { createEnvironment, updateEnvironment } from "@services/application";
import type { Dto_EnvironmentResponse } from "@services/application";

const environmentSchema = z.object({
  name: z.string().min(3, "Name must be at least 3 characters"),
  allowedOrigins: z.string().optional(),
  webhookUrl: z.string().url("Must be a valid URL").optional().or(z.literal("")),
  successResponseUrl: z.string().url("Must be a valid URL").min(1, "Success URL is required"),
  failureResponseUrl: z.string().url("Must be a valid URL").min(1, "Failure URL is required"),
});

type EnvironmentFormData = z.infer<typeof environmentSchema>;

interface Compo_EnvironmentFormProps {
  applicationId: string;
  environment?: Dto_EnvironmentResponse;
  onSuccess: () => void;
}

export function Compo_EnvironmentForm({
  applicationId,
  environment,
  onSuccess,
}: Compo_EnvironmentFormProps) {
  const isEditing = !!environment;
  const [isSandbox, setIsSandbox] = useState<boolean>(environment?.isSandbox ?? true);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<EnvironmentFormData>({
    resolver: zodResolver(environmentSchema),
    defaultValues: {
      name: environment?.name || "",
      allowedOrigins: environment?.allowedOrigins || "",
      webhookUrl: environment?.webhookUrl || "",
      successResponseUrl: environment?.successResponseUrl || "",
      failureResponseUrl: environment?.failureResponseUrl || "",
    },
  });

  const onSubmit = async (data: EnvironmentFormData) => {
    try {
      const payload = {
        applicationId,
        name: data.name,
        allowedOrigins: data.allowedOrigins || undefined,
        webhookUrl: data.webhookUrl || undefined,
        successResponseUrl: data.successResponseUrl,
        failureResponseUrl: data.failureResponseUrl,
        ...(isEditing && { isSandbox }),
      };

      const response = isEditing
        ? await updateEnvironment(environment.id, payload)
        : await createEnvironment(payload);

      if (response.success) {
        onSuccess();
      } else {
        toast.error(response.message || "Failed to save environment");
      }
    } catch (err) {
      toast.error("An unexpected error occurred");
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="name">Environment Name *</Label>
        <Input
          id="name"
          {...register("name")}
          placeholder="e.g., Production, Staging, Development"
          disabled={isSubmitting}
        />
        {errors.name && (
          <p className="text-sm text-destructive">{errors.name.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="allowedOrigins">Allowed Origins</Label>
        <Input
          id="allowedOrigins"
          {...register("allowedOrigins")}
          placeholder="https://example.com,https://app.example.com"
          disabled={isSubmitting}
        />
        <p className="text-xs text-muted-foreground">
          Comma-separated list of allowed origins for CORS
        </p>
        {errors.allowedOrigins && (
          <p className="text-sm text-destructive">{errors.allowedOrigins.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="successResponseUrl">Success URL *</Label>
        <Input
          id="successResponseUrl"
          {...register("successResponseUrl")}
          placeholder="https://example.com/payment/success"
          disabled={isSubmitting}
        />
        <p className="text-xs text-muted-foreground">
          URL to redirect users after successful payment
        </p>
        {errors.successResponseUrl && (
          <p className="text-sm text-destructive">{errors.successResponseUrl.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="failureResponseUrl">Failure URL *</Label>
        <Input
          id="failureResponseUrl"
          {...register("failureResponseUrl")}
          placeholder="https://example.com/payment/failure"
          disabled={isSubmitting}
        />
        <p className="text-xs text-muted-foreground">
          URL to redirect users after failed payment
        </p>
        {errors.failureResponseUrl && (
          <p className="text-sm text-destructive">{errors.failureResponseUrl.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="webhookUrl">Webhook URL</Label>
        <Input
          id="webhookUrl"
          {...register("webhookUrl")}
          placeholder="https://example.com/webhooks/payment"
          disabled={isSubmitting}
        />
        <p className="text-xs text-muted-foreground">
          Optional URL to receive payment status webhooks
        </p>
        {errors.webhookUrl && (
          <p className="text-sm text-destructive">{errors.webhookUrl.message}</p>
        )}
      </div>

      {isEditing && (
        <div className="space-y-2">
          <Label>Midtrans Environment</Label>
          <div className="flex items-center gap-3">
            <button
              type="button"
              role="switch"
              aria-checked={isSandbox}
              onClick={() => setIsSandbox((v) => !v)}
              disabled={isSubmitting}
              className={`relative inline-flex h-6 w-11 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50 ${
                isSandbox ? "bg-secondary" : "bg-destructive"
              }`}
            >
              <span
                className={`pointer-events-none block h-5 w-5 rounded-full bg-white shadow-lg ring-0 transition-transform ${
                  isSandbox ? "translate-x-0" : "translate-x-5"
                }`}
              />
            </button>
            {isSandbox
              ? <Badge variant="secondary">Sandbox</Badge>
              : <Badge variant="destructive">Production</Badge>
            }
          </div>
          <p className="text-xs text-muted-foreground">
            {isSandbox
              ? "Test transactions — uses Midtrans Sandbox (no real money)"
              : "Live transactions — uses Midtrans Production (real money)"}
          </p>
        </div>
      )}

      <div className="flex justify-end gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {isEditing ? "Update Environment" : "Create Environment"}
        </Button>
      </div>
    </form>
  );
}
