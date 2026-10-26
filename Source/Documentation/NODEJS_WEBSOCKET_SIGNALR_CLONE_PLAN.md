# Clone realtime SignalR sang Node.js WebSocket — giữ cả hai triển khai

Ngày lập: 26/10/2026. Trạng thái: **mới lập kế hoạch, chưa triển khai Node.js**.

## 1. Mục tiêu và ranh giới

- Thêm một realtime service Node.js TypeScript dùng thư viện `ws`, đặt tại `Realtime/`. Không dùng Socket.IO vì yêu cầu là WebSocket; không giả định client SignalR nói chuyện được với server WebSocket thuần.
- Giữ nguyên SignalR, hub, package `@microsoft/signalr`, các endpoint hiện tại và cơ chế chọn SignalR. Hai triển khai dùng cùng nghiệp vụ và event contract, không copy nghiệp vụ contract vào Node.
- .NET vẫn là nơi duy nhất xử lý quyền, tenant, OTP, comment/thread, lịch sử và database. Node chỉ xác thực quyền subscription đã được .NET cấp rồi phát sự kiện.
- Không chạy đồng thời hai connection cho cùng một UI. Chọn transport rõ ràng; mặc định `signalr`. Không tự chuyển transport khi lỗi, không fallback hay lớp dữ liệu legacy.
- Không đổi schema rich text, không thêm cột JSON. Nếu cần lưu ticket hoặc trạng thái phát riêng theo transport thì thêm bảng/cột dữ liệu thường.
- Bàn giao, nghiệm thu, thanh toán, RBAC và hợp đồng duy trì/bảo trì không đổi logic vì đổi realtime transport.

## 2. Hiện trạng đã kiểm tra trong source

| Phần | Triển khai hiện tại | Node clone phải giữ |
| --- | --- | --- |
| Thông báo | `Backend/ContractManagement.API/Domains/Notifications/NotificationHub.cs`; `/api/hubs/notifications` | Event `NotificationsChanged`, inbox/read/unread tiếp tục gọi REST |
| Phát thông báo | `NotificationDispatchWorker.cs`, `TblNotificationDispatchOutbox` | Lease/retry, loại actor, tenant và recipient hợp lệ |
| Trao đổi nhân viên | `EmployeeContractDiscussionHub`, `/api/contracts/{contractId}/versions/{versionId}/discussion-hub` | Subscription gắn tenant/contract/version, quyền theo `ContractDiscussionAccessService` |
| Trao đổi khách hàng | `CustomerContractDiscussionHub`, `/public/contracts/{tenantCode}/discussion-hub` | Session sau OTP, giới hạn đúng hợp đồng/phiên bản được chia sẻ |
| Phát trao đổi | `ContractDiscussionRealtimePublisher.cs` | `DiscussionChanged` không chứa nội dung, chỉ phát sau commit |
| FE trao đổi | `Frontend/hooks/use-contract-discussion-realtime.ts` | Debounce, refresh REST, cleanup, reconnect |
| FE thông báo | `Frontend/components/notifications/notification-provider.tsx` | Cập nhật inbox/count, đồng bộ nhiều tab, không nhân đôi toast |

Lưu ý: quyền vào phòng trao đổi hiện chặt hơn quyền xem hợp đồng vận hành của Technical. Phải gọi lại đúng `ContractDiscussionAccessService`, không thay bằng một phép kiểm tra role chung rồi mở rộng quyền ngoài ý muốn.

## 3. Kiến trúc đề xuất

`REST mutation → .NET kiểm tra quyền + ghi DB → commit → event/outbox → hai adapter độc lập: SignalR / Node WebSocket → FE nhận invalidation → tải dữ liệu qua REST có phân quyền`.

- Event tối thiểu: `eventId`, `eventName`, `occurredAtUtc`, tenant nội bộ, subject IDs và recipient scopes. Không phát ConnectionString, cookie, linkToken, OTP, password hoặc nội dung trao đổi ra broadcast.
- Client chỉ nhận metadata cần thiết. Không tin tenantId/employeeId/room do client tự gửi.
- EventName ban đầu chỉ `NotificationsChanged` và `DiscussionChanged`; kiểm kê thêm event thực tế trước khi mở rộng, không hứa presence/typing/read-receipt nếu hiện chưa có.
- Giữ Node trên loopback/private network. Endpoint publish nội bộ có service authentication, giới hạn payload/rate, chống replay. Production dùng TLS; không chia sẻ cookie session ASP.NET cho Node để tự giải mã.

