"use client";

import { useState, useEffect } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
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
import { Loader2, Package, DollarSign, Globe } from "lucide-react";
import { toast } from "@/components/ui/sonner";
import { CatalogUnitFields } from "./catalog-unit-fields";
import {
  serviceApi,
  ServiceStatus,
  type ServiceResponse,
  type ServiceOptionResponse,
} from "@/services/catalog/services-api";
import {
  serviceTypeApi,
  type ServiceTypeOptionResponse,
} from "@/services/catalog/service-types-api";

const NO_SELECTION = "none";

interface ServiceFormModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (createdService?: ServiceResponse) => void;
  item?: ServiceResponse | null;
  viewOnly?: boolean;
  initialServiceTypeId?: number | null;
}

export function ServiceFormModal({
  isOpen,
  onClose,
  onSuccess,
  item,
  viewOnly = false,
  initialServiceTypeId,
}: ServiceFormModalProps) {
  const isEditMode = !!item && !viewOnly;
  const currentServiceId = item?.serviceId;

  const [activeTab, setActiveTab] = useState("basic");

  // Basic Info
  const [serviceName, setServiceName] = useState("");
  const [serviceNameEn, setServiceNameEn] = useState("");
  const [unitName, setUnitName] = useState("");
  const [unitNameEn, setUnitNameEn] = useState("");
  const [serviceTypeId, setServiceTypeId] = useState("");
  const [serviceParentId, setServiceParentId] = useState("");
  const [serviceOrder, setServiceOrder] = useState("1");
  const [serviceRegion, setServiceRegion] = useState("");
  const [langId, setLangId] = useState("");
  const [serviceImageIcon, setServiceImageIcon] = useState("");
  const [serviceShortDesc, setServiceShortDesc] = useState("");

  // Pricing & Details
  const [servicePrice, setServicePrice] = useState("");
  const [setupPrice, setSetupPrice] = useState("");
  const [maintainPrice, setMaintainPrice] = useState("");
  const [serviceContent, setServiceContent] = useState("");
  const [others, setOthers] = useState("");

  // SEO & Metadata
  const [rewrite, setRewrite] = useState("");
  const [titleBrowser, setTitleBrowser] = useState("");
  const [metaKeyword, setMetaKeyword] = useState("");
  const [metaDescription, setMetaDescription] = useState("");

  const [errors, setErrors] = useState<Record<string, string>>({});
  const [isSaving, setIsSaving] = useState(false);
  const [serviceTypes, setServiceTypes] = useState<ServiceTypeOptionResponse[]>([]);
  const [parentServices, setParentServices] = useState<ServiceOptionResponse[]>([]);
  const [isLoadingOptions, setIsLoadingOptions] = useState(false);
  const [optionsError, setOptionsError] = useState(false);
  const [optionsAttempt, setOptionsAttempt] = useState(0);

  useEffect(() => {
    if (!isOpen) return;
    let cancelled = false;
    const controller = new AbortController();
    const loadOptions = async () => {
      setIsLoadingOptions(true);
      setOptionsError(false);
      setServiceTypes([]);
      setParentServices([]);
      try {
        const [types, services] = await Promise.all([
          serviceTypeApi.getOptions(controller.signal),
          serviceApi.getOptions(controller.signal),
        ]);
        if (cancelled) return;
        setServiceTypes(types);
        // Exclude self and descendants to avoid selecting a circular parent.
        const excludedIds = new Set<number>(
          currentServiceId == null ? [] : [currentServiceId],
        );
        let changed = true;
        while (changed) {
          changed = false;
          for (const service of services) {
            if (
              service.serviceParentId != null &&
              excludedIds.has(service.serviceParentId) &&
              !excludedIds.has(service.serviceId)
            ) {
              excludedIds.add(service.serviceId);
              changed = true;
            }
          }
        }
        setParentServices(
          services.filter(
            (service) =>
              service.status !== ServiceStatus.Deleted &&
              !excludedIds.has(service.serviceId),
          ),
        );
      } catch {
        if (!cancelled) setOptionsError(true);
      } finally {
        if (!cancelled) setIsLoadingOptions(false);
      }
    };
    void loadOptions();
    return () => {
      cancelled = true;
      controller.abort();
    };
  }, [isOpen, currentServiceId, optionsAttempt]);

  console.log(serviceTypes, parentServices);

  useEffect(() => {
    if (isOpen) {
      setErrors({});
      setActiveTab("basic");
      if (item) {
        setServiceName(item.serviceName || "");
        setServiceNameEn(item.serviceNameEn || "");
        setUnitName(item.unitName ?? "");
        setUnitNameEn(item.unitNameEn ?? "");
        setServiceTypeId(
          item.serviceTypeId !== undefined && item.serviceTypeId !== null
            ? String(item.serviceTypeId)
            : "",
        );
        setServiceParentId(
          item.serviceParentId !== undefined && item.serviceParentId !== null
            ? String(item.serviceParentId)
            : "",
        );
        setServiceOrder(
          item.serviceOrder !== undefined && item.serviceOrder !== null
            ? String(item.serviceOrder)
            : "1",
        );
        setServiceRegion(
          item.serviceRegion !== undefined && item.serviceRegion !== null
            ? String(item.serviceRegion)
            : "",
        );
        setLangId(
          item.langId !== undefined && item.langId !== null
            ? String(item.langId)
            : "",
        );
        setServiceImageIcon(item.serviceImageIcon || "");
        setServiceShortDesc(item.serviceShortDesc || "");
        setServicePrice(
          item.servicePrice !== undefined && item.servicePrice !== null
            ? String(item.servicePrice)
            : "",
        );
        setSetupPrice(
          item.setupPrice !== undefined && item.setupPrice !== null
            ? String(item.setupPrice)
            : "",
        );
        setMaintainPrice(
          item.maintainPrice !== undefined && item.maintainPrice !== null
            ? String(item.maintainPrice)
            : "",
        );
        setServiceContent(item.serviceContent || "");
        setOthers(item.others || "");
        setRewrite(item.rewrite || "");
        setTitleBrowser(item.titleBrowser || "");
        setMetaKeyword(item.metaKeyword || "");
        setMetaDescription(item.metaDescription || "");
      } else {
        setServiceName("");
        setServiceNameEn("");
        setUnitName("");
        setUnitNameEn("");
        setServiceTypeId(initialServiceTypeId ? String(initialServiceTypeId) : "");
        setServiceParentId("");
        setServiceOrder("1");
        setServiceRegion("");
        setLangId("");
        setServiceImageIcon("");
        setServiceShortDesc("");
        setServicePrice("");
        setSetupPrice("");
        setMaintainPrice("");
        setServiceContent("");
        setOthers("");
        setRewrite("");
        setTitleBrowser("");
        setMetaKeyword("");
        setMetaDescription("");
      }
    }
  }, [isOpen, item, initialServiceTypeId]);

  const validate = () => {
    const newErrors: Record<string, string> = {};
    if (!serviceName.trim()) {
      newErrors.serviceName = "Vui lòng nhập tên dịch vụ";
    }
    if (!serviceNameEn.trim()) {
      newErrors.serviceNameEn = "Vui lòng nhập tên dịch vụ tiếng Anh";
    } else if (serviceNameEn.trim().length > 500) {
      newErrors.serviceNameEn = "Tên dịch vụ tiếng Anh không được vượt quá 500 ký tự";
    }
    if (!unitName.trim()) newErrors.unitName = "Vui lòng nhập đơn vị";
    else if (unitName.trim().length > 100) newErrors.unitName = "Đơn vị tối đa 100 ký tự";
    if (!unitNameEn.trim()) newErrors.unitNameEn = "Vui lòng nhập đơn vị tiếng Anh";
    else if (unitNameEn.trim().length > 100) newErrors.unitNameEn = "Đơn vị tiếng Anh tối đa 100 ký tự";
    if (!serviceTypeId) {
      newErrors.serviceTypeId = "Vui lòng chọn loại dịch vụ";
    } else if (isLoadingOptions || optionsError) {
      newErrors.serviceTypeId = "Vui lòng tải lại danh sách loại dịch vụ trước khi lưu";
    } else if (!serviceTypes.some((type) => String(type.serviceTypeId) === serviceTypeId)) {
      newErrors.serviceTypeId = "Loại dịch vụ không còn khả dụng. Vui lòng chọn lại";
    }
    if (servicePrice && isNaN(Number(servicePrice))) {
      newErrors.servicePrice = "Đơn giá phải là số hợp lệ";
    }
    if (setupPrice && isNaN(Number(setupPrice))) {
      newErrors.setupPrice = "Phí khởi tạo phải là số";
    }
    if (maintainPrice && isNaN(Number(maintainPrice))) {
      newErrors.maintainPrice = "Phí duy trì phải là số";
    }

    setErrors(newErrors);

    if (newErrors.serviceName || newErrors.serviceNameEn || newErrors.unitName || newErrors.unitNameEn || newErrors.serviceTypeId) {
      setActiveTab("basic");
    } else if (
      newErrors.servicePrice ||
      newErrors.setupPrice ||
      newErrors.maintainPrice
    ) {
      setActiveTab("pricing_details");
    }

    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = async () => {
    if (viewOnly || !validate()) return;
    setIsSaving(true);
    try {
      const priceVal = servicePrice ? Number(servicePrice) : null;
      const setupVal = setupPrice ? Number(setupPrice) : null;
      const maintainVal = maintainPrice ? Number(maintainPrice) : null;
      const orderVal = serviceOrder ? Number(serviceOrder) : 1;
      const typeIdVal = Number(serviceTypeId);
      const parentIdVal = serviceParentId ? Number(serviceParentId) : null;
      const regionVal = serviceRegion ? Number(serviceRegion) : null;
      const langIdVal = langId ? Number(langId) : null;

      const commonPayload = {
        serviceName: serviceName.trim(),
        serviceNameEn: serviceNameEn.trim(),
        unitName: unitName.trim(),
        unitNameEn: unitNameEn.trim(),
        serviceTypeId: typeIdVal,
        serviceParentId: parentIdVal,
        servicePrice: priceVal,
        setupPrice: setupVal,
        maintainPrice: maintainVal,
        langId: langIdVal,
        serviceImageIcon: serviceImageIcon.trim() || null,
        serviceShortDesc: serviceShortDesc.trim() || null,
        serviceContent: serviceContent.trim() || null,
        serviceOrder: orderVal,
        serviceRegion: regionVal,
        rewrite: rewrite.trim() || null,
        titleBrowser: titleBrowser.trim() || null,
        metaKeyword: metaKeyword.trim() || null,
        metaDescription: metaDescription.trim() || null,
        others: others.trim() || null,
      };

      if (isEditMode && item) {
        await serviceApi.update(item.serviceId, commonPayload);
        toast.success("Cập nhật dịch vụ thành công");
        onSuccess({ ...item, serviceName: commonPayload.serviceName, serviceNameEn: commonPayload.serviceNameEn,
          unitName: commonPayload.unitName, unitNameEn: commonPayload.unitNameEn, serviceTypeId: typeIdVal });
      } else {
        const createdService = await serviceApi.create(commonPayload);
        toast.success("Thêm dịch vụ mới thành công");
        onSuccess(createdService);
      }
      onClose();
    } catch (error: any) {
      const message =
        error?.response?.data?.message ||
        error?.message ||
        (isEditMode ? "Không thể cập nhật dịch vụ" : "Không thể thêm dịch vụ");
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
              ? "Chi tiết dịch vụ"
              : isEditMode
                ? "Chỉnh sửa dịch vụ"
                : "Thêm dịch vụ mới"}
          </DialogTitle>
          <DialogDescription>
            {viewOnly
              ? "Xem toàn bộ thông tin chi tiết của dịch vụ."
              : isEditMode
                ? "Cập nhật đầy đủ thông tin dịch vụ theo DTO."
                : "Điền thông tin chi tiết để tạo dịch vụ mới."}
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
              value="pricing_details"
              className="flex items-center justify-start sm:justify-center gap-2"
            >
              <DollarSign className="w-4 h-4" />
              <span>Đơn giá & Chi tiết</span>
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
            <div className="grid gap-2">
              <Label htmlFor="serviceName">
                Tên dịch vụ{" "}
                {!viewOnly && <span className="text-destructive">*</span>}
              </Label>
              <Input
                id="serviceName"
                placeholder="Nhập tên dịch vụ (ví dụ: Gói Hosting Pro, Bảo trì hệ thống)..."
                value={serviceName}
                onChange={(e) => setServiceName(e.target.value)}
                disabled={viewOnly}
                aria-invalid={!!errors.serviceName}
                maxLength={2000}
              />
              {errors.serviceName && (
                <p className="text-xs text-destructive">{errors.serviceName}</p>
              )}
            </div>

            <div className="grid gap-2">
              <Label htmlFor="serviceNameEn">Tên dịch vụ tiếng Anh {!viewOnly && <span className="text-destructive">*</span>}</Label>
              <Input id="serviceNameEn" value={serviceNameEn} onChange={(event) => setServiceNameEn(event.target.value)}
                disabled={viewOnly} maxLength={500} aria-invalid={!!errors.serviceNameEn} placeholder="Nhập tên dịch vụ tiếng Anh..." />
              {errors.serviceNameEn && <p className="text-xs text-destructive">{errors.serviceNameEn}</p>}
            </div>
            <div className="grid gap-4 sm:grid-cols-2">
              <CatalogUnitFields idPrefix="service" unitName={unitName} unitNameEn={unitNameEn}
                disabled={viewOnly} errors={errors}
                onChange={(field, value) => field === "unitName" ? setUnitName(value) : setUnitNameEn(value)} />
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="serviceTypeId">
                  Loại dịch vụ {!viewOnly && <span className="text-destructive">*</span>}
                </Label>
                <Select
                  value={serviceTypeId || NO_SELECTION}
                  onValueChange={(value) =>
                    setServiceTypeId(value === NO_SELECTION ? "" : value)
                  }
                  disabled={
                    viewOnly || isSaving || isLoadingOptions || optionsError
                  }
                >
                  <SelectTrigger id="serviceTypeId" className="w-full" aria-invalid={!!errors.serviceTypeId} aria-required={!viewOnly}>
                    <SelectValue
                      placeholder={
                        isLoadingOptions ? "Đang tải..." : "Chọn loại dịch vụ"
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NO_SELECTION} disabled>
                      Chọn loại dịch vụ
                    </SelectItem>
                    {serviceTypeId &&
                      !serviceTypes.some(
                        (type) => String(type.serviceTypeId) === serviceTypeId,
                      ) && (
                        <SelectItem value={serviceTypeId} disabled>
                          {item?.serviceTypeName ||
                            (isLoadingOptions
                              ? "Đang tải..."
                              : "Loại dịch vụ hiện tại không khả dụng")}
                        </SelectItem>
                      )}
                    {serviceTypes.map((type) => (
                      <SelectItem
                        key={type.serviceTypeId}
                        value={String(type.serviceTypeId)}
                      >
                        {type.serviceTypeName || "Loại dịch vụ chưa có tên"}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                {errors.serviceTypeId && <p className="text-xs text-destructive">{errors.serviceTypeId}</p>}
                {!viewOnly && !isLoadingOptions && !optionsError && serviceTypes.length === 0 && (
                  <div className="space-y-1 text-xs text-muted-foreground">
                    <p>Chưa có loại dịch vụ. Hãy tạo loại dịch vụ trước khi lưu.</p>
                    <Button asChild variant="link" className="h-auto p-0 text-xs">
                      <a href="/catalog/products?tab=services" target="_blank" rel="noopener noreferrer">Mở quản lý loại dịch vụ</a>
                    </Button>
                    <Button type="button" variant="ghost" size="sm" onClick={() => setOptionsAttempt((attempt) => attempt + 1)}>
                      Tải lại loại dịch vụ
                    </Button>
                  </div>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="serviceParentId">Dịch vụ cha</Label>
                <Select
                  value={serviceParentId || NO_SELECTION}
                  onValueChange={(value) =>
                    setServiceParentId(value === NO_SELECTION ? "" : value)
                  }
                  disabled={
                    viewOnly || isSaving || isLoadingOptions || optionsError
                  }
                >
                  <SelectTrigger id="serviceParentId" className="w-full">
                    <SelectValue
                      placeholder={
                        isLoadingOptions ? "Đang tải..." : "Chọn dịch vụ cha"
                      }
                    />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={NO_SELECTION}>
                      Không có dịch vụ cha
                    </SelectItem>
                    {serviceParentId &&
                      !parentServices.some(
                        (service) =>
                          String(service.serviceId) === serviceParentId,
                      ) && (
                        <SelectItem value={serviceParentId} disabled>
                          {isLoadingOptions
                            ? "Đang tải..."
                            : "Dịch vụ cha hiện tại không khả dụng"}
                        </SelectItem>
                      )}
                    {parentServices.map((service) => (
                      <SelectItem
                        key={service.serviceId}
                        value={String(service.serviceId)}
                      >
                        {service.serviceName || "Dịch vụ chưa có tên"}
                        {service.status === ServiceStatus.Inactive
                          ? " (Ngừng hoạt động)"
                          : ""}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              <div className="grid gap-2">
                <Label htmlFor="serviceOrder">Thứ tự hiển thị</Label>
                <Input
                  id="serviceOrder"
                  type="number"
                  placeholder="1"
                  value={serviceOrder}
                  onChange={(e) => setServiceOrder(e.target.value)}
                  disabled={viewOnly}
                />
              </div>
            </div>

            {optionsError && (
              <div
                className="flex items-center gap-2 text-sm text-destructive"
                role="alert"
              >
                Không thể tải loại dịch vụ và dịch vụ cha.
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setOptionsAttempt((attempt) => attempt + 1)}
                >
                  Thử lại
                </Button>
              </div>
            )}

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="serviceRegion">Khu vực (Region ID)</Label>
                <Input
                  id="serviceRegion"
                  type="number"
                  placeholder="Ví dụ: 1"
                  value={serviceRegion}
                  onChange={(e) => setServiceRegion(e.target.value)}
                  disabled={viewOnly}
                />
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
                />
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="serviceImageIcon">Icon / Hình ảnh nhỏ</Label>
              <Input
                id="serviceImageIcon"
                placeholder="icon-service.png hoặc https://..."
                value={serviceImageIcon}
                onChange={(e) => setServiceImageIcon(e.target.value)}
                disabled={viewOnly}
                maxLength={50}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="serviceShortDesc">Mô tả ngắn</Label>
              <Textarea
                id="serviceShortDesc"
                placeholder="Mô tả tóm tắt dịch vụ..."
                value={serviceShortDesc}
                onChange={(e) => setServiceShortDesc(e.target.value)}
                disabled={viewOnly}
                rows={3}
              />
            </div>
          </TabsContent>

          {/* TAB 2: ĐƠN GIÁ & CHI TIẾT */}
          <TabsContent value="pricing_details" className="space-y-4 py-3">
            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
              <div className="grid gap-2">
                <Label htmlFor="servicePrice">Đơn giá (VNĐ)</Label>
                <Input
                  id="servicePrice"
                  type="number"
                  placeholder="1000000"
                  value={servicePrice}
                  onChange={(e) => setServicePrice(e.target.value)}
                  disabled={viewOnly}
                  aria-invalid={!!errors.servicePrice}
                />
                {errors.servicePrice && (
                  <p className="text-xs text-destructive">
                    {errors.servicePrice}
                  </p>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="setupPrice">Phí khởi tạo (VNĐ)</Label>
                <Input
                  id="setupPrice"
                  type="number"
                  placeholder="500000"
                  value={setupPrice}
                  onChange={(e) => setSetupPrice(e.target.value)}
                  disabled={viewOnly}
                  aria-invalid={!!errors.setupPrice}
                />
                {errors.setupPrice && (
                  <p className="text-xs text-destructive">
                    {errors.setupPrice}
                  </p>
                )}
              </div>

              <div className="grid gap-2">
                <Label htmlFor="maintainPrice">Phí duy trì (VNĐ/Năm)</Label>
                <Input
                  id="maintainPrice"
                  type="number"
                  placeholder="1200000"
                  value={maintainPrice}
                  onChange={(e) => setMaintainPrice(e.target.value)}
                  disabled={viewOnly}
                  aria-invalid={!!errors.maintainPrice}
                />
                {errors.maintainPrice && (
                  <p className="text-xs text-destructive">
                    {errors.maintainPrice}
                  </p>
                )}
              </div>
            </div>

            <div className="grid gap-2">
              <Label htmlFor="serviceContent">Nội dung chi tiết dịch vụ</Label>
              <Textarea
                id="serviceContent"
                placeholder="Nội dung mô tả đầy đủ thông số, điều khoản, phạm vi dịch vụ..."
                value={serviceContent}
                onChange={(e) => setServiceContent(e.target.value)}
                disabled={viewOnly}
                rows={5}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="others">Thông tin bổ sung (Others)</Label>
              <Textarea
                id="others"
                placeholder="Ghi chú thêm hoặc thông tin mở rộng..."
                value={others}
                onChange={(e) => setOthers(e.target.value)}
                disabled={viewOnly}
                rows={3}
                maxLength={4000}
              />
            </div>
          </TabsContent>

          {/* TAB 3: SEO & METADATA */}
          <TabsContent value="seo" className="space-y-4 py-3">
            <div className="grid gap-2">
              <Label htmlFor="rewrite">Rewrite URL</Label>
              <Input
                id="rewrite"
                placeholder="ten-dich-vu-url-friendly"
                value={rewrite}
                onChange={(e) => setRewrite(e.target.value)}
                disabled={viewOnly}
                maxLength={300}
              />
            </div>

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
                maxLength={500}
              />
            </div>

            <div className="grid gap-2">
              <Label htmlFor="metaKeyword">Từ khóa SEO (Meta Keyword)</Label>
              <Input
                id="metaKeyword"
                placeholder="dich-vu, hosting, cloud..."
                value={metaKeyword}
                onChange={(e) => setMetaKeyword(e.target.value)}
                disabled={viewOnly}
                maxLength={2000}
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
                maxLength={2000}
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
