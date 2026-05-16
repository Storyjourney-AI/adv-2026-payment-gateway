import type { Route } from "./+types/Page_Login";
import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate } from "react-router";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import { Label } from "~/components/ui/label";
import { useAuth } from "@services/auth";
import type { Dto_LoginRequest } from "@services/auth";
import { isTurnstileEnabled, TURNSTILE_WIDGET_HOST_ID } from "@services/auth/utils/turnstile.client";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Login - Payment Gateway" },
    { name: "description", content: "Login to Payment Gateway" },
  ];
}

const loginSchema = z.object({
  email: z.string().email("Invalid email address"),
  password: z.string().min(1, "Password is required"),
});

type LoginFormData = z.infer<typeof loginSchema>;

export default function Page_Login() {
  const navigate = useNavigate();
  const { login, isAuthenticated, isLoading, error: authError } = useAuth();
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);
  const showTurnstile = isTurnstileEnabled();

  const loginForm = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
    mode: "onBlur",
  });

  // Redirect to dashboard if already authenticated
  useEffect(() => {
    if (isAuthenticated) {
      console.log('[Page_Login] User already authenticated, redirecting to dashboard');
      navigate("/dashboard");
    }
  }, [isAuthenticated, navigate]);

  // Update message from auth hook errors
  useEffect(() => {
    if (authError) {
      setMessage({ type: "error", text: authError });
    }
  }, [authError]);

  const handleLogin = async (data: LoginFormData) => {
    setMessage(null);

    try {
      const request: Dto_LoginRequest = {
        email: data.email,
        password: data.password,
      };

      await login(request);

      // Success - hook handles state update and redirect via useEffect
      setMessage({ type: "success", text: "Login successful! Redirecting..." });
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : "Login failed";
      setMessage({ 
        type: "error", 
        text: errorMessage
      });
    }
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-screen p-4 bg-gray-50 dark:bg-gray-900">
      <div className="w-full max-w-md p-8 space-y-6 bg-white dark:bg-gray-800 rounded-lg shadow-md">
        <div className="text-center">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-gray-100" data-testid="auth-title">
            Welcome to Payment Gateway
          </h1>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400" data-testid="auth-subtitle">
            Please login to continue
          </p>
        </div>

        {message && (
          <div
            className={`p-3 rounded-md ${
              message.type === "success"
                ? "bg-green-50 text-green-800 dark:bg-green-900/20 dark:text-green-400"
                : "bg-red-50 text-red-800 dark:bg-red-900/20 dark:text-red-400"
            }`}
            data-testid="auth-message"
          >
            {message.text}
          </div>
        )}

        <form onSubmit={loginForm.handleSubmit(handleLogin)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="login-email" data-testid="login-email-label">
              Email
            </Label>
            <Input
              id="login-email"
              type="email"
              placeholder="your@email.com"
              data-testid="login-email-input"
              {...loginForm.register("email")}
            />
            {loginForm.formState.errors.email && (
              <p className="text-sm text-red-600 dark:text-red-400" data-testid="login-email-error">
                {loginForm.formState.errors.email.message}
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="login-password" data-testid="login-password-label">
              Password
            </Label>
            <Input
              id="login-password"
              type="password"
              placeholder="••••••••"
              data-testid="login-password-input"
              {...loginForm.register("password")}
            />
            {loginForm.formState.errors.password && (
              <p className="text-sm text-red-600 dark:text-red-400" data-testid="login-password-error">
                {loginForm.formState.errors.password.message}
              </p>
            )}
          </div>

          {showTurnstile && (
            <div className="space-y-2">
              <Label htmlFor={TURNSTILE_WIDGET_HOST_ID}>Verification</Label>
              <div
                id={TURNSTILE_WIDGET_HOST_ID}
                className="min-h-[72px] rounded-md border border-gray-200 bg-gray-50 p-3 dark:border-gray-700 dark:bg-gray-900"
                data-testid="turnstile-widget-host"
              />
            </div>
          )}

          <Button
            type="submit"
            className="w-full"
            disabled={isLoading}
            data-testid="login-submit-button"
          >
            {isLoading ? "Logging in..." : "Login"}
          </Button>
        </form>
      </div>
    </div>
  );
}
