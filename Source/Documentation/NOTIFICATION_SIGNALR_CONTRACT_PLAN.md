# Kế hoạch module thông báo tái sử dụng với SignalR — triển khai đầu tiên cho hợp đồng

Ngày lập: 2026-09-25. Trạng thái: **chưa triển khai**. File này là điểm tiếp tục sau mỗi phase; cập nhật bảng trạng thái và nhật ký khi thực hiện. Không chạy migration hoặc thay đổi code trong bước lập kế hoạch.

## Mục tiêu và phạm vi

- Thông báo trong ứng dụng cho **nhân viên tenant** theo toàn bộ các chuyển tiếp nghiệp vụ quan trọng của hợp đồng: đàm phán, phê duyệt, ký, nghiệm thu, thanh toán, hoàn tất. Cùng một hạ tầng phải dùng được cho module khác bằng cách thêm event producer/recipient policy, không viết lại Hub, DB hay UI shell.
- Thông báo được lưu trong SQL tenant, đọc được khi người dùng offline. SignalR chỉ báo **dữ liệu đã thay đổi** để client gọi API đồng bộ; không xem WebSocket là kho lưu hoặc nguồn sự thật. Không gửi tên hợp đồng, nội dung bình luận, số tiền hoặc dữ liệu nhạy cảm qua push.
- Chưa gửi email/SMS/push trình duyệt. Khách hàng dùng public link/OTP **không** kết nối Hub và không nhận notification trong phase này; hành động của họ có thể tạo thông báo cho nhân viên.
- Không thông báo cho chính người vừa thao tác, trừ khi sự kiện là kết quả xử lý nền cần họ chú ý. Không tạo thông báo cho mỗi lần autosave/chỉnh trường; chỉ khi có chuyển trạng thái hoặc hành động cần theo dõi.
- Tuân thủ quy tắc dự án: **không thêm cột JSON, không làm converter/fallback cho dữ liệu cũ**. Dữ liệu test cũ không cần giữ khi thay đổi `tbl_Notification`. Không dùng notification thay cho ContractAudit.

## Hiện trạng đã xác minh

- `Backend/ContractManagement.Infrastructure/Persistence/Application/Models/TblNotification.cs` và `DbDtctechContext` đã có bảng `tbl_Notification` với `UserId`, title/message, object type/id, `IsRead`, `CreatedDate`, nhưng chưa thấy service/API/UI dùng nó. `UserId` chưa thể mặc nhiên xem là `EmployeeId`; đối chiếu migration/DB trước khi quyết định sửa bảng trực tiếp. Vì không bảo lưu test data, ưu tiên migration định nghĩa lại schema mới rõ nghĩa thay vì lớp tương thích.
- `Backend/ContractManagement.API/Filter/SessionAuthorizeAttribute.cs` xác thực session và trạng thái/version nhân viên ở controller. `TenantResolutionMiddleware` hiện chỉ tự xử lý đường `/api` hoặc public endpoint. Hub **không chạy qua MVC authorization filter**; phải tái dùng logic xác thực ở một service dùng chung, không gắn attribute rồi tưởng Hub đã an toàn.
- `Program.cs` có `UseRouting` → `UseCors` → `UseSession` → `TenantResolutionMiddleware` → controllers/reverse proxy. Đặt Hub dưới `/api/hubs/notifications` và map rõ trước reverse proxy. FE dùng cookie (`withCredentials`) và có chuông giao diện ở `Frontend/components/ui/custom/header.tsx` nhưng hiện là icon tĩnh.
- Worker OTP hiện đã có mẫu duyệt tenant active từ Central DB và tạo scope `ICurrentTenant` riêng. Notification outbox worker có thể theo mẫu **phân vùng tenant**, không dùng tenant từ HTTP request trong background job.
- Màn chi tiết hợp đồng đã đọc `?tab=` hoặc `#tab` với các tab `negotiation`, `terms`, `approval`, `signature`, `closing`, `activity`. Đây là đích điều hướng có thật; API phải kiểm tra quyền lại trước khi trả nội dung.

## Thiết kế chung (không khóa vào hợp đồng)

### Dữ liệu và delivery

Trong **mỗi tenant DB**, dùng các bảng quan hệ; tên cuối cùng xác nhận khi tạo migration:

