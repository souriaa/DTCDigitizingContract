# Luồng biên bản bàn giao — lưu trữ thiết kế và hướng dẫn triển khai lại

Ngày cập nhật: 23/10/2026.

**Trạng thái: ĐÃ GỠ CHỨC NĂNG. Đây là tài liệu tham khảo, không phải mô tả tính năng đang chạy.**

## 1. Hiện trạng sau khi gỡ

- Closing không có biên bản bàn giao; code UI cũ và các khối comment đã bị xóa.
- Không còn component/editor phạm vi bàn giao, API client, controller/service/DI/model bàn giao.
- Không có loại mẫu bàn giao để chọn; không còn catalog marker, nhánh renderer hoặc chế độ editor dành cho bàn giao.
- Không còn tùy chọn tải tệp mới với phân loại bàn giao; không còn đường dẫn notification tới `section=handover`.
- Nghiệm thu không chọn nguồn bàn giao. Một phần phải chọn ít nhất một đợt thanh toán; nghiệm thu đủ tất cả đợt thì không tạo vòng mới. Đã chọn luồng một phần thì không quay về toàn bộ.
- Hoàn tất hợp đồng dựa trên bản ký hợp đồng hợp lệ, nghiệm thu đủ và thanh toán đủ; không có điều kiện phạm vi bàn giao.
- Hợp đồng Mua/Bán, đơn vị sản phẩm/dịch vụ, ngày ký thực tế nghiệm thu và các tính năng chung không bị gỡ.

### Phần cố ý giữ lại, không phải tính năng bàn giao đang hoạt động

1. **Migration lịch sử** và hai migration gỡ chức năng: cần để DB cũ lẫn DB mới đi qua đúng chuỗi phiên bản. Không xóa file migration đã có trong lịch sử.
2. **Đọc audit lịch sử:** giữ mã/nhãn trường, action và subject cũ trong lớp đọc để không mất khả năng tra cứu. Luồng ghi audit bàn giao đã bị gỡ. Audit không có liên kết tới UI bàn giao đã xóa.
3. **Tệp vật lý và metadata cũ:** không tự xóa private storage. Loại object không còn được hỗ trợ bị chặn bởi cơ chế phân quyền tệp chung; không mở một đường tải công khai để bù lại.
4. **Test gỡ schema, đọc lịch sử và hồi quy:** tên test có thể còn `Handover` để xác minh tính năng đã bị gỡ và không gây hồi quy.
5. **“Bàn giao công việc quản lý hợp đồng” khi chuyển người phụ trách:** đây là chức năng khác, không phải biên bản bàn giao sản phẩm/dịch vụ, nên vẫn giữ nguyên.
6. Từ “bàn giao” trong nội dung hợp đồng/nghiệm thu hoặc DOCX tham khảo là văn bản của người dùng, không đồng nghĩa có module bàn giao. Không tìm theo tên rồi xóa nhầm các tài liệu này.

## 2. Mốc mã nguồn và tài liệu gốc

- Mốc Git trước đợt gỡ: `66c65e017a1f67ba775d582bf4cb75f4b622bc21`.
- Plan gốc: `.codex/CONTRACT_HANDOVER_BEFORE_ACCEPTANCE_PLAN.md`. Các ô “HOÀN TẤT” trong đó là lịch sử trước khi gỡ, **không** chứng minh module còn hoạt động.
- Hướng dẫn áp migration gỡ: `Documentation/REMOVE_CONTRACT_HANDOVER_TABLES.md`.
- Schema nền cũ: `20261019090835_HandoverFoundationPhase03`.
- Migration gỡ bảng: `20261023090305_RemoveContractHandoverTables`.
- Migration dọn mẫu bàn giao và bỏ loại mẫu: `20261023092950_RetireHandoverTemplates`.

Có thể xem từng file cũ bằng Git, ví dụ chạy từ repo:

```powershell
git show 66c65e017a1f67ba775d582bf4cb75f4b622bc21:Source/Frontend/components/contracts/contract-handover-records.tsx
git show 66c65e017a1f67ba775d582bf4cb75f4b622bc21:Source/Backend/ContractManagement.API/Domains/Services/Contract/ContractHandoverService.cs
```

