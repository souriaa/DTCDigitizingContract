# Kế hoạch chuẩn hóa kết xuất tài liệu song ngữ

Ngày lập: 18/10/2026.
Trạng thái: đã triển khai mã phase 0–6; phase 7 đã có regression BE/OpenXML và PDF fixture từ LibreOffice thực. Phase 8 đã có hướng dẫn soạn mẫu/triển khai/UAT, chưa áp migration hoặc triển khai môi trường. Không chạy kiểm thử FE theo yêu cầu; SQL Server tích hợp và UAT thực tế còn chờ. Xem nhật ký cuối và BILINGUAL_DOCUMENT_AUTHORING_AND_RELEASE.md.
Quyết định cập nhật: nhập bản dịch tại **Hồ sơ pháp lý** và **Khách hàng**; các trường tiếng Anh mới của Hồ sơ pháp lý bắt buộc, các trường tiếng Anh mới của Khách hàng tùy chọn. Bỏ đề xuất nhập bản dịch chính tại hợp đồng và bảng `tbl_ContractVersionPartyTranslation`.

## 1. Mục tiêu và phạm vi

1. Mỗi điều khoản của mẫu song ngữ có tùy chọn **Gộp nội dung song ngữ**. Bật tùy chọn thì chỉ có một vùng soạn thảo nội dung; renderer kết xuất vùng đó đúng một lần, không nối thêm nội dung tiếng Anh thứ hai.
2. Bổ sung các placeholder tiếng Anh còn thiếu, có nguồn dữ liệu thật, dùng được cả trong bản xem trước mẫu lẫn tài liệu hợp đồng thật.
3. Thống nhất **tiếng Anh trước, tiếng Việt sau** trong các thành phần do hệ thống sinh: tiêu đề, nội dung tách riêng, căn cứ, phụ lục, bảng hàng hóa/dịch vụ, nhãn tổng tiền, chữ ký và nội dung nghiệm thu.
4. Dữ liệu từ mẫu phải đi xuyên suốt tạo hợp đồng, nháp, thương lượng, sao chép phiên bản, lịch sử, snapshot, DOCX và PDF. Không để bản xem trước đúng nhưng hợp đồng thật sai.
5. Không đổi nghiệp vụ phê duyệt, ký, nghiệm thu, thanh toán; không thay số tiền, tỷ lệ, hạn thanh toán, quyền chỉnh sửa hoặc trạng thái hợp đồng.
6. Mở rộng Hồ sơ pháp lý và module Khách hàng làm nguồn bản dịch dùng chung. Ràng buộc bắt buộc/tùy chọn mới tại hai module này được thay đổi có chủ đích theo yêu cầu, không áp cho mọi trường tùy chọn sẵn có.

Ngoài phạm vi: dịch tự động/AI, suy đoán ngôn ngữ trong rich text, tự gộp cấu trúc hai bảng có sẵn, thiết kế lại toàn bộ DOCX nguồn, thay cơ chế chuyển DOCX sang PDF.

## 2. Hiện trạng đã đối chiếu

Tài liệu tham chiếu: `C:/Users/hungp/Downloads/b08b8864-a681-431c-a4ec-b0a6dd7ec1e4.pdf` (5 trang).

- Trang 1: căn cứ tiếng Việt đứng trước tiếng Anh. Phần pháp nhân và phần dẫn nhập chủ yếu là tiếng Việt.
- Trang 2: bảng sản phẩm/dịch vụ có nhãn Việt trước Anh; dòng tổng chỉ có `TỔNG CỘNG`. Điều khoản thường kết xuất nội dung Việt rồi mới đến Anh. Tiêu đề điều 2 đang thiếu tên tiếng Anh.
- Trang 2–3: có hai bảng nội dung thanh toán, lần lượt Việt và Anh. Trong renderer hiện tại, `CreateTermElements` xuất lần lượt hai trường rich text; không phải có một bảng thanh toán tự sinh riêng cần sửa bằng cách thêm bảng thứ ba.
- Trang 5: tên phụ lục, căn cứ phụ lục và điều khoản phụ lục đều Việt trước Anh; nhãn chữ ký phụ lục chỉ có tiếng Việt.
- `ContractTemplatePreviewRenderer` dùng chung cho mẫu và hợp đồng, nhưng nghiệm thu thật có `ContractAcceptanceDocumentRenderer` riêng. Phải sửa cả hai, không chỉ nhánh bản xem trước.
- `TermFields` đã dùng chung giữa một số màn hợp đồng/mẫu/phụ lục. Đây là nơi đặt tùy chọn mới, thay vì tạo thêm bộ form song ngữ riêng.
- Catalog hợp đồng có `CONTRACT_NAME_EN`, nhưng chưa có các bản tiếng Anh tương ứng cho ngày tháng, tiền bằng chữ, tên pháp nhân, địa chỉ, chức danh, ngân hàng. `TblCustomer`, `TblTenantLegalProfile` và party nghiệm thu hiện chưa có đầy đủ các trường tiếng Anh này.
- `ContractTemplateRenderItem` đang nhận `Type` và `Description` dạng chuỗi đã ghép. Muốn điều khiển thứ tự chắc chắn phải truyền dữ liệu ngôn ngữ có cấu trúc, không đảo chuỗi bằng cách tách dấu `/`.

Các sửa đã có trước plan này, cần giữ và không thực hiện lại:

- Giữ nguyên tiêu đề/chữ tĩnh do tác giả đặt trong DOCX, kể cả “Căn cứ vào:” khi danh sách căn cứ trống. Đã undo logic tự xóa tiêu đề theo yêu cầu cập nhật.
- Mẫu song ngữ bắt buộc tên phụ lục tiếng Anh ở FE/BE và bước kiểm tra bản xem trước/phát hành.
- Bảng hàng hóa/dịch vụ có độ rộng cột chuẩn, dòng tổng gộp các cột trước cột tiền và căn phải.
- Form/card điều khoản, phụ lục và một số phần thanh toán đã được tách thành component dùng chung trong `Frontend/components/contract-documents/`.

## 3. Quy tắc thiết kế chốt cho triển khai

### 3.1. Chế độ nội dung của từng điều khoản

Thêm enum riêng `BilingualContentMode`: `Separate = 1`, `Combined = 2`. Không dùng `TermKind` để biểu diễn chế độ này; điều khoản thanh toán vẫn là loại thanh toán.

| Trường hợp | Form nội dung | Dữ liệu có hiệu lực | Kết xuất |
| --- | --- | --- | --- |
| Mẫu tiếng Việt | Một rich-text editor | Nội dung Việt, chế độ Separate | Một nội dung Việt |
| Song ngữ, Separate | Hai rich-text editor Anh/Việt | Cả hai trường nội dung | Nội dung Anh rồi nội dung Việt |
| Song ngữ, Combined | Một rich-text editor, nhãn “Nội dung song ngữ” | Trường nội dung chính, trường nội dung Anh riêng phải null | Kết xuất đúng một lần |