1. `tbl_Notification`: `NotificationId`, `EventKey` duy nhất, `ModuleCode`, `EventCode`, `SubjectType`, `SubjectId`, `ActorEmployeeId` nullable, `Title`, `Body` ngắn, `OccurredAtUtc`, `CreatedAtUtc`, `ActionKey` nullable. Không lưu URL tùy ý/HTML; route được dựng từ `ActionKey` + subject bằng allowlist. `EventKey` lấy từ ID sự kiện nghiệp vụ/transition cụ thể (không chỉ `ContractId + Status`, vì có nhiều vòng/đợt).
2. `tbl_NotificationRecipient`: `NotificationRecipientId`, `NotificationId`, `EmployeeId`, `CreatedAtUtc`, `ReadAtUtc` nullable; unique (`NotificationId`, `EmployeeId`), FK tới notification và employee. Index để lấy trang theo (`EmployeeId`, `CreatedAtUtc DESC`, `NotificationRecipientId DESC`) và đếm unread. Một event nhiều người nhận nhưng mỗi người có trạng thái đọc riêng.
3. `tbl_NotificationDispatchOutbox`: `OutboxId`, `NotificationId` unique, `Status`, `AttemptCount`, `NextAttemptAtUtc`, `LeaseId`/`LeaseUntilUtc`, `DispatchedAtUtc`, `LastErrorCode` giới hạn. Ghi **cùng transaction** với nghiệp vụ + notification + recipient. Worker lease, retry có giới hạn/backoff; đẩy tín hiệu `NotificationsChanged` tới group người nhận. Nếu gửi lặp do retry, FE refetch/dedup theo `NotificationRecipientId`; không hứa exactly-once realtime.

Không có cột JSON. Không cần tạo một notification row cho từng recipient. API không bao giờ tin `EmployeeId`/`TenantId` gửi từ client. Cân nhắc retention (ví dụ 90–180 ngày sau khi đã đọc) bằng chính sách vận hành riêng, **chưa tự động xóa** khi chưa chốt yêu cầu lưu trữ.

### Giao diện nội bộ module

- `INotificationPublisher.StageAsync(event, recipients, dbContext, ct)`: nhận mã sự kiện, subject typed, nội dung an toàn và danh sách employee đã qua policy; stage rows vào **DbContext/transaction hiện tại**. Không gọi SignalR trực tiếp từ controller hoặc business service trước commit.
- Các `INotificationRecipientPolicy` theo module quyết định người nhận từ dữ liệu/permission tại thời điểm sự kiện; dùng chung bộ lọc nhân viên active, khử trùng, loại actor, kiểm tra quyền xem resource. Không lưu danh sách role trong notification JSON.
- `NotificationHub` không có method client để tự phát thông báo hay tự join group. Server xác thực session + tenant + trạng thái/version nhân viên khi kết nối rồi add group `tenant:{tenantId}:employee:{employeeId}`. Client không chọn tenant/employee qua query string. Cần cơ chế giới hạn tuổi kết nối/tái xác thực khi reconnect; logout/thu hồi session không được tiếp tục trả nội dung qua socket. Push chỉ là invalidation không có payload nhạy cảm, còn GET API luôn xác thực và kiểm tra quyền mới nhất.
- Thiết kế các mã `ModuleCode/EventCode/ActionKey` thành hằng số hoặc enum có registry/formatter theo module. Module mới chỉ thêm producer, recipient policy, formatter/action resolver; không sửa lõi persistence/transport.

### API/FE chung

- `GET /api/notifications?cursor=&limit=`, `GET /api/notifications/unread-count`, `PATCH /api/notifications/{id}/read`, `POST /api/notifications/mark-all-read` (chỉ các row của chính session employee, có phân trang/giới hạn; không nhận employee ID). Quyết định có cần `mark-unread` sau khi test UX, không mặc định thêm.
- `GET` trả `NotificationRecipientId`, thời gian UTC, title/body, trạng thái đọc, `ActionKey`/subject cần cho link đã chuẩn hóa. Trước khi trả link/tên resource, recheck quyền hiện tại; resource không còn quyền xem thì ẩn nội dung/đích hoặc không trả row theo chính sách nhất quán. Click notification **không** cấp quyền truy cập hợp đồng.
- FE có một `NotificationProvider` tại protected layout, **một** HubConnection cho session (không tạo một connection cho mỗi Header/page). Sau initial GET, reconnect, `visibilitychange/focus`, và `NotificationsChanged` thì refetch unread count/page với chống gọi trùng. `withAutomaticReconnect` cộng retry khi lần kết nối đầu thất bại; đóng kết nối khi logout, tenant/user thay đổi hoặc layout unmount. Khi realtime mất, inbox vẫn hoạt động qua REST và refresh khi quay lại tab; không hiện toast lỗi liên tục.
- Chuông + badge số chưa đọc, popover 10–20 mục gần nhất, trang danh sách đầy đủ có lọc chưa đọc/đã đọc; trạng thái loading/empty/error rõ ràng, mobile dùng được, `aria-label`/keyboard. Click mở `/contracts/{id}?tab=...` hoặc inbox phê duyệt nếu được phép, rồi đánh dấu đã đọc bằng API; xử lý trường hợp hợp đồng bị xóa/không còn quyền. Không dùng `window.location` tùy ý từ dữ liệu DB.

