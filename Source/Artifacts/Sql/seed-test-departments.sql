/*
  DEV/TEST ONLY

  Tạo 10 phòng ban active trong tenant đã migrate đầy đủ.
  Script có thể chạy lại: DepartmentCode đã tồn tại sẽ được bỏ qua.

  Tên cột trong schema hiện tại là Stutus (không phải Status):
    1 = Active
    0 = Inactive
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Chọn đúng tenant database trong SSMS và điền lại tên bên dưới trước khi chạy.
DECLARE @ExpectedDatabase sysname = N'ContractManagement_Tenant_hungnd';
DECLARE @LangId int = 1;

IF DB_NAME() <> @ExpectedDatabase OR DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    THROW 50010, N'Chọn đúng tenant database và đặt @ExpectedDatabase trùng tên DB trước khi chạy.', 1;
END;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
    THROW 50011, N'Chưa có schema migration. Hãy provision tenant và chạy migration trước khi seed.', 1;
IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId = N'20261027025251_NodeRealtimeDelivery')
    THROW 50012, N'Tenant chưa có migration AcceptanceEvidenceSignDate. Hãy cập nhật schema trước khi seed.', 1;

DECLARE @Departments table
(
    DepartmentCode varchar(20) NOT NULL PRIMARY KEY,
    DepartmentName nvarchar(200) NOT NULL
);

INSERT INTO @Departments
(
    DepartmentCode,
    DepartmentName
)
VALUES
    ('BGD',      N'Ban Giám đốc'),
    ('KD',       N'Phòng Kinh doanh'),
    ('MKT',      N'Phòng Marketing'),
    ('HCNS',     N'Phòng Hành chính - Nhân sự'),
    ('KTTC',     N'Phòng Kế toán - Tài chính'),
    ('KTHUAT',   N'Phòng Kỹ thuật'),
    ('CNTT',     N'Phòng Công nghệ thông tin'),
    ('CSKH',     N'Phòng Chăm sóc khách hàng'),
    ('PHAPCHE',  N'Phòng Pháp chế'),
    ('QLDA',     N'Phòng Quản lý dự án');

DECLARE @Inserted table
(
    DepartmentID smallint NOT NULL,
    DepartmentCode varchar(20) NOT NULL
);

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.tbl_Department
    (
        DepartmentCode,
        DepartmentName,
        ModifiedDate,
        Stutus,
        LangId
    )
    OUTPUT
        inserted.DepartmentID,
        inserted.DepartmentCode
    INTO @Inserted
    (
        DepartmentID,
        DepartmentCode
    )
    SELECT
        source.DepartmentCode,
        source.DepartmentName,
        GETDATE(),
        1,
        @LangId
    FROM @Departments AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.tbl_Department AS existing WITH (UPDLOCK, HOLDLOCK)
        WHERE existing.DepartmentCode = source.DepartmentCode
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;

SELECT
    department.DepartmentID,
    department.DepartmentCode,
    department.DepartmentName,
    department.ModifiedDate,
    department.Stutus,
    department.LangId,
    CASE
        WHEN inserted.DepartmentID IS NULL THEN 'Already existed'
        ELSE 'Inserted'
    END AS ScriptResult
FROM dbo.tbl_Department AS department
INNER JOIN @Departments AS source
    ON source.DepartmentCode = department.DepartmentCode
LEFT JOIN @Inserted AS inserted
    ON inserted.DepartmentID = department.DepartmentID
ORDER BY department.DepartmentID;
