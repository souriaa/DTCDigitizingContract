"use client";

import type { ReactNode } from "react";
import { ChevronDown } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";

export function DocumentRecordSwitcher({
  title,
  count,
  selectedCode,
  onOpenChange,
  children,
}: {
  title: string;
  count: number;
  selectedCode?: string;
  onOpenChange?: (open: boolean) => void;
  children: ReactNode;
}) {
  return (
    <Collapsible defaultOpen={false} onOpenChange={onOpenChange} className="rounded-lg border">
      <CollapsibleTrigger asChild>
        <Button
          type="button"
          variant="ghost"
          className="group/record-switcher h-auto w-full justify-between gap-3 whitespace-normal p-3 text-left"
        >
          <span className="min-w-0 space-y-1">
            <span className="flex flex-wrap items-center gap-2 font-semibold">
              {title}
              <Badge variant="secondary">{count}</Badge>
            </span>
            {selectedCode && (
              <span className="block break-words text-xs font-normal text-muted-foreground">
                Đang xem: {selectedCode}
              </span>
            )}
          </span>
          <ChevronDown
            aria-hidden="true"
            className="size-4 shrink-0 transition-transform duration-200 group-data-[state=open]/record-switcher:-rotate-180 motion-reduce:transition-none"
          />
        </Button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        <div className="space-y-3 border-t p-3">{children}</div>
      </CollapsibleContent>
    </Collapsible>
  );
}
