"use client";

import { ChevronDown } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import { LegalBasisFields } from "@/components/contract-documents/legal-basis-fields";
import type { ContractLegalBasisResponse } from "@/services/contract-api";

export function ContractLegalBases({
  bases,
  isBilingual,
  canEdit = false,
  onChange,
}: {
  bases: ContractLegalBasisResponse[];
  isBilingual: boolean;
  canEdit?: boolean;
  onChange?: (
    legalBasisId: number,
    field: "contentVi" | "contentEn",
    value: string,
  ) => void;
}) {
  return (
    <div className="space-y-4">
      <div className="rounded-xl border bg-muted/20 p-4">
        <h2 className="font-semibold">Căn cứ hợp đồng ({bases.length})</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          {canEdit
            ? "Chỉnh sửa nội dung căn cứ rồi lưu bằng nút lưu chung hoặc Ctrl+S. Thay đổi không ảnh hưởng mẫu gốc."
            : "Căn cứ thuộc riêng phiên bản hợp đồng này. Nội dung đang ở chế độ chỉ xem."}
        </p>
      </div>
      {[...bases]
        .sort((a, b) => a.displayOrder - b.displayOrder)
        .map((basis, index) => (
          <Collapsible key={basis.legalBasisId} defaultOpen>
            <Card className="gap-3 p-4 shadow-xs">
              <div className="flex items-center justify-between gap-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="outline">Căn cứ {index + 1}</Badge>
                  <span className="font-mono text-xs text-muted-foreground">
                    {basis.basisCode}
                  </span>
                </div>
                <CollapsibleTrigger asChild>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="group"
                    aria-label={`Thu gọn hoặc mở rộng căn cứ ${index + 1}`}
                  >
                    <ChevronDown className="size-4 transition-transform group-data-[state=open]:rotate-180" />
                  </Button>
                </CollapsibleTrigger>
              </div>
              <CollapsibleContent>
                <LegalBasisFields
                  id={`contract-basis-${basis.legalBasisId}`}
                  value={basis}
                  isBilingual={isBilingual}
                  disabled={!canEdit}
                  codeDisabled
                  onChange={(field, next) => {
                    if (field !== "basisCode")
                      onChange?.(basis.legalBasisId, field, next);
                  }}
                />
              </CollapsibleContent>
            </Card>
          </Collapsible>
        ))}
      {bases.length === 0 && (
        <p className="rounded-xl border border-dashed p-8 text-center text-sm text-muted-foreground">
          Phiên bản hợp đồng này chưa có căn cứ được sao chép từ mẫu.
        </p>
      )}
    </div>
  );
}
