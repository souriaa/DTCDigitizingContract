# Kế hoạch triển khai không Docker, chuẩn bị trước khi có VPS/domain

Ngày rà soát: 2026-09-21. Phạm vi: **một máy Ubuntu, một instance mỗi ứng dụng**. Đây là kế hoạch có thể tiếp tục theo từng phase; các giá trị `example.com`, đường dẫn `/srv/...` và tên user là ví dụ, phải thay khi có VPS.

## Kết luận và những điểm cần sửa so với hướng dẫn ban đầu

- **Có thể chuẩn bị và kiểm thử phần lớn trên local**, nhưng không thể xác nhận DNS công cộng, chứng chỉ TLS, systemd sau reboot, firewall, kết nối SQL/SMTP thực và hành vi tải thật khi chưa có môi trường đích. “90%” và “10 phút” không phải cam kết đo được.
- Repo hiện có **hai ứng dụng Next.js**: `Frontend` và `SystemAdmin`, cùng API `Backend/ContractManagement.API` (.NET 10). **Không có `server.js` chat/Node riêng**. `server.js` sinh trong `.next/standalone` của *mỗi* Next app sau build; không được xem là dịch vụ chat. Route đề xuất: `app.example.com → 3000`, `admin.example.com → 3001`, `api.example.com → 5005`.
- Hai Next app đang đặt `output: "standalone"`. Artifact phù hợp là `.next/standalone/server.js` + bản sao `public` và `.next/static`, rồi PM2 chạy file đó. Nếu quyết định dùng `npm start`/`next start`, phải chuyển sang phương án deploy đầy đủ source + dependencies; **không trộn hai cách**.
- Backend hiện chỉ dùng EF Core **SQL Server**; PostgreSQL/MySQL không phải lựa chọn thay thế chỉ bằng sửa connection string. SQL Server có thể ở máy riêng/dịch vụ quản lý, không bắt buộc cài trên cùng VPS.
- Cần **LibreOffice/soffice và font tiếng Việt phù hợp** trên máy chạy API để render DOCX → PDF; chỉ publish DLL chưa đủ.
- PM2 tự khởi động lại process bị lỗi, nhưng **tự chạy sau reboot cần thêm `pm2 startup` và `pm2 save`**. Nginx và .NET do systemd quản lý trên Ubuntu.
- Nginx bản Windows chỉ thích hợp smoke test; bản Windows được upstream đánh dấu beta. Test local bằng HTTP không chứng minh luồng production HTTPS/cookie Secure. Có thể dùng VM/WSL Linux + TLS local cho bài test sát production hơn.
- Không có cơ sở để khẳng định non-Docker nhanh hơn Docker **1–3%** cho hệ thống này. Chọn non-Docker vì mô hình vận hành/quyền kiểm soát, không vì mức tăng hiệu năng chưa benchmark.
- `appsettings.Production.json` hiện có placeholder và production validation chặn khởi động nếu thiếu connection strings, HTTPS CORS origins, `AllowedHosts`. Không commit mật khẩu/khóa vào source hoặc tạo file production “trắng” rồi kỳ vọng app tự chạy.
- `appsettings.Development.json` trên máy dev là **file bị Git ignore**, hiện chứa cấu hình local có thông tin nhạy cảm. Không copy file này lên VPS, không đưa giá trị của nó vào plan. Nếu thông tin đã từng bị chia sẻ, cần đổi mật khẩu/app password tương ứng.

## Kiến trúc mục tiêu

```text
Internet -- HTTPS 443 --> Nginx
  app.example.com   --> 127.0.0.1:3000  Frontend Next (PM2)
  admin.example.com --> 127.0.0.1:3001  SystemAdmin Next (PM2)
  api.example.com   --> 127.0.0.1:5005  .NET API (systemd)
                                      --> SQL Server (Central + tenant DB)
                                      --> private-storage ngoài thư mục publish
                                      --> LibreOffice + fonts
```

