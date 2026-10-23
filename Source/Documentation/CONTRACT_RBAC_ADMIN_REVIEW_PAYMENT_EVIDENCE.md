# RBAC, kiểm tra trung gian và chứng từ thanh toán

## Quy tắc đã chốt

- Manager: quản lý các module quản trị, bao gồm mẫu; là role duy nhất phê duyệt cuối cùng.
- AdminOfficer: quyền quản trị như Manager, ngoại trừ duyệt cuối; được kiểm tra hồ sơ trung gian, tải bản ký, nghiệm thu, thanh toán và đóng hợp đồng.
- Accountant: như AdminOfficer nhưng **không kiểm tra trung gian, không duyệt cuối và không ký hợp đồng**; được đóng hợp đồng khi đủ điều kiện, kể cả khi là owner.
- Marketing: cùng quyền Sale.
- Owner Sale/Marketing/Technical: giữ luồng trước duyệt; sau duyệt không ký/đóng hợp đồng của mình; được nghiệm thu và thanh toán hợp đồng của mình khi đã ký.
- Manager/AdminOfficer là owner vẫn giữ quyền ký/đóng theo role. Accountant là owner vẫn được đóng nhưng không được tải bản ký hợp đồng.
- Technical không được tải/thay bản ký hợp đồng trong mọi trường hợp; chỉ quản lý nghiệm thu và thanh toán của hợp đồng mình phụ trách khi đã ký. Giữ quyền xem hợp đồng khác ở giai đoạn chờ ký/đã ký/hoàn tất, không được thao tác vận hành hợp đồng đó.
- Quyền không cho sửa nội dung hợp đồng của người khác; giữ nguyên quy tắc owner, version khóa, snapshot, hash và kiểm tra đồng thời.

## Ma trận quyền hợp đồng hiện tại

`Owner` là nhân viên phụ trách hiện tại (`Contract.EmployeeId`), không mặc định là người tạo hợp đồng. Quyền theo owner được kiểm tra lại ở backend mỗi thao tác.

| Quyền | Manager | AdminOfficer | Accountant | Sale/Marketing | Technical |
| --- | --- | --- | --- | --- | --- |
| Kiểm tra trung gian | Không | Có | Không | Không | Không |
| Duyệt cuối | Có | Không | Không | Không | Không |
| Tải lên/thay bản ký hợp đồng gốc | Có | Có | Không | Không | Không |
| Quản lý nghiệm thu | Có | Có | Có | Chỉ hợp đồng phụ trách | Chỉ hợp đồng phụ trách |
| Quản lý thanh toán | Có | Có | Có | Chỉ hợp đồng phụ trách | Chỉ hợp đồng phụ trách |
| Đóng hợp đồng | Có | Có | Có | Không | Không |
| Đọc hợp đồng khác trong tenant | Mọi trạng thái | Mọi trạng thái | Mọi trạng thái | Không | Chỉ chờ ký/đã ký/hoàn tất |

- Owner thường chỉ quản lý nghiệm thu/thanh toán khi hợp đồng đã ký. Manager/AdminOfficer/Accountant vẫn phải vượt qua kiểm tra trạng thái, dữ liệu đồng thời và điều kiện nghiệp vụ của từng thao tác.
- “Không tải bản ký hợp đồng” là không được tải **lên** hoặc thay bản scan để ghi nhận ký hợp đồng gốc. Không cắt quyền xem/tải xuống tài liệu đang được phép đọc.
- Technical vẫn được tải bản ký **nghiệm thu của hợp đồng mình phụ trách**. Đây là quyền nghiệm thu, tách biệt với ký hợp đồng gốc.
- API tệp chung không được tải lên/xóa đè các tệp nghiệp vụ bất biến (`ContractSignedEvidence`, `ContractVersionArtifact`, `ContractAcceptanceEvidence`, `ContractAcceptanceArtifact`); phải đi qua workflow có kiểm tra quyền tài nguyên.
- Các quyền ngoài contract không đổi trong lần siết Technical: Manager/AdminOfficer/Accountant quản trị nhân viên, phòng ban, danh mục sản phẩm/dịch vụ, khách hàng, mẫu, hồ sơ pháp lý và audit tenant. Cấp/thay đổi role Manager vẫn thuộc SystemAdmin. Sale/Marketing cùng quyền; Technical không có các quyền quản trị này.

## Luồng người dùng

1. Owner đàm phán và gửi hồ sơ. Nếu owner là AdminOfficer/Manager: chuyển thẳng chờ Manager duyệt. Các role khác, kể cả Accountant: chuyển **Chờ kiểm tra hành chính**.
2. AdminOfficer mở hợp đồng → tab **Phê duyệt** → **Kiểm tra hồ sơ** → thông qua để trình Manager, hoặc yêu cầu sửa và nhập lý do.
3. Chỉ Manager được duyệt cuối khi hồ sơ đã qua bước cần thiết. Không được tự duyệt hồ sơ mình gửi; không được vượt bước hoặc xử lý lặp bằng dữ liệu cũ.
4. Owner được rút hồ sơ ở cả hai bước đang chờ; trở về đàm phán. Version gửi duyệt vẫn khóa; tiếp tục bằng vòng/phiên bản mới như luồng hiện tại.
5. Manager/AdminOfficer tải bản scan hai bên đã ký. Owner thường/Accountant không được ký. Hợp đồng chuyển Đã ký khi bản ký hợp lệ.
6. Owner, Manager, AdminOfficer, Accountant được thao tác nghiệm thu/thanh toán trong phạm vi được cấp. Chứng từ thanh toán **không bắt buộc**; nếu tải lên vẫn phải hợp lệ. Khoản không có tệp vẫn được tính thanh toán và được hoàn tác theo cùng quy tắc cũ.
7. Manager/AdminOfficer/Accountant được đóng hợp đồng khi tất cả điều kiện sẵn sàng đã đạt, kể cả khi là owner.
8. Thông báo các sự kiện hợp đồng đến owner + Manager/AdminOfficer đang hoạt động, loại trùng và loại người thao tác; không phát rộng cho Accountant nếu không phải owner/người nhận nghiệp vụ được chỉ định.