## Ma trận sự kiện hợp đồng (đợt triển khai đầu tiên)

Người nhận phải được xác định ở **backend tại đúng thời điểm commit**. `Owner` là nhân viên phụ trách hợp đồng; `approver` là người thực sự đủ điều kiện cho approval request theo workflow/inbox hiện tại; người có quyền execution chỉ nhận sự kiện nếu policy xác định họ là người cần hành động. Không broadcast mặc định toàn tenant. `owner`/người có quyền thao tác vẫn phải qua `EnsureCanReadAsync` hoặc policy tương đương khi hiển thị.

| Nhóm | Sự kiện tạo thông báo | Người nhận mục tiêu | Link/ghi chú |
| --- | --- | --- | --- |
| Tạo hồ sơ | Tạo Draft/gán người phụ trách ban đầu **nếu người nhận khác actor**; hồ sơ quay lại Draft/Negotiating sau quyết định | Owner mới/người phải sửa | `overview` hoặc `negotiation`; không báo mỗi lần lưu nháp |
| Sở hữu | Chuyển nhân viên phụ trách | Owner mới; owner cũ nếu còn quyền xem | `overview`; không báo cho actor |
| Đàm phán | Bắt đầu/vòng mới; phản hồi từ cổng khách hàng; comment/resolve/reopen cần đối phương xử lý | Owner và bên tham gia nội bộ liên quan, không gửi cho khách hàng | `negotiation` hoặc `terms`; chống spam comment liên tiếp bằng quy tắc gom thông báo nếu cần, không bỏ mất event |
| Truy cập khách hàng | Tạo/thay/thu hồi link, khi cần bàn giao cho nhân viên khác | Owner/người phụ trách theo policy | `customer-access`; không gửi OTP hoặc token/link bí mật trong thông báo |
| Phê duyệt | Gửi duyệt; duyệt; yêu cầu sửa; từ chối; rút yêu cầu | Gửi duyệt → tập approver hợp lệ; quyết định → owner/người gửi; rút → approver đã nhận | `approval` hoặc inbox; phân biệt từng approval request/vòng, bỏ recipient không còn active/quyền |
| Ký | Chuyển sang chờ ký; upload/thay bản ký hợp lệ, chuyển trạng thái Signed | Owner và nhân viên thực sự phụ trách bước ký (nếu policy có) | `signature`; không thông báo lúc chỉ preview/download |
| Nghiệm thu | Tạo biên bản Draft (chỉ nếu giao việc cho người khác), chốt chờ ký, upload bản ký, hủy; ký Partial còn phần thiếu/đã đủ | Owner và nhân viên phụ trách nghiệm thu theo policy | `closing`; nội dung cho biết còn `x/y` đợt nếu recipient có quyền; không coi Finalized là đã nghiệm thu |
| Thanh toán | Đợt hoàn tất kèm chứng từ; mở lại/hủy payment; đủ tổng thanh toán | Owner và nhân viên phụ trách thanh toán theo policy | `closing`; không báo mỗi lần chỉnh nháp |
| Kết thúc | Hợp đồng Completed; trạng thái Cancelled/Rejected **nếu có action nghiệp vụ tương ứng** | Owner và người liên quan còn quyền xem | `overview`/`activity`; không bịa endpoint cho trạng thái chưa có action |
| Nhắc việc | Đợt thanh toán đến hạn/quá hạn; yêu cầu duyệt hoặc bản ký chờ quá thời hạn **nếu có dữ liệu hạn đáng tin cậy** | Người được giao xử lý theo policy | Scheduled producer riêng, `EventKey` theo loại + đối tượng + ngày/mốc; giới hạn tần suất, không gửi mỗi lần worker chạy |