- “Một text area” được triển khai bằng **một rich-text editor hiện có**, không thay bằng textarea thuần làm mất bảng, độ rộng cột, merge ô và định dạng.
- Dùng trường `TermContent`/`ContentVi` hiện có làm vùng nội dung chính trong Combined. Tên trường trong DTO cũ không có nghĩa nội dung bắt buộc chỉ chứa tiếng Việt; nhãn UI phải đổi theo chế độ. Không tạo thêm `CombinedContentJson` hoặc sao chép cùng rich text sang hai cột.
- Tiêu đề vẫn có hai trường Anh và Việt; tùy chọn này chỉ gộp **nội dung**, không gộp tên điều khoản. Tên phụ lục tiếng Anh tiếp tục bắt buộc khi song ngữ.
- Mặc định điều khoản mới là Separate. Chỉ mẫu song ngữ mới cho bật Combined. BE từ chối enum lạ, Combined trên tài liệu tiếng Việt, hoặc Combined vẫn gửi nội dung Anh riêng khác rỗng.
- Combined không phải tùy chọn toàn template: trong một mẫu có thể có điều khoản thường tách riêng và điều khoản chứa bảng dùng nội dung gộp.
- Áp dụng cùng quy tắc cho điều khoản hợp đồng, điều khoản thanh toán, điều khoản phụ lục và mục nghiệm thu; tránh nhánh mẫu cho phép nhưng hợp đồng/nghiệm thu lại bắt nhập hai nội dung.
- Nội dung là tùy chọn hay bắt buộc vẫn theo quy tắc từng loại hiện hành. Nếu một điều khoản được phép không có nội dung thì không buộc nhập chỉ vì thêm chế độ mới. Khi nội dung bắt buộc: Separate song ngữ cần cả hai nội dung hợp lệ; Combined chỉ cần nội dung chính hợp lệ.
- Khi có nội dung ở cả hai editor rồi chuyển chế độ: không âm thầm ghép hoặc xóa. Hiện xác nhận, cho chọn lấy nội dung Anh, lấy nội dung Việt, hoặc soạn mới làm bản gộp; người dùng tự hoàn thiện bản song ngữ. Giữ bản nháp cục bộ để hủy thao tác trước khi lưu. Payload Combined chỉ chứa một nội dung có hiệu lực.
- Chuyển Combined về Separate cũng cần xác nhận; người dùng chọn đưa nội dung hiện có vào editor nào và tự điền phần còn lại. Không tự phân tích/tách ngôn ngữ.

### 3.2. Anh trước – Việt sau

- Tiêu đề: `Article 1. English title / Điều 1. Tiêu đề Việt`; phụ lục: `APPENDIX ... / PHỤ LỤC ...`.
- Nội dung Separate: Anh trước, Việt sau theo từng điều khoản/căn cứ/mục, không xuất toàn bộ hợp đồng Anh rồi mới toàn bộ hợp đồng Việt.
- Combined: giữ nguyên thứ tự node do người dùng soạn. UI hướng dẫn viết Anh trước Việt sau trong mỗi đoạn/ô; renderer không tự phát hiện ngôn ngữ, đảo hàng hoặc đảo ô. Đây là giới hạn có chủ đích để bảo toàn bảng và ý nghĩa pháp lý.
- Bảng sản phẩm/dịch vụ: **một bảng**, nhãn Anh trên Việt trong cùng ô; tên và mô tả Anh trước Việt trong cùng ô; số tiền/số lượng chỉ xuất một lần. Loại hàng có cặp `Product / Sản phẩm`, `Service / Dịch vụ`. Dòng tổng `TOTAL / TỔNG CỘNG`, vẫn gộp cột và căn phải.
- Bảng do người dùng soạn trong Combined: một cây bảng từ editor, render một lần. Không yêu cầu hai bản bảng có số hàng/cột giống nhau và không viết thuật toán tự ghép hai bảng Separate.
- Căn cứ, câu tham chiếu phụ lục, nhãn chữ ký, thông báo dữ liệu mẫu: có nhãn/câu Anh trước Việt khi hệ thống sinh chúng.
- Giữ đúng đại diện khách hàng là **người liên lạc** ở những vị trí nghiệp vụ hiện đang dùng người liên lạc. Không đổi về đại diện pháp luật trong quá trình thêm tiếng Anh.
- Phụ lục đi cùng hợp đồng chưa ký vẫn không sinh “ký ngày” khi `SignDate = null`; căn cứ nghiệm thu của hợp đồng đã ký vẫn dùng ngày ký thật, cả Anh và Việt.

**Giới hạn DOCX nguồn:** renderer chỉ thay marker và sinh dynamic block, không có đủ thông tin để tự dịch hoặc đổi thứ tự mọi đoạn chữ cố định trong Word. Người soạn phải đặt câu tĩnh và các marker tương ứng theo thứ tự Anh trước Việt sau. Bổ sung hướng dẫn/mẫu minh họa và cảnh báo kiểm tra trước phát hành; không tuyên bố renderer tự biến DOCX Việt thành DOCX song ngữ hoàn chỉnh.

### 3.3. Placeholder tiếng Anh và nguồn dữ liệu

Giữ các placeholder tùy chọn. Không chuyển cả catalog thành bắt buộc. Phân biệt **marker không có trong DOCX** với **marker đã dùng nhưng chưa có dữ liệu tiếng Anh**: trường hợp thứ hai cần báo lỗi có tên trường khi tạo tài liệu cuối/phát hành, không lấy tiếng Việt thay vào.

Danh sách dự kiến tối thiểu, Phase 0 phải chốt từng mapping và phát hiện trùng khóa custom:

| Nhóm | Placeholder cần bổ sung | Nguồn/định dạng |
| --- | --- | --- |
| Hợp đồng | `CONTRACT_DATE_EN`, `EFFECTIVE_DATE_EN`, `EXPIRE_DATE_EN` | Cùng giá trị ngày, formatter tiếng Anh rõ ràng, ví dụ `15 August 2026`; không lệ thuộc culture máy chủ |
| Giá trị bằng chữ | `CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN` | Formatter số tiền tiếng Anh theo tiền tệ được hỗ trợ; dùng đúng decimal và quy tắc làm tròn của bản Việt |
| Khách hàng | `CUSTOMER_COMPANY_EN`, `CUSTOMER_ADDRESS_EN`, `CUSTOMER_BANK_NAME_EN`, `CUSTOMER_CITY_EN`, `CUSTOMER_COUNTRY_EN` | Các trường EN tùy chọn trong module Khách hàng; snapshot khi chốt version hợp đồng |
| Chức danh người liên lạc | `CUSTOMER_CONTACT_TITLE_EN` | Trường chức danh người liên lạc tiếng Anh tùy chọn trong module Khách hàng; không map nhầm chức danh đại diện pháp luật |
| Bên cung cấp | `PROVIDER_LEGAL_NAME_EN`, `PROVIDER_ADDRESS_EN`, `PROVIDER_REPRESENTATIVE_TITLE_EN`, `PROVIDER_BANK_NAME_EN` | Các trường EN bắt buộc trong Hồ sơ pháp lý; snapshot khi chốt version hợp đồng |
| Nghiệm thu | `ACCEPTANCE_DATE_EN`, `ACCEPTANCE_LOCATION_EN`, `CUSTOMER_LEGAL_NAME_EN`, `CUSTOMER_ADDRESS_EN`, `CUSTOMER_REPRESENTATIVE_TITLE_EN`, `TENANT_LEGAL_NAME_EN`, `TENANT_ADDRESS_EN`, `TENANT_REPRESENTATIVE_TITLE_EN` | Ngày định dạng EN và bản tiếng Anh của party/location nghiệm thu, đóng băng cùng biên bản |

- `CONTRACT_NAME_EN` đã có: dùng lại, không tạo khóa mới cùng nghĩa. Mã, MST, email, số điện thoại, tài khoản, currency code và tên người không cần marker `_EN` giả chỉ để lặp cùng dữ liệu; giữ nguyên tên người, không tự bỏ dấu.
- Dynamic marker như `CONTRACT_TERMS`, `CONTRACT_ITEM_TABLE`, `CONTRACT_APPENDICES`, `SIGNATURE_CUSTOMER` tiếp tục dùng một marker và render theo LanguageMode; không thêm marker `_EN` khiến một bảng/block bị chèn hai lần.
- Rà source registry/custom placeholder: các nguồn tiếng Anh mới phải chọn được trong form custom placeholder và có sample EN thật. Catalog, resolver và sample dataset phải phủ cùng tập khóa.
- Nếu khóa mới trùng custom placeholder đã khai báo: báo trùng rõ ràng và yêu cầu tác giả cấu hình lại; không tạo alias, silently remap hoặc converter.
- Dữ liệu tiếng Anh không phải dịch máy của dữ liệu Việt. Không tạo marker EN nhưng vẫn trả chuỗi Việt; không fallback khi tiếng Anh thiếu. Thông báo thiếu dữ liệu gắn đúng trường và vị trí UI cần nhập.
- Ở module Khách hàng, không bắt nhập mọi bản dịch chỉ vì tài khoản có dùng mẫu song ngữ. Ở bước render hợp đồng thật/trình duyệt, chỉ yêu cầu các field EN mà marker hoặc dynamic block thực sự dùng. Riêng bốn trường EN mới của Hồ sơ pháp lý luôn bắt buộc khi lưu hồ sơ, không phụ thuộc marker hoặc LanguageMode. Các dữ liệu dynamic block như tên phụ lục/tiêu đề và nội dung Separate kiểm tra theo đúng chế độ; tên pháp nhân dùng trong căn cứ EN cũng là dependency của block đó.
- Preview mẫu dùng sample Anh/Việt rõ ràng; tài liệu hợp đồng/nghiệm thu thật không được lấy dữ liệu mẫu. Không đưa dữ liệu giả vào snapshot thật.

