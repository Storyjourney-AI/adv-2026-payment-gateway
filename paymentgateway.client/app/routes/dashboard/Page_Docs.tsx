import type { Route } from "./+types/Page_Docs";
import { useState } from "react";
import { CheckCircle, Copy, BookOpen } from "lucide-react";
import { Button } from "~/components/ui/button";
import { Badge } from "~/components/ui/badge";

export function meta({}: Route.MetaArgs) {
  return [
    { title: "API Documentation - Payment Gateway" },
    { name: "description", content: "Full API reference for the Payment Gateway" },
  ];
}

function CodeBlock({ code, language = "bash" }: { code: string; language?: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(code);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard API not available or permission denied
    }
  };

  return (
    <div className="relative group">
      <pre className="bg-muted rounded-lg p-4 overflow-x-auto text-sm">
        <code className={`language-${language}`}>{code}</code>
      </pre>
      <Button
        variant="outline"
        size="sm"
        className="absolute top-2 right-2 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity"
        onClick={handleCopy}
      >
        {copied ? (
          <CheckCircle className="h-4 w-4 text-green-500" />
        ) : (
          <Copy className="h-4 w-4" />
        )}
      </Button>
    </div>
  );
}

export default function Page_Docs() {
  const baseUrl = window.location.origin;

  return (
    <div className="max-w-4xl mx-auto space-y-10">
      <div>
        <div className="flex items-center gap-3 mb-2">
          <BookOpen className="h-8 w-8" />
          <h1 className="text-3xl font-bold">API Documentation</h1>
        </div>
        <p className="text-muted-foreground">
          Complete reference for integrating your application with the Payment Gateway.
        </p>
      </div>

      {/* Table of Contents */}
      <nav className="rounded-lg border bg-card p-6">
        <h2 className="text-lg font-semibold mb-3">Table of Contents</h2>
        <ul className="space-y-1 text-sm">
          <li><a href="#authentication" className="text-primary hover:underline">1. Authentication</a></li>
          <li><a href="#response-envelope" className="text-primary hover:underline">2. Standard Response Envelope</a></li>
          <li><a href="#endpoints" className="text-primary hover:underline">3. Endpoint Reference</a></li>
          <li className="ml-4"><a href="#create-token" className="text-primary hover:underline">3.1 Create Payment Token</a></li>
          <li className="ml-4"><a href="#check-status" className="text-primary hover:underline">3.2 Check Payment Status</a></li>
          <li className="ml-4"><a href="#cancel-payment" className="text-primary hover:underline">3.3 Cancel Payment</a></li>
          <li><a href="#webhooks" className="text-primary hover:underline">4. Webhook Handling</a></li>
          <li><a href="#order-id-rules" className="text-primary hover:underline">5. Order ID Rules</a></li>
        </ul>
      </nav>

      {/* Authentication Section */}
      <section id="authentication" className="space-y-4">
        <h2 className="text-2xl font-bold border-b pb-2">1. Authentication</h2>
        <p>
          All child-app endpoints require an <code className="bg-muted px-1.5 py-0.5 rounded text-sm font-mono">X-Api-Key</code> HTTP header for authentication.
        </p>
        <p>
          You can find your API key on the <strong>Application Detail page</strong> → select your environment card → copy the API key.
        </p>
        <h3 className="text-lg font-semibold">Example Header</h3>
        <CodeBlock code="X-Api-Key: your_api_key_here" />
        <p className="text-sm text-muted-foreground">
          Include this header in every request to the payment gateway API.
        </p>
      </section>

      {/* Standard Response Envelope */}
      <section id="response-envelope" className="space-y-4">
        <h2 className="text-2xl font-bold border-b pb-2">2. Standard Response Envelope</h2>
        <p>
          All API responses are wrapped in a <code className="bg-muted px-1.5 py-0.5 rounded text-sm font-mono">DataWrapper&lt;T&gt;</code> envelope:
        </p>
        <CodeBlock
          language="json"
          code={`{
  "success": true,
  "message": "Operation completed successfully",
  "data": { ... },
  "errors": null
}`}
        />
        <div className="rounded-lg border bg-card p-4">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="text-left py-2 pr-4">Field</th>
                <th className="text-left py-2 pr-4">Type</th>
                <th className="text-left py-2">Description</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-b">
                <td className="py-2 pr-4 font-mono">success</td>
                <td className="py-2 pr-4">boolean</td>
                <td className="py-2">Whether the request was successful</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4 font-mono">message</td>
                <td className="py-2 pr-4">string</td>
                <td className="py-2">Human-readable status message</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4 font-mono">data</td>
                <td className="py-2 pr-4">T | null</td>
                <td className="py-2">Response payload (null on failure)</td>
              </tr>
              <tr>
                <td className="py-2 pr-4 font-mono">errors</td>
                <td className="py-2 pr-4">string[] | null</td>
                <td className="py-2">Validation error details (null on success)</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      {/* Endpoint Reference */}
      <section id="endpoints" className="space-y-8">
        <h2 className="text-2xl font-bold border-b pb-2">3. Endpoint Reference</h2>

        <div className="rounded-lg border bg-card p-4">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="text-left py-2 pr-4">Endpoint</th>
                <th className="text-left py-2 pr-4">Method</th>
                <th className="text-left py-2">Description</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-b">
                <td className="py-2 pr-4 font-mono">/api/snap/token</td>
                <td className="py-2 pr-4"><Badge>POST</Badge></td>
                <td className="py-2">Create a Snap payment token</td>
              </tr>
              <tr className="border-b">
                <td className="py-2 pr-4 font-mono">/api/snap/status/{"{orderId}"}</td>
                <td className="py-2 pr-4"><Badge variant="secondary">GET</Badge></td>
                <td className="py-2">Check payment status</td>
              </tr>
              <tr>
                <td className="py-2 pr-4 font-mono">/api/snap/cancel/{"{orderId}"}</td>
                <td className="py-2 pr-4"><Badge>POST</Badge></td>
                <td className="py-2">Cancel a pending payment</td>
              </tr>
            </tbody>
          </table>
        </div>

        {/* POST /api/snap/token */}
        <div id="create-token" className="space-y-4">
          <h3 className="text-xl font-semibold">3.1 Create Payment Token</h3>
          <div className="flex items-center gap-2">
            <Badge>POST</Badge>
            <code className="bg-muted px-2 py-1 rounded text-sm font-mono">/api/snap/token</code>
          </div>

          <p>Creates a Midtrans Snap payment token and returns a redirect URL for the payment page.</p>

          <h4 className="font-semibold">Required Headers</h4>
          <CodeBlock code="X-Api-Key: your_api_key_here" />

          <h4 className="font-semibold">Request Body</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Field</th>
                  <th className="text-left py-2 pr-4">Type</th>
                  <th className="text-left py-2 pr-4">Required</th>
                  <th className="text-left py-2">Constraints</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">orderId</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2 pr-4">Yes</td>
                  <td className="py-2">Unique per environment, max 42 characters</td>
                </tr>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">grossAmount</td>
                  <td className="py-2 pr-4">number</td>
                  <td className="py-2 pr-4">Yes</td>
                  <td className="py-2">Positive integer (in IDR)</td>
                </tr>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">customerDetails</td>
                  <td className="py-2 pr-4">object</td>
                  <td className="py-2 pr-4">No</td>
                  <td className="py-2">Customer information (see below)</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-mono">itemDetails</td>
                  <td className="py-2 pr-4">array</td>
                  <td className="py-2 pr-4">No</td>
                  <td className="py-2">List of purchased items (see below)</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">customerDetails Object</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Field</th>
                  <th className="text-left py-2 pr-4">Type</th>
                  <th className="text-left py-2">Description</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">firstName</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">Customer first name</td>
                </tr>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">lastName</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">Customer last name</td>
                </tr>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">email</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">Customer email address</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-mono">phone</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">Customer phone number</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">itemDetails Array Items</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Field</th>
                  <th className="text-left py-2 pr-4">Type</th>
                  <th className="text-left py-2">Description</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">id</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">Item identifier</td>
                </tr>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">price</td>
                  <td className="py-2 pr-4">number</td>
                  <td className="py-2">Price per unit (in IDR)</td>
                </tr>
                <tr className="border-b">
                  <td className="py-2 pr-4 font-mono">quantity</td>
                  <td className="py-2 pr-4">number</td>
                  <td className="py-2">Number of items</td>
                </tr>
                <tr>
                  <td className="py-2 pr-4 font-mono">name</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">Name of the item</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">Success Response (200)</h4>
          <CodeBlock
            language="json"
            code={`{
  "success": true,
  "message": "Snap token created successfully",
  "data": {
    "token": "snap-token-string",
    "redirectUrl": "https://app.midtrans.com/snap/v4/redirection/..."
  },
  "errors": null
}`}
          />

          <h4 className="font-semibold">Error Codes</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Code</th>
                  <th className="text-left py-2">Meaning</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">400</td><td className="py-2">Invalid request body or missing required fields</td></tr>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">401</td><td className="py-2">Missing or invalid API key</td></tr>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">409</td><td className="py-2">Duplicate order ID for this environment</td></tr>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">422</td><td className="py-2">Validation failed (e.g., amount not positive)</td></tr>
                <tr><td className="py-2 pr-4 font-mono">502</td><td className="py-2">Midtrans API error</td></tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">cURL Example</h4>
          <CodeBlock
            code={`curl -X POST ${baseUrl}/api/snap/token \\
  -H "Content-Type: application/json" \\
  -H "X-Api-Key: your_api_key_here" \\
  -d '{
    "orderId": "order-001",
    "grossAmount": 50000,
    "customerDetails": {
      "firstName": "John",
      "lastName": "Doe",
      "email": "john@example.com",
      "phone": "08123456789"
    },
    "itemDetails": [
      {
        "id": "item-1",
        "price": 50000,
        "quantity": 1,
        "name": "Premium Subscription"
      }
    ]
  }'`}
          />
        </div>

        {/* GET /api/snap/status/{orderId} */}
        <div id="check-status" className="space-y-4">
          <h3 className="text-xl font-semibold">3.2 Check Payment Status</h3>
          <div className="flex items-center gap-2">
            <Badge variant="secondary">GET</Badge>
            <code className="bg-muted px-2 py-1 rounded text-sm font-mono">/api/snap/status/{"{orderId}"}</code>
          </div>

          <p>Retrieves the current status of a payment by order ID.</p>

          <h4 className="font-semibold">Required Headers</h4>
          <CodeBlock code="X-Api-Key: your_api_key_here" />

          <h4 className="font-semibold">Path Parameters</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Parameter</th>
                  <th className="text-left py-2 pr-4">Type</th>
                  <th className="text-left py-2">Description</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="py-2 pr-4 font-mono">orderId</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">The order ID used when creating the payment token</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">Success Response (200)</h4>
          <CodeBlock
            language="json"
            code={`{
  "success": true,
  "message": "Transaction status retrieved",
  "data": {
    "callerOrderId": "order-001",
    "midtransOrderId": "a1b2c3d4_order-001",
    "transactionStatus": "settlement",
    "fraudStatus": "accept",
    "grossAmount": "50000.00",
    "transactionId": "midtrans-txn-id",
    "paymentType": "bank_transfer",
    "transactionTime": "2026-01-15 10:30:00"
  },
  "errors": null
}`}
          />

          <h4 className="font-semibold">Error Codes</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Code</th>
                  <th className="text-left py-2">Meaning</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">401</td><td className="py-2">Missing or invalid API key</td></tr>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">404</td><td className="py-2">Order ID not found for this environment</td></tr>
                <tr><td className="py-2 pr-4 font-mono">502</td><td className="py-2">Midtrans API error</td></tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">cURL Example</h4>
          <CodeBlock
            code={`curl -X GET ${baseUrl}/api/snap/status/order-001 \\
  -H "X-Api-Key: your_api_key_here"`}
          />
        </div>

        {/* POST /api/snap/cancel/{orderId} */}
        <div id="cancel-payment" className="space-y-4">
          <h3 className="text-xl font-semibold">3.3 Cancel Payment</h3>
          <div className="flex items-center gap-2">
            <Badge>POST</Badge>
            <code className="bg-muted px-2 py-1 rounded text-sm font-mono">/api/snap/cancel/{"{orderId}"}</code>
          </div>

          <p>Cancels a pending payment. Only payments with status <code className="bg-muted px-1.5 py-0.5 rounded text-sm font-mono">pending</code> can be cancelled.</p>

          <h4 className="font-semibold">Required Headers</h4>
          <CodeBlock code="X-Api-Key: your_api_key_here" />

          <h4 className="font-semibold">Path Parameters</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Parameter</th>
                  <th className="text-left py-2 pr-4">Type</th>
                  <th className="text-left py-2">Description</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="py-2 pr-4 font-mono">orderId</td>
                  <td className="py-2 pr-4">string</td>
                  <td className="py-2">The order ID of the pending payment to cancel</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">Success Response (200)</h4>
          <CodeBlock
            language="json"
            code={`{
  "success": true,
  "message": "Transaction cancelled successfully",
  "data": {
    "callerOrderId": "order-001",
    "midtransOrderId": "a1b2c3d4_order-001",
    "transactionStatus": "cancel",
    "grossAmount": "50000.00",
    "transactionId": "midtrans-txn-id"
  },
  "errors": null
}`}
          />

          <h4 className="font-semibold">Error Codes</h4>
          <div className="rounded-lg border bg-card p-4">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b">
                  <th className="text-left py-2 pr-4">Code</th>
                  <th className="text-left py-2">Meaning</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">401</td><td className="py-2">Missing or invalid API key</td></tr>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">404</td><td className="py-2">Order ID not found for this environment</td></tr>
                <tr className="border-b"><td className="py-2 pr-4 font-mono">409</td><td className="py-2">Transaction is not in a cancellable state</td></tr>
                <tr><td className="py-2 pr-4 font-mono">502</td><td className="py-2">Midtrans API error</td></tr>
              </tbody>
            </table>
          </div>

          <h4 className="font-semibold">cURL Example</h4>
          <CodeBlock
            code={`curl -X POST ${baseUrl}/api/snap/cancel/order-001 \\
  -H "X-Api-Key: your_api_key_here"`}
          />
        </div>
      </section>

      {/* Webhook Section */}
      <section id="webhooks" className="space-y-4">
        <h2 className="text-2xl font-bold border-b pb-2">4. Webhook Handling</h2>
        <p>
          When a payment status changes, Midtrans sends a notification to the gateway. The gateway stores the updated status and forwards the raw Midtrans notification payload to the <code className="bg-muted px-1.5 py-0.5 rounded text-sm font-mono">WebhookUrl</code> configured on your environment.
        </p>

        <h3 className="text-lg font-semibold">Notification Payload</h3>
        <p>The forwarded payload contains the raw Midtrans notification. Key fields include:</p>
        <CodeBlock
          language="json"
          code={`{
  "order_id": "a1b2c3d4_order-001",
  "transaction_status": "settlement",
  "fraud_status": "accept",
  "gross_amount": "50000.00",
  "transaction_id": "midtrans-txn-id"
}`}
        />

        <div className="rounded-lg border bg-card p-4 space-y-3">
          <h4 className="font-semibold">Important Notes</h4>
          <ul className="list-disc list-inside space-y-2 text-sm">
            <li>
              The <code className="bg-muted px-1.5 py-0.5 rounded font-mono">order_id</code> in the forwarded payload is the Midtrans-prefixed ID (format: <code className="bg-muted px-1.5 py-0.5 rounded font-mono">{"{envId[0..8]}"}_{"{callerOrderId}"}</code>), not your raw caller order ID.
            </li>
            <li>
              Your webhook endpoint <strong>must return a 2xx status code</strong> to acknowledge receipt of the notification.
            </li>
            <li>
              <strong>SSRF Guard:</strong> The <code className="bg-muted px-1.5 py-0.5 rounded font-mono">WebhookUrl</code> must be a valid HTTPS URL pointing to a non-loopback, non-private IP address.
            </li>
          </ul>
        </div>

        <h3 className="text-lg font-semibold">Transaction Statuses</h3>
        <div className="rounded-lg border bg-card p-4">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b">
                <th className="text-left py-2 pr-4">Status</th>
                <th className="text-left py-2">Description</th>
              </tr>
            </thead>
            <tbody>
              <tr className="border-b"><td className="py-2 pr-4 font-mono">pending</td><td className="py-2">Payment initiated, waiting for customer action</td></tr>
              <tr className="border-b"><td className="py-2 pr-4 font-mono">settlement</td><td className="py-2">Payment completed successfully</td></tr>
              <tr className="border-b"><td className="py-2 pr-4 font-mono">cancel</td><td className="py-2">Payment was cancelled</td></tr>
              <tr className="border-b"><td className="py-2 pr-4 font-mono">deny</td><td className="py-2">Payment was denied</td></tr>
              <tr><td className="py-2 pr-4 font-mono">expire</td><td className="py-2">Payment expired without completion</td></tr>
            </tbody>
          </table>
        </div>
      </section>

      {/* Order ID Rules */}
      <section id="order-id-rules" className="space-y-4">
        <h2 className="text-2xl font-bold border-b pb-2">5. Order ID Rules</h2>
        <div className="rounded-lg border bg-card p-4 space-y-3">
          <ul className="list-disc list-inside space-y-2 text-sm">
            <li>
              <strong>Uniqueness:</strong> The <code className="bg-muted px-1.5 py-0.5 rounded font-mono">orderId</code> must be unique per environment. Reusing an order ID for the same API key returns <Badge variant="destructive">409 Conflict</Badge>.
            </li>
            <li>
              <strong>Max Length:</strong> The <code className="bg-muted px-1.5 py-0.5 rounded font-mono">orderId</code> can be at most <strong>42 characters</strong> long.
            </li>
          </ul>
        </div>
      </section>
    </div>
  );
}
