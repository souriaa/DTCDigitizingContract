# Hướng dẫn soạn mẫu và triển khai tài liệu song ngữ

Phạm vi: hợp đồng, phụ lục đi cùng hợp đồng và biên bản nghiệm thu. Đây là hướng dẫn bàn giao, không phải xác nhận DB hoặc môi trường triển khai đã được cập nhật.

## 1. Quy tắc soạn DOCX

- Chữ cố định trong Word do tác giả tự dịch và sắp xếp Anh trước, Việt sau. Hệ thống không dịch hoặc đảo các câu tĩnh.
- Dòng “Căn cứ vào:” tự nhập trong Word luôn được giữ nguyên, kể cả không có căn cứ. Muốn bỏ dòng này, tác giả xóa trong DOCX rồi tải lên/tạo lại bản xem trước; renderer không tự xóa chữ tĩnh.
- Các marker đều tùy chọn. Không dùng marker nào thì không phải chèn đủ bộ marker vào tài liệu.
- Marker động đặt một mình trong một đoạn: `{{CONTRACT_LEGAL_BASES}}`, `{{CONTRACT_TERMS}}`, `{{CONTRACT_ITEM_TABLE}}`, `{{SIGNATURE_PROVIDER}}`, `{{SIGNATURE_CUSTOMER}}`, `{{CONTRACT_APPENDICES}}`.
- `{{CONTRACT_APPENDICES}}` là marker động cuối cùng; mỗi phụ lục bắt đầu trang mới. Không thêm marker riêng cho bản Anh của một block vì sẽ sinh block thứ hai.
- Marker chữ ký đã sinh nhãn đại diện. Không vừa giữ nhãn/chữ ký tĩnh vừa chèn marker nếu không muốn xuất hai bộ chữ ký.
- Marker kế thừa căn trái/giữa/phải và font nguồn khi đoạn được sinh chưa có căn lề riêng. Căn lề đã đặt rõ trong rich text/ô bảng được giữ lại, không bị căn giữa toàn bộ theo marker.
- `CONTRACT_NAME` chỉ là tên Việt; `CONTRACT_NAME_EN` chỉ là tên Anh. Không tự ghép hai tên vào input Việt.

Ví dụ phần mở đầu trong Word:

```text
SOFTWARE SUPPLY AGREEMENT
HỢP ĐỒNG CUNG CẤP PHẦN MỀM

Agreement: {{CONTRACT_NAME_EN}}
Hợp đồng: {{CONTRACT_NAME}}
Contract date: {{CONTRACT_DATE_EN}}
Ngày hợp đồng: {{CONTRACT_DATE}}

Provider: {{PROVIDER_LEGAL_NAME_EN}}
Bên cung cấp: {{PROVIDER_LEGAL_NAME}}
Address: {{PROVIDER_ADDRESS_EN}}
Địa chỉ: {{PROVIDER_ADDRESS}}

Customer: {{CUSTOMER_COMPANY_EN}}
Khách hàng: {{CUSTOMER_COMPANY}}

{{CONTRACT_LEGAL_BASES}}
{{CONTRACT_ITEM_TABLE}}
{{CONTRACT_TERMS}}
{{SIGNATURE_CUSTOMER}}
{{SIGNATURE_PROVIDER}}
{{CONTRACT_APPENDICES}}
```

Đây là hướng dẫn vị trí marker, không phải mẫu bắt buộc hoặc cơ chế tự sinh toàn bộ DOCX tĩnh.

## 2. Điều khoản và bảng song ngữ

1. Tiêu đề luôn có hai input EN/VI với mẫu song ngữ.
2. **Tách riêng Anh/Việt:** hai editor; khi xuất, nội dung Anh trước nội dung Việt. Nếu nhập một bảng ở mỗi editor thì kết quả vẫn có hai bảng.
3. **Gộp nội dung song ngữ:** một editor. Soạn một bảng và ghi Anh trên Việt trong từng ô; renderer xuất bảng đó đúng một lần. Nội dung Anh riêng phải rỗng.
4. Đổi chế độ khi có dữ liệu cần xác nhận chọn phần nội dung sẽ giữ. Không có thao tác tự gộp hai bảng, tự tách ngôn ngữ hay dịch tự động.
5. Bảng hàng hóa/dịch vụ do hệ thống sinh vẫn là một bảng, nhãn và tên/mô tả Anh trên Việt, số lượng và tiền chỉ xuất một lần. Tổng là `TOTAL / TỔNG CỘNG`, gộp bảy cột nhãn và căn phải.
6. Độ rộng dùng cấu hình cột của mẫu, tổng 10.000 bps. Với bảng song ngữ, tác giả nên cấp đủ rộng cho STT, loại, VAT và đơn giá; một cột quá hẹp vẫn có thể tự xuống dòng. Hệ thống không tự đổi độ rộng đã chọn chỉ vì chuyển sang song ngữ.

