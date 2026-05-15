import type { Route } from "./+types/Page_401";
import { Button } from "~/components/ui/button";
import { Link } from "react-router";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "401 - Unauthorized" },
    { name: "description", content: "Unauthorized" },
  ];
}

export default function Unauthorized() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-gray-50 to-gray-100 dark:from-gray-900 dark:to-gray-800">
      <div className="text-center space-y-6 p-8">
        <h1 className="text-9xl font-bold text-gray-900 dark:text-gray-100" data-testid="error-code">
          401
        </h1>
        <h2 className="text-3xl font-semibold text-gray-700 dark:text-gray-300" data-testid="error-title">
          Unauthorized
        </h2>
        <p className="text-xl text-gray-600 dark:text-gray-400" data-testid="error-description">
          You need to be authenticated to access this resource.
        </p>
        <div className="flex gap-4 justify-center">
          <Link to="/login">
            <Button data-testid="error-login-button" size="lg">
              Login
            </Button>
          </Link>
          <Link to="/">
            <Button data-testid="error-home-button" variant="outline" size="lg">
              Back to Home
            </Button>
          </Link>
        </div>
      </div>
    </div>
  );
}
