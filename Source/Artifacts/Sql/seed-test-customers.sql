/*
  DEV/TEST ONLY — dữ liệu mẫu, không xác nhận thông tin pháp lý/ngân hàng thực tế.
  Giữ bộ 25 khách hàng; không giữ ID, mật khẩu MD5 hoặc ID tham chiếu từ DB cũ.
  CustomerAccount chỉ là khóa seed ổn định để chạy lại; không tạo tài khoản đăng nhập.
  CustomerCode cấp theo identity như BE: CUST-{CustomerId:D6}.
  Không ghi đè khách hàng đã tồn tại. Không tạo contract/template/snapshot/file metadata.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Chọn đúng tenant database trong SSMS và điền lại tên bên dưới trước khi chạy.
DECLARE @ExpectedDatabase sysname = N'ContractManagement_Tenant_hungnd';
DECLARE @SourceAdminAccount varchar(50) = 'admin';
DECLARE @CreatedByEmployeeId int;

IF DB_NAME() <> @ExpectedDatabase OR DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
    THROW 50030, N'Chọn đúng tenant database và đặt @ExpectedDatabase trùng tên DB trước khi chạy.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 50031, N'Chưa có schema migration. Hãy provision tenant và chạy migration trước khi seed.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261027025251_NodeRealtimeDelivery')
    THROW 50032, N'Tenant chưa có migration AcceptanceEvidenceSignDate. Hãy cập nhật schema trước khi seed.', 1;
IF (SELECT COUNT_BIG(*) FROM dbo.tbl_Employee WHERE EmployeeAccount = @SourceAdminAccount AND EmployeeType = 6 AND Status = 1) <> 1
    THROW 50033, N'Đặt @SourceAdminAccount thành tài khoản Manager active được provision cho tenant.', 1;
SELECT @CreatedByEmployeeId = EmployeeId FROM dbo.tbl_Employee WHERE EmployeeAccount = @SourceAdminAccount AND EmployeeType = 6 AND Status = 1;

DECLARE @CustomerSeed table
(
    CustomerAccount varchar(30) NOT NULL PRIMARY KEY,
    CustomerFullName nvarchar(100) NOT NULL,
    CustomerCompany nvarchar(1000) NOT NULL,
    CustomerAddress nvarchar(500) NOT NULL,
    CustomerEmail varchar(50) NULL,
    CustomerMobile varchar(15) NULL,
    CustomerPhone varchar(15) NULL,
    CustomerFaxNumber varchar(15) NULL,
    CustomerTaxCode varchar(30) NULL,
    CustomerCity nvarchar(100) NULL,
    CustomerZipCode varchar(15) NULL,
    CustomerCountry nvarchar(200) NULL,
    Status tinyint NOT NULL,
    CustomerWebsite nvarchar(500) NULL,
    ParentAccount varchar(30) NULL,
    CustomerRepresentativeName nvarchar(200) NOT NULL,
    CustomerRepresentativeTitle nvarchar(200) NOT NULL,
    CustomerBankAccountNumber varchar(100) NULL,
    CustomerBankName nvarchar(500) NULL,
    CustomerContactPersonName nvarchar(200) NOT NULL,
    CustomerContactPersonPhone varchar(20) NULL,
    CustomerContactPersonTitle nvarchar(200) NOT NULL,
    CustomerCompanyEn nvarchar(500) NULL,
    CustomerAddressEn nvarchar(500) NULL,
    CustomerBankNameEn nvarchar(500) NULL,
    CustomerCityEn nvarchar(100) NULL,
    CustomerCountryEn nvarchar(100) NULL,
    CustomerContactPersonTitleEn nvarchar(200) NULL,
    CustomerRepresentativeTitleEn nvarchar(200) NULL
);

INSERT INTO @CustomerSeed
(
    CustomerAccount,
    CustomerFullName,
    CustomerCompany,
    CustomerAddress,
    CustomerEmail,
    CustomerMobile,
    CustomerPhone,
    CustomerFaxNumber,
    CustomerTaxCode,
    CustomerCity,
    CustomerZipCode,
    CustomerCountry,
    Status,
    CustomerWebsite,
    ParentAccount,
    CustomerRepresentativeName,
    CustomerRepresentativeTitle,
    CustomerBankAccountNumber,
    CustomerBankName,
    CustomerContactPersonName,
    CustomerContactPersonPhone,
    CustomerContactPersonTitle
)
VALUES
    (N'kh_fpt', N'Nguyễn Hoàng Dũng', N'CTCP Viễn thông FPT', N'17 Duy Tân, Cầu Giấy', N'dungnh@fpt.com.vn', N'0981234001', N'02473002222', NULL, N'0101234567', N'Hà Nội', N'100000', N'VN', 1, N'fpt.vn', NULL, N'Nguyễn Hoàng Dũng', N'Giám đốc', '000000000001', N'Ngân hàng mẫu (dữ liệu test)', N'Nguyễn Hoàng Dũng', N'0981234001', N'Điều phối dự án'),
    (N'kh_vng', N'Lê Thị Mai', N'Công ty Cổ phần VNG', N'Z06 Đường số 13, Tân Thuận Đông, Quận 7', N'mailt@vng.com.vn', N'0981234002', N'02839623000', NULL, N'0303123456', N'TP.HCM', N'700000', N'VN', 1, N'vng.com.vn', NULL, N'Lê Thị Mai', N'Giám đốc', '000000000002', N'Ngân hàng mẫu (dữ liệu test)', N'Lê Thị Mai', N'0981234002', N'Điều phối dự án'),
    (N'kh_vin', N'Phạm Văn Tuấn', N'Tập đoàn Vingroup', N'7 Bằng Lăng 1, Vinhomes Riverside', N'tuanpv@vingroup.net', N'0981234003', N'02439749999', NULL, N'0101245486', N'Hà Nội', N'100000', N'VN', 1, N'vingroup.net', NULL, N'Phạm Văn Tuấn', N'Giám đốc', '000000000003', N'Ngân hàng mẫu (dữ liệu test)', N'Phạm Văn Tuấn', N'0981234003', N'Điều phối dự án'),
    (N'kh_mwg', N'Trần Minh Đức', N'CTCP Đầu tư Thế Giới Di Động', N'128 Trần Quang Khải, Quận 1', N'ductm@mwg.vn', N'0981234004', N'02838125960', NULL, N'0303217354', N'TP.HCM', N'700000', N'VN', 1, N'mwg.vn', NULL, N'Trần Minh Đức', N'Giám đốc', '000000000004', N'Ngân hàng mẫu (dữ liệu test)', N'Trần Minh Đức', N'0981234004', N'Điều phối dự án'),
    (N'kh_th', N'Hoàng Thu Trang', N'Tập đoàn TH True Milk', N'166 Thái Hà, Đống Đa', N'tranght@thmilk.vn', N'0981234005', N'02435739777', NULL, N'0101345678', N'Hà Nội', N'100000', N'VN', 1, N'thmilk.vn', NULL, N'Hoàng Thu Trang', N'Giám đốc', '000000000005', N'Ngân hàng mẫu (dữ liệu test)', N'Hoàng Thu Trang', N'0981234005', N'Điều phối dự án'),
    (N'kh_bidv', N'Đỗ Quang Huy', N'Ngân hàng TMCP BIDV', N'194 Trần Quang Khải, Hoàn Kiếm', N'huydq@bidv.com.vn', N'0981234006', N'02422200588', NULL, N'0100150619', N'Hà Nội', N'100000', N'VN', 1, N'bidv.com.vn', NULL, N'Hoàng Nhật Vy', N'Người Đi Làm', '000000000006', N'Ngân hàng mẫu (dữ liệu test)', N'Đỗ Quang Huy', N'0981234006', N'Điều phối dự án'),
    (N'kh_viettel', N'Võ Trung Kiên', N'Tập đoàn Viettel', N'1 Trần Hữu Dực, Nam Từ Liêm', N'kienvt@viettel.com.vn', N'0981234007', N'02462556789', NULL, N'0100109106', N'Hà Nội', N'100000', N'VN', 1, N'viettel.vn', NULL, N'Võ Trung Kiên', N'Giám đốc', '000000000007', N'Ngân hàng mẫu (dữ liệu test)', N'Võ Trung Kiên', N'0981234007', N'Điều phối dự án'),
    (N'kh_shopee', N'Nguyễn Bích Ngọc', N'Công ty TNHH Shopee', N'Saigon Centre, 65 Lê Lợi, Quận 1', N'ngocnb@shopee.vn', N'0981234008', N'19001221', NULL, N'0314920827', N'TP.HCM', N'700000', N'VN', 1, N'shopee.vn', NULL, N'Nguyễn Bích Ngọc', N'Giám đốc', '000000000008', N'Ngân hàng mẫu (dữ liệu test)', N'Nguyễn Bích Ngọc', N'0981234008', N'Điều phối dự án'),
    (N'kh_misa', N'Đặng Thanh Bình', N'CTCP MISA', N'Tòa nhà Technosoft, Cầu Giấy', N'binhdt@misa.com.vn', N'0981234009', N'02437959595', NULL, N'0101233890', N'Hà Nội', N'100000', N'VN', 1, N'misa.vn', NULL, N'Đặng Thanh Bình', N'Giám đốc', '000000000009', N'Ngân hàng mẫu (dữ liệu test)', N'Đặng Thanh Bình', N'0981234009', N'Điều phối dự án'),
    (N'kh_hpg', N'Trần Tuấn Anh', N'Tập đoàn Hòa Phát', N'66 Nguyễn Du, Hai Bà Trưng', N'anhtt@hoaphat.com.vn', N'0981234010', N'02439747799', N'02439747798', N'0101416738', N'Hà Nội', N'100000', N'VN', 1, N'hoaphat.com.vn', NULL, N'Trần Tuấn Anh', N'Giám đốc', '000000000010', N'Ngân hàng mẫu (dữ liệu test)', N'Trần Tuấn Anh', N'0981234010', N'Điều phối dự án'),
    (N'kh_sun', N'Lê Bích Loan', N'Sun Group', N'Tầng 9, Tòa nhà Sun City, Hoàn Kiếm', N'loanlb@sungroup.com.vn', N'0981234011', N'02439364379', NULL, N'0400508930', N'Hà Nội', N'100000', N'VN', 1, N'sungroup.com.vn', NULL, N'Lê Bích Loan', N'Giám đốc', '000000000011', N'Ngân hàng mẫu (dữ liệu test)', N'Lê Bích Loan', N'0981234011', N'Điều phối dự án'),
    (N'kh_pnj', N'Bùi Phương Thảo', N'CTCP Vàng Bạc Đá Quý PNJ', N'170E Phan Đăng Lưu, Phú Nhuận', N'thaobp@pnj.com.vn', N'0981234012', N'02839951703', NULL, N'0300521758', N'TP.HCM', N'700000', N'VN', 1, N'pnj.com.vn', NULL, N'Bùi Phương Thảo', N'Giám đốc', '000000000012', N'Ngân hàng mẫu (dữ liệu test)', N'Bùi Phương Thảo', N'0981234012', N'Điều phối dự án'),
    (N'kh_vj', N'Nguyễn Thanh Sơn', N'Vietjet Air', N'302 Kim Mã, Ba Đình', N'sonnt@vietjetair.com', N'0981234013', N'19001886', NULL, N'0102325399', N'Hà Nội', N'100000', N'VN', 1, N'vietjetair.com', NULL, N'Nguyễn Thanh Sơn', N'Giám đốc', '000000000013', N'Ngân hàng mẫu (dữ liệu test)', N'Nguyễn Thanh Sơn', N'0981234013', N'Điều phối dự án'),
    (N'kh_vinamilk', N'Mai Trọng Kiên', N'CTCP Sữa Việt Nam Vinamilk', N'10 Tân Trào, Quận 7', N'kienmt@vinamilk.com.vn', N'0981234014', N'02854155555', NULL, N'0300588569', N'TP.HCM', N'700000', N'VN', 1, N'vinamilk.com.vn', NULL, N'Mai Trọng Kiên', N'Giám đốc', '000000000014', N'Ngân hàng mẫu (dữ liệu test)', N'Mai Trọng Kiên', N'0981234014', N'Điều phối dự án'),
    (N'kh_tcb', N'Lý Mỹ Châu', N'Ngân hàng Techcombank', N'6 Quang Trung, Hoàn Kiếm', N'chaulm@techcombank.com.vn', N'0981234015', N'02439446368', NULL, N'0100230800', N'Hà Nội', N'100000', N'VN', 1, N'techcombank.com.vn', NULL, N'Lý Mỹ Châu', N'Giám đốc', '000000000015', N'Ngân hàng mẫu (dữ liệu test)', N'Lý Mỹ Châu', N'0981234015', N'Điều phối dự án'),
    (N'kh_savills', N'Phan Anh Tú', N'Savills Việt Nam', N'Lotte Center, 54 Liễu Giai', N'tupa@savills.com.vn', N'0981234016', N'02439461300', NULL, N'0102636531', N'Hà Nội', N'100000', N'VN', 1, N'savills.com.vn', NULL, N'Phan Anh Tú', N'Giám đốc', '000000000016', N'Ngân hàng mẫu (dữ liệu test)', N'Phan Anh Tú', N'0981234016', N'Điều phối dự án'),
    (N'kh_tiki', N'Vũ Kim Ngọc', N'Tiki Corporation', N'52 Út Tịch, Tân Bình', N'ngocvk@tiki.vn', N'0981234017', N'19006035', NULL, N'0309532909', N'TP.HCM', N'700000', N'VN', 1, N'tiki.vn', NULL, N'Vũ Kim Ngọc', N'Giám đốc', '000000000017', N'Ngân hàng mẫu (dữ liệu test)', N'Vũ Kim Ngọc', N'0981234017', N'Điều phối dự án'),
    (N'kh_flc', N'Trịnh Quang Thái', N'Tập đoàn FLC', N'265 Cầu Giấy, Hà Nội', N'thaitq@flc.vn', N'0981234018', N'02437711111', NULL, N'0102683813', N'Hà Nội', N'100000', N'VN', 0, N'flc.vn', NULL, N'Trịnh Quang Thái', N'Giám đốc', '000000000018', N'Ngân hàng mẫu (dữ liệu test)', N'Trịnh Quang Thái', N'0981234018', N'Điều phối dự án'),
    (N'kh_fsoft', N'Nguyễn Hồng Sơn', N'FPT Software', N'Khu CNC Hòa Lạc, Thạch Thất', N'sonnh@fsoft.com.vn', N'0981234019', N'02437689048', NULL, N'0100412856', N'Hà Nội', N'100000', N'VN', 1, N'fpt-software.com', N'kh_fpt', N'Nguyễn Hồng Sơn', N'Giám đốc', '000000000019', N'Ngân hàng mẫu (dữ liệu test)', N'Nguyễn Hồng Sơn', N'0981234019', N'Điều phối dự án'),
    (N'kh_giao', N'Chu Minh Châu', N'Giao Hàng Tiết Kiệm', N'8 Phạm Hùng, Nam Từ Liêm', N'chaucm@ghtk.vn', N'0981234020', N'19006092', NULL, N'0106181807', N'Hà Nội', N'100000', N'VN', 1, N'ghtk.vn', NULL, N'Chu Minh Châu', N'Giám đốc', '000000000020', N'Ngân hàng mẫu (dữ liệu test)', N'Chu Minh Châu', N'0981234020', N'Điều phối dự án'),
    (N'kh_baoviet', N'Đinh Hữu Lộc', N'Tập đoàn Bảo Việt', N'8 Lê Thái Tổ, Hoàn Kiếm', N'locdh@baoviet.com.vn', N'0981234021', N'02439289999', NULL, N'0100111761', N'Hà Nội', N'100000', N'VN', 1, N'baoviet.com.vn', NULL, N'Đinh Hữu Lộc', N'Giám đốc', '000000000021', N'Ngân hàng mẫu (dữ liệu test)', N'Đinh Hữu Lộc', N'0981234021', N'Điều phối dự án'),
    (N'kh_msc', N'Trần Yến Nhi', N'Masan Consumer', N'Tòa nhà MPlaza, 39 Lê Duẩn', N'nhity@masan.com.vn', N'0981234022', N'02862555660', NULL, N'0302017440', N'TP.HCM', N'700000', N'VN', 1, N'masanconsumer.com', NULL, N'Trần Yến Nhi', N'Giám đốc', '000000000022', N'Ngân hàng mẫu (dữ liệu test)', N'Trần Yến Nhi', N'0981234022', N'Điều phối dự án'),
    (N'kh_novaland', N'Đoàn Văn Nam', N'Tập đoàn Novaland', N'65 Nguyễn Du, Quận 1', N'namdv@novaland.com.vn', N'0981234023', N'1900636666', NULL, N'0301449636', N'TP.HCM', N'700000', N'VN', 1, N'novaland.com.vn', NULL, N'Đoàn Văn Nam', N'Giám đốc', '000000000023', N'Ngân hàng mẫu (dữ liệu test)', N'Đoàn Văn Nam', N'0981234023', N'Điều phối dự án'),
    (N'kh_vnpay', N'Lê Quốc Hưng', N'CTCP Giải pháp Thanh toán Việt Nam (VNPAY)', N'Tòa nhà VNPAY, 22 Láng Hạ', N'hunglq@vnpay.vn', N'0981234024', N'1900555577', NULL, N'0102182292', N'Hà Nội', N'100000', N'VN', 1, N'vnpay.vn', NULL, N'Lê Quốc Hưng', N'Giám đốc', '000000000024', N'Ngân hàng mẫu (dữ liệu test)', N'Lê Quốc Hưng', N'0981234024', N'Điều phối dự án'),
    (N'kh_cgv', N'Hồ Thu Hà', N'CJ CGV Việt Nam', N'Tầng 3, Rivera Park Sài Gòn, Quận 10', N'hungphuoc2004@gmail.com', N'(+84) 981234025', N'19006017', N'9028408122', N'0303648411', N'TP.HCM', N'700000', N'VN', 1, N'cgv.vn', NULL, N'Hoàng Nhật Vy', N'Giám đốc', N'983102412321', N'MBBank', N'Nguyễn Đức Hùng', N'0987654321', N'Giám đốc');

-- Hai mẫu có EN đầy đủ để thử render song ngữ; các mẫu khác cố ý để EN NULL.
-- Đây là bản dịch mẫu phục vụ test, không phải tên đăng ký pháp lý được xác minh.
UPDATE @CustomerSeed SET CustomerCompanyEn = N'FPT Telecommunications Joint Stock Company',
    CustomerAddressEn = N'17 Duy Tan Street, Cau Giay, Hanoi', CustomerBankNameEn = N'Sample Bank (test data)',
    CustomerCityEn = N'Hanoi', CustomerCountryEn = N'Vietnam', CustomerContactPersonTitleEn = N'Project Coordinator',
    CustomerRepresentativeTitleEn = N'Director' WHERE CustomerAccount = 'kh_fpt';
UPDATE @CustomerSeed SET CustomerCompanyEn = N'CJ CGV Vietnam',
    CustomerAddressEn = N'Floor 3, Rivera Park Saigon, District 10', CustomerBankNameEn = N'MB Bank',
    CustomerCityEn = N'Ho Chi Minh City', CustomerCountryEn = N'Vietnam', CustomerContactPersonTitleEn = N'Director',
    CustomerRepresentativeTitleEn = N'Director' WHERE CustomerAccount = 'kh_cgv';

DECLARE @Inserted table (CustomerId int NOT NULL PRIMARY KEY, CustomerAccount varchar(30) NOT NULL);
BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM @CustomerSeed AS source JOIN dbo.tbl_Customers AS customer WITH (UPDLOCK, HOLDLOCK)
        ON customer.CustomerAccount = source.CustomerAccount GROUP BY source.CustomerAccount HAVING COUNT_BIG(*) > 1)
        THROW 50034, N'Khóa seed khách hàng bị trùng trong DB. Script không tự chọn bản ghi.', 1;

    IF EXISTS (SELECT 1 FROM @CustomerSeed AS source JOIN dbo.tbl_Customers AS customer
        ON customer.CustomerAccount = source.CustomerAccount
        WHERE customer.CustomerCompany IS NULL OR customer.CustomerCompany <> source.CustomerCompany)
        THROW 50037, N'Khóa seed khách hàng đã thuộc tổ chức khác. Script không ghi đè hoặc gắn lại quan hệ.', 1;

    INSERT INTO dbo.tbl_Customers
    (
        CustomerAccount, CustomerFullName, CustomerCompany, CustomerAddress, CustomerEmail,
        CustomerMobile, CustomerPhone, CustomerFaxNumber, CustomerTaxCode, CustomerCity,
        CustomerZipCode, CustomerCountry, Status, CustomerWebsite,
        CustomerRepresentativeName, CustomerRepresentativeTitle, CustomerBankAccountNumber,
        CustomerBankName, CustomerContactPersonName, CustomerContactPersonPhone, CustomerContactPersonTitle,
        CustomerCompanyEn, CustomerAddressEn, CustomerBankNameEn, CustomerCityEn,
        CustomerCountryEn, CustomerContactPersonTitleEn, CustomerRepresentativeTitleEn,
        UserCreated, DateCreated, CustomerNotes
    )
    OUTPUT inserted.CustomerId, inserted.CustomerAccount INTO @Inserted (CustomerId, CustomerAccount)
    SELECT
        source.CustomerAccount, source.CustomerFullName, source.CustomerCompany, source.CustomerAddress, source.CustomerEmail,
        source.CustomerMobile, source.CustomerPhone, source.CustomerFaxNumber, source.CustomerTaxCode, source.CustomerCity,
        source.CustomerZipCode, source.CustomerCountry, source.Status, source.CustomerWebsite,
        source.CustomerRepresentativeName, source.CustomerRepresentativeTitle, source.CustomerBankAccountNumber,
        source.CustomerBankName, source.CustomerContactPersonName, source.CustomerContactPersonPhone, source.CustomerContactPersonTitle,
        source.CustomerCompanyEn, source.CustomerAddressEn, source.CustomerBankNameEn, source.CustomerCityEn,
        source.CustomerCountryEn, source.CustomerContactPersonTitleEn, source.CustomerRepresentativeTitleEn,
        @CreatedByEmployeeId, GETDATE(), N'Dữ liệu test; thông tin pháp lý/ngân hàng cần thay bằng dữ liệu thật trước khi sử dụng.'
    FROM @CustomerSeed AS source
    WHERE NOT EXISTS (SELECT 1 FROM dbo.tbl_Customers AS customer WITH (UPDLOCK, HOLDLOCK)
        WHERE customer.CustomerAccount = source.CustomerAccount);

    -- Chỉ cấp mã cho những hàng vừa tạo; không đổi mã/snapshot của dữ liệu đã tồn tại.
    IF EXISTS (SELECT 1 FROM dbo.tbl_Customers AS customer JOIN @Inserted AS added ON added.CustomerId = customer.CustomerId
        JOIN dbo.tbl_Customers AS other ON other.CustomerCode = N'CUST-' + REPLICATE('0', CASE WHEN LEN(CONVERT(varchar(11), customer.CustomerId)) < 6 THEN 6 - LEN(CONVERT(varchar(11), customer.CustomerId)) ELSE 0 END) + CONVERT(varchar(11), customer.CustomerId) AND other.CustomerId <> customer.CustomerId)
        THROW 50035, N'Mã CUST theo identity đã thuộc khách hàng khác. Toàn bộ lần seed này được rollback.', 1;
    UPDATE customer SET CustomerCode = N'CUST-' + REPLICATE('0', CASE WHEN LEN(CONVERT(varchar(11), customer.CustomerId)) < 6 THEN 6 - LEN(CONVERT(varchar(11), customer.CustomerId)) ELSE 0 END) + CONVERT(varchar(11), customer.CustomerId)
    FROM dbo.tbl_Customers AS customer JOIN @Inserted AS added ON added.CustomerId = customer.CustomerId;

    IF EXISTS (SELECT 1 FROM @CustomerSeed AS source
        WHERE source.ParentAccount IS NOT NULL
            AND (SELECT COUNT_BIG(*) FROM dbo.tbl_Customers AS parent WHERE parent.CustomerAccount = source.ParentAccount) <> 1)
        THROW 50036, N'Không tìm thấy duy nhất khách hàng cha theo khóa seed.', 1;
    UPDATE customer SET CustomerParentId = parent.CustomerId
    FROM dbo.tbl_Customers AS customer
    JOIN @Inserted AS added ON added.CustomerId = customer.CustomerId
    JOIN @CustomerSeed AS source ON source.CustomerAccount = added.CustomerAccount
    JOIN dbo.tbl_Customers AS parent ON parent.CustomerAccount = source.ParentAccount;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT customer.CustomerId, customer.CustomerCode, customer.CustomerFullName,
    customer.CustomerCompany, customer.CustomerCompanyEn, customer.CustomerParentId,
    customer.CustomerRepresentativeName, customer.CustomerContactPersonName, customer.Status,
    CASE WHEN added.CustomerId IS NULL THEN 'Already existed' ELSE 'Inserted' END AS ScriptResult
FROM dbo.tbl_Customers AS customer
JOIN @CustomerSeed AS source ON source.CustomerAccount = customer.CustomerAccount
LEFT JOIN @Inserted AS added ON added.CustomerId = customer.CustomerId
ORDER BY customer.CustomerId;
