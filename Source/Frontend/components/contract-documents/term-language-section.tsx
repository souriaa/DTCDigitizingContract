import type { ReactNode } from "react";

import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const languageStyles = {
  en: {
    code: "EN",
    label: "Tiếng Anh",
    border: "border-sky-200 dark:border-sky-900",
    header: "bg-sky-50 text-sky-900 dark:bg-sky-950/40 dark:text-sky-200",
    badge:
      "border-sky-200 bg-background text-sky-800 dark:border-sky-800 dark:text-sky-200",
  },
  vi: {
    code: "VI",
    label: "Tiếng Việt",
    border: "border-emerald-200 dark:border-emerald-900",
    header:
      "bg-emerald-50 text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-200",
    badge:
      "border-emerald-200 bg-background text-emerald-800 dark:border-emerald-800 dark:text-emerald-200",
  },
  combined: {
    code: "EN / VI",
    label: "Nội dung song ngữ",
    border: "border-violet-200 dark:border-violet-900",
    header:
      "bg-violet-50 text-violet-900 dark:bg-violet-950/40 dark:text-violet-200",
    badge:
      "border-violet-200 bg-background text-violet-800 dark:border-violet-800 dark:text-violet-200",
  },
} as const;

export function TermLanguageSection({
  language,
  className,
  children,
}: {
  language: keyof typeof languageStyles;
  className?: string;
  children: ReactNode;
}) {
  const style = languageStyles[language];

  return (
    <section
      aria-label={style.label}
      className={cn(
        "min-w-0 rounded-xl border bg-background",
        style.border,
        className,
      )}
    >
      <div
        className={cn(
          "flex items-center gap-2 rounded-t-xl border-b border-inherit px-3 py-2",
          style.header,
        )}
      >
        <Badge
          variant="outline"
          className={cn("font-mono text-[11px]", style.badge)}
        >
          {style.code}
        </Badge>
        <span className="text-sm font-semibold">{style.label}</span>
      </div>
      <div className="min-w-0 space-y-3 p-3 sm:p-4">{children}</div>
    </section>
  );
}
