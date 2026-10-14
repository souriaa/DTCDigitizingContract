"use client";
import type { ReactNode } from "react";
import { ChevronDown, ChevronRight } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardContent } from "@/components/ui/card";
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from "@/components/ui/collapsible";

export function CatalogGroupPanel({
  name,
  path,
  count,
  subtreeCount,
  open,
  onToggle,
  actions,
  children,
}: {
  name: string;
  path: string;
  count: number;
  subtreeCount: number;
  open: boolean;
  onToggle: () => void;
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <Collapsible open={open} onOpenChange={() => onToggle()} asChild>
      <Card className="min-w-0 gap-0 py-0">
        <CardHeader className="flex flex-row items-center gap-2 p-3">
          <CollapsibleTrigger asChild>
            <Button
              type="button"
              variant="ghost"
              className="h-auto min-w-0 flex-1 justify-start gap-2 p-1 text-left whitespace-normal"
            >
              {open ? (
                <ChevronDown className="size-4 shrink-0" />
              ) : (
                <ChevronRight className="size-4 shrink-0" />
              )}
              <span className="min-w-0 flex-1">
                <span className="block truncate font-medium">{name}</span>
                <span
                  className="block truncate text-xs text-muted-foreground"
                  title={path}
                >
                  {path}
                </span>
              </span>
              <Badge variant="secondary" className="shrink-0">
                {count} trực tiếp
              </Badge>
              {subtreeCount !== count && (
                <span className="hidden text-xs text-muted-foreground sm:inline">
                  {subtreeCount} toàn nhánh
                </span>
              )}
            </Button>
          </CollapsibleTrigger>
          {actions}
        </CardHeader>
        {/* Keep page/selection state mounted; the parent controls lazy loading. */}
        <CollapsibleContent forceMount asChild>
          <CardContent
            hidden={!open}
            className="space-y-3 border-t p-3 data-[state=closed]:hidden"
          >
            {children}
          </CardContent>
        </CollapsibleContent>
      </Card>
    </Collapsible>
  );
}