Trước khi code từng event, đối chiếu phương thức service/transaction thực tế. Nếu một event thiếu người nhận hợp lệ thì lưu audit nghiệp vụ như hiện tại, **không** gửi tới người khác cho đủ số lượng. Sự kiện trong bảng là danh mục mục tiêu; việc có tạo notification ở một thao tác cụ thể phải được test theo recipient matrix, tránh spam và rò rỉ.

## Các phase và trạng thái

| Phase | Nội dung | Trạng thái | Tiêu chí hoàn thành |
| --- | --- | --- | --- |
| 0 | Chốt event catalog, policy người nhận, quyền truy cập và baseline DB | Chưa làm | Mỗi event có producer, recipient, action/tab, nguồn `EventKey`; xác nhận cách đổi `tbl_Notification` và migration tenant mới |
| 1 | Schema quan hệ + migration + service/API inbox | Chưa làm | CRUD đọc/đánh dấu đọc theo session; unique/index; tenant/employee isolation; test API |
| 2 | SignalR Hub + xác thực session + outbox worker | Chưa làm | Hub handshake an toàn; worker phát sau commit, retry/dedup, restart không mất notification; không push dữ liệu nhạy cảm |
| 3 | FE module dùng chung | Chưa làm | Chuông, inbox, count, read state, một connection, reconnect/focus sync, điều hướng; test UI |
| 4 | Producer nhóm draft/owner/negotiation/approval | Chưa làm | Tất cả nhánh có test đúng người nhận và đúng một event cho một transition |
| 5 | Producer nhóm signing/acceptance/payment/completion | Chưa làm | Kiểm tra Final/Partial, reopen/void, complete; không có notification giả khi transaction rollback |
| 6 | End-to-end, bảo mật, deploy | Chưa làm | Test hai tenant/hai nhân viên, offline/reconnect, concurrent, Nginx WebSocket, session thu hồi, tải thực tế |

### Phase 0 — khóa nghiệp vụ trước khi sửa schema

1. Rà `TblNotification` ở migration/DB tenant thật: `UserId` đang tham chiếu gì, có FK/data hay không. Vì đây là môi trường test, nếu không phù hợp thì migration đổi/drop/recreate đúng một lần; không thêm fallback đọc cấu trúc cũ.
2. Với từng hàng trong ma trận, ghi service method và ranh giới transaction; đặc biệt `ContractService`, `ContractApprovalService`, `ContractSigningService`, `ContractAcceptanceService`, `ContractCompletionService`, public customer comment. Chốt nhóm người nhận thực tế cho ký/nghiệm thu/thanh toán nếu hệ thống chưa có assignee chuyên trách: chỉ owner hay tất cả người có permission; **không tự đoán rồi broadcast**.
3. Xác định link deep-link tab và permission hiện có. Chốt event nào actionable, event nào chỉ thông tin; cùng một event có thể có nhiều recipient với cùng `NotificationId`.
4. Chốt riêng ngưỡng/tần suất nhắc hạn và tính đúng của `DueDate`; nếu chưa có deadline cho bước duyệt/ký thì **không suy đoán hạn** hoặc tạo nhắc việc giả.

### Phase 1 — dữ liệu và REST

1. Migration tenant cho 3 bảng; các cột scalar, FK/check/index, UTC. Tạo dữ liệu notification/recipient/outbox cùng transaction với business event. Unique `EventKey` + recipient để thao tác retry không nhân đôi. Không tự chạy migration trên DB người dùng khi chưa được yêu cầu.
2. Tách session validation từ MVC filter thành service dùng chung mà Hub cũng gọi; controller notification trả 401/403/404 đúng. Cursor pagination ổn định, unread count query/index hiệu quả; `read`/`mark-all-read` idempotent, chỉ sửa row của người nhận.
3. Test migration trên tenant DB sạch và tenant mới provision; test cross-tenant (trùng employee/notification ID), người inactive, quyền vừa bị thu hồi, race đánh dấu đọc.

### Phase 2 — transport realtime

1. AddSignalR/MapHub ở `/api/hubs/notifications`; trong `OnConnectedAsync` kiểm tra cùng tenant/session/version/active/must-change-password policy của API. Group tạo ở server theo tenant+employee, không expose method client để publish/join. Không dựa vào `[SessionAuthorize]` MVC để bảo vệ Hub.
2. Outbox worker duyệt tenant active, lease row có cạnh tranh an toàn nếu nhiều worker, phát `NotificationsChanged` sau commit, retry/backoff và log mã lỗi không lộ body. Notification đã nằm DB ngay cả khi worker/hub lỗi. Reconnect luôn GET bù, vì push không bảo đảm nhận được.
3. Test WebSocket handshake qua cookie, CORS origin chính xác, kết nối hết hạn/thu hồi session, user bị disable, tenant khác, restart worker/API. Nếu cần tái xác thực định kỳ cho kết nối dài, đặt giới hạn tuổi connection; push chỉ invalidation, REST kiểm tra quyền lại.

