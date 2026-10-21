using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Application;

public partial class DbDtctechContext
{
    private static void ConfigureCatalogUnits(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TblProduct>().Property(x => x.UnitName).HasMaxLength(100).IsUnicode();
        modelBuilder.Entity<TblProduct>().Property(x => x.UnitNameEn).HasMaxLength(100).IsUnicode();
        modelBuilder.Entity<TblService>().Property(x => x.UnitName).HasMaxLength(100).IsUnicode();
        modelBuilder.Entity<TblService>().Property(x => x.UnitNameEn).HasMaxLength(100).IsUnicode();
    }
}
