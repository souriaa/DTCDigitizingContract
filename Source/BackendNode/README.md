# Backend Node.js của eContract

Backend này nằm cùng cấp `Backend/` để có thể mở rộng thêm module Node về sau. Hiện có module realtime thông báo và trao đổi hợp đồng. **SignalR vẫn được giữ nguyên**; mặc định hệ thống dùng SignalR, Node chỉ chạy khi cấu hình bật.

## Đọc code theo dự án mẫu

Nguồn tham khảo người dùng chỉ định: `E:\test_nodejs\backend`.

| Code quen thuộc trong dự án mẫu | Code ở đây | Điều chỉnh cho eContract |
| --- | --- | --- |
| `index.js`: Express → HTTP server → initializeChatSocket → routes → listen | `index.js` | Giữ cách tổ chức; tách `createBackend()` để kiểm thử và shutdown |
| `sockets/chat.socket.js`: `Server`, `io.use`, `connection`, `disconnect` | `sockets/chat.socket.js` | Giữ tên file/hàm và CommonJS; xác thực bằng ticket .NET, phòng theo tenant/tài nguyên |
| `routes/`, `controllers/`, `services/`, `middlewares/`, `config/` | Các thư mục tương ứng | Tách route publish, controller, service gọi .NET, HMAC và cấu hình |
| `jsonwebtoken` + `JWT_ACCESS_SECRET` + Redis session version | Ticket/lease do API .NET xác thực | Không tạo hệ đăng nhập thứ hai; dùng đúng session nhân viên và OTP khách hàng |
| `io.emit('receive_message', ...)`, `chat_history` toàn cục | Phát `NotificationsChanged` / `DiscussionChanged` vào đúng room | Không phát chéo tenant hoặc viết bình luận bỏ qua RBAC |

Không bê nguyên `.env`, `node_modules`, uploads, MongoDB/users API, Redis history hay Swagger của phần mềm khác. App hiện tại không cần những phần đó. Chưa thêm `models/` rỗng; khi viết module Node có dữ liệu riêng có thể bổ sung cùng controller/service/route tương ứng.

Thứ tự đọc để học: `index.js` → `config/realtime.js` → `sockets/chat.socket.js` → `services/dotnet.service.js` → `middlewares/service-auth.js` → `routes/index.js` → `controllers/realtime.controller.js` → `tests/realtime.test.js`.

## Luồng hoạt động

1. FE đăng nhập/OTP bằng API .NET hiện tại.
2. FE xin ticket 30 giây qua .NET bằng cookie HttpOnly đang có.
3. Socket.IO gửi `handshake.auth.ticket`, không gửi mật khẩu/cookie cho Node.
4. Node gọi `.NET /internal/realtime/consume` với chữ ký HMAC. Ticket chỉ đổi được một lần. .NET trả lease/phòng đã xác minh; client không được chọn room.
5. .NET ghi nghiệp vụ qua REST như trước. Thông báo có outbox/retry riêng cho Node; trao đổi phát best-effort sau commit như SignalR hiện tại.
6. Node kiểm tra lease qua .NET ngay trước delivery và mỗi 30 giây. .NET tải lại session bằng HTTP loopback để phát hiện logout thật, rồi dùng đúng validator/RBAC/OTP đang có.
7. FE nhận event rỗng, tải lại dữ liệu qua REST. Reconnect và focus cũng đồng bộ REST. Client gửi `send_message`/`join_room` qua socket sẽ bị ngắt.

Socket.IO được giới hạn transport `websocket`, không bật HTTP polling hoặc tự chuyển về SignalR. Socket.IO có giao thức riêng nên cần `socket.io-client`, không dùng WebSocket thuần hay client SignalR để kết nối.

## Chuẩn bị migration

Migration mới: **`20261027025251_NodeRealtimeDelivery`**, thêm 7 cột thường vào `tbl_NotificationDispatchOutbox` cùng index/CHECK riêng. Trạng thái SignalR và Node độc lập. Không JSON, không physical FK, không sửa hợp đồng/đợt thanh toán/bàn giao/nghiệm thu.

Chưa chạy migration trên DB local trong lần triển khai này. Cập nhật các tenant DB theo công cụ/quy trình migration hiện có trước khi chạy API mới. Nếu dùng `dotnet ef database update`, phải chỉ rõ **connection tới tenant DB cần cập nhật**; không chạy vào `master` của `TenantDatabaseTemplate`. Cập nhật cả các migration đang pending trước migration mới này. Central DB không có migration mới cho Node.

## Chạy local

