"use client";

import { ScrollText } from "lucide-react";

import { ContractAuditLog } from "@/components/contracts/contract-audit-log";
import { Header } from "@/components/ui/custom/header";
import { PermissionGuard } from "@/components/auth/permission-guard";
import { RBAC_PERMISSIONS } from "@/lib/rbac";

export default function ContractAuditsPage() {
  return (
    <>
      <Header title="Nhật ký hợp đồng" />
      <div className="grow overflow-y-auto p-3 sm:p-6 lg:p-10">
        <div className="mx-auto space-y-6">
          <div className="flex items-start gap-3">
            <div>
              <h1 className="text-2xl font-bold tracking-tight">
                Nhật ký hợp đồng
              </h1>
              <p className="mt-1 text-sm text-muted-foreground">
                Tra cứu hoạt động hợp đồng theo người thực hiện, hành động, kết
                quả và thời gian.
              </p>
            </div>
          </div>

          <PermissionGuard
            permission={RBAC_PERMISSIONS.contractAuditReadTenant}
          >
            <ContractAuditLog mode="tenant" />
          </PermissionGuard>
        </div>
      </div>
    </>
  );
}
