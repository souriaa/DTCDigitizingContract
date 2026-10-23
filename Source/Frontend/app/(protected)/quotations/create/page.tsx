"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { isAxiosError } from "axios";
import { Plus, Trash2 } from "lucide-react";
import { Header } from "@/components/ui/custom/header";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { IntegerInput } from "@/components/ui/custom/integer-input";
import { DecimalInput } from "@/components/ui/custom/decimal-input";
import { PermissionGuard } from "@/components/auth/permission-guard";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import { formatCurrency } from "@/lib/format-currency";
import { customerApi, type CustomerResponse } from "@/services/customers-api";
import { productApi, type ProductResponse } from "@/services/catalog/products-api";
import { quotationApi } from "@/services/quotations-api";
import { toast } from "@/components/ui/sonner";

type Line = { productId: number; productName: string; quantity: number; unitPrice: number };
const selectClass = "h-10 w-full rounded-md border border-input bg-background px-3 text-sm";

function CreateQuotationContent() {
  const router = useRouter();
  const [customers, setCustomers] = useState<CustomerResponse[]>([]);
  const [products, setProducts] = useState<ProductResponse[]>([]);
  const [customerQuery, setCustomerQuery] = useState("");
  const [productQuery, setProductQuery] = useState("");
  const [customerPage, setCustomerPage] = useState(1);
  const [productPage, setProductPage] = useState(1);
  const [customerPages, setCustomerPages] = useState(1);
  const [productPages, setProductPages] = useState(1);
  const [customerId, setCustomerId] = useState("");
  const [customerLabel, setCustomerLabel] = useState("");
  const [productId, setProductId] = useState("");
  const [lines, setLines] = useState<Line[]>([]);
  const [saving, setSaving] = useState(false);
  const [customerLoading, setCustomerLoading] = useState(true);
  const [productLoading, setProductLoading] = useState(true);
  const [lookupError, setLookupError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    const timer = setTimeout(async () => {
      setCustomerLoading(true);
      try {
        const result = await customerApi.getList({ page: customerPage, pageSize: 20, status: 1, keyword: customerQuery });
        if (!cancelled) { setCustomers(result.items); setCustomerPages(result.totalPages); }
      } catch { if (!cancelled) setLookupError("Không thể tải khách hàng. Kiểm tra quyền truy cập và thử tải lại trang."); }
      finally { if (!cancelled) setCustomerLoading(false); }
    }, 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [customerQuery, customerPage]);

  useEffect(() => {
    let cancelled = false;
    const timer = setTimeout(async () => {
      setProductLoading(true);
      try {
        const result = await productApi.getList({ page: productPage, pageSize: 20, status: 1, keyword: productQuery });
        if (!cancelled) { setProducts(result.items); setProductPages(result.totalPages); }
      } catch { if (!cancelled) setLookupError("Không thể tải sản phẩm. Kiểm tra quyền truy cập và thử tải lại trang."); }
      finally { if (!cancelled) setProductLoading(false); }
    }, 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [productQuery, productPage]);

  const updateLine = (id: number, changes: Partial<Line>) => setLines((previous) => previous.map((line) => line.productId === id ? { ...line, ...changes } : line));
  const total = lines.reduce((sum, line) => sum + line.quantity * line.unitPrice, 0);
  const addProduct = () => {
    const product = products.find((entry) => entry.productId === Number(productId));
    if (!product) return;
    if (lines.some((line) => line.productId === product.productId)) { toast.info("Sản phẩm đã có trong báo giá."); return; }
    setLines((previous) => [...previous, { productId: product.productId, productName: product.productName || product.productCode || `SP-${product.productId}`, quantity: 1, unitPrice: product.productPrice || 0 }]);
    setProductId("");
  };
  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (saving) return;
    if (!customerId || !lines.length || lines.some((line) => !Number.isInteger(line.quantity) || line.quantity < 1 || line.quantity > 2147483647 || !Number.isFinite(line.unitPrice) || line.unitPrice < 0)) {
      setError("Chọn khách hàng, thêm ít nhất một sản phẩm và nhập số lượng, đơn giá hợp lệ."); return;
    }
    if (!Number.isFinite(total) || total >= 1e16) {
      setError("Tổng giá trị báo giá vượt giới hạn lưu trữ. Vui lòng giảm số lượng hoặc đơn giá."); return;
    }
    setSaving(true); setError(null);
    try {
      const result = await quotationApi.create({ customerId: Number(customerId), quotationItems: lines.map(({ productId, quantity, unitPrice }) => ({ productId, quantity, unitPrice })) });
      toast.success("Tạo báo giá thành công");
      router.push(`/quotations/${result.quotationId}`);
    } catch (cause) {
      setError(isAxiosError(cause) && typeof cause.response?.data?.message === "string" ? cause.response.data.message : "Không thể tạo báo giá. Vui lòng thử lại.");
      setSaving(false);
    }
  };

  return <><Header /><main className="grow overflow-y-auto p-4 lg:p-10 space-y-6">
    <Link href="/quotations" className="text-sm text-primary">← Quay lại danh sách báo giá</Link>
    <h1 className="text-2xl font-bold">Tạo báo giá mới</h1>
    <p className="text-sm text-muted-foreground">Mã, ngày và trạng thái bản nháp được tạo tự động khi lưu.</p>
    {lookupError && <p role="alert" className="text-destructive">{lookupError}</p>}
    <form onSubmit={submit} className="space-y-6">
      <fieldset disabled={saving} className="space-y-6 disabled:opacity-70">
        <section className="rounded-xl border bg-card p-5 space-y-3">
          <Label htmlFor="customer-search">Khách hàng *</Label>
          <Input id="customer-search" placeholder="Tìm khách hàng..." value={customerQuery} onChange={(event) => { setCustomerQuery(event.target.value); setCustomerPage(1); }} />
          <select id="quotation-customer" aria-label="Chọn khách hàng" className={selectClass} value={customerId} disabled={customerLoading} onChange={(event) => {
            setCustomerId(event.target.value);
            const customer = customers.find((entry) => entry.customerId === Number(event.target.value));
            setCustomerLabel(customer?.customerCompany || customer?.customerFullName || "");
          }}>
            <option value="">{customerLoading ? "Đang tải..." : "Chọn khách hàng"}</option>
            {customerId && !customers.some((customer) => customer.customerId === Number(customerId)) && <option value={customerId}>{customerLabel}</option>}
            {customers.map((customer) => <option key={customer.customerId} value={customer.customerId}>{customer.customerCode} · {customer.customerCompany || customer.customerFullName}</option>)}
          </select>
          {!customerLoading && !customers.length && <p className="text-sm text-muted-foreground">Không tìm thấy khách hàng đang hoạt động.</p>}
          <PickerPagination page={customerPage} pages={customerPages} loading={customerLoading} onChange={setCustomerPage} />
        </section>
        <section className="rounded-xl border bg-card p-5 space-y-4">
          <h2 className="font-semibold">Sản phẩm báo giá</h2>
          <Input aria-label="Tìm sản phẩm" placeholder="Tìm tên hoặc mã sản phẩm..." value={productQuery} onChange={(event) => { setProductQuery(event.target.value); setProductPage(1); setProductId(""); }} />
          <div className="flex flex-col sm:flex-row gap-3">
            <select aria-label="Chọn sản phẩm" className={selectClass} value={productId} disabled={productLoading} onChange={(event) => setProductId(event.target.value)}>
              <option value="">{productLoading ? "Đang tải..." : "Chọn sản phẩm"}</option>
              {products.map((product) => <option key={product.productId} value={product.productId}>{product.productCode} · {product.productName}</option>)}
            </select>
            <Button type="button" disabled={!productId || productLoading} onClick={addProduct}><Plus className="mr-2 h-4 w-4" />Thêm sản phẩm</Button>
          </div>
          {!productLoading && !products.length && <p className="text-sm text-muted-foreground">Không tìm thấy sản phẩm đang kinh doanh.</p>}
          <PickerPagination page={productPage} pages={productPages} loading={productLoading} onChange={(page) => { setProductPage(page); setProductId(""); }} />
          {!lines.length && <p className="py-6 text-center text-muted-foreground">Chưa chọn sản phẩm.</p>}
          {lines.map((line) => <div key={line.productId} className="rounded-xl border p-4 space-y-3">
            <div className="flex justify-between items-center gap-3"><h3 className="font-medium">{line.productName}</h3><Button type="button" variant="ghost" size="icon" aria-label={`Xóa ${line.productName}`} onClick={() => setLines((previous) => previous.filter((entry) => entry.productId !== line.productId))}><Trash2 className="h-4 w-4 text-destructive" /></Button></div>
            <div className="grid gap-4 sm:grid-cols-3">
              <div className="space-y-2"><Label htmlFor={`quantity-${line.productId}`}>Số lượng *</Label><IntegerInput id={`quantity-${line.productId}`} value={line.quantity} max={2147483647} onValueChange={(quantity) => updateLine(line.productId, { quantity })} /></div>
              <div className="space-y-2"><Label htmlFor={`price-${line.productId}`}>Đơn giá *</Label><DecimalInput id={`price-${line.productId}`} value={line.unitPrice} onValueChange={(unitPrice) => updateLine(line.productId, { unitPrice: Math.round(unitPrice * 100) / 100 })} /><p className="text-xs text-muted-foreground">Đơn giá làm tròn đến 2 chữ số thập phân.</p></div>
              <div className="space-y-2"><p className="text-sm">Thành tiền</p><p className="font-semibold text-primary">{formatCurrency(line.quantity * line.unitPrice)}</p></div>
            </div>
          </div>)}
          <p className="text-right text-lg font-semibold">Tổng cộng: {formatCurrency(total)}</p>
        </section>
      </fieldset>
      {error && <p role="alert" className="text-destructive">{error}</p>}
      <div className="flex justify-end gap-3"><Button type="button" variant="outline" disabled={saving} onClick={() => router.push("/quotations")}>Hủy</Button><Button type="submit" disabled={saving || !customerId || !lines.length}>{saving ? "Đang tạo..." : "Tạo báo giá"}</Button></div>
    </form>
  </main></>;
}

function PickerPagination({ page, pages, loading, onChange }: { page: number; pages: number; loading: boolean; onChange: (page: number) => void }) {
  if (pages <= 1) return null;
  return <div className="flex items-center justify-end gap-3 text-sm"><Button type="button" variant="outline" size="sm" disabled={loading || page <= 1} onClick={() => onChange(page - 1)}>Trước</Button><span>Trang {page}/{pages}</span><Button type="button" variant="outline" size="sm" disabled={loading || page >= pages} onClick={() => onChange(page + 1)}>Sau</Button></div>;
}

export default function CreateQuotationPage() {
  return <PermissionGuard permission={RBAC_PERMISSIONS.quotationManage}><CreateQuotationContent /></PermissionGuard>;
}