Không rollback nguyên repository hoặc phục hồi nguyên các file Closing/Acceptance/renderer chỉ để bật bàn giao: sẽ ghi đè các sửa lỗi mới. Đọc mã cũ, đối chiếu mã hiện hành và tích hợp có chủ đích.

## 3. Mục đích nghiệp vụ

Bàn giao ghi nhận bên cung cấp đã giao những hạng mục/khối lượng nào; nghiệm thu ghi nhận bên mua đã kiểm tra và chấp nhận những phần nào. Đây là hai chứng từ khác nhau.

Luồng cũ dự kiến:

Hợp đồng đã ký → xác nhận phạm vi → bàn giao từng phần/toàn bộ → ký bàn giao → nghiệm thu từ các phần đã giao → ký nghiệm thu → kích hoạt mốc thanh toán tương ứng → hoàn tất khi mọi điều kiện đủ.

Không mặc định một biên bản bàn giao = một nghiệm thu = một đợt thanh toán. Không lấy phần trăm thanh toán để suy ra số lượng đã giao.

### Vai trò Mua/Bán

| Chiều giao dịch | Bên giao/Provider | Bên nhận/Customer |
| --- | --- | --- |
| Bán | Doanh nghiệp sử dụng hệ thống | Đối tác |
| Mua | Đối tác | Doanh nghiệp sử dụng hệ thống |

Dùng resolver vai trò hiện hành. Quyền người dùng, tenant sở hữu dữ liệu và vị trí lưu tệp không đảo theo Mua/Bán. Không tự đổi chữ Bên A/Bên B do người dùng soạn trong DOCX.

## 4. Luồng người dùng nếu triển khai lại

### 4.1. Chuẩn bị mẫu

1. Quản trị tạo mẫu tài liệu bàn giao, chọn ngôn ngữ; mẫu chứng từ hỗ trợ không bắt chọn chiều Mua/Bán khi tạo. Chiều thực tế lấy từ hợp đồng khi sử dụng.
2. Soạn DOCX và cấu hình mục nội dung. Mục nội dung chỉ bắt buộc khi DOCX có marker nội dung tương ứng; không ép tất cả marker thành bắt buộc.
3. Cấu hình trường tùy chỉnh theo cơ chế dùng chung, không tạo registry riêng hoặc thêm alias cho marker đã bỏ.
4. Tải DOCX lên, kiểm tra, tạo xem trước PDF/DOCX, phát hành mẫu.
5. Các phần tiếng Anh tuân theo ngôn ngữ và separate/combined hiện hành; EN trước VI khi tách, chỉ một nội dung/bảng khi gộp.

### 4.2. Thiết lập phạm vi và tạo bàn giao trong Closing

1. Mở hợp đồng đã ký hợp lệ, chưa hoàn tất/hủy, đúng quyền quản lý thực hiện.
2. Khối Bàn giao xuất hiện trước Nghiệm thu. Chọn “Thiết lập phạm vi”.
3. Lấy toàn bộ hạng mục từ snapshot của phiên bản đã ký, không đọc tên/đơn vị/số lượng mới từ catalog master.
4. Người dùng chọn rõ cho từng dòng: **Theo số lượng đã ký** hoặc **Nguyên gói**. Không tự suy đoán dịch vụ tháng/gói hay sản phẩm thuộc chế độ nào.
5. Xác nhận baseline đầy đủ; sau đó baseline bất biến. Không tự thêm nghĩa vụ mới ngoài hợp đồng đã ký.
6. Chọn “Tạo biên bản”, mẫu tương thích ngôn ngữ, mã/ngày/địa điểm, các phụ lục làm căn cứ, dòng phạm vi và lượng giao lần này.
7. Pháp nhân hai bên lấy từ snapshot, không sửa. Cho chỉnh người tham gia, chức vụ và thông tin được phép trong draft; không ghi ngược về master/hợp đồng gốc.
8. Dùng universal save/Ctrl+S cho draft; thông tin hai bên, nội dung và danh sách biên bản có collapsible, giữ trạng thái mở/đóng sau lưu.
9. Xem trước → xác nhận chốt → lưu cố định DOCX/PDF và khóa dữ liệu.
10. Chọn tệp bản ký, nhập ngày ký thực tế, xác nhận hai bên đã ký → xác nhận tải lên → Signed.
11. UI hiện mã, ngày, người tạo/chốt/tải ký, lượng đã giao/chờ ký/còn lại; PDF/DOCX ở góc trên phải. Danh sách biên bản có phân trang và dùng card chuyển biên bản chung với nghiệm thu.
12. Còn lượng thì tạo biên bản tiếp; đủ lượng thì không tạo trùng. Một contract/version tối đa một Draft hoặc Finalized đang hoạt động.