### 3.3.1. Nơi nhập và mức bắt buộc của dữ liệu master

| Module | Trường tiếng Anh mới | Bắt buộc khi lưu master? |
| --- | --- | --- |
| Hồ sơ pháp lý | `LegalEntityNameEn` — Tên pháp nhân tiếng Anh | Có |
| Hồ sơ pháp lý | `AddressEn` — Địa chỉ tiếng Anh | Có |
| Hồ sơ pháp lý | `RepresentativeTitleEn` — Chức danh người đại diện tiếng Anh | Có |
| Hồ sơ pháp lý | `BankNameEn` — Tên ngân hàng tiếng Anh | Có |
| Khách hàng | `CustomerCompanyEn`, `CustomerAddressEn`, `CustomerBankNameEn`, `CustomerCityEn`, `CustomerCountryEn` | Không |
| Khách hàng | `CustomerContactPersonTitleEn`, `CustomerRepresentativeTitleEn` | Không; hai chức danh khác nhau, không tự thay thế nhau |

- Các ràng buộc bắt buộc nêu trên áp dụng cho **trường tiếng Anh bổ sung**. Không tự chuyển fax, điện thoại, tài khoản hoặc mọi trường tùy chọn hiện hành thành bắt buộc.
- Hồ sơ pháp lý: thêm input EN cạnh trường Việt tương ứng, dấu `*`, trim và chặn rỗng/whitespace ở cả FE/BE; schema dùng cột `nvarchar` không null và CHECK không rỗng cho các trường EN bắt buộc. Áp dụng khi tạo/cập nhật hồ sơ, kể cả tenant chỉ đang dùng hợp đồng tiếng Việt.
- Khách hàng: hiển thị trường EN cạnh trường Việt hoặc trong nhóm mở rộng “Thông tin tiếng Anh”; không dấu `*`, để trống vẫn tạo/sửa khách hàng được. BE chuẩn hóa rỗng thành null; chỉ kiểm tra độ dài nếu có giá trị.
- Ví dụ: muốn cung cấp giá trị cho `PROVIDER_BANK_NAME_EN`, vào **Quản trị → Hồ sơ pháp lý → Thông tin ngân hàng → Tên ngân hàng tiếng Anh**. Nhập một lần dùng chung cho các tài liệu mới; ở template chỉ sao chép marker.
- Khách hàng chưa có bản dịch vẫn sử dụng được với hợp đồng tiếng Việt. Nếu tài liệu song ngữ thật cần một bản dịch đang thiếu, báo rõ tên trường, khách hàng và hướng dẫn về màn sửa khách hàng theo quyền hiện có. Người không có quyền sửa không được cấp quyền ngầm; cần nhờ người phụ trách bổ sung.
- Không tạo thêm bảng bản dịch theo version chỉ để nhập lại dữ liệu master. Snapshot là dữ liệu đóng băng phục vụ tài liệu, không phải màn nhập master thứ hai. Không thêm tính năng override bản dịch riêng cho hợp đồng trong phạm vi này.

### 3.4. Dữ liệu, snapshot và hash

1. Thêm cột byte `BilingualContentMode` vào các entity/bảng đang lưu nội dung: `TblContractTemplateTerm`, `TblContractTemplateAppendixTerm`, `TblContractTerm`, `TblContractAppendixTerm`, `TblContractAcceptanceSection`. Cấu hình CHECK enum, DTO request/response có mode, chống client bỏ quên mode khi cập nhật.
2. Nội dung template được sao chép sang contract/appendix/acceptance phải copy cả mode. Version mới/lịch sử/negotiation/revision phải giữ nguyên mode. Rà mọi `new`/projection của các entity, không chỉ endpoint create.
3. Không tạo cột JSON mới. Rich text vẫn dùng schema hiện hành đã được dự án cho phép; không tạo schema rich text mới chỉ để chứa mode.
4. Thêm cột EN thường vào `TblTenantLegalProfile` và `TblCustomer` theo mục 3.3.1, đồng bộ DTO tạo/sửa/đọc, service, EF và FE. Không tạo `tbl_ContractVersionPartyTranslation`, bảng key-value hoặc cột JSON cho các bản dịch này.
5. Bản dịch chỉ sửa tại master theo quyền Hồ sơ pháp lý/Khách hàng hiện có. Bản xem trước nháp chưa chốt đọc master hiện hành và phải tính cả dữ liệu đó vào fingerprint; nếu master thay đổi giữa preview và submit, kiểm tra lại dữ liệu/fingerprint, không dùng artifact cũ. Không âm thầm sửa party/snapshot hoặc file của version đã chốt. Tên pháp nhân tiếng Anh không làm thay đổi danh tính bên ký hoặc tên pháp nhân Việt.
6. Khi tạo immutable legal snapshot: copy các trường tiếng Anh cần dùng từ master vào `TblContractVersionPartySnapshot`, cập nhật `TenantLegalSnapshot`/`CustomerLegalSnapshot` hoặc cấu trúc tương đương. Sau submit/render chính thức, không đọc lại master đang thay đổi để dựng lại version cũ.
7. Các dữ liệu chỉ dùng bởi scalar nhưng không nằm trong party snapshot (ví dụ city/country) phải được snapshot tương ứng hoặc đóng băng qua hạ tầng snapshot placeholder relational hiện có; không bỏ sót vì đang lấy trực tiếp từ master ở bản preview.
8. Nghiệm thu: thêm các cột tiếng Anh thường tương ứng vào party của biên bản và `LocationEn` vào record. Prefill từ version hợp đồng tham chiếu, sau đó theo quyền sửa party/location hiện hành của nghiệm thu; không cho sửa tên pháp nhân Việt đã bị khóa từ nghiệp vụ trước.
9. Snapshot model của term/appendix term phải mang mode. Hiện nội dung term đang được giữ theo version riêng, không mặc định rằng phải tạo bảng snapshot term mới; rà code lưu/đọc thực tế trước khi quyết định thêm bảng.
10. Canonical hash và fingerprint phải tính mode, nội dung có hiệu lực và dữ liệu tiếng Anh được dùng. Chuyển mode hoặc thay bản dịch phải làm preview cũ stale. Tăng renderer/catalog/dataset format version khi thay contract đầu vào, không thêm nhánh chạy renderer cũ.
11. Đây là thay đổi schema có migration. Dự án không bảo trì data cũ: không thêm script chuyển đổi hai nội dung cũ, không converter V3/V4, không thêm fallback/legacy hoặc runtime đoán mode. Migration không tự điền dịch EN giả; dữ liệu test cũ không hợp lệ phải được người dùng reset/tạo lại khi cần. Không tự xóa dữ liệu khi triển khai.
12. Theo quy tắc tenant DB hiện tại: giữ cột liên kết/index/unique/check cần thiết, query qua ID; migration không tạo mới physical FK nếu chính sách remove physical FK vẫn đang áp dụng. Không đưa lại cascade delete vật lý.
13. Các cột bản dịch văn bản mới dùng `nvarchar` với giới hạn phù hợp trường gốc; đồng bộ max length FE/DTO/EF để không bị cắt chuỗi hoặc mất ký tự trong tên pháp nhân/địa chỉ. Dùng cơ chế rowversion/audit sẵn có của từng module; chốt yêu cầu nullable của cột master và snapshot trong Phase 0. Cột EN snapshot không bắt mọi hợp đồng tiếng Việt phải có bản dịch khách hàng.

