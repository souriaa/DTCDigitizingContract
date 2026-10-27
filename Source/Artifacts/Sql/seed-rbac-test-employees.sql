/*
  DEV/TEST ONLY

  Tạo một nhân viên active cho mỗi EmployeeType trong tenant đã migrate đầy đủ.
  Mật khẩu được sao chép nguyên giá trị hash từ tài khoản nguồn @SourceAdminAccount.

  EmployeeType:
    1 = Sale
    2 = Marketing
    3 = AdminOfficer
    4 = Technical
    5 = Accountant
    6 = Manager

  Script có thể chạy lại: tài khoản đã tồn tại sẽ không được insert lần nữa.
  Chỉ 6 account/code/role test khớp bên dưới được bỏ cờ bắt đổi mật khẩu.
  RowVersion không được insert vì đây là cột rowversion do SQL Server tự sinh.
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Chọn đúng tenant database trong SSMS và điền lại tên bên dưới trước khi chạy.
DECLARE @ExpectedDatabase sysname = N'ContractManagement_Tenant_hungnd';
DECLARE @SourceAdminAccount varchar(50) = 'admin';
DECLARE @SourceAdminId int;
DECLARE @SourcePassword varchar(255);
IF DB_NAME() <> @ExpectedDatabase OR DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 50000, N'Chọn đúng tenant database và đặt @ExpectedDatabase trùng tên DB trước khi chạy.', 1;
END;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 50004, N'Chưa có schema migration. Hãy provision tenant và chạy migration trước khi seed.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261027025251_NodeRealtimeDelivery')
    THROW 50005, N'Tenant chưa có migration AcceptanceEvidenceSignDate. Hãy cập nhật schema trước khi seed.', 1;

IF (
    SELECT COUNT_BIG(*)
    FROM dbo.tbl_Employee
    WHERE EmployeeAccount = @SourceAdminAccount
      AND EmployeeType = 6 AND Status = 1
) <> 1
BEGIN
    THROW 50001, N'Đặt @SourceAdminAccount thành tài khoản Manager active được provision cho tenant.', 1;
END;

SELECT
    @SourceAdminId = EmployeeId,
    @SourcePassword = EmployeePassword
FROM dbo.tbl_Employee
WHERE EmployeeAccount = @SourceAdminAccount AND EmployeeType = 6 AND Status = 1;

IF NULLIF(@SourcePassword, '') IS NULL
BEGIN
    THROW 50002, N'Tài khoản nguồn không có mật khẩu để sao chép.', 1;
END;

DECLARE @Roles table
(
    EmployeeType tinyint NOT NULL PRIMARY KEY,
    EmployeeCode varchar(30) NOT NULL,
    EmployeeAccount varchar(50) NOT NULL UNIQUE,
    EmployeeFullName nvarchar(100) NOT NULL,
    DepartmentCode varchar(20) NOT NULL
);

INSERT INTO @Roles
(
    EmployeeType,
    EmployeeCode,
    EmployeeAccount,
    EmployeeFullName,
    DepartmentCode
)
VALUES
    (1, 'RBAC-SALE',          'test.sale',          N'Test Kinh doanh', 'KD'),
    (2, 'RBAC-MARKETING',     'test.marketing',     N'Test Marketing', 'MKT'),
    (3, 'RBAC-ADMIN-OFFICER', 'test.adminofficer',  N'Test Quản trị', 'HCNS'),
    (4, 'RBAC-TECHNICAL',     'test.technical',     N'Test Kỹ thuật', 'KTHUAT'),
    (5, 'RBAC-ACCOUNTANT',    'test.accountant',    N'Test Kế toán', 'KTTC'),
    (6, 'RBAC-MANAGER',       'test.manager',       N'Test Quản lý', 'BGD');

IF EXISTS (SELECT 1 FROM @Roles AS role WHERE
    (SELECT COUNT_BIG(*) FROM dbo.tbl_Department AS department WHERE department.DepartmentCode = role.DepartmentCode AND department.Stutus = 1) <> 1)
    THROW 50006, N'Chạy seed-test-departments.sql trước; mỗi mã phòng ban phải có đúng một phòng ban active.', 1;

IF EXISTS (SELECT 1 FROM @Roles AS role INNER JOIN dbo.tbl_Employee AS employee
    ON employee.EmployeeCode = role.EmployeeCode WHERE employee.EmployeeAccount <> role.EmployeeAccount OR employee.EmployeeAccount IS NULL)
    THROW 50007, N'Mã nhân viên test đã thuộc tài khoản khác. Script không ghi đè tài khoản hiện có.', 1;

IF EXISTS (SELECT 1 FROM @Roles AS role INNER JOIN dbo.tbl_Employee AS employee
    ON employee.EmployeeAccount = role.EmployeeAccount GROUP BY role.EmployeeAccount HAVING COUNT_BIG(*) > 1)
    THROW 50008, N'Tài khoản test bị trùng trong DB. Script không tự chọn một bản ghi.', 1;

-- Không âm thầm dùng lại một account đã tồn tại nhưng mang role khác.
IF EXISTS
(
    SELECT 1
    FROM @Roles AS role
    INNER JOIN dbo.tbl_Employee AS employee
        ON employee.EmployeeAccount = role.EmployeeAccount
    WHERE ISNULL(employee.EmployeeType, 0) <> role.EmployeeType
)
BEGIN
    THROW 50003, N'Có tài khoản test đã tồn tại nhưng EmployeeType không khớp.', 1;
END;

DECLARE @Inserted table
(
    EmployeeId int NOT NULL,
    EmployeeAccount varchar(50) NOT NULL,
    EmployeeType tinyint NOT NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.tbl_Employee
    (
        EmployeeCode,
        EmployeeAccount,
        EmployeePassword,
        EmployeeFullName,
        UserCreated,
        DateCreated,
        HireDate,
        Status,
        DepartmentId,
        EmployeeType,
        DefaultPage,
        MustChangePassword,
        PasswordChangedAt,
        SessionVersion,
        Others
    )
    OUTPUT
        inserted.EmployeeId,
        inserted.EmployeeAccount,
        inserted.EmployeeType
    INTO @Inserted
    (
        EmployeeId,
        EmployeeAccount,
        EmployeeType
    )
    SELECT
        role.EmployeeCode,
        role.EmployeeAccount,
        @SourcePassword,
        role.EmployeeFullName,
        @SourceAdminId,
        GETUTCDATE(),
        GETUTCDATE(),
        1,
        department.DepartmentID,
        role.EmployeeType,
        '/contracts',
        0,
        SYSUTCDATETIME(),
        1,
        N'Tài khoản test RBAC; mật khẩu sao chép từ ' + @SourceAdminAccount
    FROM @Roles AS role
    INNER JOIN dbo.tbl_Department AS department ON department.DepartmentCode = role.DepartmentCode AND department.Stutus = 1
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.tbl_Employee AS employee WITH (UPDLOCK, HOLDLOCK)
        WHERE employee.EmployeeAccount = role.EmployeeAccount
    );

    -- DEV/TEST ONLY: áp dụng cả tài khoản test đã seed trước đó; không đổi hash.
    -- Tăng session version để phiên cũ còn cờ bắt đổi mật khẩu phải đăng nhập lại.
    UPDATE employee
    SET MustChangePassword = 0,
        SessionVersion = employee.SessionVersion + 1,
        UserModified = @SourceAdminId,
        DateModified = GETUTCDATE()
    FROM dbo.tbl_Employee AS employee
    INNER JOIN @Roles AS role ON employee.EmployeeAccount = role.EmployeeAccount
        AND employee.EmployeeCode = role.EmployeeCode
        AND employee.EmployeeType = role.EmployeeType
    WHERE employee.MustChangePassword = 1;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT
    employee.EmployeeId,
    employee.EmployeeCode,
    employee.EmployeeAccount,
    employee.EmployeeFullName,
    employee.EmployeeType,
    CASE employee.EmployeeType
        WHEN 1 THEN 'Sale'
        WHEN 2 THEN 'Marketing'
        WHEN 3 THEN 'AdminOfficer'
        WHEN 4 THEN 'Technical'
        WHEN 5 THEN 'Accountant'
        WHEN 6 THEN 'Manager'
        ELSE 'Unknown'
    END AS EmployeeTypeName,
    employee.Status,
    employee.DepartmentId,
    employee.DefaultPage,
    employee.MustChangePassword,
    employee.SessionVersion,
    employee.RowVersion,
    CASE WHEN inserted.EmployeeId IS NULL THEN 'Already existed' ELSE 'Inserted' END AS ScriptResult
FROM dbo.tbl_Employee AS employee
INNER JOIN @Roles AS role
    ON role.EmployeeAccount = employee.EmployeeAccount
LEFT JOIN @Inserted AS inserted
    ON inserted.EmployeeId = employee.EmployeeId
ORDER BY employee.EmployeeType;