### 4.3. Nghiệm thu từ bàn giao

1. Có thể soạn draft nghiệm thu trước khi có bàn giao; chưa có nguồn Signed thì chưa được chốt.
2. Chọn một/nhiều bàn giao Signed của đúng contract/version và lượng nghiệm thu từng dòng còn khả dụng.
3. Chọn loại nghiệm thu và đợt thanh toán riêng; không tự gắn một dòng bàn giao với một đợt tiền.
4. Hệ thống tự sinh căn cứ hợp đồng/phụ lục và căn cứ các bàn giao thực sự được chọn.
5. Chốt phải kiểm tra nguồn, số lượng, giữ chỗ và concurrency ở BE. Lưu lỗi thì không tiếp tục chốt; chốt lỗi thì giữ confirm/draft và dữ liệu đang nhập.
6. Bản nghiệm thu Signed mới cộng vào tiến độ, kích hoạt mốc `AcceptanceCompleted` theo ngày ký thực tế nghiệm thu, không theo ngày tải tệp hay ngày ký bàn giao.
7. Còn phạm vi/đợt tiền thì hiển thị rõ phần còn lại. Đã dùng luồng một phần thì không chuyển về toàn bộ.

**Điểm phải chốt lại trước khi code:** luồng cũ cho phép tạo vòng một phần chỉ nghiệm thu phạm vi còn lại khi mọi đợt tiền đã được nghiệm thu; nếu vòng lấy hết phạm vi cuối cùng thì phải chọn hết đợt tiền còn lại. Luồng hiện tại sau khi gỡ lại yêu cầu mỗi biên bản một phần có ít nhất một đợt và dừng khi đủ các đợt. Hai quy tắc này khác nhau; không tự nới validation hiện tại khi tái triển khai. Phải thống nhất cơ chế các vòng cuối để không bị kẹt phạm vi hoặc bỏ sót nghĩa vụ.

### 4.4. Hoàn tất hợp đồng khi bật đầy đủ bàn giao

Checklist dự kiến gồm: bản ký hợp đồng hợp lệ; bàn giao đủ; nghiệm thu đủ phạm vi và đợt tiền; thanh toán đủ. Không còn draft/chờ ký chưa xử lý.

Đây là quy tắc cần triển khai lại cả BE lẫn FE. Hiện tại checklist chỉ có ba điều kiện, không chặn bằng bàn giao.

## 5. Trạng thái và cách tính tiến độ

| Trạng thái | Chỉnh nội dung | Giữ chỗ lượng | Tính đã giao | Dùng làm nguồn nghiệm thu |
| --- | --- | --- | --- | --- |
| Draft | Có | Không | Không | Không |
| Finalized | Không | Có | Không | Không |
| Signed | Không | Không còn tính riêng là giữ chỗ | Có | Có, phần còn khả dụng |
| Cancelled | Không | Không | Không | Không |

- Chuyển `Draft → Finalized → Signed`; cho hủy Draft/Finalized có lý do. Không sửa/hủy Signed trực tiếp.
- `đã giao = tổng lượng của dòng thuộc Signed có bản ký hợp lệ`.
- `giữ chỗ bàn giao = tổng lượng của dòng thuộc Finalized`.
- `còn được giao = lượng bắt buộc − đã giao − giữ chỗ bàn giao`.
- `đã nghiệm thu = tổng allocation của nghiệm thu Signed có bản ký hợp lệ`.
- `giữ chỗ nghiệm thu = tổng allocation của nghiệm thu Finalized`.
- `còn nghiệm thu được trên dòng bàn giao = lượng dòng Signed − đã nghiệm thu − giữ chỗ nghiệm thu`.
- Dùng decimal, dự kiến tối đa bốn số lẻ; không float. Nguyên gói có lượng bắt buộc 1, không giao/nghiệm thu 0.5 gói.
- Draft có thể chọn lượng; BE kiểm tra lại lúc chốt/ký. Không cộng số biên bản thành phần trăm tiến độ.
- Source sai tenant/contract/version, chưa ký, đã hủy, chứng từ thiếu hash hoặc không đúng ownership đều không được tính.

