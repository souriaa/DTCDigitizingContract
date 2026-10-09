# Triển khai không Docker: Frontend và API chung một hostname

Cập nhật: 2026-09-22. Đây là hướng dẫn thao tác từ máy Windows test đến Ubuntu production, tổng hợp từ các lần build và debug thực tế. [Kế hoạch cũ](NON_DOCKER_DEPLOYMENT_PLAN.md) mô tả ba hostname riêng; **về định tuyến, tài liệu này thay thế phương án đó**. Không coi việc test HTTP trên Windows là đã nghiệm thu production.

## 1. Kiến trúc và đường dẫn

| Thành phần | Local test | Production mẫu | Tiến trình nội bộ |
| --- | --- | --- | --- |
| Frontend | `http://app.app.test:8080/` | `https://app.example.com/` | Next.js `127.0.0.1:3000` |
| API | `http://app.app.test:8080/api/...` | `https://app.example.com/api/...` | .NET `127.0.0.1:5005` |
| SystemAdmin | `http://admin.app.test:8080/` | `https://admin.example.com/` | Next.js `127.0.0.1:3001` |

`app.example.com` và `admin.example.com` là **ví dụ**, không phải domain đã mua. Không có `api.example.com` hay `api.app.test` trong mô hình mới. SystemAdmin vẫn gọi API qua hostname của Frontend. Chỉ Nginx công khai cổng 80/443; các cổng ứng dụng bind loopback. Chạy một API instance vì session hiện lưu trong memory.

Đường dẫn source đã xác nhận trên máy test; `DTC-Publish` là thư mục output sẽ được tạo khi publish:

```text
Source:  C:\DTCDigitizingContractTestDeploy\DTCDigitizingContract\Source
Output:  C:\DTCDigitizingContractTestDeploy\DTC-Publish
API DLL: C:\DTCDigitizingContractTestDeploy\DTC-Publish\api\ContractManagement.API.dll
Nginx:   C:\DTCDigitizingContractTestDeploy\DTC-Publish\nginx
Config:  C:\DTCDigitizingContractTestDeploy\DTC-Publish\nginx\conf\dtc_digitizingcontract.conf
```

`phase2.conf` là tên ví dụ cũ và **không tồn tại** trong bộ Nginx hiện tại. Các lệnh dưới đây dùng `dtc_digitizingcontract.conf`.

## 2. Điều kiện tiên quyết

- Source và hai `package-lock.json` phải cùng commit/bản dự định triển khai. Dùng `npm ci`, không trộn npm với pnpm. Máy build cần Node tương thích với phiên bản Next.js trong repo và .NET 10 SDK; máy chạy Ubuntu cần Node và .NET 10 runtime tương ứng.
- API dùng **SQL Server** cho central/tenant DB. Có DB test riêng, connection strings hợp lệ và backup trước mọi migration production. Không suy ra có thể đổi sang PostgreSQL/MySQL chỉ bằng connection string.
- API render DOCX → PDF bằng LibreOffice; cần `soffice`/`libreoffice` và font tiếng Việt trên máy chạy API. Private storage phải là thư mục bền vững, có quyền ghi cho service API, nằm ngoài release và Nginx document root.
- Không đưa mật khẩu SQL/SMTP, OTP key, `appsettings.Development.json` hoặc private storage vào Git, artifact production hay tài liệu này. `ContractManagement.API.csproj` hiện cấu hình copy file Development vào output; **luôn kiểm tra artifact trước khi upload**. Nếu credential thật từng xuất hiện trong ảnh/log chia sẻ, hãy thay credential đó trước khi dùng production.
- Nếu chưa có VPS/domain, chỉ hoàn tất các bước local và chuẩn bị Linux artifact. Không thể xác nhận TLS công cộng, DNS, reboot, SQL/SMTP production khi chưa có môi trường đích.

## 3. Build và chạy thử trên Windows

### 3.1. Khai báo hostname test

Mở Notepad bằng quyền Administrator và sửa `C:\Windows\System32\drivers\etc\hosts`:

```text
127.0.0.1 app.app.test
127.0.0.1 admin.app.test
```

Không cần `api.app.test`. Dùng `.test` để tránh va chạm với domain thật.

### 3.2. Build hai ứng dụng Next.js

Nếu đang chạy hai `server.js`, dừng chúng trong terminal tương ứng trước khi build. Trong PowerShell:

```powershell
$source = 'C:\DTCDigitizingContractTestDeploy\DTCDigitizingContract\Source'

foreach ($app in @('Frontend', 'SystemAdmin')) {
    Push-Location (Join-Path $source $app)
    try {
        $env:NEXT_PUBLIC_API_URL = 'http://app.app.test:8080'
        npm ci
        if ($LASTEXITCODE -ne 0) { throw "npm ci $app thất bại" }
        npm run typecheck
        if ($LASTEXITCODE -ne 0) { throw "typecheck $app thất bại" }
        npm run lint
        if ($LASTEXITCODE -ne 0) { throw "lint $app thất bại" }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "build $app thất bại" }

        if (-not (Test-Path -LiteralPath '.next\standalone\server.js')) {
            throw "Thiếu standalone/server.js của $app"
        }
        New-Item -ItemType Directory -Force '.next\standalone\.next\static' | Out-Null
        Copy-Item '.next\static\*' '.next\standalone\.next\static' -Recurse -Force
        if (Test-Path -LiteralPath 'public') {
            New-Item -ItemType Directory -Force '.next\standalone\public' | Out-Null
            Copy-Item 'public\*' '.next\standalone\public' -Recurse -Force
        }
    }
    finally { Pop-Location }
}
```

`NEXT_PUBLIC_API_URL` được nhúng khi build. Đổi URL sau đó **phải build lại cả Frontend và SystemAdmin**; chỉ sửa biến môi trường lúc chạy `server.js` không đổi URL trong bundle. Không dùng bản `.next/standalone` build trên Windows để chạy Ubuntu.

Chạy từng app trong **terminal riêng**:

```powershell
cd C:\DTCDigitizingContractTestDeploy\DTCDigitizingContract\Source\Frontend\.next\standalone
$env:HOSTNAME = '127.0.0.1'
$env:PORT = '3000'
node .\server.js
```

```powershell
cd C:\DTCDigitizingContractTestDeploy\DTCDigitizingContract\Source\SystemAdmin\.next\standalone
$env:HOSTNAME = '127.0.0.1'
$env:PORT = '3001'
node .\server.js
```

### 3.3. Publish và chạy API test

Nếu API cũ đang chạy từ cùng output, dừng bằng `Ctrl+C` trước khi publish để tránh lỗi DLL bị khóa. Output đặt **ngoài project**:

```powershell
$source = 'C:\DTCDigitizingContractTestDeploy\DTCDigitizingContract\Source'
$apiOut = 'C:\DTCDigitizingContractTestDeploy\DTC-Publish\api'
dotnet publish "$source\Backend\ContractManagement.API\ContractManagement.API.csproj" -c Release -o $apiOut
if ($LASTEXITCODE -ne 0) { throw 'Publish API thất bại' }
Test-Path -LiteralPath "$apiOut\ContractManagement.API.dll"
```

Chạy từ **thư mục chứa DLL**, trong terminal giữ các biến DB/OTP test đã cấp sẵn bằng cấu hình local an toàn:

```powershell
cd C:\DTCDigitizingContractTestDeploy\DTC-Publish\api
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5005'
$env:Development__LaunchFrontends = 'false'
$env:AllowedHosts = 'app.app.test;localhost;127.0.0.1'
$env:Cors__AllowedOrigins__0 = 'http://app.app.test:8080'
$env:Cors__AllowedOrigins__1 = 'http://admin.app.test:8080'
$env:PrivateFileStorage__RootPath = 'C:\DTCDigitizingContractTestDeploy\DTC-Publish\private-storage-test'
dotnet .\ContractManagement.API.dll
```

Không mở terminal mới rồi chỉ chạy DLL nếu terminal cũ còn chứa các biến DB cần thiết: terminal mới phải được cấp lại toàn bộ cấu hình. `AllowedHosts` phải có `app.app.test` **không kèm port**. File `appsettings.json` hiện mặc định `AllowedHosts=localhost`; nếu bỏ biến override, Nginx chuyển Host thật tới API sẽ bị `400 Bad Request - Invalid Hostname`.

### 3.4. Cấu hình Nginx Windows cho cùng hostname

Lưu file tại `C:\DTCDigitizingContractTestDeploy\DTC-Publish\nginx\conf\dtc_digitizingcontract.conf`:

```nginx
worker_processes 1;
events { worker_connections 1024; }

http {
    map $http_upgrade $connection_upgrade {
        default upgrade;
        '' close;
    }
    server {
        listen 127.0.0.1:8080 default_server;
        return 444;
    }

    server {
        listen 127.0.0.1:8080;
        server_name app.app.test;
        client_max_body_size 50m;

        location ^~ /api/ {
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;
            proxy_read_timeout 120s;
            proxy_set_header Host $http_host;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_pass http://127.0.0.1:5005;
        }

        location ^~ /health/ {
            proxy_set_header Host $http_host;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_pass http://127.0.0.1:5005;
        }

        location ~ ^/public/contracts/[^/]+/(shared|comments|discussion-hub(/negotiate)?|[^/]+/(availability|otp/(request|verify)))$ {
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection $connection_upgrade;
            proxy_read_timeout 120s;
            proxy_set_header Host $http_host;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            proxy_pass http://127.0.0.1:5005;
        }

        location / {
            proxy_set_header Host $http_host;
            proxy_pass http://127.0.0.1:3000;
        }
    }

    server {
        listen 127.0.0.1:8080;
        server_name admin.app.test;
        location / {
            proxy_set_header Host $http_host;
            proxy_pass http://127.0.0.1:3001;
        }
    }
}
```

Regex `/public/contracts/...` là bắt buộc với **route hiện tại**: API public của khách dùng `availability`, `otp`, `shared`, `comments`, `discussion-hub` và `discussion-hub/negotiate`, còn trang nhập OTP cũng nằm dưới `/public/contracts/{tenantCode}/{linkToken}`. Chuyển cả tiền tố `/public/contracts/` sang API sẽ phá trang; không chuyển gì sẽ phá các request OTP. Nếu sau này thêm endpoint public, cập nhật route proxy hoặc chuyển API công khai về một tiền tố `/api/...` bằng một thay đổi code có test. Dòng `proxy_pass` **không có dấu `/` cuối** để giữ nguyên URI `/api/...`.

Các header `Upgrade`/`Connection` và `proxy_http_version 1.1` phục vụ WebSocket cho thông báo và trao đổi hợp đồng. Không cần cài SignalR riêng. Sau khi cập nhật API/Frontend, phải kiểm tra và tải lại cấu hình Nginx trên máy triển khai. Khách hàng kết nối bằng cookie HttpOnly đã cấp sau OTP; không đưa `linkToken` vào URL hub.

Trước khi khởi động, kiểm tra các Nginx test cũ; nhiều master cùng giữ `8080` từng khiến request chạy vào cấu hình cũ và trả Next.js 404. Chỉ dừng tiến trình có đúng đường dẫn binary này, sau khi xem danh sách:

```powershell
$nginxDir = 'C:\DTCDigitizingContractTestDeploy\DTC-Publish\nginx'
$nginxExe = (Resolve-Path "$nginxDir\nginx.exe").Path
$testProcesses = @(Get-Process nginx -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $nginxExe })
$testProcesses | Select-Object Id, Path
# Chỉ chạy dòng sau khi đã xác nhận đây đều là Nginx của bộ test:
$testProcesses | Stop-Process -Force

cd $nginxDir
.\nginx.exe -t -c conf\dtc_digitizingcontract.conf
if ($LASTEXITCODE -ne 0) { throw 'Nginx config không hợp lệ' }
.\nginx.exe -c conf\dtc_digitizingcontract.conf
```

Nginx khởi động thành công trên Windows thường **không in gì**. Sau này chỉ sửa cấu hình của cùng một master thì dùng `nginx.exe -t -c conf\dtc_digitizingcontract.conf` rồi `nginx.exe -s reload -c conf\dtc_digitizingcontract.conf`; không chạy thêm lệnh `-c` để tạo master mới. Xem `logs\error.log` nếu không lên.

### 3.5. Kiểm tra end-to-end local

```powershell
curl.exe --noproxy '*' -i http://app.app.test:8080/health/live
curl.exe --noproxy '*' -i http://app.app.test:8080/health/ready
curl.exe --noproxy '*' -i http://app.app.test:8080/api/auth/me
curl.exe --noproxy '*' -I http://app.app.test:8080/
curl.exe --noproxy '*' -I http://admin.app.test:8080/
```