### Phase 3 — FE dùng chung

1. Thêm package client SignalR theo phiên bản tương thích backend; xây API client/store/provider/hooks, mount ở protected layout sau khi `getMe` thành công. Header dùng chuông thật; không mount trong từng trang.
2. Lấy danh sách/count ban đầu, optimistic mark-read có rollback khi lỗi, đồng bộ nhiều tab bằng refetch/broadcast local nếu cần. Tín hiệu realtime chỉ gọi refetch có debounce; không render nội dung từ socket. Logout dọn connection và cache.
3. Test deep link với route hiện có, keyboard/mobile, notification bị mất quyền xem, thông báo cùng event cho nhiều người, kết nối rớt hoặc chưa bao giờ nối được.

### Phase 4–5 — gắn vào luồng hợp đồng

- Gắn ở **domain service sau khi nghiệp vụ thành công**, stage cùng transaction, không phát từ FE/controller hoặc sau `SaveChanges` rời rạc. Nếu service có nhiều lần save, bọc transition + notification trong transaction hoặc refactor điểm commit; tuyệt đối không tạo notification cho thao tác rollback/thất bại.
- Tạo test theo mỗi loại event: actor/recipient, không gửi cho actor, permission/resource, cùng transition retry không trùng, nhiều vòng approval/negotiation và nhiều biên bản Partial/payment milestone không ghi đè `EventKey`.
- Không để lỗi SignalR làm thất bại thao tác hợp đồng **sau commit**; lỗi lưu notification **trước commit** phải được xử lý nhất quán theo policy đã chốt (không âm thầm nuốt lỗi và mất thông báo).

### Phase 6 — vận hành và nghiệm thu

- Với Nginx triển khai không Docker, route `/api/hubs/notifications`/`negotiate` về API (không rơi sang Next), hỗ trợ `Upgrade`/`Connection`, HTTP/1.1 và timeout phù hợp. Cùng domain `/api` là thuận lợi cho cookie; nếu khác origin thì origin phải allowlist chính xác và client gửi credentials. Không cần Redis khi chỉ chạy một API process; nếu scale nhiều instance, cần quyết định Redis backplane/SignalR service và sticky sessions tương ứng **trước** khi bật multi-instance.
- Test E2E: nhân viên A tạo/gửi duyệt → manager B online/offline nhận đúng một notification → B trả duyệt → A nhận; khách hàng comment → owner nhận; ký → nghiệm thu Partial 1/N → Partial đủ → thanh toán → hoàn tất. Lặp với tenant khác, user không quyền, hai tab, reload, mất mạng, restart worker, retry outbox, thay đổi quyền/đăng xuất. Kiểm tra không đẩy data nhạy cảm qua frame WebSocket.
- Đo queue age/outbox pending/failed, số connection, tỉ lệ reconnect, latency từ commit đến chuông, unread count query. Đặt tiêu chí chấp nhận (ví dụ online <5 giây trong điều kiện bình thường) và xác minh không tăng lỗi nghiệp vụ.

## Quy tắc cập nhật khi tiếp tục

Sau mỗi phase: cập nhật trạng thái bảng, liệt kê file/migration đã sửa, lệnh test và kết quả, số liệu còn thiếu, việc cần làm ở phase kế tiếp. Không đánh dấu hoàn thành chỉ vì build pass; Phase 6 cần kiểm thử trình duyệt và SQL thực tế. Không thay đổi các sửa dở của phase nghiệm thu trước đó.

## Nhật ký thực hiện

- 2026-09-25 — Chỉ lập plan; rà bảng notification có sẵn nhưng chưa dùng, session filter/tenant middleware, OTP outbox worker, Header và route tab hợp đồng. Chưa sửa code, chưa tạo migration, chưa chạy test.

## Tài liệu kỹ thuật tham chiếu

- [SignalR authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0)
- [SignalR JavaScript client and reconnect](https://learn.microsoft.com/en-us/aspnet/core/signalr/javascript-client?view=aspnetcore-10.0)
- [SignalR Redis backplane/scale-out](https://learn.microsoft.com/en-us/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0)