Yêu cầu Node.js >=22.15 và .NET theo project hiện tại. Chạy lệnh từ thư mục `Source` đã clone của bạn, không phụ thuộc ổ E:.

**Workspace local hiện tại đã cấu hình sẵn (27/10/2026):**

- `Backend/ContractManagement.API/appsettings.Development.json`: bật Node realtime, URL Node `http://127.0.0.1:4000`, URL API `http://127.0.0.1:5005` và secret local.
- `BackendNode/.env`: đủ host/port/origin/API URL, secret khớp với API. `index.js` đọc file này theo vị trí của chính nó nên chạy từ thư mục khác cũng đọc đúng `.env`.
- `Frontend/.env.development`: API `http://localhost:5005`, chọn `socketio`, socket URL `http://localhost:4000`.

Local cho phép mở FE trực tiếp tại `http://localhost:3000` hoặc qua reverse proxy API tại `http://localhost:5005`. Hai origin đã có trong `BackendNode/.env` và CORS Development của API. Endpoint cấp ticket chấp nhận origin trùng scheme/host/port của request hoặc nằm trong allowlist; không bỏ kiểm tra origin. Khi đổi origin, cập nhật cả API và Node rồi khởi động lại. Launcher bỏ qua cổng đang chạy nên restart API không reload `.env` của Node đã chạy sẵn.

Không cần gõ lại `$env:...` mỗi lần chạy local. Chạy `dotnet run` trong project API: ở môi trường Development, `FrontendLauncher` dùng chung `npm run dev` để mở Node Backend (4000), Tenant Frontend (3000) và SystemAdmin (3001), bỏ qua ứng dụng có cổng đang chạy. Có thể tắt riêng Node bằng `Development.LaunchNodeBackend=false`, hoặc tắt mở FE bằng `Development.LaunchFrontends=false` trong `appsettings.Development.json`. Node cũng chỉ tự mở khi `Realtime.NodeWebSocket.Enabled=true`; production không tự mở các app này. Các file cấu hình local được Git ignore; không dùng secret local cho production. DB vẫn cần được cập nhật migration theo phần trên; thay đổi cấu hình không tự áp migration.

Node chạy giống hai FE: có thể tiếp tục chạy khi API dừng, lần chạy API sau sẽ dùng lại tiến trình đó. Muốn chạy riêng, dùng `npm run dev` trong `BackendNode`. Node lấy biến từ `.env` của nó; secret/URL của file này cần khớp cấu hình API như phần trên.

Các bước dưới dành cho clone/máy mới hoặc khi muốn cấu hình thủ công; không cần làm lại ở workspace đã cấu hình sẵn:

1. Trong `BackendNode`, chạy `npm ci`, copy `.env.example` thành `.env` rồi điền secret ngẫu nhiên ít nhất 32 ký tự. Không commit `.env`. `ALLOWED_ORIGINS` là danh sách origin chính xác, phân cách dấu phẩy.
2. Trong `Backend/ContractManagement.API/appsettings.Development.json`, thêm/gộp section sau ở root, giữ nguyên các section SQL/SMTP và cấu hình khác đang có. Secret phải giống hệt `REALTIME_SERVICE_SECRET` của Node:

```json
{
  "Realtime": {
    "NodeWebSocket": {
      "Enabled": true,
      "BaseUrl": "http://127.0.0.1:4000",
      "ApiBaseUrl": "http://127.0.0.1:5005",
      "ServiceSecret": "<secret giống BackendNode/.env>"
    }
  }
}
```

`Cors:AllowedOrigins` cần có `http://localhost:3000` (mặc định development đã có). Sau đó chạy API trong thư mục project:

```powershell
dotnet run --launch-profile http
```

`ApiBaseUrl` dùng để .NET probe lại chính nó; `DOTNET_BASE_URL` trong Node phải trỏ tới cùng API/port này. Host của Node/API nội bộ phải loopback; không đi vòng qua domain Nginx bên ngoài để probe.

Các request control/probe nội bộ gửi `X-Forwarded-Proto: https` để hoạt động với `UseHttpsRedirection` sau TLS termination. Giữ cấu hình API hiện có: chỉ tin forwarded headers từ proxy/loopback trên cùng máy; không mở `KnownProxies/KnownNetworks` cho mọi nguồn. Request vẫn bắt buộc HMAC, còn probe vẫn kiểm tra cookie/RBAC/OTP.

