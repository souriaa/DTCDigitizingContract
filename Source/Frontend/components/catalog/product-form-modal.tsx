"use client";

import { useState, useEffect, useMemo } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { CodeInput } from "@/components/ui/custom/code-input";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Loader2, Package, FileText, Globe } from "lucide-react";
import { toast } from "@/components/ui/sonner";
import { getApiErrorCode, getApiErrorMessage } from "@/lib/api-error";
import {
  productApi,
  ProductResponse,
  CreateProductRequest,
  UpdateProductRequest,
} from "@/services/catalog/products-api";
import { categoryApi, CategoryResponse } from "@/services/catalog/category-api";
import { CatalogUnitFields } from "./catalog-unit-fields";

interface ProductFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (createdProduct?: ProductResponse) => void;
  item?: ProductResponse | null;
  viewOnly?: boolean;
  onViewDeleted?: (code: string) => void;
  initialCategoryId?: number | null;
}

export function ProductFormModal({
  isOpen,
  onClose,
  onSuccess,
  item,
  viewOnly = false,
  onViewDeleted,
  initialCategoryId,
}: ProductFormModalProps) {
  const isEditMode = !!item && !viewOnly;

  const [activeTab, setActiveTab] = useState("basic");

  // Basic Info
  const [productCode, setProductCode] = useState("");
  const [deletedConflictCode, setDeletedConflictCode] = useState<string | null>(
    null,
  );
  const [productName, setProductName] = useState("");
  const [productNameEn, setProductNameEn] = useState("");
  const [unitName, setUnitName] = useState("");
  const [unitNameEn, setUnitNameEn] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [productPrice, setProductPrice] = useState("");
  const [productOrder, setProductOrder] = useState("1");
  const [langId, setLangId] = useState("");
  const [productTags, setProductTags] = useState("");
  const [productShortDesc, setProductShortDesc] = useState("");

  // Details & Features
  const [productDetails, setProductDetails] = useState("");
  const [productFeatures, setProductFeatures] = useState("");
  const [productBenefit, setProductBenefit] = useState("");
  const [productSmallImage, setProductSmallImage] = useState("");
  const [productLargeImage, setProductLargeImage] = useState("");

  // SEO & Metadata
  const [titleBrowser, setTitleBrowser] = useState("");
  const [metaKeyword, setMetaKeyword] = useState("");
  const [metaDescription, setMetaDescription] = useState("");

  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSaving, setIsSaving] = useState(false);

  const [categories, setCategories] = useState<CategoryResponse[]>([]);
  const [isLoadingCategories, setIsLoadingCategories] = useState(false);
  const [categoriesError, setCategoriesError] = useState(false);
  const [categoriesAttempt, setCategoriesAttempt] = useState(0);

  useEffect(() => {
    if (isOpen) {
      let cancelled = false;
      const controller = new AbortController();
      const fetchCategories = async () => {
        setIsLoadingCategories(true);
        setCategoriesError(false);
        setCategories([]);
        try {
          const res = await categoryApi.getList(
            { page: 1, pageSize: 1000 },
            controller.signal,
          );
          if (cancelled) return;
          setCategories(res.items || []);
        } catch {
          if (!cancelled) setCategoriesError(true);
        } finally {
          if (!cancelled) setIsLoadingCategories(false);
        }
      };

      fetchCategories();
      return () => {
        cancelled = true;
        controller.abort();
      };
    }
  }, [isOpen, categoriesAttempt]);

  const categoryOptions = useMemo(() => {
    if (!categories.length) return [];

    const parentMap = new Map<number, CategoryResponse>();
    categories.forEach((cat) => parentMap.set(cat.categoryId, cat));

    const childrenMap = new Map<number, CategoryResponse[]>();
    const roots: CategoryResponse[] = [];

    categories.forEach((cat) => {
      if (cat.categoryParentId && parentMap.has(cat.categoryParentId)) {
        const list = childrenMap.get(cat.categoryParentId) || [];
        list.push(cat);
        childrenMap.set(cat.categoryParentId, list);
      } else {
        roots.push(cat);
      }
    });

    const result: { id: number; name: string }[] = [];
    const traverse = (cats: CategoryResponse[], prefix = "") => {
      cats.forEach((c) => {
        result.push({
          id: c.categoryId,
          name: `${prefix}${c.categoryName || `Danh mục #${c.categoryId}`}`,
        });
        const children = childrenMap.get(c.categoryId);
        if (children && children.length > 0) {
          traverse(children, `${prefix}— `);
        }
      });
    };

    traverse(roots);
    return result;
  }, [categories]);

  const selectedCategoryExists = useMemo(() => {
    if (!categoryId) return true;
    return categories.some((c) => String(c.categoryId) === String(categoryId));
  }, [categories, categoryId]);

  useEffect(() => {
    if (isOpen) {
      setErrors({});
      setDeletedConflictCode(null);
      setActiveTab("basic");
      if (item) {
        setProductCode(item.productCode || "");
        setProductName(item.productName || "");
        setProductNameEn(item.productNameEn || "");
        setUnitName(item.unitName ?? "");
        setUnitNameEn(item.unitNameEn ?? "");
        setCategoryId(
          item.categoryId !== undefined && item.categoryId !== null
            ? String(item.categoryId)
            : "",
        );
        setProductPrice(
          item.productPrice !== undefined && item.productPrice !== null
            ? String(item.productPrice)
            : "",
        );
        setProductOrder(
          item.productOrder !== undefined && item.productOrder !== null
            ? String(item.productOrder)
            : "1",
        );
        setLangId(
          item.langId !== undefined && item.langId !== null
            ? String(item.langId)
            : "",
        );
        setProductTags(item.productTags || "");
        setProductShortDesc(item.productShortDesc || "");
        setProductDetails(item.productDetails || "");
        setProductFeatures(item.productFeatures || "");
        setProductBenefit(item.productBenefit || "");
        setProductSmallImage(item.productSmallImage || "");
        setProductLargeImage(item.productLargeImage || "");
        setTitleBrowser(item.titleBrowser || "");
        setMetaKeyword(item.metaKeyword || "");
        setMetaDescription(item.metaDescription || "");
      } else {
        setProductCode("");
        setProductName("");
        setProductNameEn("");
        setUnitName("");
        setUnitNameEn("");
        setCategoryId(initialCategoryId ? String(initialCategoryId) : "");
        setProductPrice("");
        setProductOrder("1");
        setLangId("");
        setProductTags("");
        setProductShortDesc("");
        setProductDetails("");
        setProductFeatures("");
        setProductBenefit("");
        setProductSmallImage("");
        setProductLargeImage("");
        setTitleBrowser("");
        setMetaKeyword("");
        setMetaDescription("");
      }
    }
  }, [isOpen, item, initialCategoryId]);

  const validate = () => {
    const newErrors: Record<string, string> = {};
    if (productCode.trim() === "") {
      newErrors.productCode = "Vui lòng nhập mã sản phẩm";
    }
    if (!productName.trim()) {
      newErrors.productName = "Vui lòng nhập tên sản phẩm";
    }
    if (!productNameEn.trim()) {
      newErrors.productNameEn = "Vui lòng nhập tên sản phẩm tiếng Anh";
    } else if (productNameEn.trim().length > 500) {
      newErrors.productNameEn =
        "Tên sản phẩm tiếng Anh không được vượt quá 500 ký tự";
    }
    if (productPrice && isNaN(Number(productPrice))) {
      newErrors.productPrice = "Đơn giá phải là số hợp lệ";
    }
    if (!unitName.trim()) newErrors.unitName = "Vui lòng nhập đơn vị";
    else if (unitName.trim().length > 100)
      newErrors.unitName = "Đơn vị tối đa 100 ký tự";
    if (!unitNameEn.trim())
      newErrors.unitNameEn = "Vui lòng nhập đơn vị tiếng Anh";
    else if (unitNameEn.trim().length > 100)
      newErrors.unitNameEn = "Đơn vị tiếng Anh tối đa 100 ký tự";
    if (!categoryId) {
      newErrors.categoryId = "Vui lòng chọn danh mục sản phẩm";
    } else if (isLoadingCategories || categoriesError) {
      newErrors.categoryId =
        "Vui lòng tải lại danh sách danh mục trước khi lưu";
    } else if (!selectedCategoryExists) {
      newErrors.categoryId = "Danh mục không còn khả dụng. Vui lòng chọn lại";
    }
    if (langId && isNaN(Number(langId))) {
      newErrors.langId = "ID Ngôn ngữ phải là số";
    }

    setErrors(newErrors);

    if (
      newErrors.productCode ||
      newErrors.productName ||
      newErrors.productNameEn ||
      newErrors.unitName ||
      newErrors.unitNameEn ||
      newErrors.productPrice ||
      newErrors.categoryId ||
      newErrors.langId
    ) {
      setActiveTab("basic");
    }

    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async () => {
    if (viewOnly || !validate()) return;
    setDeletedConflictCode(null);
    setIsSaving(true);
    try {
      let createdProduct: ProductResponse | undefined;

      const priceVal = productPrice ? Number(productPrice) : null;
      const orderVal = productOrder ? Number(productOrder) : 1;
      const categoryIdVal = Number(categoryId);
      const langIdVal = langId ? Number(langId) : null;

      const commonPayload = {
        productCode: productCode.trim() || null,
        productName: productName.trim(),
        productNameEn: productNameEn.trim(),
        unitName: unitName.trim(),
        unitNameEn: unitNameEn.trim(),
        categoryId: categoryIdVal,
        productShortDesc: productShortDesc.trim() || null,
        productDetails: productDetails.trim() || null,
        productFeatures: productFeatures.trim() || null,
        productBenefit: productBenefit.trim() || null,
        productPrice: priceVal,
        productSmallImage: productSmallImage.trim() || null,
        productLargeImage: productLargeImage.trim() || null,
        langId: langIdVal,
        productOrder: orderVal,
        productTags: productTags.trim() || null,
        titleBrowser: titleBrowser.trim() || null,
        metaKeyword: metaKeyword.trim() || null,
        metaDescription: metaDescription.trim() || null,
      };

      if (isEditMode && item) {
        await productApi.update(item.productId, commonPayload);
        toast.success("Cập nhật sản phẩm thành công");
      } else {
        createdProduct = await productApi.create(commonPayload);
        toast.success("Thêm sản phẩm mới thành công");
      }
      onSuccess(
        createdProduct ??
          (item
            ? {
                ...item,
                productName: commonPayload.productName,
                productNameEn: commonPayload.productNameEn,
                unitName: commonPayload.unitName,
                unitNameEn: commonPayload.unitNameEn,
                categoryId: categoryIdVal,
              }
            : undefined),
      );
      onClose();
    } catch (error: unknown) {
      const message = getApiErrorMessage(
        error,
        isEditMode ? "Không thể cập nhật sản phẩm" : "Không thể thêm sản phẩm",
      );
      if (getApiErrorCode(error) === "ProductCodeDeleted") {
        setDeletedConflictCode(productCode.trim());
        setErrors((current) => ({ ...current, productCode: message }));
        setActiveTab("basic");
      }
      toast.error(message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-3xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {viewOnly
              ? "Chi tiết sản phẩm"
              : isEditMode
                ? "Chỉnh sửa sản phẩm"
                : "Thêm sản phẩm mới"}
          </DialogTitle>
          <DialogDescription>
            {viewOnly
              ? "Xem toàn bộ thông tin chi tiết của sản phẩm."
              : isEditMode
                ? "Cập nhật đầy đủ thông tin sản phẩm theo DTO."
                : "Điền thông tin chi tiết để tạo sản phẩm mới."}
          </DialogDescription>
        </DialogHeader>

        <Tabs
          value={activeTab}
          onValueChange={setActiveTab}
          className="w-full mt-2"
        >
          <TabsList className="grid w-full grid-cols-1 sm:grid-cols-3 h-auto gap-1 sm:gap-0">
            <TabsTrigger
              value="basic"
              className="flex items-center justify-start sm:justify-center gap-2"
            >
              <Package className="w-4 h-4" />
              <span>Thông tin cơ bản</span>
            </TabsTrigger>
            <TabsTrigger
              value="details"
              className="flex items-center justify-start sm:justify-center gap-2"
            >
              <FileText className="w-4 h-4" />
              <span>Chi tiết & Đặc điểm</span>
            </TabsTrigger>
            <TabsTrigger
              value="seo"
              className="flex items-center justify-start sm:justify-center gap-2"
            >
              <Globe className="w-4 h-4" />
              <span>SEO & Metadata</span>
            </TabsTrigger>
          </TabsList>

          {/* TAB 1: THÔNG TIN CƠ BẢN */}
          <TabsContent value="basic" className="space-y-4 py-3">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="productCode">
                  Mã sản phẩm{" "}
                  {!viewOnly && <span className="text-destructive">*</span>}
                </Label>
                <CodeInput
                  id="productCode"
                  placeholder="PROD-001"
                  value={productCode}
                  onValueChange={(value) => {
                    setProductCode(value);
                    setDeletedConflictCode(null);
                    setErrors((current) => ({ ...current, productCode: "" }));
                  }}
                  disabled={viewOnly}
                  aria-invalid={!!errors.productCode}
                  maxLength={50}
                />
                {errors.productCode && (
                  <p className="text-xs text-destructive">
                    {errors.productCode}
                  </p>
                )}
                {!viewOnly &&
                  deletedConflictCode !== null &&
                  deletedConflictCode === productCode.trim() &&
                  onViewDeleted && (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => onViewDeleted(deletedConflictCode)}
                    >
                      Xem sản phẩm đã xóa
                    </Button>
                  )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="productOrder">Thứ tự hiển thị</Label>
                <Input
                  id="productOrder"
                  type="number"
                  placeholder="1"
                  value={productOrder}
                  onChange={(e) => setProductOrder(e.target.value)}
                  disabled={viewOnly}
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productName">
                Tên sản phẩm{" "}
                {!viewOnly && <span className="text-destructive">*</span>}
              </Label>
              <Input
                id="productName"
                placeholder="Nhập tên sản phẩm..."
                value={productName}
                onChange={(e) => setProductName(e.target.value)}
                disabled={viewOnly}
                aria-invalid={!!errors.productName}
                maxLength={500}
              />
              {errors.productName && (
                <p className="text-xs text-destructive">{errors.productName}</p>
              )}
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productNameEn">
                Tên sản phẩm tiếng Anh{" "}
                {!viewOnly && <span className="text-destructive">*</span>}
              </Label>
              <Input
                id="productNameEn"
                value={productNameEn}
                onChange={(event) => setProductNameEn(event.target.value)}
                disabled={viewOnly}
                maxLength={500}
                aria-invalid={!!errors.productNameEn}
                placeholder="Nhập tên sản phẩm tiếng Anh..."
              />
              {errors.productNameEn && (
                <p className="text-xs text-destructive">
                  {errors.productNameEn}
                </p>
              )}
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <CatalogUnitFields
                idPrefix="product"
                unitName={unitName}
                unitNameEn={unitNameEn}
                disabled={viewOnly}
                errors={errors}
                onChange={(field, value) =>
                  field === "unitName"
                    ? setUnitName(value)
                    : setUnitNameEn(value)
                }
              />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="productPrice">Đơn giá (VNĐ)</Label>
                <Input
                  id="productPrice"
                  type="number"
                  placeholder="Ví dụ: 1500000"
                  value={productPrice}
                  onChange={(e) => setProductPrice(e.target.value)}
                  disabled={viewOnly}
                  aria-invalid={!!errors.productPrice}
                />
                {errors.productPrice && (
                  <p className="text-xs text-destructive">
                    {errors.productPrice}
                  </p>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="categoryId">
                  Danh mục{" "}
                  {!viewOnly && <span className="text-destructive">*</span>}
                </Label>
                <Select
                  value={categoryId || "none"}
                  onValueChange={(val) =>
                    setCategoryId(val === "none" ? "" : val)
                  }
                  disabled={
                    viewOnly ||
                    isSaving ||
                    isLoadingCategories ||
                    categoriesError
                  }
                >
                  <SelectTrigger
                    id="categoryId"
                    className="w-full"
                    aria-invalid={!!errors.categoryId}
                    aria-required={!viewOnly}
                  >
                    <SelectValue
                      placeholder={
                        isLoadingCategories
                          ? "Đang tải danh mục..."
                          : "-- Chọn danh mục --"
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem
                      value="none"
                      disabled
                      className="font-semibold text-primary"
                    >
                      -- Chọn danh mục --
                    </SelectItem>
                    {categoryOptions.map((cat) => (
                      <SelectItem key={cat.id} value={String(cat.id)}>
                        {cat.name}
                      </SelectItem>
                    ))}
                    {!selectedCategoryExists &&
                      item?.categoryName &&
                      categoryId && (
                        <SelectItem value={String(categoryId)} disabled>
                          {item.categoryName}
                        </SelectItem>
                      )}
                  </SelectContent>
                </Select>
                {!viewOnly &&
                  !isLoadingCategories &&
                  (categoriesError || categories.length === 0) && (
                    <div className="space-y-1 text-xs text-muted-foreground">
                      <p>
                        {categoriesError
                          ? "Không thể tải danh mục."
                          : "Chưa có danh mục. Hãy tạo danh mục trước khi lưu sản phẩm."}
                      </p>
                      <Button
                        asChild
                        variant="link"
                        className="h-auto p-0 text-xs"
                      >
                        <a
                          href="/catalog/products?tab=products"
                          target="_blank"
                          rel="noopener noreferrer"
                          className="underline"
                        >
                          Mở quản lý danh mục
                        </a>
                      </Button>
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        onClick={() =>
                          setCategoriesAttempt((attempt) => attempt + 1)
                        }
                      >
                        Tải lại danh mục
                      </Button>
                    </div>
                  )}
                {errors.categoryId && (
                  <p className="text-xs text-destructive">
                    {errors.categoryId}
                  </p>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="langId">ID Ngôn ngữ</Label>
                <Input
                  id="langId"
                  type="number"
                  placeholder="Ví dụ: 1 (VN)"
                  value={langId}
                  onChange={(e) => setLangId(e.target.value)}
                  disabled={viewOnly}
                  aria-invalid={!!errors.langId}
                />
                {errors.langId && (
                  <p className="text-xs text-destructive">{errors.langId}</p>
                )}
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productTags">Từ khóa / Tags</Label>
              <Input
                id="productTags"
                placeholder="chuyen-doi-so, crm, contract..."
                value={productTags}
                onChange={(e) => setProductTags(e.target.value)}
                disabled={viewOnly}
                maxLength={500}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productShortDesc">Mô tả ngắn</Label>
              <Textarea
                id="productShortDesc"
                placeholder="Mô tả tóm tắt sản phẩm (hiển thị trên danh sách)..."
                value={productShortDesc}
                onChange={(e) => setProductShortDesc(e.target.value)}
                disabled={viewOnly}
                rows={3}
                maxLength={2000}
              />
            </div>
          </TabsContent>

          {/* TAB 2: CHI TIẾT & ĐẶC ĐIỂM */}
          <TabsContent value="details" className="space-y-4 py-3">
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="productSmallImage">
                  Hình ảnh nhỏ (URL / Path)
                </Label>
                <Input
                  id="productSmallImage"
                  placeholder="https://example.com/small.jpg"
                  value={productSmallImage}
                  onChange={(e) => setProductSmallImage(e.target.value)}
                  disabled={viewOnly}
                  maxLength={500}
                />
              </div>

              <div className="grid gap-2">
                <Label htmlFor="productLargeImage">
                  Hình ảnh lớn (URL / Path)
                </Label>
                <Input
                  id="productLargeImage"
                  placeholder="https://example.com/large.jpg"
                  value={productLargeImage}
                  onChange={(e) => setProductLargeImage(e.target.value)}
                  disabled={viewOnly}
                  maxLength={500}
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productDetails">Chi tiết sản phẩm</Label>
              <Textarea
                id="productDetails"
                placeholder="Nội dung giới thiệu chi tiết về sản phẩm..."
                value={productDetails}
                onChange={(e) => setProductDetails(e.target.value)}
                disabled={viewOnly}
                rows={4}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productFeatures">Tính năng nổi bật</Label>
              <Textarea
                id="productFeatures"
                placeholder="Liệt kê các tính năng nổi bật của sản phẩm..."
                value={productFeatures}
                onChange={(e) => setProductFeatures(e.target.value)}
                disabled={viewOnly}
                rows={3}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="productBenefit">Lợi ích mang lại</Label>
              <Textarea
                id="productBenefit"
                placeholder="Liệt kê các lợi ích khi sử dụng sản phẩm..."
                value={productBenefit}
                onChange={(e) => setProductBenefit(e.target.value)}
                disabled={viewOnly}
                rows={3}
              />
            </div>
          </TabsContent>

          {/* TAB 3: SEO & METADATA */}
          <TabsContent value="seo" className="space-y-4 py-3">
            <div className="grid gap-2">
              <Label htmlFor="titleBrowser">
                Tiêu đề trình duyệt (Title Browser)
              </Label>
              <Input
                id="titleBrowser"
                placeholder="Tiêu đề hiển thị trên thanh tab trình duyệt / Google..."
                value={titleBrowser}
                onChange={(e) => setTitleBrowser(e.target.value)}
                disabled={viewOnly}
                maxLength={255}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="metaKeyword">Từ khóa SEO (Meta Keyword)</Label>
              <Input
                id="metaKeyword"
                placeholder="từ khóa 1, từ khóa 2..."
                value={metaKeyword}
                onChange={(e) => setMetaKeyword(e.target.value)}
                disabled={viewOnly}
                maxLength={500}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="metaDescription">
                Mô tả SEO (Meta Description)
              </Label>
              <Textarea
                id="metaDescription"
                placeholder="Mô tả chuẩn SEO hiển thị trên kết quả tìm kiếm..."
                value={metaDescription}
                onChange={(e) => setMetaDescription(e.target.value)}
                disabled={viewOnly}
                rows={4}
                maxLength={1000}
              />
            </div>
          </TabsContent>
        </Tabs>

        <DialogFooter className="mt-4 pt-4 border-t">
          {viewOnly ? (
            <Button variant="outline" onClick={onClose}>
              Đóng
            </Button>
          ) : (
            <>
              <Button variant="outline" onClick={onClose} disabled={isSaving}>
                Hủy bỏ
              </Button>
              <Button onClick={handleSubmit} disabled={isSaving}>
                {isSaving && <Loader2 className="w-4 h-4 mr-2 animate-spin" />}
                {isEditMode ? "Cập nhật" : "Tạo mới"}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