## 4. Luồng FE sau khi hoàn tất

### 4.1. Tác giả mẫu hợp đồng hoặc mẫu nghiệm thu

1. Chọn mẫu song ngữ, mở tab nội dung, thêm/sửa điều khoản hoặc mục nghiệm thu.
2. Nhập tiêu đề Anh và Việt. Chọn cách soạn **Tách riêng Anh/Việt** hoặc **Gộp nội dung song ngữ**.
3. Separate: thấy hai editor, Anh ở trên Việt ở dưới. Combined: thấy đúng một editor và hướng dẫn thứ tự Anh/Việt trong từng đoạn/ô bảng.
4. Với điều khoản thanh toán chứa bảng, chọn Combined và soạn một bảng song ngữ. Các đợt thanh toán có cấu trúc vẫn quản lý ở phần đợt thanh toán, không bị suy ra từ chữ trong bảng.
5. Lưu bằng cơ chế save/universal save/Ctrl+S hiện có; không xuất hiện thêm một nút lưu riêng chỉ cho mode. Giữ nguyên trạng thái mở/đóng card sau lưu.
6. Trong catalog marker, thấy cặp trường tiếng Anh rõ ràng, có sao chép marker và ví dụ. Sắp DOCX nguồn Anh trước Việt; tải lên, kiểm tra, tạo preview PDF/DOCX.
7. Hệ thống chỉ render một nội dung cho Combined và chỉ một bảng sản phẩm/dịch vụ; tên và nhãn tự sinh Anh trước Việt. Thiếu field bắt buộc theo mode/marker thì hiện lỗi cụ thể; tác giả quay lại đúng vùng nhập.

### 4.2. Người tạo/sửa/thương lượng hợp đồng

1. Chọn template: nội dung và mode từng điều khoản/phụ lục được copy đầy đủ.
2. Điền thông tin hợp đồng như hiện hành. Thông tin EN của bên cung cấp lấy từ Hồ sơ pháp lý, bên khách hàng lấy từ module Khách hàng; không thêm form nhập lại bản dịch riêng cho từng hợp đồng. Nếu thiếu dữ liệu khách hàng cần dùng, hiện lỗi và liên kết đến màn sửa khách hàng theo quyền; sau bổ sung cần tải lại dữ liệu/tạo lại preview.
3. Những điều khoản cho phép chỉnh dùng đúng shared editor và mode như template; điều khoản không được sửa vẫn bị khóa cả nội dung lẫn mode. Giữ quyền, term kind, milestones và rowversion hiện hành.
4. Lưu hợp đồng/nháp/thương lượng bằng cơ chế đang có. Tạo preview kiểm tra Anh trước Việt và không lặp bảng.
5. Trình duyệt: hệ thống snapshot mode/bản dịch, tạo file/hash như hiện hành. Master hoặc bản dịch mới không làm đổi nội dung tài liệu đã chốt.

### 4.3. Người tạo nghiệm thu

1. Chọn mẫu nghiệm thu đã phát hành: copy mode và nội dung của từng mục.
2. Bản dịch party được lấy từ version hợp đồng đã ký; người dùng nhập phần tiếng Anh còn thiếu theo quyền hiện hành và địa điểm tiếng Anh nếu marker sử dụng.
3. Mục Combined chỉ có một editor. Căn cứ tự sinh Anh trước Việt, có ngày ký hợp đồng thật; không lấy ngày tạo hoặc ngày chốt thay ngày ký.
4. Lưu nháp, tạo preview, chốt và tải bản ký theo luồng hiện hành; không thay quy tắc nghiệm thu một phần/toàn phần, đợt được nghiệm thu hoặc điều kiện đóng hợp đồng.

### 4.4. Người quản trị Hồ sơ pháp lý và người quản lý Khách hàng

1. Quản trị mở Hồ sơ pháp lý, nhập đủ bốn trường EN bắt buộc cạnh trường Việt; lưu thiếu/whitespace bị chặn ở FE và API, chỉ rõ từng trường.
2. Người có quyền tạo/sửa Khách hàng có thể nhập EN hoặc để trống toàn bộ; lưu khách hàng vẫn được, không buộc có hợp đồng song ngữ mới tạo được khách hàng.
3. Các tài liệu mới lấy dữ liệu master hiện hành. Đổi `BankNameEn` hoặc thông tin EN khác tại master không sửa hợp đồng/nghiệm thu đã chốt.
4. Đối với draft đang mở, sau khi master đổi phải nạp lại dữ liệu và tạo preview mới; preview cache dựa trên bản dịch cũ phải stale.

## 5. Các phase triển khai và điểm dừng

Quy ước: `Chưa bắt đầu` / `Đang làm` / `Hoàn tất` / `Bị chặn`. Chỉ đánh dấu hoàn tất khi đã có bằng chứng kiểm tra; viết code xong không đồng nghĩa UAT xong.

| Phase | Nội dung | Trạng thái | Bằng chứng hiện có |
| --- | --- | --- | --- |
| 0 | Chốt mapping và dependency matrix | Hoàn tất | BILINGUAL_CONTRACT_RENDERER_PHASE0_MATRIX.md; VND/USD, mapping master/snapshot/mode đã chốt |
| 1 | Schema, DTO và snapshot | Hoàn tất mã; chưa áp DB | Migration 20261018045400_BilingualContentAndPartyEnglish, test model/migration và hash/snapshot; không thêm physical FK |
| 2 | BE xử lý mode xuyên suốt các luồng | Hoàn tất | Policy dùng chung, copy/create/update/nego/acceptance và gate; full regression BE 874 đạt, 8 bỏ qua |
| 3 | Shared editor, Hồ sơ pháp lý/Khách hàng và tích hợp FE | Hoàn tất mã; chưa kiểm thử FE | Toggle/confirm/restore, payload mode, dirty-state, master inputs và acceptance EN; chỉ rà tĩnh |
| 4 | Nguồn dữ liệu EN, marker và formatter | Hoàn tất mã; FE chỉ rà tĩnh | Catalog hợp đồng V5/nghiệm thu V3, source registry EN, formatter ngày/tiền, dependency theo marker, nguồn snapshot và liên kết theo quyền |
| 5 | Chuẩn hóa renderer và thứ tự ngôn ngữ | Hoàn tất mã | Policy EN→VI dùng chung, Combined một body, bảng item EN trên VI, tổng/chữ ký/căn cứ/phụ lục; OpenXML và PDF fixture thực |
| 6 | Preview, hash, validation và DOCX hướng dẫn | Hoàn tất mã và hướng dẫn | Renderer V15/dataset V5; V15 undo tự xóa tiêu đề tĩnh DOCX; test stale theo mode/title, EN draft refresh/locked freeze, hướng dẫn DOCX riêng |
| 7 | Regression BE và đối chiếu DOCX/PDF | BE/OpenXML/PDF fixture đã kiểm tra; SQL/UAT còn chờ | LibreOffice thực có chạy; không dùng DB tenant thật. Kết quả chi tiết tại nhật ký cuối |
| 8 | Hướng dẫn triển khai và bàn giao test người dùng | Hoàn tất hướng dẫn; chưa thực hiện deploy/UAT | BILINGUAL_DOCUMENT_AUTHORING_AND_RELEASE.md: thứ tự API+FE+DB, cảnh báo hồ sơ pháp lý cũ, backup và checklist người dùng |

### Phase 0 — Khóa phạm vi và mapping

Bằng chứng: xem `BILINGUAL_CONTRACT_RENDERER_PHASE0_MATRIX.md`. Các step dưới đã chốt thiết kế; kiểm tra collision custom key theo dữ liệu tenant thực thuộc phase 4, không được tự đổi key.