## 6. Dữ liệu relational đã dùng trước khi gỡ

| Bảng | Vai trò chính |
| --- | --- |
| `tbl_ContractHandoverBaseline` | Contract/version, người/ngày xác nhận baseline |
| `tbl_ContractHandoverScopeItem` | Hạng mục nguồn, chế độ lượng/gói, mô tả VI/EN, đơn vị, lượng gốc/bắt buộc, thứ tự |
| `tbl_ContractHandoverRecord` | Header, template version, mã/ngày/địa điểm, trạng thái, hash, actors/timestamps, row version |
| `tbl_ContractHandoverLine` | Scope item, lượng giao lần này, ghi chú VI/EN, thứ tự |
| `tbl_ContractHandoverParty` | Snapshot pháp nhân/người tham gia của hai chủ thể |
| `tbl_ContractHandoverReference` | Căn cứ hợp đồng/phụ lục cùng version |
| `tbl_ContractHandoverSection` | Nội dung, thứ tự và chế độ song ngữ |
| `tbl_ContractHandoverEvidence` | File bản ký, ngày ký thực tế, người/thời điểm tải |
| `tbl_ContractHandoverPlaceholderValue` | Giá trị scalar đã resolve cho tài liệu |
| `tbl_ContractAcceptanceScopeAllocation` | Dòng bàn giao nguồn và lượng thuộc một nghiệm thu |

DOCX/PDF/bản ký dùng metadata tệp chung. Object type cũ `ContractHandoverArtifact` và `ContractHandoverEvidence` đều có ObjectId là **HandoverRecordId**; phân quyền phải đi từ biên bản về hợp đồng cha. Không nhầm với bản ký nghiệm thu dùng ObjectId hợp đồng.

Không thêm cột JSON ngoài rich text, không physical FK; vẫn có cột ID, index, check constraint và kiểm tra logical references trong transaction. Unique mã theo contract, một Draft/Finalized theo contract/version, thứ tự và dòng scope không trùng. Khóa contract trước rồi các tập con theo một thứ tự thống nhất; dùng Serializable/UPDLOCK/HOLDLOCK hoặc cơ chế tương đương đã kiểm chứng SQL Server thật.

## 7. Template, renderer, ngày và lưu trữ

- Mã loại mẫu cũ là 4; mã phân loại tệp đính kèm cũ là 2. Hiện đã retire; không đổi số enum các loại còn lại. Nếu khai báo lại phải có migration tương ứng, không thêm alias/fallback.
- Marker cũ: `HANDOVER_CODE`, `HANDOVER_DATE`, `HANDOVER_DATE_EN`, `HANDOVER_LOCATION`, `HANDOVER_LOCATION_EN`, `DOCUMENT_REFERENCES`, `HANDOVER_ITEM_TABLE`, `HANDOVER_SECTIONS`, `PROVIDER_*`, `CUSTOMER_*`, `SIGNATURE_PROVIDER`, `SIGNATURE_CUSTOMER`.
- Chữ ký chỉ tiêu đề đại diện hai bên, không tự thêm tên/chức vụ; alignment theo đoạn marker trong DOCX, font và layout dùng renderer chung.
- Bảng cũ gồm STT, mã, hạng mục, đơn vị, số lượng; tỷ lệ cột cũ `[700,1500,4600,1400,1800]` trên 10.000 phần, chỉ là tham khảo chứ không bắt buộc tái dùng.
- PDF chuyển từ chính DOCX vừa render. Chốt chỉ thành công khi đủ DOCX/PDF và DB; lỗi converter/storage phải rollback trạng thái và thu hồi tệp tạo dở phù hợp.
- Hash bao gồm nội dung snapshot, direction/parties/references/scope; không tự sửa hash cũ để “chuyển đổi” dữ liệu sau khi nguồn đã xóa.
- Khóa nội dung/nguồn sau Finalized; Signed/Cancelled và bản ký bất biến. Người dùng sửa tệp tải về không làm thay đổi bản lưu trong hệ thống; hash dùng kiểm tra toàn vẹn, không khóa Word trên máy người dùng.
- Phân biệt ngày chứng từ, ngày ký thực tế và thời điểm tải bản ký. Ngày thuần không dịch múi giờ; timestamps hiển thị helper UTC+7 hiện hành.
- Ngày ký bàn giao không trước ngày bàn giao và không trong tương lai. Ngày nghiệm thu không trước ngày ký hợp đồng/ngày bàn giao của những nguồn được chọn. Nếu muốn dùng cả ngày ký bàn giao làm cận dưới thì phải chốt thêm, không âm thầm thay quy tắc cũ.
- Pháp nhân/đơn vị lấy snapshot đã ký; EN của đối tác tùy chọn thiếu thì trống, không VI thay EN. Địa điểm EN bắt buộc khi song ngữ. Căn cứ dùng ngày ký thực tế khi đã có, không timestamp upload.

