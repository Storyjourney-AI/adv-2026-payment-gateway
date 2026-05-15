import type { Route } from "./+types/Page_AdminPanel";
import { useAuth } from "@services/auth";
import { useEffect } from "react";
import { useNavigate } from "react-router";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Admin Panel - Payment Gateway" },
    { name: "description", content: "Admin Panel" },
  ];
}

export default function Page_AdminPanel() {
  const { user, hasRole } = useAuth();
  const navigate = useNavigate();

  useEffect(() => {
    if (!hasRole("Super Admin")) {
      navigate("/403");
    }
  }, [hasRole, navigate]);

  if (!hasRole("Super Admin")) {
    return null;
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Admin Panel</h1>
        <p className="text-gray-600 dark:text-gray-400 mt-2">
          Super Admin access required
        </p>
      </div>

      <div className="rounded-lg border bg-card p-6">
        <h3 className="font-semibold mb-4">Administrator Tools</h3>
        <p className="text-sm text-muted-foreground mb-4">
          This page is only accessible to users with the "Super Admin" role.
        </p>
        <div className="space-y-2">
          <p className="text-sm">
            <span className="font-medium">Current User:</span> {user?.email}
          </p>
          <p className="text-sm">
            <span className="font-medium">Roles:</span> {user?.roles.join(", ")}
          </p>
        </div>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        <div className="rounded-lg border bg-card p-6">
          <h4 className="font-semibold mb-2">User Management</h4>
          <p className="text-sm text-muted-foreground">
            Manage system users and permissions
          </p>
        </div>
        <div className="rounded-lg border bg-card p-6">
          <h4 className="font-semibold mb-2">System Settings</h4>
          <p className="text-sm text-muted-foreground">
            Configure application settings
          </p>
        </div>
      </div>
    </div>
  );
}
