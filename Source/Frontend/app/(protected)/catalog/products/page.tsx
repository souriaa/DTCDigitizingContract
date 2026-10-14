import { Suspense } from "react";
import { CatalogWorkspace } from "@/components/catalog/catalog-workspace";

export default function CatalogPage() {
  return (
    <Suspense fallback={<p className="p-6">Đang tải danh mục...</p>}>
      <CatalogWorkspace />
    </Suspense>
  );
}
