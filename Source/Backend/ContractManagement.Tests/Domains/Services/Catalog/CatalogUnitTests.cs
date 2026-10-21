using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace ContractManagement.Tests.Domains.Services.Catalog;

public sealed class CatalogUnitTests
{
    private static async Task<DbDtctechContext> SeedAsync()
    {
        var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.TblCategories.Add(new() { CategoryId = 1, CategoryName = "Danh mục" });
        db.TblServiceTypes.Add(new() { ServiceTypeId = 1, ServiceTypeName = "Loại dịch vụ" });
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task CatalogUnits_TrimPersistAndProjectThroughDetailListAndWorkspace()
    {
        await using var db = await SeedAsync();
        var products = new ProductService(db);
        var services = new ServiceService(db);
        var product = await products.CreateAsync(new() { ProductName = "Phần mềm", ProductNameEn = "Software",
            CategoryId = 1, UnitName = " Giấy phép ", UnitNameEn = " License " });
        var service = await services.CreateAsync(new() { ServiceName = "Hỗ trợ", ServiceNameEn = "Support",
            ServiceTypeId = 1, UnitName = " Tháng ", UnitNameEn = " Month " }, 9);
        Assert.Equal("Giấy phép", product.UnitName); Assert.Equal("License", product.UnitNameEn);
        Assert.Equal("Tháng", service.UnitName); Assert.Equal("Month", service.UnitNameEn);
        await products.UpdateAsync(product.ProductId, new() { ProductName = "Phần mềm", ProductNameEn = "Software",
            CategoryId = 1, UnitName = " Gói ", UnitNameEn = " Package " });
        await services.UpdateAsync(service.ServiceId, new() { ServiceName = "Hỗ trợ", ServiceNameEn = "Support",
            ServiceTypeId = 1, UnitName = " Năm ", UnitNameEn = " Year " }, 9);
        db.ChangeTracker.Clear();
        Assert.Equal("Gói", (await products.GetByIdAsync(product.ProductId)).UnitName);
        Assert.Equal("Package", Assert.Single((await products.GetListAsync(new())).Items).UnitNameEn);
        Assert.Equal("Năm", (await services.GetByIdAsync(service.ServiceId)).UnitName);
        Assert.Equal("Year", Assert.Single((await services.GetListAsync(new())).Items).UnitNameEn);
        var workspace = new CatalogWorkspaceService(db);
        var p = Assert.Single((await workspace.GetProductsAsync(new())).Items);
        var s = Assert.Single((await workspace.GetServicesAsync(new())).Items);
        Assert.Equal("Gói", p.UnitName); Assert.Equal("Package", p.UnitNameEn);
        Assert.Equal("Năm", s.UnitName); Assert.Equal("Year", s.UnitNameEn);
    }

    public static IEnumerable<object[]> InvalidUnits()
    {
        foreach (var product in new[] { true, false })
        foreach (var update in new[] { true, false })
        foreach (var english in new[] { true, false })
        foreach (var value in new[] { null, " ", new string('x', 101) })
            yield return [product, update, english, value!];
    }

    [Theory, MemberData(nameof(InvalidUnits))]
    public async Task CatalogUnits_InvalidInputRejectedByDtoAndServiceWithoutMutation(bool product, bool update, bool english, string? value)
    {
        await using var db = await SeedAsync();
        db.TblProducts.Add(new() { ProductId = 1, ProductName = "Gốc", ProductNameEn = "Original", CategoryId = 1,
            UnitName = "Giấy phép", UnitNameEn = "License", Status = 1 });
        db.TblServices.Add(new() { ServiceId = 1, ServiceName = "Gốc", ServiceNameEn = "Original", ServiceTypeId = 1,
            UnitName = "Giấy phép", UnitNameEn = "License", Status = 1 });
        await db.SaveChangesAsync();
        object request = (product, update) switch
        {
            (true, false) => new CreateProductRequest { ProductName = "Mới", ProductNameEn = "New", CategoryId = 1 },
            (true, true) => new UpdateProductRequest { ProductName = "Mới", ProductNameEn = "New", CategoryId = 1 },
            (false, false) => new CreateServiceRequest { ServiceName = "Mới", ServiceNameEn = "New", ServiceTypeId = 1 },
            _ => new UpdateServiceRequest { ServiceName = "Mới", ServiceNameEn = "New", ServiceTypeId = 1 }
        };
        request.GetType().GetProperty("UnitName")!.SetValue(request, english ? "Gói" : value);
        request.GetType().GetProperty("UnitNameEn")!.SetValue(request, english ? value : "Package");
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(request, new ValidationContext(request), errors, true));
        Assert.Contains(errors, e => e.MemberNames.Contains(english ? "UnitNameEn" : "UnitName"));
        await Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            switch (request)
            {
                case CreateProductRequest r: await new ProductService(db).CreateAsync(r); break;
                case UpdateProductRequest r: await new ProductService(db).UpdateAsync(1, r); break;
                case CreateServiceRequest r: await new ServiceService(db).CreateAsync(r, 9); break;
                case UpdateServiceRequest r: await new ServiceService(db).UpdateAsync(1, r, 9); break;
            }
        });
        var p = Assert.Single(db.TblProducts); var s = Assert.Single(db.TblServices);
        Assert.Equal("Gốc", p.ProductName); Assert.Equal("Giấy phép", p.UnitName); Assert.Equal("License", p.UnitNameEn);
        Assert.Equal("Gốc", s.ServiceName); Assert.Equal("Giấy phép", s.UnitName); Assert.Equal("License", s.UnitNameEn);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified);
    }

    [Fact]
    public void CatalogUnitMigration_OnlyAddsFourNullableUnicodeColumnsWithoutDefaultsOrDataChanges()
    {
        using var db = new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Trusted_Connection=True;TrustServerCertificate=True").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations.Single(x => x.Key.EndsWith("_CatalogItemUnits")).Value, db.Database.ProviderName!);
        Assert.Equal(4, migration.UpOperations.Count);
        var pairs = new HashSet<string>();
        foreach (var operation in migration.UpOperations)
        {
            var column = Assert.IsType<AddColumnOperation>(operation);
            Assert.True(column.IsNullable); Assert.Equal("nvarchar(100)", column.ColumnType);
            Assert.Equal(100, column.MaxLength); Assert.Null(column.DefaultValue); Assert.Null(column.DefaultValueSql);
            pairs.Add($"{column.Table}.{column.Name}");
        }
        Assert.Equal(4, pairs.Count);
        foreach (var table in new[] { "tbl_Products", "tbl_Services" })
        foreach (var column in new[] { "UnitName", "UnitNameEn" }) Assert.Contains($"{table}.{column}", pairs);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
