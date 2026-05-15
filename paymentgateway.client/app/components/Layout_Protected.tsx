/**
 * Layout_Protected
 * 
 * Protected route guard component.
 * Validates authentication before rendering child routes.
 * Redirects to login if not authenticated.
 */

import { useEffect, useState } from "react";
import { Navigate, Outlet } from "react-router";
import { useAuth } from "@services/auth";

export default function Layout_Protected() {
  const { isAuthenticated, isLoading, initializeAuth } = useAuth();
  const [isChecking, setIsChecking] = useState(true);

  useEffect(() => {
    // Initialize auth on component mount
    initializeAuth()
      .finally(() => {
        setIsChecking(false);
      });
  }, [initializeAuth]);

  // Show loading spinner while checking authentication
  if (isChecking || isLoading) {
    return (
      <div 
        className="flex min-h-screen items-center justify-center"
        data-testid="auth-loading-spinner"
      >
        <div className="text-center">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-solid border-current border-r-transparent align-[-0.125em] motion-reduce:animate-[spin_1.5s_linear_infinite]">
            <span className="sr-only">Loading...</span>
          </div>
          <p className="mt-4 text-gray-600 dark:text-gray-400">
            Checking authentication...
          </p>
        </div>
      </div>
    );
  }

  // Redirect to 401 if not authenticated
  if (!isAuthenticated) {
    console.log('[Layout_Protected] User not authenticated, redirecting to 401');
    return <Navigate to="/401" replace />;
  }

  // Render protected content
  return <Outlet />;
}