- `live`: `200`. `ready`: `200` khi SQL, storage và LibreOffice đã sẵn sàng; nếu `503`, đọc dependency status/log, không bỏ qua.
- `/api/auth/me`: có thể `401` khi chưa đăng nhập; phải là response API, **không phải HTML 404 của Next.js**.
- Trong DevTools Network, Frontend gọi `app.app.test:8080/api/...`; SystemAdmin gọi cùng API URL. Kiểm tra đăng nhập cả hai ứng dụng, cookie, tải/upload file, tạo preview DOCX/PDF và một link public khách hàng gồm trang, OTP, `shared`, comment.
- `/_vercel/insights/script.js` có thể 404 trên bản self-host nếu chưa cấu hình dịch vụ đó; không đánh đồng với 404 của API.

## 4. Chuẩn bị bản chạy Ubuntu và triển khai production

### 4.1. Chốt thông số trước khi build

Ghi lại domain thật, kiến trúc CPU VPS (`uname -m`), phiên bản Ubuntu, Node, .NET 10 runtime, Nginx, LibreOffice và fonts. Bản Next.js phải build trên Linux/CI Linux tương thích với VPS; **không copy `.next/standalone` Windows**. `NEXT_PUBLIC_API_URL` production là `https://app.example.com` sau khi thay bằng hostname thật. Chưa có domain thì bản build dùng `.test` chỉ là bản diễn tập, không phải artifact phát hành.

Trên môi trường Linux build (WSL Ubuntu hoặc CI Linux), dùng cùng commit/lockfile:

```bash
cd /path/to/Source/Frontend
export NEXT_PUBLIC_API_URL='https://app.example.com'
npm ci && npm run typecheck && npm run lint && npm run build
mkdir -p .next/standalone/.next
cp -a .next/static .next/standalone/.next/
cp -a public .next/standalone/

cd /path/to/Source/SystemAdmin
export NEXT_PUBLIC_API_URL='https://app.example.com'
npm ci && npm run typecheck && npm run lint && npm run build
mkdir -p .next/standalone/.next
cp -a .next/static .next/standalone/.next/
cp -a public .next/standalone/
```

Chỉ đóng gói **nội dung** của mỗi `.next/standalone` để đưa vào thư mục release tương ứng. Có thể cross-publish API từ Windows bằng `dotnet publish ... -c Release -r linux-x64 --self-contained false -o <output-ngoai-repo>`, nhưng chỉ dùng `linux-x64` nếu VPS là x64 (`linux-arm64` nếu ARM64). Không dùng `--no-restore` khi đổi RID lần đầu nếu chưa restore cho RID đó. API phải được smoke test lại trên Linux.

**Gate bảo mật trước upload API:** kiểm tra output có `appsettings.Development.json` hay `.env` chứa secret không; loại khỏi artifact production, **không xóa bản gốc trong source local**. Xác nhận không kèm `private-storage`, DB backup hoặc file người dùng. Không đưa `appsettings.json` mặc định `AllowedHosts=localhost` lên rồi mong hoạt động; production sẽ override qua environment file.

### 4.2. Chuẩn bị VPS và các dịch vụ

Trên Ubuntu: cài đúng Node, .NET 10 ASP.NET Core runtime, PM2, Nginx, LibreOffice và font tiếng Việt; kiểm tra `node --version`, `dotnet --list-runtimes`, `libreoffice --version`, `fc-list`. Cài và cấu hình SQL Server hoặc kết nối đến SQL Server riêng; không bắt buộc DB ở cùng VPS. Chỉ mở 80/443 ra Internet. Tạo user service không phải root và các thư mục:

```text
/srv/contract-management/frontend/current  # nội dung standalone Frontend
/srv/contract-management/admin/current     # nội dung standalone SystemAdmin
/srv/contract-management/api/current       # nội dung publish API Linux
/srv/contract-management/private-storage   # dữ liệu bền vững, ngoài release
/etc/contract-management/api.env           # secret, không nằm trong web root/Git
```

Đặt quyền tối thiểu: user PM2 đọc được hai Next release; user `contract-api` đọc API release và ghi `private-storage`; `api.env` chỉ root/service manager được đọc, ví dụ mode `0600`. Không cấp quyền public web tới `private-storage`.

File `/etc/contract-management/api.env` cần có các **tên biến** sau, giá trị thực phải cấp trên VPS bằng secret phù hợp, không commit:

```text
AllowedHosts=app.example.com
Cors__AllowedOrigins__0=https://app.example.com
Cors__AllowedOrigins__1=https://admin.example.com
ConnectionStrings__CentralDatabase=<connection-string-thật>
ConnectionStrings__TenantDatabaseTemplate=<connection-string-thật>
PrivateFileStorage__RootPath=/srv/contract-management/private-storage
TemplatePdfRendering__ExecutablePath=/usr/bin/libreoffice
CustomerOtp__Provider=<Http-hoặc-Smtp>
# Các CustomerOtp__... khác theo provider thật; không dùng Fake trong production.
```

Các dòng chứa dấu cách/ký tự đặc biệt trong `EnvironmentFile` phải được quote/escape theo cú pháp systemd; không dùng nguyên `<...>` làm giá trị thật. API production hiện từ chối khởi động nếu thiếu central/tenant connection strings, HTTPS CORS origin hoặc `AllowedHosts` cụ thể. `AllowedHosts` là hostname **không có scheme/port**.

Tạo `/etc/systemd/system/contract-api.service`:

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

Sau khi đã cấp secret, SQL và storage đúng:

```bash
sudo systemctl daemon-reload
sudo systemctl enable --now contract-api
systemctl status contract-api
journalctl -u contract-api -n 100 --no-pager
```

Chạy hai Next standalone bằng **đúng user deploy PM2**:

```bash
cd /srv/contract-management/frontend/current
HOSTNAME=127.0.0.1 PORT=3000 pm2 start server.js --name frontend-next
cd /srv/contract-management/admin/current
HOSTNAME=127.0.0.1 PORT=3001 pm2 start server.js --name system-admin-next
pm2 list
pm2 startup  # Chạy tiếp đúng lệnh sudo mà PM2 in ra cho user deploy này.
pm2 save
```

Không dùng `pm2 delete all` trên máy có ứng dụng khác. Sau khi đổi phiên bản Node, kiểm tra lại startup script PM2.

### 4.3. Nginx production một hostname cho Frontend + API

Chỉ cấu hình HTTPS khi đã có DNS và certificate hợp lệ cho **cả** `app.example.com` và `admin.example.com`; thay các đường dẫn certificate mẫu dưới đây bằng đường dẫn thật. Block redirect HTTP có thể thêm sau khi TLS hoạt động.

Đặt đoạn cấu hình dưới đây trong phạm vi `http` của Nginx (ví dụ tệp được include từ `http`). `map` phải ở ngoài các block `server`; nếu đã khai báo `$connection_upgrade` trong cấu hình chung thì chỉ giữ một khai báo.

```nginx
map $http_upgrade $connection_upgrade {
    default upgrade;
    '' close;
}

server {
    listen 80;
    server_name app.example.com admin.example.com;
    return 301 https://$host$request_uri;
}

server {
    listen 443 ssl;
    server_name app.example.com;
    ssl_certificate     /etc/letsencrypt/live/app.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/app.example.com/privkey.pem;
    client_max_body_size 50m;

    location ^~ /api/ {
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 120s;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_pass http://127.0.0.1:5005;
    }
    location ^~ /health/ {
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_pass http://127.0.0.1:5005;
    }
    location ~ ^/public/contracts/[^/]+/(shared|comments|discussion-hub(/negotiate)?|[^/]+/(availability|otp/(request|verify)))$ {
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection $connection_upgrade;
        proxy_read_timeout 120s;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_pass http://127.0.0.1:5005;
    }
    location / {
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_pass http://127.0.0.1:3000;
    }
}

server {
    listen 443 ssl;
    server_name admin.example.com;
    ssl_certificate     /etc/letsencrypt/live/admin.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/admin.example.com/privkey.pem;
    location / {
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_pass http://127.0.0.1:3001;
    }
}
```

Không thêm dấu `/` cuối `proxy_pass` của API hoặc regex public; không expose storage bằng `root`/`alias`. Tùy giới hạn file và thời gian render thật, điều chỉnh `client_max_body_size` và timeout có kiểm thử. Sau khi lưu: `sudo nginx -t` **trước** `sudo systemctl reload nginx`.

Kiểm tra trao đổi thời gian thực bằng hai cửa sổ: nhân viên mở trao đổi chung/điều khoản; khách hàng xác minh OTP và mở cùng trao đổi. Tạo chủ đề, trả lời và đóng/mở lại chủ đề từ nhân viên phải cập nhật phía còn lại mà không tải lại trang. Khi đang nhập câu trả lời, tin mới không được xóa nội dung đang nhập. Sau mất mạng/kết nối lại, danh sách tự đồng bộ. Cookie OTP hết hạn, liên kết bị thu hồi hoặc hợp đồng sang vòng mới phải bị từ chối đọc; hợp đồng/tenant khác không nhận sự kiện của hợp đồng này.

