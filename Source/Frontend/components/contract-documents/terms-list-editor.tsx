"use client";

import { useState, type Key } from "react";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  ContractTermCard,
  type ContractTermCardProps,
  type ContractTermCardValue,
} from "@/components/contracts/contract-term-card";

/** Chỉ quản lý UI danh sách/đóng mở; API, bản nháp và quyền thuộc màn sử dụng. */
export function TermsListEditor<T extends ContractTermCardValue>({
  terms,
  getKey,
  getCardProps,
  title,
  description,
  onAdd,
  addLabel = "Thêm điều khoản",
  emptyText = "Chưa có điều khoản.",
}: {
  terms: T[];
  getKey: (term: T) => Key;
  getCardProps: (term: T, index: number) => Omit<ContractTermCardProps, "term">;
  title?: string;
  description?: string;
  onAdd?: () => void;
  addLabel?: string;
  emptyText?: string;
}) {
  const [openKeys, setOpenKeys] = useState<Set<Key>>(new Set());
  return (
    <div className="space-y-4">
      {title && (
        <div className="flex flex-col justify-between gap-3 rounded-xl border bg-muted/20 p-4 sm:flex-row sm:items-center">
          <div>
            <p className="font-semibold">
              {title} ({terms.length})
            </p>
            {description && (
              <p className="mt-1 text-sm text-muted-foreground">
                {description}
              </p>
            )}
          </div>
          {onAdd && (
            <Button type="button" onClick={onAdd}>
              <Plus className="size-4" />
              {addLabel}
            </Button>
          )}
        </div>
      )}
      {terms.map((term, index) => {
        const key = getKey(term);
        const props = getCardProps(term, index);
        return (
          <ContractTermCard
            key={key}
            term={term}
            canMoveUp={index > 0}
            canMoveDown={index < terms.length - 1}
            open={openKeys.has(key)}
            onOpenChange={(open) =>
              setOpenKeys((current) => {
                const next = new Set(current);
                if (open) next.add(key);
                else next.delete(key);
                return next;
              })
            }
            {...props}
          />
        );
      })}
      {terms.length === 0 && (
        <div className="rounded-xl border border-dashed py-10 text-center text-sm text-muted-foreground">
          {emptyText}
        </div>
      )}
    </div>
  );
}