## 4. Xác thực và thu hồi quyền

1. Nhân viên/khách hàng đăng nhập bằng REST hiện tại. FE xin ticket dùng một lần qua .NET, có CSRF/origin checks phù hợp cookie session.
2. .NET xác định tenant từ session/route hợp lệ, kiểm tra tài khoản hoạt động, sessionVersion, bắt buộc đổi mật khẩu, quyền tài nguyên; khách hàng phải còn session OTP và link/version hiệu lực.
3. Ticket ngẫu nhiên, lưu hash (nếu lưu DB), TTL ngắn (đề xuất 30 giây); exchange một lần và bind đúng transport/audience. Không để secret lâu dài trong URL/query/log.
4. Node nhận ticket trong khung xác thực đầu tiên sau upgrade; socket chưa xác thực không được join/phát bất kỳ phòng nào và đóng sau vài giây. Node đổi ticket qua endpoint nội bộ .NET; phía .NET thực hiện consume nguyên tử.
5. Node chỉ tạo room từ scope server trả về. Hạn lease không vượt thời hạn session khách hàng hoặc lease 15 phút hiện tại của discussion; đóng/renew có xác thực khi hết hạn.
6. Logout, đổi mật khẩu/role, khóa nhân viên/tenant, đổi owner, thu hồi link khách hàng hoặc tạo version mới phải chặn subscription cũ. Chốt cách push revoke + periodic revalidation trước khi coi phase bảo mật hoàn tất; không chỉ kiểm tra lúc connect.
7. Validate Origin chính xác ở upgrade, không chỉ dựa vào CORS. Có giới hạn số connection theo actor/IP và số message/byte, schema whitelist, heartbeat, backpressure.

## 5. Delivery khi giữ hai transport

- Không để hai worker cùng cạnh tranh một outbox rồi một transport mất sự kiện. Có một coordinator fan-out và trạng thái delivery riêng cho từng transport được bật.
- Nếu cần persistence: bảng `tbl_RealtimeDelivery` gồm khóa outbox/event, Transport, Status, AttemptCount, NextAttemptAtUtc, LeaseId, LeaseUntilUtc, CompletedAtUtc, lỗi đã lọc. Unique `(EventId, Transport)`; giữ cột khóa liên kết logic, không thêm physical FK theo quy ước hiện tại.
- SignalR phát thành công không được đánh dấu Node đã phát; Node down không chặn SignalR và không rollback comment/payment đã commit.
- Node ACK nội bộ nghĩa là nhận event, không phải người dùng đã đọc. Inbox/read-state thuộc REST/database hiện tại.
- Reconnect/refocus luôn tải lại dữ liệu chuẩn; dedupe eventId, không thêm duplicate thông báo hay tự tạo lại comment.
- Với discussion hiện chưa có durable outbox: chọn bổ sung outbox chuẩn nếu cần bảo đảm retry; không tuyên bố exactly-once. Không thay đổi quyền/transaction ghi comment chỉ vì thêm transport.

## 6. Các phase và checkpoint

Trạng thái dùng: `Chưa bắt đầu / Đang làm / Hoàn thành / Cần quyết định`. Chỉ đánh dấu hoàn thành khi có bằng chứng kiểm tra.

| Phase | Phạm vi và điều kiện hoàn tất | Trạng thái |
| --- | --- | --- |
| 0 | Kiểm kê endpoints/event/auth và integration tests hiện tại; chốt phạm vi clone. Xác nhận không xóa SignalR | Chưa bắt đầu |
| 1 | Định nghĩa envelope/schema dùng chung, interface transport và cấu hình bật từng transport; .NET vẫn mặc định SignalR | Chưa bắt đầu |
| 2 | Ticket exchange, session/RBAC/OTP/tenant isolation và revoke; kiểm thử giả mạo/expired/replay/đổi owner | Chưa bắt đầu |
| 3 | Tạo `Realtime/` Node TypeScript + ws, upgrade/auth, phòng theo scope, heartbeat, limits, health, graceful shutdown | Chưa bắt đầu |
| 4 | Adapter publish .NET → Node có auth, fan-out không tranh outbox; retry/idempotency và độc lập lỗi từng transport | Chưa bắt đầu |
| 5 | Clone notifications: nhận `NotificationsChanged`, inbox/count/read-state REST, nhiều tab, không nhân đôi toast | Chưa bắt đầu |
| 6 | Clone general discussion + term discussion, cả FE nội bộ và public sau OTP, đúng tenant/contract/version; không truyền nội dung qua broadcast | Chưa bắt đầu |
| 7 | FE transport factory `signalr` / `websocket`, dùng chung hook/state/UI; chỉ một connection cho mỗi feature; cleanup/reconnect không báo lỗi giả khi reload | Chưa bắt đầu |
| 8 | Backend/Node contract tests + bảo mật, mất kết nối, restart, duplicate, out-of-order, quyền thay đổi; SignalR regression không lỗi | Chưa bắt đầu |
| 9 | Cấu hình PM2/systemd + Nginx cùng domain `/realtime/`, TLS/Origin, internal endpoint private; tài liệu chạy local/deploy | Chưa bắt đầu |
| 10 | Đối chiếu hai transport cùng bộ kịch bản; ghi kết quả demo/CV và hướng dẫn lựa chọn. SignalR vẫn chạy độc lập khi không bật Node | Chưa bắt đầu |

