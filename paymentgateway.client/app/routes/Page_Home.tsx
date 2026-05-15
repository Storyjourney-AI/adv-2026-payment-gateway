import type { Route } from "./+types/Page_Home";
import { Button } from "~/components/ui/button";
import { Link } from "react-router";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Payment Gateway - Home" },
    { name: "description", content: "Payment Gateway" },
  ];
}

export default function Page_Home() {
  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-gray-50 to-gray-100 dark:from-gray-900 dark:to-gray-800">
      <div className="text-center space-y-6 p-8">
        <h1 className="text-6xl font-bold text-gray-900 dark:text-gray-100" data-testid="home-title">
          Hey, this is the front page! 🎉
        </h1>
        <p className="text-xl text-gray-600 dark:text-gray-400" data-testid="home-description">
          Welcome to Payment Gateway
        </p>
        <div className="flex gap-4 justify-center">
          <Link to="/login">
            <Button data-testid="home-login-button" size="lg">
              Login
            </Button>
          </Link>
          <Button data-testid="home-get-started-button" size="lg" variant="outline">
            Get Started
          </Button>
        </div>
      </div>
    </div>
  );
}