## 8. API và thông báo khi xây lại

Nhóm cũ: `/api/contracts/{contractId}/handover-records` với list/detail/templates/readiness/baseline, create/update, preview/finalize/signed-evidence/cancel và tải tài liệu. Endpoint cụ thể phải đối chiếu phiên bản API mới khi làm lại, không coi tên trong tài liệu là API hiện tồn tại.

Nghiệm thu từng có GET/PUT `acceptance-records/{recordId}/handover-scope`; hiện không còn. Nếu đưa lại, payload phải có dòng nguồn, lượng và row version; không chỉ gửi danh sách ID header.

Mọi endpoint kiểm tra session, quyền trên hợp đồng cha, đúng tenant/version và concurrency. Không đưa hồ sơ thực hiện nội bộ vào public negotiation DTO.

Thông báo cũ: tạo/chốt/ký/hủy, ghi outbox cùng transaction, không gửi cho chính actor, recipient theo chủ hợp đồng/quyền hiện tại. Không mặc định đổi recipient theo vai trò Provider khi Mua. Nếu khôi phục action key/đường dẫn `handover`, phải đăng ký đồng thời producer, resource policy, SignalR và FE routing; hiện đã gỡ đường dẫn đó.

## 9. Checklist tái triển khai và checkpoint để tiếp tục

Các phase sau **CHƯA LÀM cho lần triển khai lại**. Chỉ đổi trạng thái khi có bằng chứng thực tế.

| Phase | Công việc | Trạng thái | Điều kiện xong |
| --- | --- | --- | --- |
| 0 | Rà soát code hiện tại, quyền, Mua/Bán, nguồn phạm vi; chốt quy tắc vòng nghiệm thu cuối | CHƯA LÀM | Quyết định được ghi rõ, không suy đoán số lượng/gói |
| 1 | Schema/model/index/check, coverage policy và transaction | CHƯA LÀM | Migration mới, snapshot khớp; DB mới và nâng cấp được kiểm tra |
| 2 | Loại mẫu/catalog/custom placeholder/renderer | CHƯA LÀM | Upload → kiểm tra → xem trước → phát hành đúng VI/song ngữ |
| 3 | Vòng đời API, storage, audit, phân quyền | CHƯA LÀM | Draft/chốt/ký/hủy đầy đủ, lỗi render/row version không mất nhất quán |
| 4 | UI Closing và universal save | CHƯA LÀM | Wizard, card phân trang, collapsible, confirm, ngày ký và tài liệu |
| 5 | Liên kết nghiệm thu với nguồn bàn giao | CHƯA LÀM | Không vượt/trùng lượng; partial/final không kẹt vòng cuối |
| 6 | Completion, notification và routing | CHƯA LÀM | BE/FE cùng gate; thông báo đúng người và đúng trang |
| 7 | Hồi quy, SQL concurrency và converter thật, tài liệu | CHƯA LÀM | Có kết quả thực, liệt kê test bỏ qua/UAT chưa làm |

