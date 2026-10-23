"use client";

import { Suspense, useEffect, useState } from "react";
import Link from "next/link";
import { APP_NAME } from "@/lib/application-branding";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import {
  Building2,
  ChevronLeft,
  ChevronDown,
  ClipboardCheck,
  ListFilter,
  FileBadge,
  FileSignature,
  FileText,
  LibraryBig,
  LogOut,
  Menu,
  Package,
  ScrollText,
  Settings,
  ShieldCheck,
  User,
  Users,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useAuthStore } from "@/hooks/use-auth-store";
import {
  hasAnyPermission,
  RBAC_PERMISSIONS,
  type RbacPermission,
} from "@/lib/rbac";
import { useSidebar } from "./sidebar-context";
import { resolveProfileImageUrl } from "@/services/profile-api";
import { EmployeeType } from "@/services/employees-api";
import { ContractStatus } from "@/services/contract-api";

interface NavItem {
  label: string;
  icon: typeof FileText;
  href: string;
  requiredPermissions?: RbacPermission[];
}

const navItems: NavItem[] = [
  // Dashboard is retained at /dashboard; restore its navigation entry when needed.
  {
    label: "Hợp đồng",
    icon: FileText,
    href: "/contracts",
    requiredPermissions: [
      RBAC_PERMISSIONS.contractReadOwn,
      RBAC_PERMISSIONS.contractReadTenant,
    ],
  },
  {
    label: "Phê duyệt hợp đồng",
    icon: ClipboardCheck,
    href: "/contract-approvals",
    requiredPermissions: [RBAC_PERMISSIONS.contractApprovalDecide],
  },
  {
    label: "Mẫu hợp đồng",
    icon: LibraryBig,
    href: "/admin/contract-templates",
    requiredPermissions: [RBAC_PERMISSIONS.templateManage],
  },
  {
    label: "Báo giá",
    icon: FileSignature,
    href: "/quotations",
    requiredPermissions: [RBAC_PERMISSIONS.quotationManage],
  },
  {
    label: "Khách hàng",
    icon: Users,
    href: "/customers",
    requiredPermissions: [RBAC_PERMISSIONS.customerManage],
  },
  {
    label: "Sản phẩm và dịch vụ",
    icon: Package,
    href: "/catalog/products",
    requiredPermissions: [RBAC_PERMISSIONS.catalogRead],
  },
  {
    label: "Nhân viên",
    icon: User,
    href: "/admin/employees",
    requiredPermissions: [RBAC_PERMISSIONS.employeeManage],
  },
  {
    label: "Phòng ban",
    icon: Building2,
    href: "/admin/departments",
    requiredPermissions: [RBAC_PERMISSIONS.departmentManage],
  },
  {
    label: "Hồ sơ pháp lý",
    icon: FileBadge,
    href: "/admin/legal-profile",
    requiredPermissions: [RBAC_PERMISSIONS.tenantLegalProfileManage],
  },
  {
    label: "Nhật ký hợp đồng",
    icon: ScrollText,
    href: "/contract-audits",
    requiredPermissions: [RBAC_PERMISSIONS.contractAuditReadTenant],
  },
  {
    label: "Nhật ký bảo mật",
    icon: ShieldCheck,
    href: "/security-audits",
    requiredPermissions: [RBAC_PERMISSIONS.securityAuditReadTenant],
  },
];

const contractStatusItems = [
  { label: "Tất cả", status: "All" },
  { label: "Đã ký kết", status: String(ContractStatus.Signed) },
  { label: "Đang đàm phán", status: String(ContractStatus.Negotiating) },
  { label: "Chờ duyệt", status: String(ContractStatus.PendingApproval) },
  { label: "Đã duyệt, đang chờ ký", status: String(ContractStatus.PendingSignature) },
  { label: "Dừng gia hạn", status: "stopped-renewal" },
  { label: "Thất bại", status: "failed" },
  { label: "Thành công", status: String(ContractStatus.Completed) },
];