- [x] Lập matrix từng field: marker, nguồn EN, màn nhập, cột/bảng, dependency dynamic block, snapshot, sample và test cần có.
- [x] Lập matrix từng luồng sao chép/đọc term: template version copy → create contract → draft → negotiation version → preview → snapshot/history; appendix và acceptance tương ứng.
- [x] Chốt tên trường mode/EN và enum, mapping master → snapshot; các field EN Hồ sơ pháp lý bắt buộc, Khách hàng tùy chọn theo mục 3.3.1. Liệt kê khóa custom có thể trùng catalog mới, không thay binding ngầm.
- [x] Chốt danh sách tiền tệ hỗ trợ formatter bằng chữ EN; tiền tệ chưa hỗ trợ phải báo rõ, không dùng chuỗi Việt thay thế.
- [x] Ghi các trường hợp cần tác giả sửa DOCX tĩnh và nội dung Combined; không cam kết tự dịch hoặc tự sắp ngôn ngữ trong rich text.

Điều kiện đạt: mapping không có marker EN “không biết lấy dữ liệu từ đâu”, không còn đường tạo tài liệu thiếu mode.

### Phase 1 — Schema, DTO, render model và snapshot

Migration gốc `20261018045400_BilingualContentAndPartyEnglish` đã sửa: bốn cột EN Hồ sơ pháp lý cho phép NULL để áp trên bảng có dữ liệu. `20261018120857_CatalogItemEnglishNames` áp cùng quy tắc cho hai tên EN sản phẩm/dịch vụ. Migration nối tiếp `20261018144616_AllowPendingEnglishMasterFields` chỉ đồng bộ nullability cho tenant đã áp phiên bản cũ. Không backfill/default EN giả, không xóa hoặc UPDATE dữ liệu. Chỉ cần chạy lại `database update`, không sửa lịch sử migration/SQL tay. FE/BE vẫn bắt buộc các trường này khi tạo/sửa; EN chưa nhập vẫn bị chặn nếu tài liệu cần đến. NOT NULL ở DB là bước riêng sau khi nhập đủ EN thật, chưa đưa vào chuỗi hiện tại. Agent chưa áp DB; test model/SQL sinh ra không thay thế kiểm thử SQL Server thật.

- [x] Thêm enum/cột mode, check constraints, DTO request/response; thêm cột EN vào master Hồ sơ pháp lý/Khách hàng và snapshot/acceptance theo matrix Phase 0. Không tạo bảng nhập bản dịch theo version hợp đồng.
- [x] Mode là trường tường minh trong payload ghi nội dung; response trả đúng mode, không cho client cũ vô tình làm mất chế độ Combined khi sửa.
- [x] Tách `TypeVi/TypeEn`, `DescriptionVi/DescriptionEn` hoặc dữ liệu item tương đương trong render model; không ghép rồi tách chuỗi.
- [x] Cập nhật model EF/migration/model snapshot; kiểm tra migration chỉ có thay đổi dự kiến, không tạo physical FK và không chuyển đổi/xóa dữ liệu cũ.
- [x] Cập nhật legal snapshot factory/reader/writer/hash model và projections; không thêm JSON storage mới.

Điều kiện đạt của mã: round-trip lưu/đọc giữ mode/bản dịch, metadata EF và migration khớp, operations chỉ thêm cột/check. Kiểm thử áp migration trên SQL Server trống hoặc DB test có schema thuộc phase 7/8; không ghi là đã chạy khi chưa thực hiện.

### Phase 2 — BE mode, copy và kiểm tra dữ liệu

- [x] Template Add/UpdateTerm, appendix term, acceptance template section hiểu Separate/Combined.
- [x] Contract Create/UpdateDraft và negotiation clone/copy, Add/UpdateAppendixTerm giữ mode và điều kiện quyền/khóa hiện hành.
- [x] Acceptance create từ mẫu, update sections, finalize và render mang mode nhất quán.
- [x] Tạo/cập nhật Hồ sơ pháp lý chặn thiếu bốn field EN ở service và DTO; tạo/cập nhật Khách hàng chấp nhận null/rỗng EN. Cả hai kiểm tra max length, quyền, audit và concurrency theo module hiện hành.
- [x] Tạo policy validation dùng chung thay vì mỗi service tự kiểm tra song ngữ khác nhau. Kiểm tra nội dung rich text thực sự rỗng, không chỉ `IsNullOrWhiteSpace` trên chuỗi JSON của editor.
- [x] Rà đồng thời căn cứ EN và các trường có cấu trúc của đợt thanh toán (TitleEn, ConditionEn). Combined chỉ thay cách lưu/render body điều khoản, không miễn kiểm tra dữ liệu EN của căn cứ hoặc các field milestone cần dùng độc lập. Không tự thêm chế độ Combined cho căn cứ nếu chưa có yêu cầu.
- [x] Combined không yêu cầu ContentEn, từ chối trường EN riêng khác rỗng; Separate song ngữ kiểm tra theo quy tắc nội dung và tiêu đề cần thiết. Không áp điều kiện Combined lên title/name EN.
- [x] Preview/publish/submit/finalize kiểm tra lại BE; lỗi có mã và thông báo tiếng Việt cụ thể, không rơi vào lỗi audit metadata chung.

Điều kiện đạt: tạo được điều khoản Combined qua API và không có validator bước sau bắt điền ContentEn; client sai mode bị từ chối mà không làm mất nội dung đã lưu.

### Phase 3 — Shared UI và form master

- [x] `TermFields`: toggle/select chế độ, một editor Combined hoặc hai editor EN→VI, nhãn và hướng dẫn rõ ràng.
- [x] `TermsListEditor`/card và phần xem chỉ đọc tôn trọng mode; không hiện ô “Chưa có nội dung tiếng Anh” cho Combined.
- [x] Tích hợp template terms, appendix terms, create contract, contract terms/appendices, acceptance sections và lịch sử/public view tương ứng.
- [x] Confirm chuyển mode khi có nội dung, giữ bản nháp cục bộ khi hủy, payload chỉ có dữ liệu của chế độ đã chọn.
- [x] Dirty-state/universal save/Ctrl+S tính cả mode; giữ collapsible, thứ tự và focus sau lưu, không thêm save phân mảnh.
- [x] Hồ sơ pháp lý: thêm bốn input EN bắt buộc tại đúng nhóm thông tin, validation realtime/lưu và dấu `*`, giữ universal save/Ctrl+S hiện hành.
- [x] Khách hàng: thêm input EN tùy chọn trong modal tạo/sửa, đồng bộ xem chi tiết và response; không chặn lưu master khi thiếu bản dịch.
- [x] Hợp đồng: không thêm form nhập bản dịch riêng hoặc mở khóa trường nghiệp vụ. Phần thông báo thiếu nguồn EN và đường dẫn về master được triển khai tiếp ở phase 4; đã hoàn tất mã, FE chỉ rà tĩnh.

Điều kiện đạt: một mode dùng chung trong mọi màn; chỉ một rich editor khi Combined; lưu/mở lại không đổi mode hoặc mất bảng. Không chạy test FE tự động/browser nếu người dùng chưa yêu cầu.

### Phase 4 — Catalog, resolver và nguồn EN

- [x] Tích hợp lỗi dependency EN của marker mới với giao diện hợp đồng, nêu đúng trường/master cần bổ sung và liên kết theo quyền; đã nối tiếp phần chuyển từ phase 3, chỉ rà tĩnh FE. Không thêm form override bản dịch tại hợp đồng.
- [x] Thêm đúng các key từ Phase 0 vào software-supply/acceptance catalogs và source provider/registry/resolver liên quan.
- [x] Cập nhật sample dataset và catalog UI: nhãn Việt dễ hiểu, ví dụ EN thật, sao chép marker; mọi marker vẫn tùy chọn.
- [x] Source provider đọc các cột EN từ master đã nạp; khi submit copy/freeze đúng vào legal snapshot và khi tạo nghiệm thu prefill từ signed version. Tài liệu đã chốt đọc dữ liệu đóng băng, không truy vấn master cho bản dịch mới.
- [x] Thêm formatter ngày EN và số tiền bằng chữ EN, test culture và decimal/currency; không dùng dịch tự động.
- [x] Kiểm tra thiếu dữ liệu chỉ theo marker/block thật sự dùng; cảnh báo/lỗi có hướng xử lý. Không fallback sang VI hoặc sample trong file thật.
- [x] Bulk load dữ liệu render một lần theo context; không truy vấn DB cho từng placeholder, không tạo N+1.