## Theo dõi thực hiện

- [x] Catalog quyền FE/BE v3 và phân quyền tài nguyên theo owner.
- [x] Trạng thái hợp đồng 8 và yêu cầu duyệt 5 cho kiểm tra hành chính; không đổi số trạng thái cũ.
- [x] API kiểm tra, lịch sử người kiểm tra, duyệt cuối, rút hồ sơ và UI tab Phê duyệt.
- [x] Recipient thông báo dùng chung và các sự kiện vận hành.
- [x] Chứng từ tùy chọn: request, storage, ledger, readiness, hoàn tác và modal.
- [x] Migration `20261023111646_ContractAdministrativeReviewRbac`, snapshot và SQL seed theo schema mới.
- [x] Kiểm thử toàn bộ khi triển khai RBAC: 1.177 đạt, 9 bỏ qua (tích hợp cần SQL Server/LibreOffice), 0 lỗi. Kết quả: `.test-output/rbac-review-results/rbac-review.trx`. Rà soát tĩnh FE; không chạy kiểm thử/build/UAT FE theo yêu cầu.
- [x] Điều chỉnh Accountant được đóng nhưng không ký (23/10/2026): 92 kiểm thử liên quan đạt, 0 lỗi. Bao gồm là owner/không phải owner và chặn đóng khi thiếu điều kiện. Kết quả: `.test-output/rbac-review-results/accountant-completion.trx`. Điều chỉnh này không cần migration.
- [ ] Siết Technical: bỏ quyền tải/thay bản ký hợp đồng gốc, bỏ quyền nghiệm thu toàn tenant; kiểm thử các action nghiệm thu bằng authorization thật, kiểm tra lại khi đổi owner, phiên đăng nhập và API tệp. Kết quả theo dõi: `.test-output/rbac-review-results/technical-rbac-review.trx`. Không cần migration; FE dùng quyền do BE trả về, không hardcode lại role.
- [x] Cập nhật graphify sau khi hoàn tất code. Hai cảnh báo parser cũ ở `employee-profile-form.tsx` và `my_application.h` không thuộc phạm vi thay đổi này.
- [ ] Người vận hành áp migration trên tenant DB, khởi động lại API/FE và đăng nhập lại để nhận quyền mới.

## Triển khai

Migration chỉ thêm 3 cột dữ liệu thường nullable trên `tbl_ContractApprovalRequest` để lưu người/ngày/ghi chú kiểm tra; mở check trạng thái và đổi unique index để mỗi hợp đồng chỉ có một hồ sơ đang chờ ở một trong hai bước. Không thêm JSON, không thêm khóa ngoại, không chuyển đổi hoặc xóa dữ liệu cũ trong migration này.

Chưa tự chạy migration trên DB local. Dùng luồng cập nhật tenant hiện có; không áp application context vào central/master. Các migration xóa bàn giao chuẩn bị từ công việc trước vẫn được giữ nguyên trong chuỗi migration.

## Ca kiểm tra thủ công

| Ca | Mong đợi |
| --- | --- |
| Sale/Marketing/Technical/Accountant gửi duyệt | Chờ kiểm tra hành chính; Manager chưa duyệt được |
| AdminOfficer thông qua | Chờ duyệt cuối; lưu tên, ngày, ghi chú kiểm tra |
| Accountant gọi API kiểm tra/duyệt/ký | 403, không thay đổi dữ liệu |
| Accountant đóng hợp đồng (là owner hoặc không) | Thành công khi đủ điều kiện; thiếu điều kiện vẫn bị chặn như cũ |
| AdminOfficer gọi API duyệt cuối | 403 |
| Manager gọi duyệt cuối trước kiểm tra | 409, hồ sơ không đổi |
| Owner rút ở mỗi bước | Đàm phán, version đã gửi giữ nguyên khóa |
| Manager/AdminOfficer là owner | Được ký/đóng theo role; không tự duyệt |
| Owner thường làm nghiệm thu/thanh toán hợp đồng khác | Bị chặn |
| Technical tải/thay bản ký hợp đồng (là owner hoặc không) | 403, không lưu tệp hay đổi trạng thái |
| Technical tạo/sửa/chốt/tải bản ký/hủy nghiệm thu hợp đồng khác | 403, không đổi dữ liệu |
| Technical quản lý nghiệm thu hợp đồng mình phụ trách đã ký | Được phép; vẫn giữ kiểm tra trạng thái, version và dữ liệu đồng thời |
| Thanh toán không có chứng từ | Lưu ledger, tính số tiền đã trả, cho hoàn tác theo điều kiện cũ |
| Thanh toán có tệp sai loại/rỗng/quá cỡ | Bị chặn, không lưu trạng thái thanh toán |
| AdminOfficer tải bản ký/chốt nghiệm thu/hoàn tất đợt | Manager + owner nhận thông báo; người thao tác không nhận |
| Tài khoản bị khóa hoặc khác tenant | Không nhận/không truy cập thông báo của tenant hiện tại |