Sau mỗi lượt làm, ghi: phase/step; file/API/migration đã đổi; test đã chạy và kết quả; DB nào đã áp/chưa áp; việc đang dở; blocker; bước tiếp theo. Không coi code xong là DB đã cập nhật. Không tự chạy migration lên DB thật, reset dữ liệu, gửi thư hay UAT trình duyệt nếu chưa được phép.

### Mẫu nhật ký

```text
Ngày/commit:
Phase/step:
Đã xong:
Đang dở:
Kiểm tra thực tế:
Migration tạo / DB đã áp:
Blocker / quyết định còn thiếu:
Bước tiếp theo chính xác:
```

## 10. Các ca kiểm tra tối thiểu khi triển khai lại

- Cả Mua/Bán, VI/song ngữ, separate/combined; vai trò hai bên và người liên lạc đúng snapshot.
- Không ký hợp đồng hoặc không có quyền: không tạo/chốt/ký bàn giao. Khác tenant/version/contract: không đọc/chọn nguồn.
- Mẫu không có marker sections: nội dung tùy chọn; có marker mà không có mục hợp lệ: lỗi rõ. Marker chữ ký khác vẫn tùy chọn.
- Baseline quantity/package là lựa chọn của người dùng; số âm/0/quá bốn số lẻ/gói bị chia/vượt lượng đều bị từ chối.
- Hai lần chốt đồng thời, stale row version, trùng mã, hai Draft/Finalized: chỉ thao tác hợp lệ thành công.
- Chốt chưa ký không tăng delivered; ký tăng đúng một lần; hủy chờ ký giải phóng lượng nhưng giữ lịch sử.
- Lỗi PDF/storage/DB/audit không để thiếu artifact, trạng thái sai hoặc thông báo thành công giả.
- Nghiệm thu từ nhiều bàn giao, giao nhiều lần, nghiệm thu nhiều lần, không lấy lại lượng đã ký/giữ chỗ.
- Cận ngày hợp đồng/bàn giao, ngày ký thực tế, timestamps UTC+7 và date-only không lệch ngày.
- Đủ tiền nhưng thiếu phạm vi không hoàn tất khi gate bàn giao đã bật; đủ bàn giao nhưng chưa ký nghiệm thu cũng không hoàn tất.
- Vòng cuối theo quyết định Phase 0: không trùng milestone, không scope-only trái quy tắc, không thiếu phạm vi sau khi hết đợt tiền.
- Tệp private storage có đúng ownership/hash; không tải/xóa bằng API generic để vượt quyền; public customer không nhìn thấy hồ sơ nội bộ.
- Audit/notification đúng actor/recipient, không lặp khi retry; không lộ dữ liệu nhạy cảm.
- Xóa mẫu không dùng được; mẫu đã phát hành/được dùng được bảo vệ theo policy lúc triển khai lại.

## 11. Nhật ký gỡ chức năng

- Đã gỡ luồng bàn giao BE và điều kiện bắt buộc bàn giao trước nghiệm thu/hoàn tất.
- Đã tạo migration xóa 10 bảng, có guard cho nghiệm thu chờ ký đang gắn phạm vi bàn giao.
- Đã xóa UI/client và các đoạn comment bàn giao, loại mẫu/marker/renderer, liên kết thông báo và tùy chọn tệp mới.
- Đã tạo migration dọn riêng mẫu loại 4, bảo vệ tham chiếu hợp đồng/nghiệm thu; không xóa audit hoặc private storage.
- Mã nguồn gỡ có thể tra lại từ Git. Dữ liệu đã bị xóa bởi migration chỉ khôi phục bằng backup phù hợp; Git không chứa dữ liệu DB.
- Chưa tự áp hai migration gỡ lên DB thật. Tài liệu này không xác nhận schema DB đang chạy đã được cập nhật.
- Kiểm tra sau đợt dọn hoàn toàn: backend build thành công; 1.156 test đạt, 0 lỗi, 9 test tích hợp SQL Server/LibreOffice bỏ qua khi chưa bật cấu hình. Không chạy build/typecheck/lint/UAT FE theo yêu cầu trước đó; FE chỉ rà soát tĩnh và diff.