## 3. Nguồn dữ liệu tiếng Anh

Hồ sơ pháp lý bắt buộc bốn trường mới khi lưu: tên doanh nghiệp, địa chỉ, chức danh đại diện và tên ngân hàng tiếng Anh. Khách hàng có bảy trường EN tùy chọn; chỉ khi tài liệu thật dùng dữ liệu đó mới phải bổ sung phần thiếu.

| Marker hợp đồng | Nguồn |
| --- | --- |
| `CONTRACT_NAME_EN` | Tên hợp đồng tiếng Anh |
| `CONTRACT_DATE_EN` | Ngày hợp đồng, định dạng cố định `15 August 2026` |
| `EFFECTIVE_DATE_EN`, `EXPIRE_DATE_EN` | Ngày hiệu lực/hết hạn tương ứng |
| `CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN` | Tổng tiền phiên bản; VND làm tròn 0 chữ số, USD 2 chữ số, AwayFromZero; cả bằng chữ VI/EN và tổng USD hiển thị giữ phần xu, không đổi giá trị kế toán |
| `PROVIDER_LEGAL_NAME_EN`, `PROVIDER_ADDRESS_EN` | Hồ sơ pháp lý: tên doanh nghiệp/địa chỉ EN |
| `PROVIDER_REPRESENTATIVE_TITLE_EN`, `PROVIDER_BANK_NAME_EN` | Hồ sơ pháp lý: chức danh/tên ngân hàng EN |
| `CUSTOMER_COMPANY_EN`, `CUSTOMER_ADDRESS_EN` | Khách hàng: tên công ty/địa chỉ EN |
| `CUSTOMER_BANK_NAME_EN`, `CUSTOMER_CITY_EN`, `CUSTOMER_COUNTRY_EN` | Khách hàng: ngân hàng/tỉnh thành/quốc gia EN |
| `CUSTOMER_CONTACT_TITLE_EN` | Khách hàng: chức danh **người liên lạc** EN, không lấy chức danh đại diện pháp luật thay thế |

Các nguồn tương ứng cũng chọn được trong form trường dữ liệu tùy chỉnh. Nguồn chức danh đại diện pháp luật EN là `customer.representative-title-en`, độc lập với người liên lạc. Tên người, MST, số tài khoản, điện thoại và mã không được dịch hay bỏ dấu tự động.

- Thiếu nguồn EN: API trả thông báo cụ thể; màn hợp đồng hiển thị nơi cần bổ sung. Liên kết sửa master chỉ hiện cho người có quyền; người khác nhờ người phụ trách.
- Không lấy dữ liệu Việt, giá trị mặc định của custom field hoặc dữ liệu mẫu để thay EN thiếu trong tài liệu thật.
- Nếu custom key trùng marker hệ thống mới, hệ thống báo `PlaceholderKeyCollision`, không tự đổi binding. Phải xử lý cấu hình trùng và tải lại DOCX; không giữ hai định nghĩa có cùng key.
- Bản nháp lấy EN master hiện hành; phải tải lại/tạo lại preview sau khi sửa master. Nguồn EN custom của nháp được làm mới; quy tắc snapshot custom không phải EN hiện có vẫn giữ nguyên.
- Khi trình duyệt, party EN và scalar values được chụp vào snapshot/hash. File đã chốt không tự đổi khi master thay đổi.

## 4. Biên bản nghiệm thu

Các marker mới: `ACCEPTANCE_DATE_EN`, `ACCEPTANCE_LOCATION_EN`, `CUSTOMER_LEGAL_NAME_EN`, `CUSTOMER_ADDRESS_EN`, `CUSTOMER_REPRESENTATIVE_TITLE_EN`, `TENANT_LEGAL_NAME_EN`, `TENANT_ADDRESS_EN`, `TENANT_REPRESENTATIVE_TITLE_EN`.

