import type { Route } from "./+types/Page_PaymentPending";
import { Button } from "~/components/ui/button";
import { Link, useSearchParams } from "react-router";

type DetailItem = {
  label: string;
  value: string;
};

function getPendingContent(transactionStatus: string | null) {
  if (transactionStatus === "pending" || transactionStatus === "unfinish") {
    return {
      eyebrow: "Payment Still Open",
      title: "Payment still in progress",
      description:
        "Your payment has not been finalized yet. Return to the original application to continue or wait for the status to finish updating.",
    };
  }

  return {
    eyebrow: "Payment Status Updated",
    title: "Payment status updated",
    description:
      "We received a browser callback for this transaction. Review the details below and continue from the original application if more action is needed.",
  };
}

export function meta({}: Route.MetaArgs) {
  return [
    { title: "Payment Pending - Payment Gateway" },
    { name: "description", content: "Payment is still in progress" },
  ];
}

export default function Page_PaymentPending() {
  const [searchParams] = useSearchParams();
  const callerOrderId = searchParams.get("caller_order_id");
  const orderId = searchParams.get("order_id");
  const statusCode = searchParams.get("status_code");
  const transactionStatus = searchParams.get("transaction_status");

  const primaryReference = callerOrderId || orderId;
  const detailItems: DetailItem[] = [
    ...(primaryReference
      ? [
          {
            label: "Reference ID",
            value: primaryReference,
          },
        ]
      : []),
    ...(transactionStatus
      ? [
          {
            label: "Transaction Status",
            value: transactionStatus,
          },
        ]
      : []),
    ...(statusCode
      ? [
          {
            label: "Status Code",
            value: statusCode,
          },
        ]
      : []),
    ...(callerOrderId && orderId
      ? [
          {
            label: "Gateway Order ID",
            value: orderId,
          },
        ]
      : []),
  ];
  const content = getPendingContent(transactionStatus);

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-amber-50 to-white px-4 dark:from-amber-950/40 dark:to-gray-950">
      <div className="w-full max-w-2xl rounded-2xl border border-amber-200 bg-white p-8 shadow-xl dark:border-amber-900/70 dark:bg-gray-900">
        <div className="space-y-4 text-center">
          <p className="text-sm font-medium uppercase tracking-[0.2em] text-amber-700 dark:text-amber-300">
            {content.eyebrow}
          </p>
          <h1 className="text-4xl font-bold text-gray-900 dark:text-gray-100">
            {content.title}
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            {content.description}
          </p>
        </div>

        {detailItems.length > 0 && (
          <div className="mt-8 rounded-xl bg-amber-50 p-5 text-sm dark:bg-amber-950/30">
            <div className="grid gap-4 sm:grid-cols-2">
              {detailItems.map((item) => (
                <div key={item.label}>
                  <p className="text-muted-foreground">{item.label}</p>
                  <p className="mt-1 break-all font-medium text-gray-900 dark:text-gray-100">
                    {item.value}
                  </p>
                </div>
              ))}
            </div>
          </div>
        )}

        <div className="mt-6 rounded-xl border border-amber-200/70 bg-amber-50/60 p-5 text-sm text-amber-950 dark:border-amber-900/60 dark:bg-amber-950/20 dark:text-amber-100">
          <p>Payment confirmation can still arrive later, even if this page appears before the final update.</p>
          <p className="mt-2">If your original application asks you to continue payment, resume there instead of starting a new payment from scratch.</p>
          <p className="mt-2">Only retry after your application or support team confirms that this transaction will not complete.</p>
        </div>

        <div className="mt-8 flex flex-col gap-3 sm:flex-row sm:justify-center">
          <Button asChild size="lg" className="w-full sm:w-auto">
            <Link to="/">Back to Home</Link>
          </Button>
          <Button asChild size="lg" variant="outline" className="w-full sm:w-auto">
            <Link to="/login">Go to Login</Link>
          </Button>
        </div>
      </div>
    </div>
  );
}