Điều kiện đạt: key mới được nhận diện từ upload đến preview và render thật, sample và dữ liệu thật không lệch; marker thiếu dữ liệu cho lỗi dễ hiểu.

### Phase 5 — Renderer thống nhất EN→VI

- [x] Tạo helper/policy thứ tự ngôn ngữ dùng chung để template preview và acceptance thật không tự viết hai bộ quy tắc.
- [x] Terms/appendix terms/acceptance sections: tiêu đề EN→VI, Separate body EN→VI, Combined render chính xác một body.
- [x] Legal bases/references/appendix heading/notice/signature labels: EN→VI. Căn cứ trống chỉ bỏ marker động, giữ nguyên tiêu đề tĩnh của DOCX theo yêu cầu cập nhật; phụ lục không có ngày ký vẫn không có cụm ký ngày.
- [x] Bảng hàng hóa/dịch vụ: chỉ một bảng, header EN→VI, item type/name/description EN→VI; giữ grid/width/cell margin/merge, nhãn TOTAL→TỔNG CỘNG căn phải.
- [x] Chữ ký hợp đồng/phụ lục/nghiệm thu đều có nhãn EN→VI; phụ lục chỉ hai nhãn đại diện như nghiệp vụ hiện tại, không tự thêm tên/địa chỉ hai bên.
- [x] Giữ alignment của marker và định dạng rich text/cell; không áp căn giữa lên toàn nội dung chỉ vì tiêu đề đang center.
- [x] Không suy đoán bảng nhập tay là lịch thanh toán tự sinh. Không tái thêm block lịch thanh toán cũ/fallback.

Điều kiện đạt: các vị trí hệ thống sinh đều Anh trước Việt; Combined có một lần kết xuất, Separate vẫn hai phần theo đúng lựa chọn; DOCX và PDF dùng cùng tài liệu nguồn đã merge.

### Phase 6 — Preview/hash và hướng dẫn tác giả

- [x] Canonical/fingerprint/hash tính mode và các bản dịch được dùng; kiểm tra stale sau đổi mode, title/content EN hoặc party translation.
- [x] Tăng phiên bản format/catalog/dataset cần thiết, cập nhật test coverage key. Không giữ renderer cũ để mở data test cũ.
- [x] Rà các bước upload/preview/publish của software-supply và acceptance: không bị validator cũ bắt hai nội dung cho Combined, không buộc mọi marker phải có.
- [x] Viết đoạn DOCX mẫu/hướng dẫn chèn EN marker trước VI marker và bố trí câu tĩnh; hướng dẫn một bảng song ngữ trong Combined.
- [x] Ghi rõ preview/artifact cũ không tự thay đổi. Tác giả phải tạo preview mới, kiểm tra và phát hành phiên bản mới khi cần; không ghi đè file đã ký.

Điều kiện đạt: không tái sử dụng preview sai format và không có trường hợp preview mẫu đúng nhưng render hợp đồng/nghiệm thu sai thứ tự.

### Phase 7 — Regression backend và kiểm tra tài liệu

- [ ] Toàn bộ SQL integration theo matrix: còn chờ DB kiểm thử được cho phép. Unit/InMemory/renderer/backend regression đã chạy, fixture mới đủ EN; không nới validator hoặc sửa snapshot khóa để fixture vượt qua.
- [x] Test serialized API/snapshot round-trip, ownership/permission/rowversion và tránh N+1.
- [x] Kiểm tra OpenXML: số bảng, thứ tự paragraph/run, merged cell, widths, không còn token chưa thay và không sinh hai lần body Combined.
- [x] Chạy LibreOffice thật và xem ảnh PDF fixture hợp đồng/phụ lục/nghiệm thu: kiểm tra thứ tự ngôn ngữ, một bảng Combined, merge và căn chữ ký. Các service contract/acceptance có regression riêng; chưa UAT PDF với bộ DOCX nghiệp vụ thật của người dùng. Không coi fixture đạt là mọi bố cục tác giả đều đã đạt.
- [x] Chạy regression BE theo phạm vi phù hợp, ghi lệnh/kết quả vào nhật ký. Không test FE/browser theo yêu cầu hiện tại của người dùng.

Bằng chứng hiện có: regression BE, OpenXML, 4 test LibreOffice thực (ba release gate có sẵn + một test song ngữ hợp đồng/nghiệm thu), đã xem ảnh PDF fixture hợp đồng/phụ lục/nghiệm thu. Chưa có bằng chứng migration/transaction SQL cho thay đổi mới hoặc UAT tài liệu nghiệp vụ thực tế. Cấu hình cột quá hẹp vẫn có thể xuống dòng, không tự thay width đã chọn.

Điều kiện đạt: test BE và kiểm tra tài liệu có bằng chứng; những phần chưa chạy phải ghi rõ, không gộp vào trạng thái hoàn tất.

### Phase 8 — Triển khai và bàn giao

Đã bàn giao hướng dẫn tại `BILINGUAL_DOCUMENT_AUTHORING_AND_RELEASE.md`; không đồng nghĩa đã backup, migration, deploy hoặc UAT. Bốn cột EN Hồ sơ pháp lý không thể được điền trước khi cột tồn tại: với DB có hồ sơ cũ cần quyết định môi trường/dữ liệu được cho phép, không chạy mù migration và không thêm backfill giả.

- [x] Ghi chính xác migration mới, thứ tự triển khai API+FE, bước điền đủ EN Hồ sơ pháp lý và bước tạo lại sample/template/preview; không tự áp migration khi chưa được yêu cầu. Không thêm default dịch giả để vượt NOT NULL/CHECK trên DB test cũ; nếu dữ liệu cũ gây xung đột, nêu rõ và chỉ reset khi người dùng cho phép.
- [ ] Backup DB/storage trước thao tác môi trường thật, không xóa dữ liệu/file ngoài scope; data test không còn hợp lệ được xử lý thủ công có xác nhận.
- [x] Ghi các bước test FE dành cho người dùng theo luồng mục 4 và matrix mục 6; cập nhật trạng thái UAT riêng.
- [x] Nếu đã sửa code: chạy `graphify update .` ở Source, không chạy trong từng module con.
- [x] Chốt danh sách file đổi, phần còn thiếu, test đạt/bỏ qua, cảnh báo deploy FE/BE đồng bộ; không để schema/DTO mới bị dùng với client cũ.

Điều kiện đạt: có hướng dẫn tiếp tục và triển khai đủ rõ, không phụ thuộc lịch sử chat.

## 6. Matrix kiểm thử và kết quả mong đợi