Chỉ mở 80/443 ra Internet; port 3000/3001/5005 bind vào `127.0.0.1`. SQL chỉ cho truy cập từ mạng/host được phép. Nginx chuyển tiếp `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`; API chỉ tin forwarded headers từ loopback (`Program.cs`). Chạy một API instance vì session hiện lưu trong memory; scale nhiều instance cần thiết kế lại session/store và kiểm tra cache, không chỉ thêm process.

## Phase 0 — Rà soát repo và sửa điều kiện tiên quyết — **Đã hoàn thành**

- [x] Xác nhận thực tế 2 Next app, .NET 10 API, SQL Server, cấu hình PDF/private storage/health endpoint.
- [x] Bổ sung Forwarded Headers Middleware trước HSTS/HTTPS redirect, chỉ tin IPv4/IPv6 loopback và giới hạn một proxy hop.
- [x] Kiểm tra production config hiện tại; không sửa/ghi đè file config local bị ignore và không tạo secret trong Git.
- [ ] Khi có domain/VPS: xác nhận Ubuntu/CPU architecture, RAM/disk, DNS, email/OTP provider, vị trí SQL Server và chính sách backup.

## Phase 1 — Build và kiểm thử trên local — **Chưa thực hiện**

1. Chọn một package manager cho cả hai frontend. Vì repo có `package-lock.json`, ví dụ dưới đây dùng `npm ci`; không chạy lẫn `pnpm install` để tránh khác lockfile. Cài phiên bản Node được Next.js 16 hỗ trợ và .NET 10 SDK.
2. Với từng thư mục `Frontend` và `SystemAdmin`: đặt `NEXT_PUBLIC_API_URL` **trước khi build**, chạy `npm ci`, `npm run typecheck`, `npm run lint`, `npm run build`. Giá trị `NEXT_PUBLIC_*` được nhúng vào JS khi build; đổi API URL sau build thì phải build lại. Với test local sau Nginx, dùng `http://api.app.test:8080`; production dùng `https://api.example.com`.
3. Xác nhận `.next/standalone/server.js` tồn tại cho **từng** app. Để thử artifact độc lập, copy `public` (nếu có) vào `.next/standalone/public` và `.next/static` vào `.next/standalone/.next/static`; chạy lần lượt với `HOSTNAME=127.0.0.1`, `PORT=3000`/`3001`. Trên Windows dùng cú pháp biến môi trường PowerShell tương ứng.
4. Publish backend ra **thư mục ngoài project/repo**, không dùng `-o ./publish-output` ngay trong thư mục API (tránh output lồng bị quét khi build):

   ```powershell
   dotnet publish E:\DTC-DigitizingContract\Source\Backend\ContractManagement.API\ContractManagement.API.csproj -c Release --no-restore -o E:\DTC-publish-check\api
   ```

   Nếu cần restore, chạy `dotnet restore` trước. Chạy DLL từ thư mục output chỉ khi đã cấp cấu hình DB/storage phù hợp; startup có seed/kiểm tra storage nên thiếu DB sẽ không lên. Không dùng DB production để thử.
5. Kiểm tra build, `GET /health/live` và `GET /health/ready` (ready cần các dependency thực). Test một lượt đăng nhập, tạo preview PDF, upload/download private file trên local test DB.

**Điều kiện đạt:** hai Next artifact hoạt động độc lập, API chạy từ publish directory, health trả đúng, không phát sinh output lồng trong source. **Chưa chứng minh** systemd, TLS công cộng hay binary Linux từ test Windows.

## Phase 2 — Test reverse proxy không cần domain/VPS — **Chưa thực hiện**

1. Trên máy test, thêm vào `hosts` bằng quyền quản trị (không sửa tự động trong repo):

   ```text
   127.0.0.1 app.app.test
   127.0.0.1 admin.app.test
   127.0.0.1 api.app.test
   ```

   Dùng `.test` cho môi trường test, tránh `.local` vốn có thể va chạm mDNS. Có thể test Host header bằng `curl` nếu không muốn sửa `hosts`.