function SidebarContent() {
  const { isExpanded, setIsExpanded } = useSidebar();
  const [isMobileOpen, setIsMobileOpen] = useState(false);
  const [isContractsOpen, setIsContractsOpen] = useState(true);
  const [isStatusOpen, setIsStatusOpen] = useState(false);
  const { user, logout } = useAuthStore();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const selectedStatus = searchParams.get("status") || "All";
  const canBrowseStatuses = !!user && [EmployeeType.Manager, EmployeeType.AdminOfficer, EmployeeType.Sale, EmployeeType.Technical].includes(user.employeeType)
    && hasAnyPermission(user.permissions, [RBAC_PERMISSIONS.contractReadOwn, RBAC_PERMISSIONS.contractReadTenant]);

  useEffect(() => {
    setIsMobileOpen(false);
  }, [pathname, selectedStatus]);

  const handleLogout = () => {
    logout();
    router.push("/");
  };

  const visibleItems = navItems.filter((item) =>
    !item.requiredPermissions || hasAnyPermission(user?.permissions, item.requiredPermissions),
  );
  const contractItems = visibleItems.filter((item) =>
    !["/quotations", "/catalog/products"].includes(item.href),
  );
  const isItemActive = (item: NavItem) =>
    pathname === item.href || pathname.startsWith(`${item.href}/`) ||
    (item.href === "/catalog/products" && pathname.startsWith("/catalog/"));
  const renderNavLink = (item: NavItem, nested = false) => {
    const Icon = item.icon;
    const active = isItemActive(item);
    return (
      <Link key={item.href} href={item.href} title={item.label}
        aria-current={active ? "page" : undefined}
        className={`flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors ${nested ? "ml-4" : ""} ${active ? "bg-primary/10 text-primary font-semibold" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}>
        <Icon className="h-5 w-5 shrink-0" />
        <span className={isExpanded || isMobileOpen ? "" : "lg:hidden"}>{item.label}</span>
      </Link>
    );
  };

  const renderNavItems = () => (
    <nav className="flex-1 px-3 py-4 space-y-1.5 flex flex-col items-stretch overflow-y-auto">
      {contractItems.length > 0 && (
        <div>
          <button type="button" title="Hợp đồng" aria-expanded={isContractsOpen && (isExpanded || isMobileOpen)}
            aria-label="Mở hoặc thu gọn menu Hợp đồng"
            onClick={() => {
              if (!isExpanded && !isMobileOpen) {
                setIsExpanded(true);
                setIsContractsOpen(true);
              } else setIsContractsOpen(!isContractsOpen);
            }}
            className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors ${contractItems.some(isItemActive) ? "bg-primary/10 text-primary font-semibold" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}>
            <FileText className="h-5 w-5 shrink-0" />
            <span className={`flex-1 text-left ${isExpanded || isMobileOpen ? "" : "lg:hidden"}`}>Hợp đồng</span>
            <ChevronDown className={`h-4 w-4 transition-transform ${isContractsOpen ? "rotate-180" : ""} ${isExpanded || isMobileOpen ? "" : "lg:hidden"}`} />
          </button>
          {isContractsOpen && (isExpanded || isMobileOpen) && (
            <div className="mt-1 space-y-1 border-l border-border ml-5">
              {contractItems.map((item) => renderNavLink(
                item.href === "/contracts" ? { ...item, label: "Danh sách hợp đồng" } : item, true,
              ))}
            </div>
          )}
        </div>
      )}
      {canBrowseStatuses && <div>
        <button type="button" title="Trạng thái hợp đồng" aria-label="Mở hoặc thu gọn Trạng thái hợp đồng"
          aria-expanded={isStatusOpen && (isExpanded || isMobileOpen)}
          onClick={() => {
            if (!isExpanded && !isMobileOpen) { setIsExpanded(true); setIsStatusOpen(true); }
            else setIsStatusOpen(!isStatusOpen);
          }}
          className={`flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-colors ${pathname === "/contracts" && searchParams.has("status") ? "bg-primary/10 text-primary font-semibold" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}>
          <ListFilter className="h-5 w-5 shrink-0" />
          <span className={`flex-1 text-left ${isExpanded || isMobileOpen ? "" : "lg:hidden"}`}>Trạng thái hợp đồng</span>
          <ChevronDown className={`h-4 w-4 shrink-0 transition-transform ${isStatusOpen ? "rotate-180" : ""} ${isExpanded || isMobileOpen ? "" : "lg:hidden"}`} />
        </button>
        {isStatusOpen && (isExpanded || isMobileOpen) && <div className="mt-1 space-y-1 border-l border-border ml-5">
          {contractStatusItems.map((item) => <Link key={item.status} href={item.status === "All" ? "/contracts" : `/contracts?status=${item.status}`}
                aria-current={pathname === "/contracts" && selectedStatus === item.status ? "page" : undefined}
                className={`ml-4 block rounded-xl px-3 py-2.5 text-sm transition-colors ${pathname === "/contracts" && selectedStatus === item.status ? "bg-primary/10 text-primary font-semibold" : "text-muted-foreground hover:bg-accent hover:text-foreground"}`}>
                {item.label}
              </Link>)}
        </div>}
      </div>}
      {visibleItems.filter((item) => ["/quotations", "/catalog/products"].includes(item.href)).map((item) => renderNavLink(item))}
    </nav>
  );

  return (
    <>
      <button
        onClick={() => setIsMobileOpen(true)}
        className="fixed top-3 left-4 z-40 p-2 lg:hidden hover:bg-accent rounded-xl transition-colors text-muted-foreground bg-card shadow-sm border border-border"
      >
        <Menu className="h-5 w-5" />
      </button>

      <aside
        className={`relative hidden lg:flex h-screen bg-card border-r border-border flex-col flex-shrink-0 transition-all duration-300 ease-in-out z-20 ${
          isExpanded ? "w-64" : "w-20"
        }`}
      >
        <div
          className={`h-16 border-b border-border p-4 flex items-center transition-all duration-300 ${
            isExpanded ? "justify-between" : "justify-center"
          }`}
        >
          {isExpanded && (
            <div className="flex items-center gap-2.5 overflow-hidden animate-in fade-in duration-300">
              <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center flex-shrink-0 shadow-sm shadow-primary/20">
                <FileText className="w-4 h-4 text-primary-foreground" />
              </div>
              <div className="min-w-0">
                <h1 className="text-sm font-bold leading-tight text-foreground truncate">
                  {APP_NAME}
                </h1>
                <p className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider truncate">
                  Quản lý
                </p>
              </div>
            </div>
          )}
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setIsExpanded(!isExpanded)}
            className="text-muted-foreground hover:text-foreground hover:bg-accent h-8 w-8 rounded-lg flex-shrink-0"
          >
            <ChevronLeft
              className={`h-5 w-5 transition-transform duration-300 ${
                isExpanded ? "" : "rotate-180"
              }`}
            />
          </Button>
        </div>

        {renderNavItems()}
      </aside>

      {isMobileOpen && (
        <>
          <button
            type="button"
            aria-label="Đóng menu"
            className="fixed inset-0 bg-black/40 backdrop-blur-sm z-50 lg:hidden animate-in fade-in duration-200"
            onClick={() => setIsMobileOpen(false)}
          />

          <aside className="fixed left-0 top-0 h-screen w-[280px] bg-card z-50 flex flex-col animate-in slide-in-from-left duration-300 shadow-2xl lg:hidden">
            <div className="h-16 border-b border-border p-4 flex items-center justify-between shrink-0">
              <div className="flex items-center gap-2.5">
                <div className="w-8 h-8 bg-primary rounded-lg flex items-center justify-center flex-shrink-0 shadow-sm shadow-primary/20">
                  <FileText className="w-4 h-4 text-primary-foreground" />
                </div>
                <div>
                  <h1 className="text-sm font-bold leading-tight text-foreground">
                    {APP_NAME}
                  </h1>
                  <p className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">
                    Quản lý
                  </p>
                </div>
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setIsMobileOpen(false)}
                className="text-muted-foreground hover:text-foreground hover:bg-accent h-8 w-8 rounded-lg flex-shrink-0"
              >
                <X className="h-5 w-5" />
              </Button>
            </div>

            {renderNavItems()}

            <div className="border-t border-border p-4 shrink-0">
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <button className="w-full flex items-center gap-3 px-2 py-2 rounded-xl hover:bg-accent transition-colors justify-start focus:outline-none">
                    <Avatar className="size-10 shadow-sm">
                      {user?.imageUrl && (
                        <AvatarImage
                          src={resolveProfileImageUrl(user.imageUrl)}
                          alt={user.fullName || "Người dùng"}
                          className="object-cover"
                        />
                      )}
                      <AvatarFallback className="bg-primary text-sm font-bold text-primary-foreground">
                        {user?.fullName?.[0]?.toUpperCase() ?? "U"}
                      </AvatarFallback>
                    </Avatar>
                    <div className="flex-1 text-left min-w-0">
                      <p className="text-sm font-semibold text-foreground truncate">
                        {user?.fullName || "Người dùng"}
                      </p>
                      <p className="text-xs text-muted-foreground truncate">
                        {[user?.roleName, user?.tenantName]
                          .filter(Boolean)
                          .join(" • ") || "Chưa cập nhật vai trò"}
                      </p>
                    </div>
                  </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent
                  align="end"
                  side="top"
                  className="w-64 rounded-xl z-60"
                >
                  <DropdownMenuItem asChild className="cursor-pointer py-2.5">
                    <Link href="/profile">
                      <User className="mr-2 h-4 w-4 text-muted-foreground" />
                      <span className="font-medium">Hồ sơ cá nhân</span>
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuItem asChild className="cursor-pointer py-2.5">
                    <Link href="/change-password">
                      <Settings className="mr-2 h-4 w-4 text-muted-foreground" />
                      <span className="font-medium">Đổi mật khẩu</span>
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    className="cursor-pointer py-2.5 text-destructive focus:text-destructive dark:focus:bg-destructive/10 focus:bg-destructive/10"
                    onClick={handleLogout}
                  >
                    <LogOut className="mr-2 h-4 w-4" />
                    <span className="font-medium">Đăng xuất</span>
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </aside>
        </>
      )}
    </>
  );
}

export function Sidebar() {
  return <Suspense fallback={null}><SidebarContent /></Suspense>;
}
