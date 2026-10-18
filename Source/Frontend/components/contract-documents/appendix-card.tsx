"use client";

import type { ReactNode } from "react";
import { ChevronDown } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";

export function AppendixCard({
  index,
  code,
  name,
  nameEn,
  description,
  required,
  selectedByDefault,
  dirty,
  termCount,
  actions,
  children,
  className,
  open,
  defaultOpen,
  onOpenChange,
}: {
  index: number;
  code: string;
  name: string;
  nameEn?: string | null;
  description?: string | null;
  required: boolean;
  selectedByDefault?: boolean;
  dirty?: boolean;
  termCount: number;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
}) {
  return (
    <Collapsible
      open={open}
      defaultOpen={defaultOpen}
      onOpenChange={onOpenChange}
    >
      <Card className={className}>
        <CardHeader className="gap-3">
          <div className="flex flex-col justify-between gap-3 sm:flex-row sm:items-start">
            <div className="min-w-0 space-y-2">
              <div className="flex flex-wrap items-center gap-2">
                <Badge variant="outline">Phụ lục {index + 1}</Badge>
                <Badge variant={required ? "default" : "secondary"}>
                  {required ? "Bắt buộc" : "Tùy chọn"}
                </Badge>
                {!required && selectedByDefault && (
                  <Badge variant="secondary">Chọn mặc định</Badge>
                )}
                {dirty && <Badge variant="destructive">Chưa lưu</Badge>}
                <span className="font-mono text-xs text-muted-foreground">
                  {code}
                </span>
              </div>
              <h3 className="font-semibold">{name}</h3>
              {nameEn && (
                <p className="text-sm text-muted-foreground">{nameEn}</p>
              )}
              {description && (
                <p className="text-sm text-muted-foreground">{description}</p>
              )}
              <p className="text-xs text-muted-foreground">
                {termCount} điều khoản
              </p>
            </div>
            <div className="flex shrink-0 flex-wrap items-center gap-1">
              {actions}
              <CollapsibleTrigger asChild>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="group"
                  aria-label={`Thu gọn hoặc mở rộng phụ lục ${name}`}
                >
                  <ChevronDown className="size-4 transition-transform group-data-[state=open]:rotate-180" />
                </Button>
              </CollapsibleTrigger>
            </div>
          </div>
        </CardHeader>
        <CollapsibleContent>
          <CardContent className="space-y-4">{children}</CardContent>
        </CollapsibleContent>
      </Card>
    </Collapsible>
  );
}
