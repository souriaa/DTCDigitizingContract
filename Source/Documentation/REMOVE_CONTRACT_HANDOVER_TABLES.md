# Gỡ bảng bàn giao khỏi tenant database

Migration: `20261023090305_RemoveContractHandoverTables` và `20261023092950_RetireHandoverTemplates`.

## Phạm vi

- Xóa 9 bảng `tbl_ContractHandover*` và bảng `tbl_ContractAcceptanceScopeAllocation`, bao gồm dữ liệu trong các bảng này.
- Không sửa/xóa dữ liệu hợp đồng, nghiệm thu, đợt thanh toán, audit hoặc metadata tệp. Dữ liệu biên soạn thuộc riêng mẫu bàn giao sẽ bị xóa bởi migration thứ hai.
- Không xóa tệp vật lý trong private storage. Tệp bàn giao không còn đối tượng cha sẽ bị từ chối truy cập qua API tệp chung.
- Gỡ API/dịch vụ/model bàn giao và các truy vấn tới bảng đã xóa. UI bàn giao trong Closing, kể cả code comment, đã bị xóa.
- Không sửa migration cũ, không thêm converter hoặc tự chuyển đổi dữ liệu bàn giao cũ.

## Trước khi áp dụng

1. Sao lưu tenant database. `Down()` chỉ tạo lại bảng rỗng, không phục hồi dữ liệu bị xóa.
2. Kiểm tra biên bản nghiệm thu đang chốt/chờ ký có gắn phạm vi bàn giao: ký xong bằng phiên bản phần mềm cũ hoặc hủy biên bản qua ứng dụng. Migration dừng trước khi xóa bảng nếu còn trường hợp này, vì hash của biên bản có thể chứa dữ liệu bàn giao sắp bị xóa. Không sửa hash hoặc ép trạng thái bằng SQL.
3. Dừng API/worker phiên bản cũ trước khi đổi schema, tránh tiến trình cũ tiếp tục truy vấn bảng đã xóa.
4. Áp migration mới cho từng **tenant database** bằng luồng cập nhật tenant đang sử dụng. Xác nhận đúng database đích; không áp `DbDtctechContext` vào `master` hoặc central database.
5. Triển khai API mới, khởi động lại và kiểm tra tạo/chốt/ký nghiệm thu, thanh toán, hoàn tất và xóa mẫu chưa sử dụng.

## Sau khi áp dụng

- Không có 10 bảng đã nêu trong schema/model mới.
- Nghiệm thu không cần chọn nguồn bàn giao; một phần vẫn phải chọn đợt thanh toán và không được trùng đợt đã chốt/ký.
- Hoàn tất yêu cầu hợp đồng có bản ký hợp lệ, nghiệm thu đủ và thanh toán đủ.
- Các bản DOCX/PDF nghiệm thu đã phát hành vẫn là tệp lưu sẵn, không được tự render lại hay sửa hash để chuyển đổi dữ liệu cũ. Phạm vi/căn cứ bàn giao được sinh từ các bảng đã xóa không còn xuất hiện trong dữ liệu nghiệm thu trả về; đối chiếu lịch sử bằng tệp đã phát hành và audit.
- Loại mẫu bàn giao (mã 4) cũng bị loại bỏ; migration `RetireHandoverTemplates` xóa đúng các mẫu loại này và dữ liệu biên soạn con, nhưng giữ audit và metadata/tệp. Migration dừng nếu có hợp đồng/nghiệm thu hoặc nguồn điều khoản/phụ lục/đợt tiền tham chiếu các mẫu cần xóa. Mẫu nghiệm thu (mã 5), kể cả tiêu đề có chữ “bàn giao”, không bị xóa. Rollback không khôi phục dữ liệu mẫu đã xóa.

## Kiểm tra đã thực hiện

- Kiểm thử backend kiểm tra model/snapshot đồng bộ, migration chỉ xóa đúng 10 bảng và có chốt bảo vệ bản nghiệm thu chờ ký.
- Kiểm thử luồng nghiệm thu/hoàn tất không phụ thuộc bàn giao cho cả hợp đồng Mua và Bán.
- Không tự áp migration lên database đang chạy; không chạy kiểm thử FE.
