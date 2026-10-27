# Seed dữ liệu test cho tenant DB mới

Cập nhật theo schema đến `20261027025251_NodeRealtimeDelivery` (27/10/2026). Migration này bổ sung trạng thái retry riêng cho Node trong notification outbox; SignalR giữ nguyên trạng thái cũ. Không tự bật Node hoặc replay thông báo lịch sử.

Ngày ký thực tế nghiệm thu lưu riêng ở `tbl_ContractAcceptanceEvidence.SignDate` và được nhập khi tải bản ký mới. Migration không lấy ngày upload để điền cho bản ký cũ, không đổi các mốc thanh toán đã kích hoạt trước đó.

Catalog seed khai báo rõ đơn vị VI/EN cho từng sản phẩm và dịch vụ mới. Không tự cập nhật đơn vị của bản ghi đã có; hãy điền đơn vị trong màn sửa danh mục nếu cần dùng các bản ghi đó cho hợp đồng mới.

Các file SQL ở đây **chỉ thêm dữ liệu mẫu**, không tạo database/schema, không thay thế EF migration và không phải bản backup. Không dùng trên production.

## Chuẩn bị

1. Dựng Central DB/SystemAdmin theo cấu hình dự án, sau đó tạo tenant qua SystemAdmin để provision database và Manager ban đầu.
2. Đảm bảo tenant đã chạy toàn bộ migration hiện tại. Khi provision tenant mới, kiểm tra provisioning thành công; nếu còn migration pending thì cập nhật trước khi seed. Không seed contract/template cũ trước migration mua/bán.
3. Mở từng file SQL, đổi `@ExpectedDatabase = N'CHANGE_ME_TENANT_DATABASE'` thành **tên DB tenant mới**. Script không còn `USE` hardcode sang `hungnd`; phải chọn đúng database trong SSMS hoặc dùng `sqlcmd -d`.
4. Trong ba file có `@SourceAdminAccount`, đổi `'admin'` thành account Manager active đã tạo khi provision tenant. Đây không phải account SystemAdmin ở Central DB. Script không tự chọn một Manager khác.

Mỗi script kiểm tra tên DB và migration tối thiểu trước khi seed. Không tạo bảng giả hoặc sửa lịch sử `__EFMigrationsHistory` để vượt kiểm tra.

## Thứ tự chạy

| Thứ tự | File | Nội dung |
|---|---|---|
| 1 | `seed-test-departments.sql` | 10 phòng ban active; giữ đúng tên cột `Stutus` của schema |
| 2 | `seed-rbac-test-employees.sql` | 6 nhân viên cho 6 vai trò, gắn phòng ban theo mã thay vì ID cũ |
| 3 | `seed-test-customers.sql` | 25 khách hàng, đại diện và người liên lạc; mã `CUST-{ID:D6}` theo identity mới |
| 4 | `seed-test-catalog.sql` | 10 danh mục có cha/con, 10 loại dịch vụ, 10 sản phẩm và 10 dịch vụ có tên EN |

Chạy **toàn bộ một file** mỗi lần. Có transaction/rollback trong từng file; không có transaction chung cho cả bốn file. Có thể chạy lại sau khi sửa lỗi: hàng đã tồn tại theo khóa seed được giữ nguyên, không reset mật khẩu, không ghi đè hồ sơ, không tự khôi phục hàng đã xóa/khóa. Riêng script RBAC sẽ bỏ cờ bắt đổi mật khẩu cho đúng 6 account/code/role test được khai báo trong script. Dữ liệu trùng khóa/khác cấu trúc hoặc thiếu điều kiện cần thiết sẽ báo lỗi thay vì âm thầm chọn một hàng.

Ví dụ PowerShell, sau khi đã đổi các biến trong file (tự chạy, không phải bước agent đã thực hiện):

```powershell
cd E:\DTC-DigitizingContract\Source
$seedDatabase = 'TEN_DB_TENANT_MOI'
foreach ($seedScript in @(
    'seed-test-departments.sql',
    'seed-rbac-test-employees.sql',
    'seed-test-customers.sql',
    'seed-test-catalog.sql'
)) {
    sqlcmd -S localhost -d $seedDatabase -E -b -f 65001 -i ".\Artifacts\Sql\$seedScript"
    if ($LASTEXITCODE -ne 0) { throw "Seed thất bại: $seedScript" }
}
```

`-E` dùng Windows Authentication. Nếu SQL Server của bạn dùng SQL Authentication, chọn cơ chế đăng nhập phù hợp; không ghi password thật vào repo. Đọc/lưu các script bằng UTF-8 để giữ tiếng Việt.

## Sau khi seed

- Tài khoản `test.sale`, `test.marketing`, `test.adminofficer`, `test.technical`, `test.accountant`, `test.manager` dùng cùng password với account Manager nguồn **tại thời điểm chạy lần đầu**. Script chỉ sao chép hash, không chứa password rõ. Không bắt đổi mật khẩu (`MustChangePassword = 0`), trang mặc định `/contracts`, `SessionVersion = 1` khi tạo mới. Nếu đã seed trước đó, chạy lại script RBAC sẽ bỏ cờ này và tăng session version cho đúng tài khoản test đang bị bắt đổi; đăng nhập lại để dùng trạng thái mới. Không thay đổi chính sách tài khoản thật hoặc account Manager nguồn ngoài danh sách test.
- Tài khoản RBAC không tự có email recovery đã xác minh. Muốn test quên mật khẩu, thiết lập/xác minh email theo luồng hiện tại; không giả `RecoveryEmailVerifiedAt` bằng SQL.
- Khách hàng không có mật khẩu MD5 cũ hoặc tài khoản đăng nhập được kích hoạt bằng script này. `CustomerAccount` chỉ là khóa nhận diện seed; truy cập hợp đồng public vẫn theo link/OTP của hệ thống.
- CustomerId do DB cấp; FPT Software liên kết với khách hàng FPT theo khóa seed, không giả định FPT có ID = 1. Các ID source/career/region/card từ dump cũ không được chép sang DB mới.
- Hai mẫu khách hàng `kh_fpt`, `kh_cgv` có EN để test song ngữ. Các mẫu khác cố ý để EN trống đúng quy tắc EN tùy chọn của khách hàng. Tên/người tham gia/ngân hàng/địa chỉ trong seed là dữ liệu test, không phải hồ sơ pháp lý đã xác minh; một số email được giữ từ bộ seed cũ, không dùng để gửi thử hàng loạt.
- Hoàn thiện **Hồ sơ pháp lý doanh nghiệp**, gồm các trường EN bắt buộc trên UI. Không tự bịa hồ sơ pháp lý của tenant bằng seed.
- Tạo/upload/phát hành mẫu hợp đồng, bàn giao, nghiệm thu bằng UI theo chiều Mua/Bán và ngôn ngữ. Sau đó tạo hợp đồng mới theo luồng hiện tại. Bốn script không chèn contract, snapshot, artifact hay metadata file cũ; không cần sửa migration để gán chiều cho dữ liệu cũ.
- Các file DOCX trong `Artifacts/ContractTemplates` không được sửa trong lần cập nhật SQL này; đối chiếu marker với catalog hiện tại trước khi upload, không mặc định chúng đã tương thích.