```text
SOFTWARE ACCEPTANCE RECORD
BIÊN BẢN NGHIỆM THU PHẦN MỀM
{{DOCUMENT_REFERENCES}}
Date: {{ACCEPTANCE_DATE_EN}}
Ngày: {{ACCEPTANCE_DATE}}
Location: {{ACCEPTANCE_LOCATION_EN}}
Địa điểm: {{ACCEPTANCE_LOCATION}}
{{ACCEPTANCE_SECTIONS}}
{{SIGNATURE_CUSTOMER}}
{{SIGNATURE_PROVIDER}}
```

- Nguồn EN party được điền từ snapshot phiên bản hợp đồng đã ký; không đọc lại EN master mới cho căn cứ hoặc custom EN của nghiệm thu.
- Địa điểm EN và thông tin party được phép chỉnh tại nháp nghiệm thu theo quyền hiện hành. Tên pháp nhân vẫn không mở khóa trên FE.
- Căn cứ hợp đồng có ngày ký thật; căn cứ phụ lục đi cùng không tự thêm ngày ký phụ lục. Nếu nguồn EN cho căn cứ chưa đủ, preview song ngữ có dùng block căn cứ bị chặn; không in tên Việt thay tiếng Anh.
- Mục nghiệm thu áp dụng cùng Separate/Combined và EN→VI. Không đổi quy tắc nghiệm thu một phần/toàn phần, chọn đợt, ký, kích hoạt thanh toán hoặc đóng hợp đồng.

## 5. Triển khai API + FE + tenant DB

Các migration, context `DbDtctechContext`: `20261018045400_BilingualContentAndPartyEnglish`, `20261018120857_CatalogItemEnglishNames`, `20261018144616_AllowPendingEnglishMasterFields`. Không có migration central mới cho tính năng này; không thêm physical FK hoặc cột JSON mới.

**Chưa áp migration trong đợt triển khai mã này. Không chạy thẳng lên DB có dữ liệu mà chưa đọc cảnh báo dưới đây.**

1. Dừng cập nhật dữ liệu, backup tenant DB và private storage bằng quy trình hiện hành. Chốt người chịu trách nhiệm và phương án phục hồi cả bộ API/FE/DB/storage.
2. Kiểm tra đúng tenant DB và chạy lại lệnh `dotnet ef database update` hiện hành. Bốn cột EN Hồ sơ pháp lý và hai cột tên EN sản phẩm/dịch vụ được thêm dưới dạng NULL, không có default/backfill. Những hàng đã có dữ liệu được giữ nguyên; phần EN chưa nhập là NULL. Không cần xóa hàng, chạy SQL tay hoặc đổi lịch sử migration để vượt lỗi 4901.
3. EF tự bỏ qua migration đã áp. Migration nối tiếp `AllowPendingEnglishMasterFields` chỉ ALTER nullability của sáu cột, đồng bộ cả DB đã áp phiên bản NOT NULL trước đây; không UPDATE/DELETE dữ liệu. Down của migration nối tiếp không siết NOT NULL vì hai migration tiền nhiệm đã được sửa thành nullable và bản dịch có thể còn thiếu. Không chạy nhầm context central hay DB dùng chung.
4. Triển khai API và Frontend đồng bộ. Client mới phải gửi `bilingualContentMode` tường minh; client cũ không được dùng với API/schema mới. SystemAdmin không có thay đổi renderer trong đợt này.

Trong giai đoạn nhập bổ sung EN, NULL chỉ thể hiện dữ liệu chưa được nhập, không phải bản dịch dự phòng. FE/BE vẫn bắt buộc EN khi tạo/sửa Hồ sơ pháp lý và sản phẩm/dịch vụ; form đọc NULL thành ô trống để nhập, không ghi tên Việt hoặc tên giả vào DB. Renderer và hợp đồng song ngữ tiếp tục chặn nguồn EN còn thiếu theo dependency hiện hành. Tên EN của dòng sản phẩm/dịch vụ đã chụp vào hợp đồng không tự đổi theo danh mục.

