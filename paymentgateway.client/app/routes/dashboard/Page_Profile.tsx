import { useState } from "react";
import { toast } from "sonner";
import { Loader2, User } from "lucide-react";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { useAuth } from "@services/auth";
import { changePassword } from "@services/auth";

export function meta() {
  return [
    { title: "Profile - Payment Gateway" },
    { name: "description", content: "Manage your profile and password" },
  ];
}

export default function Page_Profile() {
  const { user } = useAuth();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [confirmError, setConfirmError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (newPassword !== confirmPassword) {
      setConfirmError("Passwords do not match");
      return;
    }
    setConfirmError(null);

    if (!user?.email) return;

    setLoading(true);
    try {
      const response = await changePassword({
        email: user.email,
        currentPassword,
        newPassword,
      });

      if (response.success) {
        toast.success("Password changed successfully");
        setCurrentPassword("");
        setNewPassword("");
        setConfirmPassword("");
      } else {
        const message = response.errors?.length
          ? response.errors.join(", ")
          : response.message || "Failed to change password";
        toast.error(message);
      }
    } catch {
      toast.error("An unexpected error occurred");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6 max-w-lg">
      <div>
        <h1 className="text-3xl font-bold">Profile</h1>
        <p className="text-muted-foreground mt-2">Manage your account settings</p>
      </div>

      {/* Account Info */}
      <div className="rounded-lg border bg-card p-6 space-y-3">
        <h2 className="font-semibold">Account</h2>
        <div className="flex items-center gap-3">
          <div className="rounded-full bg-muted p-2">
            <User className="h-5 w-5 text-muted-foreground" />
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Email</p>
            <p className="font-medium">{user?.email}</p>
          </div>
        </div>
      </div>

      {/* Change Password */}
      <div className="rounded-lg border bg-card p-6">
        <h2 className="font-semibold mb-4">Change Password</h2>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="currentPassword">Current Password</Label>
            <Input
              id="currentPassword"
              type="password"
              value={currentPassword}
              onChange={(e) => setCurrentPassword(e.target.value)}
              required
              disabled={loading}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="newPassword">New Password</Label>
            <Input
              id="newPassword"
              type="password"
              value={newPassword}
              onChange={(e) => setNewPassword(e.target.value)}
              required
              disabled={loading}
            />
            <p className="text-xs text-muted-foreground">
              Min. 8 characters with uppercase, lowercase, digit, and special character.
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="confirmPassword">Confirm New Password</Label>
            <Input
              id="confirmPassword"
              type="password"
              value={confirmPassword}
              onChange={(e) => {
                setConfirmPassword(e.target.value);
                if (confirmError) setConfirmError(null);
              }}
              required
              disabled={loading}
            />
            {confirmError && (
              <p className="text-xs text-destructive">{confirmError}</p>
            )}
          </div>

          <Button type="submit" disabled={loading}>
            {loading && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Change Password
          </Button>
        </form>
      </div>
    </div>
  );
}
