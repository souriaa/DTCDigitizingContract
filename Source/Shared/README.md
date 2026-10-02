# Tên hiển thị của ứng dụng

Sửa `name` trong `application-branding.json` để đổi tên dự án cho Backend, Frontend và SystemAdmin. Đây là nguồn tên duy nhất; không đặt tên riêng trong cấu hình SMTP hay biến môi trường.

- Backend nhúng tệp vào assembly Infrastructure khi build, dùng cho email và tên người gửi.
- Hai FE đọc tệp trong `next.config.mjs` và đóng gói tên vào mã khi build, dùng cho đăng nhập, thanh bên, tiêu đề trang và các màn khôi phục tài khoản.
- Sau khi đổi tên, build/publish lại Backend và cả hai FE. Bản đã đóng gói không cần đọc tệp này từ ổ đĩa khi chạy.
- Khi clone/copy mã nguồn để build, giữ cả thư mục `Shared` bên cạnh `Backend`, `Frontend`, `SystemAdmin`. Cấu trúc đầu ra `.next/standalone` không thay đổi.

Tên kỹ thuật của project, namespace, database, đường dẫn lưu trữ, mã tenant và thông tin pháp nhân không thuộc tên hiển thị và không được đổi theo tệp này.
