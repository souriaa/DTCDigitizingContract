# Phase 0 - Mapping triển khai song ngữ

Ngày: 18/10/2026. Đây là mapping chốt ở phase 0; phần mã phase 0–6 nay đã triển khai. Kết quả kiểm tra và phần còn chờ xem plan chính và BILINGUAL_DOCUMENT_AUTHORING_AND_RELEASE.md.

## Dữ liệu master và placeholder

| Master/cột mới | FE | Mức bắt buộc | Marker dự kiến | Snapshot |
| --- | --- | --- | --- | --- |
| TenantLegalProfile.LegalEntityNameEn | Hồ sơ pháp lý | Bắt buộc | PROVIDER_LEGAL_NAME_EN, TENANT_LEGAL_NAME_EN của nghiệm thu | Party.LegalNameEn |
| TenantLegalProfile.AddressEn | Hồ sơ pháp lý | Bắt buộc | PROVIDER_ADDRESS_EN, TENANT_ADDRESS_EN | Party.AddressEn |
| TenantLegalProfile.RepresentativeTitleEn | Hồ sơ pháp lý | Bắt buộc | PROVIDER_REPRESENTATIVE_TITLE_EN, TENANT_REPRESENTATIVE_TITLE_EN | Party.RepresentativeTitleEn |
| TenantLegalProfile.BankNameEn | Hồ sơ pháp lý | Bắt buộc | PROVIDER_BANK_NAME_EN | Party.BankNameEn |
| Customer.CustomerCompanyEn | Tạo/sửa khách hàng | Tùy chọn | CUSTOMER_COMPANY_EN, CUSTOMER_LEGAL_NAME_EN của nghiệm thu | Party.LegalNameEn |
| Customer.CustomerAddressEn | Tạo/sửa khách hàng | Tùy chọn | CUSTOMER_ADDRESS_EN | Party.AddressEn |
| Customer.CustomerBankNameEn | Tạo/sửa khách hàng | Tùy chọn | CUSTOMER_BANK_NAME_EN | Party.BankNameEn |
| Customer.CustomerCityEn | Tạo/sửa khách hàng | Tùy chọn | CUSTOMER_CITY_EN | Party.CityEn |
| Customer.CustomerCountryEn | Tạo/sửa khách hàng | Tùy chọn | CUSTOMER_COUNTRY_EN | Party.CountryEn |
| Customer.CustomerContactPersonTitleEn | Tạo/sửa khách hàng | Tùy chọn | CUSTOMER_CONTACT_TITLE_EN; chức danh đại diện nghiệm thu khi dùng người liên lạc | Party.ContactTitleEn |
| Customer.CustomerRepresentativeTitleEn | Tạo/sửa khách hàng | Tùy chọn | Nguồn custom chức danh đại diện pháp luật EN, không thay thế người liên lạc | Party.RepresentativeTitleEn |
| ContractAcceptanceRecord.LocationEn | Nháp nghiệm thu | Theo marker sử dụng | ACCEPTANCE_LOCATION_EN | Lưu trên biên bản, hash khi chốt |

Các cột EN dùng nvarchar, không tạo JSON/key-value/translation table mới. Profile EN giới hạn 500/2000/200/500; customer EN giới hạn 500/500/500/100/100/200/200 theo thứ tự trên. Snapshot đủ rộng để không cắt dữ liệu master. Cột EN khách hàng nullable.

Catalog tĩnh hiện chỉ có CONTRACT_NAME_EN trong nhóm scalar EN này. Các key mới không trùng key hệ thống hiện tại. Không đọc DB local để tự đổi custom key: phase 4 kiểm tra collision theo tenant và báo tác giả cấu hình lại, không tạo alias.

CONTRACT_DATE_EN/EFFECTIVE_DATE_EN/EXPIRE_DATE_EN/ACCEPTANCE_DATE_EN cùng ngày nguồn, format culture cố định. CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN chỉ hỗ trợ VND/USD (đúng currency gate hiện tại), loại khác bị từ chối, không dịch/fallback. Sample EN riêng; file thật không nhận sample.

## Mode và đường dữ liệu

- Enum byte: Separate=1, Combined=2. Giá trị mặc định Separate chỉ cho bản ghi mới, không có runtime đoán mode từ dữ liệu cũ.
- Cột mode: ContractTemplateTerm, ContractTemplateAppendixTerm, ContractTerm, ContractAppendixTerm, ContractAcceptanceSection.
- Request ghi nội dung phải có mode tường minh (JsonRequired), response/public/history trả mode. Title/name EN không được miễn vì Combined.
- Template add/update/copy-version -> term/appendix term -> create contract từ template hoặc term tùy chỉnh -> draft update -> negotiation version clone -> preview/render model -> legal snapshot/hash/history/public.
- Acceptance template section -> create acceptance -> replace sections -> detail -> finalize/hash/render.
- Combined: nội dung chính duy nhất; ContentEn phải null; không tự nối/tách hai cây rich text. Separate: giữ hai nội dung, gate đầy đủ theo policy.
- Căn cứ không có mode Combined. Các trường milestone giữ cấu trúc và validation độc lập. Không đổi tiền, tỷ lệ, quyền, hoặc trạng thái nghiệp vụ.

## Snapshot và phạm vi

- Party snapshot EN bất biến sau submit; acceptance prefill từ signed snapshot. Không đọc master mới để sửa artifact cũ.
- Mode nằm trong term/appendix term theo version và canonical/hash, copy đầy đủ qua các projection. Không tạo bảng snapshot term giả nếu đã có term versioned.
- Draft preview dùng master hiện hành; phase 4/6 đã dùng EN master cùng một render context, hash snapshot và refresh custom EN của nháp trước khi khóa. Không tạo form override EN riêng tại hợp đồng.
- DOCX tĩnh và nội dung Combined do tác giả sắp Anh trước Việt; hệ thống không nhận diện ngôn ngữ/đảo ô bảng.
- Phase 0–3 chuẩn bị dữ liệu/UI; phase 4–6 đã thêm catalog/formatter EN và đổi thứ tự renderer. Migration/SQL/UAT chưa được thực hiện tự động.
- Không áp migration, không reset DB hoặc xóa data test trong quá trình viết code. Không hỗ trợ nâng cấp dữ liệu cũ bằng EN giả/converter.

## Bằng chứng kiểm tra cần giữ

- Unit BE: enum/Combined/Separate/rich text rỗng; profile bắt buộc và customer tùy chọn.
- Mapping BE: copy-version/create/draft/nego/appendix/acceptance/public/snapshot/hash.
- Migration model: đúng cột/check/nullable, không tạo physical FK; không áp DB tự động.
- Không chạy kiểm thử FE/browser theo yêu cầu hiện tại. Renderer DOCX/PDF thực để phase 5/7.