| Tình huống | Kết quả đúng |
| --- | --- |
| Mẫu tiếng Việt | UI vẫn một editor; tài liệu không sinh thêm nhãn/nội dung EN |
| Song ngữ Separate có văn bản | Tiêu đề và nội dung EN trước VI; cả hai phần đúng dữ liệu |
| Song ngữ Combined có bảng 3×4 | Một bảng 3×4 được xuất từ body đó; không render thêm body EN |
| Combined có ô merge và độ rộng riêng | Grid span/row span/width/alignment giữ đúng từ rich text trong DOCX/PDF |
| Separate chứa một bảng mỗi ngôn ngữ | Vẫn hai bảng, EN trước VI; UI hướng dẫn Combined nếu muốn một bảng |
| Combined thiếu ContentEn | Lưu/preview/publish được nếu nội dung chính và field cần khác hợp lệ |
| Combined gửi cả ContentEn khác rỗng | API từ chối rõ, không âm thầm bỏ phần nội dung |
| Separate thiếu EN hoặc chỉ rich text rỗng | Chặn tại gate yêu cầu nội dung đầy đủ, nêu chính xác điều khoản/trường |
| Song ngữ thiếu title/name phụ lục EN | Vẫn bị chặn; Combined không làm các tên này thành tùy chọn |
| Đổi mode có nội dung rồi hủy | Không mất nội dung, không tự nối hai bảng hoặc tự lưu |
| Save/universal save/Ctrl+S, mở lại | Mode và nội dung đúng, card không tự collapse/reset, không save riêng lẻ |
| Copy template version/create contract/negotiation | Mode, title, content và bản dịch của version đúng, không sửa version cũ |
| Hợp đồng/phụ lục locked hoặc readonly/history | Không sửa được mode/nội dung trái quyền; xem đúng snapshot |
| Bảng hàng hóa/dịch vụ song ngữ | Một bảng; nhãn, loại, tên/mô tả EN→VI; tổng tiền không bị nhân đôi |
| Bảng item không có tên/mô tả EN cần render | Lỗi dependency rõ theo chính sách Phase 0, không tự thay bằng VI |
| Ngày EN trên server khác culture | Cùng kết quả định dạng, không đổi ngày vì xử lý múi giờ không cần thiết |
| Tiền bằng chữ EN | Khớp giá trị làm tròn và tiền tệ của tổng, không đổi số tiền kế toán |
| Marker EN tùy chọn không có trong DOCX | Không ép nhập bản dịch khách hàng nếu không có dependency khác; các field EN Hồ sơ pháp lý vẫn bắt buộc khi lưu master |
| Lưu Hồ sơ pháp lý thiếu một field EN hoặc chỉ whitespace | FE/BE từ chối đúng field, kể cả chưa sử dụng marker EN hoặc chỉ dùng mẫu tiếng Việt |
| Lưu Hồ sơ pháp lý có đủ bốn field EN | Thành công; các trường tùy chọn sẵn có như fax không bị bắt nhập thêm |
| Tạo/sửa Khách hàng không có trường EN nào | Thành công, lưu null; hợp đồng tiếng Việt vẫn dùng được |
| Khách hàng thiếu EN mà tài liệu song ngữ thật cần | Không cấm lưu khách hàng; render/submit báo trường thiếu và hướng đến màn sửa theo quyền, không lấy VI thay EN |
| Nhập BankNameEn một lần tại Hồ sơ pháp lý | PROVIDER_BANK_NAME_EN trong tài liệu mới nhận đúng giá trị, không cần nhập lại theo hợp đồng |
| Marker EN có nhưng thiếu dữ liệu | Nêu tên trường cần điền, không in token hoặc dữ liệu Việt giả EN |
| Không có căn cứ | Marker căn cứ được thay bằng nội dung rỗng; giữ nguyên dòng “Căn cứ vào:” và chữ tĩnh tác giả đặt trong DOCX |
| Phụ lục chưa có ngày ký | Reference EN→VI, không có “signed on/ký ngày” với ngày null |
| Nghiệm thu từ hợp đồng đã ký | Reference EN→VI có SignDate thật; section Combined một lần, signature đúng |
| Đổi bản dịch/master sau khi chốt | Artifact/snapshot cũ không đổi; chỉ version mới nhận dữ liệu mới |
| Thay mode hoặc bản dịch đang dùng trong preview | Preview cũ stale, lần tạo mới không dùng cache sai |
| DOCX nguồn chỉ có câu tĩnh tiếng Việt | Không tự xuất bản dịch giả; có hướng dẫn tác giả sửa DOCX, không tự reorder câu tĩnh |
| Nghiệp vụ thanh toán/approval/partial acceptance | Quyền, trạng thái, tiền, tỷ lệ, milestone và điều kiện đóng giữ nguyên |

## 7. Điểm code dự kiến cần rà/sửa

Các đường dẫn tương đối dưới `E:/DTC-DigitizingContract/Source`:

- BE catalogs/policies: `Backend/ContractManagement.API/Domains/Policies/ContractTemplate/{SoftwareSupplyPlaceholderCatalog,AcceptanceRecordPlaceholderCatalog,SoftwareSupplyPreviewDatasetV1,ContractTemplateRenderData}.cs`.
- BE template: `Backend/ContractManagement.API/Domains/Services/ContractTemplate/ContractTemplateService.cs`, `ContractTemplateService.Appendices.cs`, `ContractTemplatePreviewRenderer.cs`, `ContractPlaceholderSourceProviders.cs`, `ContractPlaceholderSourceRegistry.cs` và resolver/catalog liên quan.
- BE contract: `Backend/ContractManagement.API/Domains/Services/Contract/ContractService.cs`, các partial chứa terms/appendices/payment draft/snapshot; `ContractDocumentPreviewService.cs`, `ContractAcceptanceService.cs`, `ContractAcceptanceDocumentRenderer.cs`.
- BE immutable data: `Backend/ContractManagement.API/Domains/Models/Contract/SoftwareSupplyContractSnapshot.cs`; `Backend/ContractManagement.Infrastructure/Persistence/Application/Models/TblContractVersionLegalSnapshot.cs`, context snapshot config và các entity term/appendix/acceptance.
- BE DTO: cả request/response template, contract, acceptance; audit vocabulary nếu cần ghi thêm mode phải rà CHECK/schema tương ứng trước, không tự thêm enum vượt miền DB.
- BE master: `Backend/ContractManagement.API/Domains/Services/Tenant/TenantLegalProfileService.cs`, service Khách hàng, DTO tạo/sửa/đọc tương ứng và `TblTenantLegalProfile`/`TblCustomer` cùng EF config.
- FE shared: `Frontend/components/contract-documents/{term-fields,terms-list-editor}.tsx`, contract term card và bộ controller/form state đang dùng chung.
- FE template: editors terms/appendices/legal bases/payment milestones, placeholder catalog/form và trang khu vực biên soạn.
- FE contract: create, terms, appendices, version snapshot tabs, public contract view và acceptance records; service types/serializer/dirty payload liên quan.
- FE master: `Frontend/app/(protected)/admin/legal-profile/page.tsx`, `Frontend/app/(protected)/customers/customer-form-modal.tsx`, trang chi tiết Khách hàng và các service/type/validation tương ứng.
- Tests: các test template preview/service, contract document preview, legal snapshot, appendix/negotiation và acceptance renderer/service. Không tạo test FE hoặc chạy trình duyệt trong phạm vi đã chốt hiện tại.

## 8. Nhật ký và cách tiếp tục khi ngắt quãng

Trước khi dừng mỗi lần triển khai, cập nhật đủ:

| Ngày | Phase/step | Việc đã làm | File/migration đổi | Lệnh và kết quả kiểm tra | Việc còn lại/blocker | Bước tiếp theo |
| --- | --- | --- | --- | --- | --- | --- |
| 18/10/2026 | Lập kế hoạch | Đối chiếu PDF, khảo sát catalog/render model/shared form; chốt đề xuất mode và thứ tự | Chỉ file plan này | Chưa chạy test/migration cho tính năng mới | Tất cả phase chưa bắt đầu | Phase 0: lập dependency matrix và chốt mapping EN |
| 18/10/2026 | Cập nhật nguồn dữ liệu theo yêu cầu | Chuyển nơi nhập EN sang Hồ sơ pháp lý (bắt buộc bốn field mới) và Khách hàng (EN tùy chọn); bỏ bảng/form nhập bản dịch theo contract version, cập nhật snapshot, phase và test | Chỉ file plan này | Đối chiếu model master, chưa sửa/chạy code | Tất cả phase vẫn chưa bắt đầu | Phase 0 theo mapping master mới |
| 18/10/2026 | Triển khai phase 0–3, trước khi ngắt | Thêm mode/EN master/party snapshot/acceptance, policy, migration, shared form/payload/dirty-state; test BE đạt 870, bỏ qua 8 tại checkpoint, thêm 3 test create/draft/nego chưa chạy vì hết usage | Các file bilingual/DTO/model/service/FE liên quan; migration 20261018045400 | Không áp migration, không test FE | Chưa cập nhật graph, chưa chốt 3 test mới; bảng trạng thái nay đã hiệu chỉnh | Tiếp tục regression, rà tĩnh form/copy, cập nhật graph |
| 18/10/2026 | Tiếp tục và xác minh sau ngắt | Sửa test dùng nhầm DTO vòng thương lượng, thêm revalidation ở readiness trình duyệt, rà batch-save khi đổi mode, tách Type/Description EN cho render model và sửa AddressEn acceptance lên 2000 để khớp master | Không thêm migration thứ hai; sửa migration chưa áp/model snapshot cho đúng độ dài | Lượt BE sau sửa test đạt 873, bỏ qua 8; đang chạy lại sau thay đổi item model/địa chỉ EN | FE chưa kiểm thử; SQL/LibreOffice integration bỏ qua; phase 4 marker/formatter và phase 5 thứ tự chưa hoàn thành | Chốt test cuối và graph, sau đó phase 4 |
| 18/10/2026 | Chốt xác minh phase 0–3 | Sửa fixture item EN theo render model mới, thêm regression Combined có bảng chỉ xuất một body/bảng; giữ title EN ở readonly card; đồng bộ model snapshot/migration AddressEn 2000 | Migration 20261018045400 chưa áp; renderer format V13 để không tái dùng cache trước thay đổi model | Full `dotnet test ... -c Release --no-restore -p:OutputPath=E:/DTC-DigitizingContract/Source/.test-output/contract-editors/` đạt 874, bỏ qua 8, 0 lỗi; test metadata xác nhận không pending model changes, không FK/default EN giả | Không chạy FE/build/typecheck/browser; không cập nhật DB. SQL/LibreOffice thật chưa kiểm chứng | Phase 4: source providers/catalog/formatter EN và tích hợp lỗi thiếu nguồn EN vào FE; phase 5 mới thống nhất EN→VI toàn renderer |