Chưa tự động chuyển sáu cột sang NOT NULL trong cùng đợt này: migration không thể biết bản dịch thật. Sau khi đã nhập đầy đủ trên các tenant cần triển khai, mới tạo migration siết NOT NULL riêng. Không thêm ngay migration siết vào chuỗi hiện tại vì `database update` sẽ chạy luôn và lại thất bại khi còn NULL.
5. Tạo lại hoặc lưu Hồ sơ pháp lý với đủ EN thật. Bổ sung EN khách hàng cần dùng; không ép toàn bộ khách hàng phải có EN.
6. Với mẫu muốn thêm marker EN: sửa DOCX, tải lên kiểm tra lại, tạo preview mới và phát hành phiên bản mới. Catalog hợp đồng V5, catalog nghiệm thu V3, dataset V5, renderer V15 làm cache preview cũ không còn hiện hành. V15 giữ nguyên tiêu đề tĩnh “Căn cứ vào:” theo DOCX nguồn.
7. Không ghi đè artifact hợp đồng/nghiệm thu đã chốt hoặc đã ký. Không tự cập nhật các file cũ trong private storage.
8. Người dùng chạy UAT bên dưới rồi mới mở rộng môi trường. Nếu schema/data test cũ không tương thích thì dừng để quyết định xử lý, không bật nhánh legacy/fallback.

## 6. Checklist FE do người dùng kiểm thử

Chưa chạy FE build/typecheck/browser theo yêu cầu. Đánh dấu kết quả thực tế khi tự kiểm tra:

- [ ] Hồ sơ pháp lý thiếu từng EN/whitespace: không lưu được, chỉ rõ trường. Đủ EN: lưu được; fax/điện thoại tùy chọn cũ không bị bắt thêm.
- [ ] Khách hàng mới không nhập EN: lưu được. Nhập EN rồi sửa/mở lại: đủ bảy trường, không đổi mã/danh tính khách hàng.
- [ ] Mẫu VI: một editor. Mẫu song ngữ: chọn Separate có hai editor, Combined có một editor; EN title/name vẫn cần nhập.
- [ ] Chuyển mode, hủy, lưu bằng nút/Ctrl+S: không mất bảng, không nhân đôi nội dung, giữ trạng thái mở/đóng card.
- [ ] Combined có bảng, merge ô, căn trái/phải/giữa: DOCX/PDF có đúng một bảng nội dung đó. Separate có hai bảng: EN trước VI.
- [ ] Copy version, tạo hợp đồng, lưu nháp, vòng thương lượng mới: mode và nội dung giữ đúng; version cũ/history/public không bị đổi.
- [ ] Dùng `PROVIDER_BANK_NAME_EN`: nhận đúng ngân hàng EN. Không dùng marker EN nào: không buộc bổ sung mọi trường EN khách hàng.
- [ ] Marker khách hàng EN hoặc block căn cứ phụ lục cần EN nhưng thiếu: báo đúng nguồn; liên kết sửa chỉ có khi đủ quyền. Bổ sung rồi tải lại/tạo lại preview thành công.
- [ ] Hợp đồng chưa ký có phụ lục: căn cứ không thêm ngày ký. Bản nghiệm thu từ hợp đồng đã ký: có ngày ký thật, EN trước VI.
- [ ] Nghiệm thu mới: prefill EN từ snapshot đã ký, kể cả master EN đã đổi. Sửa nháp EN party/location rồi preview/chốt: đúng bản đã sửa.
- [ ] Bảng item một bảng, EN trên VI; tổng tiền không đổi, dòng tổng gộp/căn phải. Điều chỉnh độ rộng tại mẫu nếu cột tự xuống dòng quá nhiều.
- [ ] Chữ ký có nhãn EN trước VI và theo alignment marker; phụ lục chỉ hai nhãn đại diện, không tự thêm hồ sơ pháp nhân.
- [ ] Sửa mode/title/body hoặc bản dịch nguồn của nháp: preview mới phản ánh dữ liệu mới. File đã chốt/ký không đổi.
- [ ] Phê duyệt, rút hồ sơ, ký, nghiệm thu từng phần, thanh toán và hoàn tất: trạng thái, quyền và số tiền đúng luồng hiện hành.

Kết quả test BE/PDF và việc còn chờ được ghi trong `BILINGUAL_CONTRACT_RENDERER_PLAN.md`. Hướng dẫn này không thay cho UAT với DOCX nghiệp vụ thực tế của từng doanh nghiệp.