Checkpoint mỗi phase phải ghi: commit nền; file đã sửa; task đã xong/chưa xong; lệnh và kết quả test; migration đã tạo/đã áp ở đâu; cấu hình thay đổi; việc tiếp theo. Khi tiếp tục đọc checkpoint và git diff, không làm lại phase đã xác minh. Không tự chạy UAT/build FE hoặc áp DB nếu chưa được cho phép.

## 7. Cấu hình dự kiến

- .NET: `Realtime:SignalR:Enabled=true`; `Realtime:NodeWebSocket:Enabled=false`; URL nội bộ Node và secret riêng qua biến môi trường. Không hardcode IP/secret vào source.
- FE: `NEXT_PUBLIC_REALTIME_TRANSPORT=signalr|websocket` và path cùng origin `/realtime/`. Nếu dùng biến NEXT_PUBLIC, phải xác định đây là cấu hình build-time và ghi bước rebuild; không giả định đổi env sau build sẽ tự có tác dụng.
- Node: host loopback, port riêng không trùng API/FE, allowlist origins, địa chỉ exchange endpoint .NET, service credential, max payload/connections, heartbeat interval và shutdown timeout.
- Nginx giữ nguyên routes/hubs SignalR và thêm `/realtime/` cho Node, chuyển tiếp Upgrade/Connection đúng, timeout phù hợp heartbeat. Không công khai endpoint publish/exchange nội bộ.

## 8. Luồng nhìn từ người dùng và checklist

- Nhân viên mở hợp đồng → UI chọn transport theo cấu hình → xác thực → bình luận/đổi trạng thái qua REST → đối tượng được phép nhận cập nhật mà không tải lại trang.
- Khách hàng vào link → OTP thành công → chỉ nhận trao đổi hợp đồng/version được cấp. Thu hồi link hoặc hết session thì socket đóng, REST cũng từ chối.
- Tạo/chốt bàn giao, ký hợp đồng, nghiệm thu, thanh toán và hoàn tất vẫn theo RBAC mới. Owner/AdminOfficer/Manager nhận thông báo như trước, trừ actor; Node không tự quyết định người nhận.
- Kiểm tra tenant A/B cùng employeeId, hai contract/version khác nhau không nhận chéo; client giả room bị từ chối; Technical không có thêm quyền ký/nghiệm thu hợp đồng khác.
- Dừng Node: REST và SignalR vẫn hoạt động. Dừng SignalR khi cấu hình Node: Node vẫn hoạt động, không tự tạo SignalR fallback. Cả hai được bật trên server: hai trình duyệt chọn hai transport vẫn nhận cùng sự kiện, mỗi UI chỉ xử lý một lần.
- Kiểm tra deploy: wss, reverse proxy, HTTPS origin, reconnect, nhiều tab, logout, process restart, outbox độc lập, không lộ token trong access log.

## 9. Tài liệu kỹ thuật tham khảo

Đề xuất trên dựa vào code hiện tại, không phải xác nhận Node đã có trong dự án. `ws` hỗ trợ server WebSocket và xác thực ở HTTP upgrade; thư viện không thay thế RBAC/nghiệp vụ của .NET. [Tài liệu chính thức ws](https://github.com/websockets/ws/blob/master/README.md).

Origin cần kiểm tra riêng cho WebSocket; giới hạn CORS của SignalR không tự bảo vệ WebSocket thuần. [Microsoft: SignalR security](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-10.0).

Reverse proxy phải chuyển tiếp header upgrade và đặt timeout/heartbeat phù hợp. [Nginx WebSocket proxying](https://nginx.org/en/docs/http/websocket.html).