### Kết quả bàn giao phase 4–8 ngày 18/10/2026

- Phase 4–6 đã triển khai mã: thêm 14 marker EN hợp đồng và 8 marker EN nghiệm thu, đều tùy chọn; source custom EN/dependency theo field snapshot; lỗi API cụ thể và liên kết FE theo quyền. Hai renderer dùng policy EN→VI, Combined xuất một body, bảng item có line break thật Anh trên Việt, giữ grid/width/margin/merge/alignment.
- Snapshot/hash chứa mode và EN; nghiệm thu dùng nguồn EN của signed snapshot. Custom EN của draft refresh nhưng locked version không đọc master mới; custom không phải EN giữ cơ chế capture cũ. Snapshot và custom marker hợp đồng dùng cùng context đã nạp, tránh đọc master lần hai gây lệch bản dịch.
- Formatter EN ngày dùng en-GB; tiền VND/USD dùng decimal/AwayFromZero. Sửa trình bày tổng USD và bằng chữ VI để không mất phần xu; không đổi số tiền, thuế, tỷ lệ hoặc phép tính tài chính lưu DB. Không lấy VI/default/sample thay EN thiếu.
- Format mới: software catalog V5, acceptance catalog V3, dataset V5, renderer V14. Không giữ renderer cũ/converter dữ liệu. Migration vẫn là `20261018045400_BilingualContentAndPartyEnglish`, chưa áp; phase 4–8 không thêm migration mới.
- Test cuối: **902 đạt, 5 bỏ qua SQL integration, 0 lỗi (907 tổng)**. Lệnh: `dotnet test Backend/ContractManagement.Tests/ContractManagement.Tests.csproj -c Release --no-restore -p:NuGetAudit=false -p:OutputPath=E:/DTC-DigitizingContract/Source/.test-output/contract-editors/ --verbosity minimal`, với `PHASE12_LIBREOFFICE_PATH=C:/Program Files/LibreOffice/program/soffice.exe`. Bốn test LibreOffice chạy thật. Đã xem ảnh PDF fixture hai trang contract/appendix, một trang acceptance; artifacts ở `.test-output/bilingual-documents/` được gitignore. Không coi fixture là UAT DOCX nghiệp vụ thật.
- Test mới bao phủ formatter/culture/cents, marker optional/missing EN, source collision, EN draft refresh/locked freeze, cùng render context, nghiệm thu dùng signed source, cache stale theo mode/title, OpenXML thứ tự/break/table/merge/alignment, response lỗi API và readiness. Regression hiện có bao phủ permission/rowversion/copy/create/nego/partial acceptance/payment. Metadata migration/model đạt, không thay thế áp SQL thật.
- `git diff --check` đạt. `graphify update .` tại Source hoàn tất: 13.629 node, 33.348 edge. Hai cảnh báo AST có sẵn ở employee-profile-form.tsx/flutter header không phải kết quả build FE và không sửa ngoài phạm vi. NU1900 vulnerability NuGet vẫn là cảnh báo nguồn mạng.
- File bàn giao: `BILINGUAL_DOCUMENT_AUTHORING_AND_RELEASE.md`. Mã mới chính: `DocumentEnglishPolicy.cs`, `EnglishMoneyTextFormatter.cs`, `ContractEnglishDependencies.cs`; sửa catalogs/dataset/source registry/value service, hai renderers, contract preview/acceptance/readiness, middleware và contract FE. Test mới: `BilingualDocumentRenderingTests.cs`, `DocumentEnglishApiErrorTests.cs`; mở rộng test custom/preview/acceptance/mapping đã có.
- **Chưa thực hiện:** FE build/typecheck/browser, áp migration/đọc DB tenant, SQL integration cần DB, backup/deploy và UAT. Hồ sơ pháp lý cũ có hàng cần quyết định chuẩn bị DB sạch hoặc xử lý có phép trước migration NOT NULL, không điền EN giả.
- **Tiếp tục từ đây:** đọc hướng dẫn, chọn DB/môi trường được phép; chạy SQL integration/migration theo quy trình tenant khi dữ liệu đã sẵn sàng, sau đó người dùng tự test FE. Không còn bước mã phase 4–6 cần làm lại.

### Cập nhật theo yêu cầu: giữ chữ tĩnh của DOCX

- Đã gỡ hoàn toàn `RemoveEmptyLegalBasesHeading` và lời gọi. Dòng “Căn cứ vào:”/“Pursuant to:” do tác giả soạn trong DOCX không bị tự xóa khi căn cứ trống; chỉ marker động được thay bằng danh sách căn cứ hoặc nội dung rỗng.
- Renderer tăng V15 để bản xem trước tạo bởi hành vi cũ không được tái sử dụng. Cần tạo lại preview; artifact đã chốt/ký không bị thay đổi.
- Đổi regression thành `Renderer_LegalBases_PreservesAuthoredHeadingEvenWhenEmpty`, kiểm tra giữ nguyên tiêu đề có/không có căn cứ. UI căn cứ/điều khoản phân nhóm EN/VI vẫn giữ nguyên, không undo các thay đổi đó.
- Kiểm tra sau undo: 79 test renderer/preview đạt, 1 test LibreOffice bỏ qua vì lần này không đặt biến executable, 0 lỗi. Đã chạy `graphify update .`; không chạy FE hoặc migration, không thay file đã chốt.

Quy tắc tiếp tục:

1. Đọc bảng trạng thái và dòng nhật ký mới nhất trước khi sửa; đối chiếu `git diff/status` với các thay đổi đang có. Không khôi phục/reset code của người dùng hoặc triển khai lại refactor đã hoàn thành.
2. Chỉ tick step đã làm thật; ghi rõ phần code đã viết nhưng chưa test, migration đã tạo nhưng chưa áp, và UAT chưa chạy.
3. Nếu bị ngắt giữa phase: ghi hàm/file đang xử lý, payload/schema đã đổi và chỗ chưa được cập nhật. Lần sau làm tiếp ở step chưa hoàn tất, không bắt đầu lại toàn phase.
4. Không đánh dấu cả tính năng hoàn tất khi chỉ preview mẫu đã đúng; phải kiểm chứng contract thật, phụ lục, acceptance thật và dữ liệu snapshot/version.
5. Phát hiện phải đổi schema/mapping ngoài plan: cập nhật quyết định và lý do trước khi làm, không âm thầm mở rộng thành sửa toàn module master hoặc thêm fallback.