2. Chạy local Nginx/VM/WSL trên port `8080` để không cần chiếm port 80. Đây là cấu hình **smoke test HTTP**, không phải production config:

   ```nginx
   server { listen 8080; server_name app.app.test;
     location / { proxy_set_header Host $host; proxy_pass http://127.0.0.1:3000; }
   }
   server { listen 8080; server_name admin.app.test;
     location / { proxy_set_header Host $host; proxy_pass http://127.0.0.1:3001; }
   }
   server { listen 8080; server_name api.app.test;
     location / {
       proxy_set_header Host $host;
       proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
       proxy_set_header X-Forwarded-Proto $scheme;
       proxy_pass http://127.0.0.1:5005;
     }
   }
   ```

3. API chạy `Development` trên `127.0.0.1:5005` và thêm chính xác `http://app.app.test:8080`, `http://admin.app.test:8080` vào `Cors:AllowedOrigins` bằng environment/user-secrets local. Tắt `Development:LaunchFrontends` để API không tự bật Next dev servers khi đang test artifact. Cấu hình frontend `NEXT_PUBLIC_API_URL=http://api.app.test:8080` **trước build**.
4. `nginx -t`; truy cập cả ba host, kiểm tra request API/cookie/CORS, download/upload nhỏ và lớn trong giới hạn nghiệp vụ. Nếu test HTTPS local, dùng chứng chỉ local **được máy test tin cậy** và chuyển CORS/API URL sang `https`; không dùng `-k` rồi kết luận trình duyệt đã ổn.

**Điều kiện đạt:** định tuyến ba host, session/CORS và file transfer hoạt động. Nginx Windows không phải bằng chứng về ổn định production Linux.

## Phase 3 — Chuẩn bị artifact/config Ubuntu — **Chưa thực hiện**

1. Build Next **trên Linux hoặc CI Linux cùng kiến trúc với VPS** để tránh sai khác dependency/native binary giữa Windows và Ubuntu. Deploy riêng hai artifact standalone như Phase 1; không đưa `.env.development` lên server. Pin phiên bản Node, .NET runtime, Nginx, LibreOffice và fonts đã test.
2. Có thể cross-publish .NET từ local với `-r linux-x64 --self-contained false` ra thư mục ngoài repo, nhưng vẫn phải chạy smoke trên Linux có .NET 10 runtime, SQL network, LibreOffice và quyền storage thực. Không copy output `win-x64` lên Ubuntu.
3. Thư mục đề xuất: `/srv/contract-management/frontend/current`, `/srv/contract-management/admin/current`, `/srv/contract-management/api/current`; private file ở `/srv/contract-management/private-storage` hoặc volume riêng **ngoài** `current` và ngoài Nginx document root.
4. PM2 chạy hai `server.js` standalone (không có chat process):

   ```bash
   cd /srv/contract-management/frontend/current
   HOSTNAME=127.0.0.1 PORT=3000 pm2 start server.js --name frontend-next
   cd /srv/contract-management/admin/current
   HOSTNAME=127.0.0.1 PORT=3001 pm2 start server.js --name system-admin-next
   pm2 startup    # chạy lệnh sudo mà PM2 in ra, bằng đúng deploy user
   pm2 save
   ```

   Mỗi `current` ở trên là **nội dung** của `.next/standalone` đã copy static/public. Sau nâng Node version, cập nhật PM2 startup script. Trên Windows local chỉ cần test process; không dùng `pm2 delete all` vì có thể xóa app khác của máy.
5. systemd service mẫu cho API, lưu thành `/etc/systemd/system/contract-api.service` **khi có VPS**:

   ```ini
   [Unit]
   Description=Contract Management API
   Wants=network-online.target
   After=network-online.target

   [Service]
   User=contract-api
   Group=contract-api
   WorkingDirectory=/srv/contract-management/api/current
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=ASPNETCORE_URLS=http://127.0.0.1:5005
   EnvironmentFile=/etc/contract-management/api.env
   ExecStart=/usr/bin/dotnet /srv/contract-management/api/current/ContractManagement.API.dll
   Restart=on-failure
   RestartSec=5
   UMask=0077

   [Install]
   WantedBy=multi-user.target
   ```

   `api.env` chỉ nằm trên server, owner/quyền đọc tối thiểu (`0600`); không commit. Dùng `systemctl daemon-reload`, `enable --now contract-api`, `journalctl -u contract-api` khi triển khai.
