import type { Route } from "./+types/Page_PaymentFailed";
import { Button } from "~/components/ui/button";
import { Link, useSearchParams } from "react-router";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Payment Failed - Payment Gateway" },
    { name: "description", content: "Payment failed or was cancelled" },
  ];
}

export default function Page_PaymentFailed() {
  const [searchParams] = useSearchParams();
  const orderId = searchParams.get("order_id");
  const statusCode = searchParams.get("status_code");
  const transactionStatus = searchParams.get("transaction_status");

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-rose-50 to-white px-4 dark:from-rose-950 dark:to-gray-950">
      <div className="w-full max-w-2xl rounded-2xl border border-rose-200 bg-white p-8 shadow-xl dark:border-rose-900 dark:bg-gray-900">
        <div className="space-y-4 text-center">
          <p className="text-sm font-medium uppercase tracking-[0.2em] text-rose-600 dark:text-rose-400">
            Payment Not Completed
          </p>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-gray-100">
            Payment failed or was cancelled
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            The payment was not completed. Review the transaction details below and retry from your application if needed.
          </p>
        </div>

        <div className="mt-8 grid gap-4 rounded-xl bg-rose-50 p-5 text-sm dark:bg-rose-950/40 sm:grid-cols-3">
          <div>
            <p className="text-muted-foreground">Order ID</p>
            <p className="mt-1 break-all font-medium text-gray-900 dark:text-gray-100">{orderId || "-"}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Status Code</p>
            <p className="mt-1 font-medium text-gray-900 dark:text-gray-100">{statusCode || "-"}</p>
          </div>
          <div>
            <p className="text-muted-foreground">Transaction Status</p>
            <p className="mt-1 font-medium capitalize text-gray-900 dark:text-gray-100">{transactionStatus || "-"}</p>
          </div>
        </div>

        <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:justify-center">
          <Link to="/">
            <Button size="lg" className="w-full sm:w-auto">
              Back to Home
            </Button>
          </Link>
          <Link to="/login">
            <Button size="lg" variant="outline" className="w-full sm:w-auto">
              Go to Login
            </Button>
          </Link>
        </div>
      </div>
    </div>
  );
}