### 4.4. DB, nghiệm thu và vận hành

1. Trước migration: backup **central DB, toàn bộ tenant DB và private-storage cùng checkpoint**, kiểm tra khả năng restore; xác nhận migration nào chưa áp dụng và chạy theo quy trình riêng cho central/từng tenant. Không cho ứng dụng tự migrate production chỉ vì service khởi động. Không dùng DB production để thử lệnh chưa kiểm chứng.
2. Kiểm tra `systemctl status contract-api`, `pm2 list`, `nginx -t`, `https://app.example.com/health/live`, `/health/ready`, trang Frontend và SystemAdmin. `ready` phải phản ánh đúng DB/storage/PDF dependency.
3. Test đăng nhập hai ứng dụng, cookie `Secure`/`HttpOnly`, CORS từ `admin.example.com`, nghiệp vụ hợp đồng, public link + OTP, upload/download private file, render DOCX/PDF tiếng Việt và audit. Reboot VPS có kiểm soát rồi kiểm tra các process tự lên lại.
4. Bật log rotation/monitoring dung lượng storage, CPU/RAM, SQL, hạn TLS, OTP outbox; thử restore định kỳ. Giữ bản release trước để rollback app, nhưng rollback DB/storage cần checkpoint tương thích.

## 5. Bảng xử lý lỗi đã gặp

| Hiện tượng | Kiểm tra và hướng xử lý |
| --- | --- |
| `/api/...` hoặc `/health/live` trả **404 HTML**, có `X-Powered-By: Next.js` | Request đang tới Next thay vì API. Kiểm tra file Nginx **đang nạp**, cổng `8080` và các master Nginx cũ; dừng đúng các tiến trình test rồi chạy **một** master với `dtc_digitizingcontract.conf`. API trực tiếp `127.0.0.1:5005/health/live` phải `200`. |
| `/health/live` qua Nginx trả **400 Invalid Hostname**, API trực tiếp bằng `127.0.0.1` trả `200` | Nginx đã tới API, nhưng `AllowedHosts` chỉ có `localhost`. Giữ `Host` thật và đặt `AllowedHosts=app.app.test;localhost;127.0.0.1` cho local, `AllowedHosts=app.example.com` cho production; **restart API**. Không sửa Nginx để giả Host `localhost`. |
| **502 Bad Gateway** | Kiểm tra API/Next có lắng nghe đúng `127.0.0.1:5005/3000/3001`, log của service và Nginx. |
| **CORS/cookie lỗi ở SystemAdmin** | SystemAdmin build với URL API của `app`, API cho phép origin `admin` chính xác gồm scheme/port. Local HTTP test không chứng minh cookie `Secure` hoạt động trên HTTPS production. |
| **413 Request Entity Too Large** | Kiểm tra `client_max_body_size` ở Nginx và giới hạn upload API; không tăng vô hạn. |
| CSS/JS 404 sau build | Chưa copy `.next/static` vào `.next/standalone/.next/static`, hoặc đang chạy artifact cũ. Kiểm tra `public` tương tự. |
| DOCX có PDF lỗi font/`ready=503` | Kiểm tra `libreoffice`, fonts tiếng Việt, quyền storage và `TemplatePdfRendering__ExecutablePath`. |

## 6. Tài liệu kỹ thuật tham khảo

- [Next.js standalone output](https://nextjs.org/docs/app/api-reference/config/next-config-js/output), [biến môi trường public được nhúng lúc build](https://nextjs.org/docs/pages/guides/environment-variables).
- [Nginx `proxy_pass` và cách giữ URI](https://nginx.org/en/docs/http/ngx_http_proxy_module.html), [Nginx trên Windows](https://nginx.org/en/docs/windows.html).
- [ASP.NET Core Host Filtering/`AllowedHosts`](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/host-filtering?view=aspnetcore-10.0), [ASP.NET Core với Nginx/systemd](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/linux-nginx?view=aspnetcore-10.0).
- [PM2 startup và `pm2 save`](https://pm2.keymetrics.io/docs/usage/startup/).