6. Biến bắt buộc/quan trọng trong `api.env`: `AllowedHosts=api.example.com`; `ConnectionStrings__CentralDatabase`; `ConnectionStrings__TenantDatabaseTemplate`; `Cors__AllowedOrigins__0=https://app.example.com`; `Cors__AllowedOrigins__1=https://admin.example.com`; `PrivateFileStorage__RootPath`; `TemplatePdfRendering__ExecutablePath`; `CustomerOtp__...` theo provider thật; key/secret bootstrap nếu dùng. Không dùng `Fake` OTP ở production. Kiểm tra connection template không trỏ nhầm `master` khi migrate tenant. Nên xem thêm `PHASE11-PRODUCTION-RUNBOOK.md` trước khi migration.
7. Nginx production: ba `server_name`, TLS cert riêng/phù hợp wildcard, HTTP → HTTPS, `proxy_pass` vào loopback tương ứng. **API block phải chuyển `Host`, `X-Forwarded-For`, `X-Forwarded-Proto`** như Phase 2. Ví dụ một block; lặp lại cho app/admin với port tương ứng:

   ```nginx
   server {
     listen 443 ssl;
     server_name api.example.com;
     ssl_certificate /etc/ssl/contract-management/api-fullchain.pem;
     ssl_certificate_key /etc/ssl/contract-management/api-key.pem;
     location / {
       proxy_set_header Host $host;
       proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
       proxy_set_header X-Forwarded-Proto $scheme;
       proxy_pass http://127.0.0.1:5005;
     }
   }
   ```

   Cấu hình upload limit/timeouts theo giới hạn API/loại tệp thực tế; không mặc định Nginx sẽ nhận mọi file. Test `nginx -t` trước reload. Không expose private-storage bằng `root`/`alias`.

**Điều kiện đạt:** có artifact Linux, service/process config và danh sách secret có thể triển khai; các ví dụ trong file này chưa phải thông tin VPS thực.

## Phase 4 — Khi đã có VPS/domain: triển khai, nghiệm thu, vận hành — **Chờ hạ tầng**

1. Chốt DNS cho ba subdomain, phát hành/renew TLS cert; cài Node, .NET 10 runtime, Nginx, LibreOffice/fonts và SQL Server client/kết nối tới SQL instance. SQL Server có thể local hoặc riêng; chỉ có lựa chọn khác sau khi port provider/migration và test.
2. Tạo user service không phải root, thư mục/permission, secret file; cài artifact; chạy migrations Central và từng tenant theo runbook, sau backup **DB + private-storage cùng checkpoint**. Không tự bật migration ở startup.
3. `systemctl status contract-api`, `pm2 list`, `nginx -t`; kiểm tra `/health/live`, `/health/ready`, hai web, đăng nhập hai vai trò, cookie `Secure/HttpOnly`, CORS chính xác, OTP, template DOCX→PDF, upload/download, audit, reboot rồi kiểm tra các process tự lên lại.
4. Cấu hình giám sát: log rotation cho PM2/journald/Nginx, dung lượng storage/SQL, CPU/RAM, thời hạn TLS, outbox OTP, backup và thử **restore**. Một crash có thể tự phục hồi process nhưng không thay cho theo dõi lỗi nghiệp vụ.
5. Chỉ công bố hoàn thành sau khi test trên môi trường đích; giữ đường rollback artifact + DB/storage checkpoint tương thích.

## Nguồn kỹ thuật dùng để xác minh

- [Next.js self-hosting/reverse proxy](https://nextjs.org/docs/app/guides/self-hosting), [standalone output](https://nextjs.org/docs/app/api-reference/config/next-config-js/output), [biến môi trường nhúng lúc build](https://nextjs.org/docs/pages/guides/environment-variables).
- [PM2 startup và save](https://pm2.keymetrics.io/docs/usage/startup/).
- [ASP.NET Core forwarded headers](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0), [ASP.NET Core + Nginx/systemd](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0).
- [Nginx for Windows (beta)](https://nginx.org/en/docs/windows.html); [RFC 6761: `.test` dành cho test](https://www.rfc-editor.org/rfc/rfc6761).
