import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Textarea } from "~/components/ui/textarea";
import { Label } from "~/components/ui/label";
import { Loader2 } from "lucide-react";
import { useApplications } from "@services/application";
import type { Dto_ApplicationListItem } from "@services/application";

const applicationSchema = z.object({
  name: z.string().min(3, "Name must be at least 3 characters"),
  description: z.string().optional(),
});

type ApplicationFormData = z.infer<typeof applicationSchema>;

interface Compo_ApplicationFormProps {
  application?: Dto_ApplicationListItem;
  onSuccess: () => void;
}

export function Compo_ApplicationForm({ application, onSuccess }: Compo_ApplicationFormProps) {
  const { createApplication, updateApplication } = useApplications();
  const isEditing = !!application;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ApplicationFormData>({
    resolver: zodResolver(applicationSchema),
    defaultValues: {
      name: application?.name || "",
      description: application?.description || "",
    },
  });

  const onSubmit = async (data: ApplicationFormData) => {
    try {
      const response = isEditing
        ? await updateApplication(application.id, data)
        : await createApplication(data);

      if (response.success) {
        onSuccess();
      } else {
        alert(response.message || "Failed to save application");
      }
    } catch (err) {
      alert("An unexpected error occurred");
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="name">Name *</Label>
        <Input
          id="name"
          {...register("name")}
          placeholder="My Application"
          disabled={isSubmitting}
        />
        {errors.name && (
          <p className="text-sm text-destructive">{errors.name.message}</p>
        )}
      </div>

      <div className="space-y-2">
        <Label htmlFor="description">Description</Label>
        <Textarea
          id="description"
          {...register("description")}
          placeholder="Optional description for this application"
          rows={3}
          disabled={isSubmitting}
        />
        {errors.description && (
          <p className="text-sm text-destructive">{errors.description.message}</p>
        )}
      </div>

      <div className="flex justify-end gap-2">
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
          {isEditing ? "Update Application" : "Create Application"}
        </Button>
      </div>
    </form>
  );
}
