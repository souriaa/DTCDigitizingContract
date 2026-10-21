"use client";

import { useState } from "react";
import {
  ChevronLeft,
  ChevronRight,
  FileText,
  FileStack,
  MessageSquareText,
  Package,
  ScrollText,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { TermsListEditor } from "@/components/contract-documents/terms-list-editor";
import { AppendixCard } from "@/components/contract-documents/appendix-card";
import { ContractLegalBases } from "./contract-legal-bases";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { formatCurrency } from "@/lib/format-currency";
import {
  ContractItemDiscountMode,
  ContractVersionDetailResponse,
  getContractItemTypeLabel,
} from "@/services/contract-api";
import { ContractTermComments } from "./contract-term-comments";

const PAGE_SIZE = 5;

function PaginationControls({
  page,
  totalItems,
  label,
  onPageChange,
}: {
  page: number;
  totalItems: number;
  label: string;
  onPageChange: (page: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(totalItems / PAGE_SIZE));
  if (totalItems <= PAGE_SIZE) return null;

  return (
    <div className="mt-4 flex items-center justify-between gap-3 border-t pt-4">
      <Button
        variant="outline"
        size="sm"
        onClick={() => onPageChange(Math.max(1, page - 1))}
        disabled={page === 1}
      >
        <ChevronLeft className="mr-1 size-4" />
        Trước
      </Button>
      <div className="text-center text-xs text-muted-foreground">
        <p className="font-medium text-foreground">
          Trang {page} / {totalPages}
        </p>
        <p>
          {totalItems} {label}
        </p>
      </div>
      <Button
        variant="outline"
        size="sm"
        onClick={() => onPageChange(Math.min(totalPages, page + 1))}
        disabled={page === totalPages}
      >
        Sau
        <ChevronRight className="ml-1 size-4" />
      </Button>
    </div>
  );
}

function EmptyState({
  icon,
  children,
}: {
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="rounded-xl border border-dashed p-10 text-center">
      <div className="mx-auto mb-3 w-fit text-muted-foreground">{icon}</div>
      <p className="font-medium">{children}</p>
    </div>
  );
}

export function ContractVersionSnapshotTabs({
  contractId,
  version,
}: {
  contractId: number;
  version: ContractVersionDetailResponse;
}) {
  const [itemsPage, setItemsPage] = useState(1);
  const [termsPage, setTermsPage] = useState(1);

  const comments = version.comments || [];
  const rootComments = comments.filter((comment) => !comment.parentCommentId);
  const commentTermIds = Array.from(
    new Set(rootComments.map((comment) => comment.termId ?? null)),
  );
  const commentScopes = commentTermIds.map((termId) => {
    const term =
      termId == null
        ? null
        : version.terms.find((item) => item.termId === termId);

    return {
      termId,
      termCode:
        term?.termCode ||
        (termId == null ? undefined : "Điều khoản đã xóa"),
      termTitle:
        term?.termTitle ||
        (termId == null ? undefined : "Điều khoản không còn trong bản lưu"),
    };
  });
  const paginatedItems = version.items.slice(
    (itemsPage - 1) * PAGE_SIZE,
    itemsPage * PAGE_SIZE,
  );
  const paginatedTerms = version.terms.slice(
    (termsPage - 1) * PAGE_SIZE,
    termsPage * PAGE_SIZE,
  );
  return (
    <Tabs defaultValue="items" className="space-y-4">
      <TabsList className="grid h-auto w-full grid-cols-2 lg:grid-cols-5">
        <TabsTrigger value="items">
          <Package className="size-4" />
          <span className="hidden sm:inline">Sản phẩm/Dịch vụ</span>
          <span>({version.items.length})</span>
        </TabsTrigger>
        <TabsTrigger value="terms">
          <ScrollText className="size-4" />
          <span className="hidden sm:inline">Điều khoản</span>
          <span>({version.terms.length})</span>
        </TabsTrigger>
        <TabsTrigger value="appendices">
          <FileStack className="size-4" />
          <span className="hidden sm:inline">Phụ lục</span>
          <span>({version.appendices?.length ?? 0})</span>
        </TabsTrigger>
        <TabsTrigger value="legal-bases">Căn cứ ({version.legalBases.length})</TabsTrigger>
        <TabsTrigger value="comments">
          <MessageSquareText className="size-4" />
          <span className="hidden sm:inline">Bình luận</span>
          <span>({comments.length})</span>
        </TabsTrigger>
      </TabsList>

      <TabsContent value="items" className="space-y-3">
        {version.items.length === 0 ? (
          <EmptyState icon={<Package className="size-8" />}>
            Bản lưu không có sản phẩm hoặc dịch vụ
          </EmptyState>
        ) : (
          paginatedItems.map((item) => (
            <div
              key={item.contractItemId}
              className="overflow-hidden rounded-xl border"
            >
              <div className="flex flex-col gap-2 bg-muted/30 p-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <p className="font-semibold">{item.itemName}</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {item.itemCode || "Không có mã"} ·{" "}
                    {getContractItemTypeLabel(item.itemType)}
                  </p>
                </div>
                <p className="font-semibold text-primary">
                  {formatCurrency(item.lineTotal, version.currencyCode)}
                </p>
              </div>
              <div className="grid gap-3 p-4 text-sm sm:grid-cols-2 lg:grid-cols-4">
                <div>
                  <p className="text-muted-foreground">Số lượng</p>
                  <p className="mt-1 font-medium">
                    {item.quantity} {item.unitName || ""}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Đơn giá</p>
                  <p className="mt-1 font-medium">
                    {formatCurrency(item.unitPrice, version.currencyCode)}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">Chiết khấu</p>
                  <p className="mt-1 font-medium text-emerald-600">
                    {item.discountMode === ContractItemDiscountMode.Percentage
                      ? `${item.discountPercent}% (${formatCurrency(
                          item.discountAmount,
                          version.currencyCode,
                        )})`
                      : item.discountMode ===
                          ContractItemDiscountMode.FixedAmount
                        ? formatCurrency(
                            item.discountAmount,
                            version.currencyCode,
                          )
                        : "Không"}
                  </p>
                </div>
                <div>
                  <p className="text-muted-foreground">VAT</p>
                  <p className="mt-1 font-medium">
                    {item.isTaxable
                      ? `${item.vatPercent}% (${formatCurrency(
                          item.vatAmount,
                          version.currencyCode,
                        )})`
                      : "Không thuế"}
                  </p>
                </div>
              </div>
            </div>
          ))
        )}
        <PaginationControls
          page={itemsPage}
          totalItems={version.items.length}
          label="sản phẩm/dịch vụ"
          onPageChange={setItemsPage}
        />
      </TabsContent>

      <TabsContent value="terms" className="space-y-3">
        {version.terms.length === 0 ? (
          <EmptyState icon={<ScrollText className="size-8" />}>
            Bản lưu không có điều khoản
          </EmptyState>
        ) : (
          <TermsListEditor terms={paginatedTerms} getKey={(term) => term.termId}
            getCardProps={(term) => ({ inputId: `snapshot-${term.termId}`, isBilingual: Boolean(term.termTitleEn || term.termContentEn) })} />
        )}
        <PaginationControls
          page={termsPage}
          totalItems={version.terms.length}
          label="điều khoản"
          onPageChange={setTermsPage}
        />
      </TabsContent>

      <TabsContent value="appendices" className="space-y-4">
        {(version.appendices?.length ?? 0) === 0 ? (
          <EmptyState icon={<FileStack className="size-8" />}>
            Bản lưu không có phụ lục
          </EmptyState>
        ) : (
          [...version.appendices]
            .sort((a, b) => a.displayOrder - b.displayOrder)
            .map((appendix, index) => (
              <AppendixCard key={appendix.appendixId} index={index} code={appendix.appendixCode}
                name={appendix.appendixName} nameEn={appendix.appendixNameEn}
                description={appendix.appendixDescription} required={appendix.isRequired} termCount={appendix.terms.length}>
                <TermsListEditor terms={appendix.terms.map((term) => ({ ...term, isNegotiable: false }))}
                  getKey={(term) => term.appendixTermId} getCardProps={(term) => ({
                    inputId: `snapshot-appendix-${term.appendixTermId}`, isBilingual: Boolean(term.termTitleEn || term.termContentEn),
                    showNegotiation: false,
                  })} />
              </AppendixCard>
            ))
        )}
      </TabsContent>

      <TabsContent value="legal-bases">
        <ContractLegalBases bases={version.legalBases} isBilingual={version.legalBases.some((basis) => Boolean(basis.contentEn))} />
      </TabsContent>

      <TabsContent value="comments" className="space-y-3">
        {commentScopes.length === 0 ? (
          <EmptyState icon={<MessageSquareText className="size-8" />}>
            Phiên bản này chưa có bình luận
          </EmptyState>
        ) : (
          commentScopes.map((scope) => (
            <div
              key={scope.termId ?? "general"}
              className="rounded-xl border bg-muted/10 p-4 transition-colors hover:border-primary/30 sm:p-5"
            >
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="flex min-w-0 items-start gap-3">
                  <div className="rounded-lg bg-primary/10 p-2 text-primary">
                    <FileText className="size-5" />
                  </div>
                  <div className="min-w-0">
                    <p className="font-semibold">
                      {scope.termId == null
                        ? "Trao đổi chung"
                        : scope.termTitle}
                    </p>
                    <p className="mt-1 text-sm text-muted-foreground">
                      {scope.termId == null
                        ? "Nội dung trao đổi chung của hợp đồng"
                        : scope.termCode}
                    </p>
                  </div>
                </div>

                <ContractTermComments
                  contractId={contractId}
                  versionId={version.versionId}
                  termId={scope.termId}
                  termCode={scope.termCode}
                  termTitle={scope.termTitle}
                  comments={comments}
                  canWrite={false}
                  triggerClassName="mt-0 shrink-0 self-start"
                />
              </div>
            </div>
          ))
        )}
      </TabsContent>
    </Tabs>
  );
}