3. `dotnet run` ở Development đã tự mở Node theo launcher chung. Nếu tắt tự mở thì chạy riêng trong `BackendNode`: `npm run dev` hoặc `npm start`. `GET http://127.0.0.1:4000/health/live` trả `{"status":"live"}`. Health này chỉ xác nhận tiến trình Node, không xác nhận session/API .NET đã hoạt động.
4. Trong `Frontend/.env.development` thêm các biến sau, giữ các cấu hình khác đang dùng, chạy `npm ci` nếu vừa pull dependency mới rồi khởi động lại FE:

```dotenv
NEXT_PUBLIC_API_URL=http://localhost:5005
NEXT_PUBLIC_REALTIME_TRANSPORT=socketio
NEXT_PUBLIC_REALTIME_SOCKET_URL=http://localhost:4000
```

Không đặt service secret vào biến `NEXT_PUBLIC_*`. SystemAdmin chưa có tính năng realtime này, không cần client Socket.IO riêng.

Muốn dùng lại SignalR: đặt `NEXT_PUBLIC_REALTIME_TRANSPORT=signalr` hoặc bỏ biến này rồi khởi động/rebuild FE. Local hiện tại chỉnh biến trong `Frontend/.env.development`, và có thể tắt `Realtime.NodeWebSocket.Enabled` tại `appsettings.Development.json`; khi dùng biến môi trường thì đặt `Realtime__NodeWebSocket__Enabled=false`. Không cần gỡ code Node/SignalR. Không tự đổi transport khi mất kết nối.

## Deploy cùng domain

- Chạy một Node process bằng `pm2 start ecosystem.config.cjs` trong `BackendNode`, điền `.env` cho môi trường triển khai và lưu process theo quy trình PM2 của server. Cấu hình này dùng `instances: 1`, không cluster.
- API và Node nằm cùng host; URL nội bộ/secret qua biến môi trường. Nginx HTTPS là điểm truy cập từ bên ngoài. `ALLOWED_ORIGINS` và `Cors:AllowedOrigins` đều phải có origin FE thật, bao gồm port nếu có.
- FE build với `NEXT_PUBLIC_REALTIME_TRANSPORT=socketio`; bỏ `NEXT_PUBLIC_REALTIME_SOCKET_URL` để dùng cùng origin của API/FE. Biến NEXT_PUBLIC được chốt khi build; đổi env ở runtime không thay bundle cũ.
- Ghép các location trong `nginx-realtime.conf.example` vào server block hiện có. Giữ các location API, public, FE và hub SignalR cũ. Chặn `/internal/realtime/` từ Internet.
- Kiểm tra đồng hồ Node/.NET cùng host được đồng bộ: chữ ký request có cửa sổ 30 giây. Không log ticket/cookie/signature/secret.

## Giới hạn được chốt

- Triển khai hiện tại dành cho **một API instance + một Node instance**, cùng host. Ticket/lease, dedupe và nonce nằm trong memory có TTL/giới hạn dung lượng. Khởi động lại buộc lấy ticket mới; FE đọc lại REST. Muốn scale cần shared session/ticket store và Socket.IO adapter trước, không chỉ tăng PM2 instances.
- Lease tối đa 15 phút và không vượt expiry phiên khách hàng. Revalidation hỏng thì đóng socket. Probe có dùng session HTTP thực, nên tham gia cơ chế idle timeout của ASP.NET session; logout/password reset/khóa tài khoản vẫn được kiểm tra từ dữ liệu hiện tại.
- ACK publish chỉ là Node đã xử lý invalidation, không phải khách hàng đã đọc. Dedupe 5 phút, không hứa exactly-once. UI có thể bỏ lỡ invalidation khi mất mạng và đồng bộ lại qua REST.
- Notification Node thử tối đa 8 lần, backoff tối đa 5 phút, lease 1 phút. `Failed` cần vận hành kiểm tra; không tự replay dữ liệu lịch sử tạo khi Node tắt (`Disabled`). SignalR vẫn dùng các cột Status/Lease cũ.
- Discussion Node giữ best-effort như discussion SignalR hiện tại; không có durable chat event queue. Nội dung/lịch sử/đọc chưa đọc vẫn ở .NET.

## Kiểm tra

`npm test` trong `BackendNode` chạy server/socket loopback tạm và fake .NET authority: phòng, tenant, origin, ticket replay, revoke, nội bộ HMAC, client write bị chặn, dedupe và giới hạn kết nối. Backend .NET có tests ticket/lease, chữ ký, migration và integration HTTP/session/logout với DB InMemory. Không cần real DB cho các tests này.

Chưa chạy UAT/build/typecheck FE theo yêu cầu người dùng. Chạy qua proxy production và trình duyệt thật vẫn là checklist triển khai, không coi unit test là đã UAT xong.
