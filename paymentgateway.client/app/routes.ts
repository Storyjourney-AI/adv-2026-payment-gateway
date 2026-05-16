import { type RouteConfig, index, route, layout } from "@react-router/dev/routes";

export default [
  index("routes/Page_Home.tsx"),
  route("login", "routes/auth/Page_Login.tsx"),
  route("payment/success", "routes/payment/Page_PaymentSuccess.tsx"),
  route("payment/pending", "routes/payment/Page_PaymentPending.tsx"),
  route("payment/failed", "routes/payment/Page_PaymentFailed.tsx"),
  route("400", "routes/Page_400.tsx"),
  route("401", "routes/Page_401.tsx"),
  route("403", "routes/Page_403.tsx"),
  
  // Protected routes - require authentication
  layout("components/Layout_Protected.tsx", [
    layout("components/Layout_Dashboard.tsx", [
      route("dashboard", "routes/dashboard/Page_Dashboard.tsx"),
      route("dashboard/admin", "routes/dashboard/Page_AdminPanel.tsx"),
      route("dashboard/applications", "routes/dashboard/Page_Applications.tsx"),
      route("dashboard/applications/:id", "routes/dashboard/Page_ApplicationDetail.tsx"),
      route("dashboard/transactions", "routes/dashboard/Page_Transactions.tsx"),
      route("dashboard/activity-logs", "routes/dashboard/Page_ActivityLogs.tsx"),
      route("dashboard/profile", "routes/dashboard/Page_Profile.tsx"),
      route("dashboard/docs", "routes/dashboard/Page_Docs.tsx"),
    ]),
  ]),
] satisfies RouteConfig;
