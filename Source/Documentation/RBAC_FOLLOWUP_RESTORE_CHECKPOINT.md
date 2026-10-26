# Áp lại RBAC và hợp đồng tiếp nối — checkpoint

Nền: commit `66c65e0` (trước khi xóa bàn giao). Ngày: 26/10/2026.

## Phạm vi

- Giữ nguyên module bàn giao, schema, mẫu loại 4, thứ tự bàn giao → nghiệm thu và completion gate bàn giao. Không khôi phục các migration xóa bàn giao của nhánh công việc cũ.
- Áp RBAC theo quyết định cuối cùng, gồm bước AdminOfficer kiểm tra trung gian và thông báo người theo dõi.
- Duy trì/bảo trì chỉ là hợp đồng mới liên kết hợp đồng cung cấp gốc đã hoàn tất. Chưa thêm scheduler nhắc hạn, gia hạn tự động hoặc tự tạo hợp đồng.
- Node.js chỉ lập plan trong `Documentation/NODEJS_WEBSOCKET_SIGNALR_CLONE_PLAN.md`, chưa tạo service/đổi transport.
- Không thay đổi yêu cầu chứng từ thanh toán trong lượt này: yêu cầu hiện tại là áp lại **RBAC**, không tự mang thay đổi chứng từ từ công việc cũ sang.

## RBAC đã áp

| Role | Kiểm tra | Duyệt cuối | Tải/thay bản ký HĐ | Bàn giao/nghiệm thu | Thanh toán | Đóng HĐ |
| --- | --- | --- | --- | --- | --- | --- |
| Manager | Không | Có | Có | Có | Có | Có |
| AdminOfficer | Có | Không | Có | Có | Có | Có |
| Accountant | Không | Không | Không | Có | Có | Có |
| Sale/Marketing | Không | Không | Không | HĐ phụ trách | HĐ phụ trách | Không |
| Technical | Không | Không | Không | HĐ phụ trách | HĐ phụ trách | Không |

Owner là nhân viên phụ trách hiện tại, không phải mặc định người tạo. Quyền owner vận hành chỉ khi Signed. Manager/AdminOfficer/Accountant giữ quyền theo role khi là owner, nhưng không tự duyệt hồ sơ mình gửi. Technical giữ quyền xem chỉ đọc hợp đồng khác ở chờ ký/đã ký/hoàn tất; không có quyền sửa nghiệm thu của các hợp đồng đó.

Manager/AdminOfficer/Accountant quản trị các module tenant theo catalog, gồm template; Marketing = Sale. Chỉ SystemAdmin được cấp/đổi role Manager. Không cấp quyền sửa nội dung hợp đồng người khác qua quyền đọc tenant.

Luồng trình duyệt: owner Manager/AdminOfficer đi thẳng chờ Manager; các role khác, gồm Accountant, qua AdminOfficer trước. Owner được rút ở cả hai bước. Thông báo đến owner + AdminOfficer/Manager đang hoạt động, loại actor và recipient trùng.

## Luồng duy trì/bảo trì cho người dùng

1. Admin mở Mẫu hợp đồng → tạo loại **Bảo trì phần mềm** hoặc **Duy trì phần mềm**. Hai loại này đã có trong code/FE nên không thêm enum trùng, không tự chèn mẫu giả vào DB.
2. Soạn điều khoản, tải DOCX có `{{CONTRACT_LEGAL_BASES}}` tại vị trí căn cứ, xem trước và phát hành. Riêng hai loại này cần marker dù chưa nhập căn cứ cố định, vì căn cứ nguồn được sinh khi tạo hợp đồng thật.
3. Tạo hợp đồng → chọn ngôn ngữ, chiều Mua/Bán và mẫu tương ứng → chọn khách hàng → chọn hợp đồng cung cấp gốc **đã hoàn tất**, cùng khách hàng/chiều giao dịch và nằm trong phạm vi được phép đọc/chọn.
4. Lưu hợp đồng mới: tự sinh căn cứ `PARENT_CONTRACT`. Mã/tên lấy từ legal snapshot bất biến của phiên bản hợp đồng gốc; ngày ký lấy từ `Contract.SignDate` do workflow ký ghi nhận (snapshot được khóa trước khi ký). Thiếu dữ liệu phải báo rõ, không lấy bản nháp/live name thay thế, không tự dịch tiếng Anh.
5. Căn cứ được lưu trong bảng căn cứ hiện có, có dữ liệu VI và EN nếu hợp đồng song ngữ; hiện ở chi tiết hợp đồng và kết xuất qua marker DOCX/PDF. Không thêm cột JSON hoặc bảng không cần thiết.
6. Hợp đồng mới có mã, version, điều khoản và thanh toán riêng; không sửa nội dung/trạng thái hợp đồng gốc. Sau đó đi qua đàm phán → kiểm tra/duyệt → ký → bàn giao → nghiệm thu → thanh toán → hoàn tất như luồng hiện tại.

## Checkpoint thực hiện

- [x] RBAC FE/BE, quyền owner cho acceptance/handover/payment; ký chỉ Manager/AdminOfficer; Accountant được đóng.
- [x] Kiểm tra trung gian, metadata reviewer, audit, thông báo, UI phê duyệt và trạng thái/filter/dashboard.
- [x] Migration `20261026090000_RestoreAdministrativeReviewRbac`, snapshot và guard SQL seed; không xóa bảng bàn giao, không thêm physical FK/JSON.
- [x] Bổ sung căn cứ nguồn, kiểm tra marker cho mẫu duy trì/bảo trì và mở renderer/gửi duyệt cho cả ba loại hợp đồng.
- [x] Rà soát tĩnh FE, không chạy FE test/build/UAT theo yêu cầu trước đó.
- [x] Plan Node.js clone SignalR đã viết, có phase/status và checkpoint để tiếp tục.
- [x] Kiểm thử BE cuối: **1.228 đạt, 11 bỏ qua** (tích hợp SQL Server/LibreOffice chưa bật), **0 lỗi**. Kết quả `.test-output/reapply-rbac-results/reapply-rbac-followup.trx`. Bao gồm ma trận 6 role/owner, duyệt hai bước, schema giữ bàn giao, duy trì/bảo trì VI-song ngữ và kết xuất DOCX/PDF với bộ chuyển đổi test.
- [x] `graphify update .` sau khi hoàn tất code. Hai cảnh báo parser có sẵn ở `employee-profile-form.tsx` và `my_application.h` nằm ngoài phạm vi lần sửa này.
- [ ] Người vận hành áp migration đúng tenant DB và khởi động lại API/FE. Agent chưa áp migration lên DB.

Lưu ý database khi quay về commit cũ: rollback Git không rollback database. Nếu database đã từng áp các migration xóa bàn giao ở nhánh khác, phải chọn DB đúng schema hoặc xác định phương án khôi phục riêng trước khi chạy; thay đổi này không âm thầm tạo lại dữ liệu đã xóa hoặc xóa migration history.

Khi tiếp tục: đọc file này, plan Node và git diff. Không reset worktree, không mang migration xóa bàn giao trở lại, không chạy lại phase đã xác minh nếu không có thay đổi liên quan.
