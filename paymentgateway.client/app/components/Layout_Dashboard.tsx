import { Link, Outlet, useNavigate } from "react-router";
import { useAuth } from "@services/auth";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarSeparator,
  SidebarTrigger,
} from "~/components/ui/sidebar";
import { Button } from "~/components/ui/button";
import { Home, Shield, LogOut, User, CreditCard, UserCog, BookOpen, Receipt, ScrollText } from "lucide-react";

export default function Layout_Dashboard() {
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();

  const handleLogout = async () => {
    await logout();
    navigate("/login");
  };

  const menuItems = [
    {
      title: "Dashboard",
      url: "/dashboard",
      icon: Home,
    },
    {
      title: "Applications",
      url: "/dashboard/applications",
      icon: CreditCard,
    },
    {
      title: "Transactions",
      url: "/dashboard/transactions",
      icon: Receipt,
    },
    {
      title: "Profile",
      url: "/dashboard/profile",
      icon: UserCog,
    },
    {
      title: "Docs",
      url: "/dashboard/docs",
      icon: BookOpen,
    },
  ];

  const adminMenuItems = [
    {
      title: "Admin Panel",
      url: "/dashboard/admin",
      icon: Shield,
    },
    {
      title: "Activity Logs",
      url: "/dashboard/activity-logs",
      icon: ScrollText,
    },
  ];

  return (
    <SidebarProvider>
      <div className="flex h-screen w-full">
        <Sidebar>
          <SidebarHeader className="border-b px-4 py-4">
            <h2 className="text-lg font-semibold">Payment Gateway</h2>
          </SidebarHeader>

          <SidebarContent>
            {/* Main Menu */}
            <SidebarGroup>
              <SidebarGroupLabel>Menu</SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu>
                  {menuItems.map((item) => (
                    <SidebarMenuItem key={item.title}>
                      <SidebarMenuButton asChild>
                        <Link to={item.url}>
                          <item.icon className="h-4 w-4" />
                          <span>{item.title}</span>
                        </Link>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  ))}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>

            {/* Admin Menu - Only shown to Super Admin or Admin */}
            {(hasRole("Super Admin") || hasRole("Admin")) && (
              <>
                <SidebarSeparator />
                <SidebarGroup>
                  <SidebarGroupLabel>Administration</SidebarGroupLabel>
                  <SidebarGroupContent>
                    <SidebarMenu>
                      {adminMenuItems.map((item) => (
                        <SidebarMenuItem key={item.title}>
                          <SidebarMenuButton asChild>
                            <Link to={item.url}>
                              <item.icon className="h-4 w-4" />
                              <span>{item.title}</span>
                            </Link>
                          </SidebarMenuButton>
                        </SidebarMenuItem>
                      ))}
                    </SidebarMenu>
                  </SidebarGroupContent>
                </SidebarGroup>
              </>
            )}
          </SidebarContent>

          <SidebarFooter className="border-t p-4">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <User className="h-4 w-4" />
                <span className="text-sm truncate">{user?.email}</span>
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={handleLogout}
                title="Logout"
              >
                <LogOut className="h-4 w-4" />
              </Button>
            </div>
          </SidebarFooter>
        </Sidebar>

        <main className="flex-1 overflow-auto">
          <header className="border-b p-4">
            <SidebarTrigger />
          </header>
          <div className="p-6">
            <Outlet />
          </div>
        </main>
      </div>
    </SidebarProvider>
  );
}

